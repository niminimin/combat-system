using System.Collections.Generic;
using UnityEngine;

/// <summary>判定盒的来源。</summary>
public enum HitboxSource
{
    WeaponCollider,   // 用一个武器上的 BoxCollider 作为形状（玩家用）
    AttackerFront     // 用"角色前方的一块区域"（敌人没有武器碰撞体时用）
}

/// <summary>
/// 近战命中检测（数据驱动）。
///
/// 判定窗口来自 AttackDefinitionSO（占动画时长的百分比），不依赖动画事件：
/// 每帧查询攻击进度，落在窗口内才做一次几何检测。
///
/// 两种形状来源：
///   · WeaponCollider：跟随武器（刀身扫过哪里就判哪里），适合绑定好的武器
///   · AttackerFront ：角色正前方一个盒子，适合没有武器碰撞体的角色
///
/// 命中后通过 IDamageReceiver 通知对方（玩家/敌人通用）。
/// </summary>
[DisallowMultipleComponent]
public class MeleeHitbox : MonoBehaviour
{
    [Header("形状来源")]
    [SerializeField] private HitboxSource source = HitboxSource.WeaponCollider;

    [Header("WeaponCollider 模式")]
    [Tooltip("武器上的碰撞体（通常是一个 isTrigger 的 BoxCollider）")]
    [SerializeField] private Collider hitbox;

    [Tooltip("判定盒放大倍数。1 = 与武器完全一致；动作游戏通常放大 10%~30% 更符合手感")]
    [SerializeField, Min(1f)] private float sizeMultiplier = 1.2f;

    [Header("AttackerFront 模式")]
    [Tooltip("判定区域向前伸出的长度（米）")]
    [SerializeField, Min(0.1f)] private float reach = 1.6f;

    [Tooltip("判定区域宽度（米）")]
    [SerializeField, Min(0.1f)] private float width = 1.2f;

    [Tooltip("判定区域高度（米）")]
    [SerializeField, Min(0.1f)] private float height = 1.4f;

    [Tooltip("判定区域中心离地高度（米）")]
    [SerializeField, Min(0f)] private float centerHeight = 1.0f;

    [Header("引用")]
    [Tooltip("留空则自动向上查找")]
    [SerializeField] private AttackController attackController;

    [Header("检测")]
    [Tooltip("只检测这些层上的碰撞体")]
    [SerializeField] private LayerMask targetLayers;

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

        if (attackController == null)
            Debug.LogWarning($"[{nameof(MeleeHitbox)}] 找不到 AttackController，命中检测不会生效。", this);
        if (source == HitboxSource.WeaponCollider && hitbox == null)
            Debug.LogWarning($"[{nameof(MeleeHitbox)}] 武器模式但没有碰撞体，命中检测不会生效。", this);
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
        _hitThisSwing.Clear();
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
        Vector3 center;
        Vector3 halfExtents;
        Quaternion rotation;

        if (!TryGetShape(out center, out halfExtents, out rotation)) return;

        Collider[] hits = Physics.OverlapBox(
            center, halfExtents, rotation,
            targetLayers, QueryTriggerInteraction.Ignore);

        int applied = 0;

        // 伤害来自"这一招"的数据（AttackDefinitionSO）
        float damage = attackController.CurrentAttack != null
            ? attackController.CurrentAttack.damage
            : 0f;

        foreach (var c in hits)
        {
            if (c == null) continue;
            if (!_hitThisSwing.Add(c)) continue;      // 本次挥击已经打过

            var receiver = c.GetComponentInParent<IDamageReceiver>();
            if (receiver == null) continue;           // 不是可受击对象（墙、道具）

            receiver.OnHit(gameObject, damage);
            OnHitSomething?.Invoke(c);

            if (logHits)
                Debug.Log($"[MeleeHitbox] 命中 {c.name}（{c.transform.root.name}），伤害 {damage:F0}", this);

            applied++;
            if (applied >= maxTargetsPerSwing) break;
        }
    }

    /// <summary>算出判定盒的世界空间中心、半尺寸和朝向。</summary>
    private bool TryGetShape(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        center = Vector3.zero;
        halfExtents = Vector3.zero;
        rotation = Quaternion.identity;

        if (source == HitboxSource.AttackerFront)
        {
            Transform t = transform;
            center = t.position + Vector3.up * centerHeight + t.forward * (reach * 0.5f);
            halfExtents = new Vector3(width, height, reach) * 0.5f;
            rotation = t.rotation;
            return true;
        }

        var box = hitbox as BoxCollider;
        if (box == null) return false;

        // 局部 → 世界（武器可能被缩放，必须乘 lossyScale）
        center = box.transform.TransformPoint(box.center);
        halfExtents = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f * sizeMultiplier;
        rotation = box.transform.rotation;
        return true;
    }

    // Scene 视图里画出实际判定范围
    private void OnDrawGizmosSelected()
    {
        Vector3 center, halfExtents;
        Quaternion rotation;
        if (!TryGetShape(out center, out halfExtents, out rotation)) return;

        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.7f);
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
    }
}
