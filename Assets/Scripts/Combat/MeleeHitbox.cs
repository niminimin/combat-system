using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 近战武器命中检测（数据驱动版）。
///
/// 判定窗口来自 <see cref="AttackDefinitionSO"/>（占动画时长的百分比），
/// 不再依赖动画事件。每帧查询攻击进度，落在窗口内才做几何检测。
///
/// 判定方式：**不是物理碰撞**，而是"窗口内每帧做一次 OverlapBox 查询"。
/// 武器上的 Collider 仅作为形状/尺寸参考（isTrigger，不产生物理推挤）。
///
/// 为什么用进度轮询而不是动画事件：
///   · 判定窗口是一个区间，事件只能表达瞬间
///   · normalizedTime 跟随动画，变速/顿帧/换动画都自动对齐
///   · 窗口值放在 SO 里，可被玩家和敌人复用
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

    [Tooltip("判定盒放大倍数。1 = 与武器完全一致；动作游戏通常放大 10%~30% 更符合手感")]
    [SerializeField, Min(1f)] private float sizeMultiplier = 1.2f;

    [Tooltip("一次挥击最多命中多少个目标")]
    [SerializeField, Min(1)] private int maxTargetsPerSwing = 8;

    [Tooltip("是否在控制台打印命中日志")]
    [SerializeField] private bool logHits = true;

    // 同一次挥击内不重复命中同一个碰撞体
    private readonly HashSet<Collider> _hitThisSwing = new HashSet<Collider>();

    /// <summary>命中某个碰撞体时触发。</summary>
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
    }

    private void OnDisable()
    {
        if (attackController == null) return;
        attackController.AttackStarted -= HandleAttackStarted;
    }

    private void HandleAttackStarted()
    {
        _hitThisSwing.Clear();   // 新的一次挥击，重新记录
    }

    private void Update()
    {
        if (!attackController.IsAttacking) return;

        AttackDefinitionSO def = attackController.CurrentAttack;
        if (def == null) return;

        float t = attackController.CurrentNormalizedTime;
        if (t < def.hitWindowStart || t > def.hitWindowEnd) return;

        TryDetect();
    }

    private void TryDetect()
    {
        var box = hitbox as BoxCollider;
        if (box == null) return;

        // 局部 → 世界（武器被缩放了 100 倍，必须乘 lossyScale）
        Vector3 worldCenter = box.transform.TransformPoint(box.center);
        Vector3 worldHalfExtents = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f * sizeMultiplier;

        Collider[] hits = Physics.OverlapBox(
            worldCenter, worldHalfExtents, box.transform.rotation,
            targetLayers, QueryTriggerInteraction.Ignore);

        int applied = 0;
        foreach (var c in hits)
        {
            if (c == null) continue;
            if (!_hitThisSwing.Add(c)) continue;   // 本次挥击已经打过

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

    // Scene 视图里画出实际判定范围（含放大倍数），选中剑可见
    private void OnDrawGizmosSelected()
    {
        var box = hitbox as BoxCollider;
        if (box == null) return;

        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.7f);
        Gizmos.matrix = box.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size * sizeMultiplier);
    }
}
