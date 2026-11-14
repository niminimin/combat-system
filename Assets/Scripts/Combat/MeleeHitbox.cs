using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 近战武器命中检测：在攻击动画的命中帧，用武器碰撞体的范围做一次重叠查询。
///
/// 为什么用 OverlapBox 而不是 OnTriggerEnter：
///   · 不需要给武器加 Rigidbody（加了会被物理系统干扰）
///   · 命中时机完全由动画事件决定，不会出现"刀还举着就判定命中"
///   · 每次挥击只查一次，结果稳定、好调试
///
/// 依附对象：武器（带 BoxCollider），会自动向上找角色身上的 AttackController。
/// </summary>
[DisallowMultipleComponent]
public class MeleeHitbox : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("武器上的碰撞体（通常是一个 isTrigger 的 BoxCollider）")]
    [SerializeField] private Collider hitbox;

    [Tooltip("留空则自动向上查找（武器是角色手部的子物体）")]
    [SerializeField] private AttackController attackController;

    [Header("检测")]
    [Tooltip("只检测这些层上的碰撞体，默认只查 Enemy 层")]
    [SerializeField] private LayerMask targetLayers;

    [Tooltip("一次挥击最多命中多少个目标（防止大范围技能爆表）")]
    [SerializeField, Min(1)] private int maxTargetsPerSwing = 8;

    [Tooltip("是否在控制台打印命中日志")]
    [SerializeField] private bool logHits = true;

    // 同一次挥击内不重复命中同一个碰撞体
    private readonly HashSet<Collider> _hitThisSwing = new HashSet<Collider>();

    /// <summary>命中某个碰撞体时触发（参数是被命中的 Collider）。</summary>
    public event System.Action<Collider> OnHitSomething;

    private void Awake()
    {
        if (hitbox == null) hitbox = GetComponent<Collider>();
        if (attackController == null) attackController = GetComponentInParent<AttackController>();

        if (hitbox == null)
            Debug.LogWarning($"[{nameof(MeleeHitbox)}] 没有碰撞体，命中检测不会生效。", this);
        if (attackController == null)
            Debug.LogWarning($"[{nameof(MeleeHitbox)}] 找不到 AttackController，命中检测不会生效。", this);
    }

    private void OnEnable()
    {
        if (attackController == null) return;
        attackController.AttackStarted += HandleAttackStarted;
        attackController.HitFrame += HandleHitFrame;
    }

    private void OnDisable()
    {
        if (attackController == null) return;
        attackController.AttackStarted -= HandleAttackStarted;
        attackController.HitFrame -= HandleHitFrame;
    }

    private void HandleAttackStarted()
    {
        _hitThisSwing.Clear();
    }

    private void HandleHitFrame()
    {
        var box = hitbox as BoxCollider;
        if (box == null)
        {
            Debug.LogWarning($"[{nameof(MeleeHitbox)}] 目前只支持 BoxCollider。", this);
            return;
        }

        // 碰撞体是局部坐标，换算到世界空间（注意武器被缩放了 100 倍）
        Vector3 worldCenter = box.transform.TransformPoint(box.center);
        Vector3 worldHalfExtents = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f;

        Collider[] hits = Physics.OverlapBox(
            worldCenter, worldHalfExtents, box.transform.rotation,
            targetLayers, QueryTriggerInteraction.Ignore);

        int applied = 0;
        foreach (var c in hits)
        {
            if (c == null) continue;
            if (!_hitThisSwing.Add(c)) continue;   // 本次挥击已经打过

            // 从被撞到的碰撞体往上找受击组件
            var reaction = c.GetComponentInParent<EnemyHitReaction>();
            if (reaction == null) continue;

            reaction.OnHit(gameObject);
            OnHitSomething?.Invoke(c);

            if (logHits)
                Debug.Log($"[MeleeHitbox] 命中 {c.name}（{c.transform.root.name}）", this);

            applied++;
            if (applied >= maxTargetsPerSwing) break;
        }
    }

    // 在 Scene 视图里画出检测范围，方便调手感
    private void OnDrawGizmosSelected()
    {
        var box = hitbox as BoxCollider;
        if (box == null) return;

        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.6f);
        Gizmos.matrix = box.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
