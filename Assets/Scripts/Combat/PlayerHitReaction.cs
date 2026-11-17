using UnityEngine;

/// <summary>
/// 玩家受击表现：播受击动画 + 短暂锁定移动。
///
/// 目前只做"表现"，不做伤害 —— 等接入血量系统后，
/// 在同一处调用 TakeDamage 即可。
/// </summary>
[DisallowMultipleComponent]
public class PlayerHitReaction : MonoBehaviour, IDamageReceiver
{
    [Tooltip("留空则从子物体里找（玩家的 Animator 在 Visual 上）")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator 里的受击触发器名")]
    [SerializeField] private string hitTrigger = "Impact";

    [Tooltip("Animator 里的受击状态名（受击期间持续锁定，避免'受击动画平移'）")]
    [SerializeField] private string hitStateName = "Impact";

    [Tooltip("受击后最少锁定多久（秒）。实际锁定会持续到受击动画结束")]
    [SerializeField, Min(0f)] private float reactionDuration = 0.45f;

    [Tooltip("是否在控制台打印受击日志")]
    [SerializeField] private bool logHits = true;

    [Header("相机抖动")]
    [Tooltip("玩家被命中时的抖动强度（0~1）")]
    [SerializeField, Range(0f, 1f)] private float hitShake = 0.5f;

    [Tooltip("弹反成功时的抖动强度（0~1），通常比挨打更强")]
    [SerializeField, Range(0f, 1f)] private float parryShake = 0.65f;

    private int _triggerHash;
    private float _reactingUntil;

    /// <summary>
    /// 是否处于受击硬直（ThirdPersonMotor 据此锁输入）。
    /// 两个条件取并集：最短锁定时间未到，或者受击动画还没播完。
    /// </summary>
    public bool IsReacting
    {
        get
        {
            if (Time.time < _reactingUntil) return true;
            if (animator == null) return false;
            return animator.GetCurrentAnimatorStateInfo(0).IsName(hitStateName);
        }
    }

    /// <summary>受击事件（以后接血量/UI 抖动/音效）。</summary>
    public event System.Action<GameObject> Hit;

    /// <summary>弹反成功事件（以后接音效/顿帧/镜头）。</summary>
    public event System.Action<EnemyController> Parried;

    [Tooltip("玩家身上的弹反组件（留空则自动查找）")]
    [SerializeField] private PlayerParry parry;

    [Tooltip("血量组件（留空则自动查找）")]
    [SerializeField] private CharacterStats stats;

    [Tooltip("处决组件（留空则自动查找）：处决期间玩家无敌")]
    [SerializeField] private PlayerFinisher finisher;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (parry == null) parry = GetComponent<PlayerParry>();
        if (stats == null) stats = GetComponent<CharacterStats>();
        if (finisher == null) finisher = GetComponent<PlayerFinisher>();

        _triggerHash = Animator.StringToHash(hitTrigger);
    }

    /// <summary>
    /// 当前是否无敌。
    /// 注意：不能靠"禁用本组件"来实现无敌——命中判定是直接调 OnHit 的，
    /// 组件禁用不会阻止这次调用，必须在这里显式拦下。
    /// </summary>
    public bool IsInvulnerable => finisher != null && finisher.IsExecuting;

    public void OnHit(GameObject source, float damage)
    {
        // 处决期间完全免疫：不掉血、不播受击、不打断处决动作
        if (IsInvulnerable) return;

        // 弹反优先：窗口内 + 正面来敌 → 敌人被打断，玩家不受击
        if (parry != null)
        {
            EnemyController parriedEnemy;
            if (parry.TryParryAttack(source, out parriedEnemy))
            {
                parriedEnemy.OnParried();

                // 顿帧 + 抖动：弹反成功的瞬间
                if (HitStop.Instance != null) HitStop.Instance.Freeze();
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(parryShake);

                Parried?.Invoke(parriedEnemy);
                return;
            }
        }

        // 没弹住 → 结算伤害（可能触发死亡）
        if (stats != null) stats.TakeDamage(damage, source);

        // 已经死了：交给死亡流程，不播受击
        if (stats != null && stats.IsDead) return;

        if (animator == null)
        {
            Debug.LogWarning($"[{nameof(PlayerHitReaction)}] 找不到 Animator，受击动画不会播放。", this);
            return;
        }

        animator.SetTrigger(_triggerHash);
        _reactingUntil = Time.time + reactionDuration;

        // 相机抖动：玩家被命中
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(hitShake);

        if (logHits)
            Debug.Log($"[PlayerHitReaction] 被 {(source != null ? source.name : "未知")} 命中", this);

        Hit?.Invoke(source);
    }
}
