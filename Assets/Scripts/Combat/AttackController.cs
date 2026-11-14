using UnityEngine;

/// <summary>
/// 通用攻击控制器：只负责"播放攻击动画 + 冷却 + 连招缓冲 + 事件广播"。
///
/// 设计原则（为敌人复用）：
///   · 不读任何 Input —— 玩家输入在 PlayerAttackInput，AI 输入在敌人的 AI 脚本里。
///   · 只依赖 Animator，只要角色有这个 Animator 和同名 Trigger 就能用。
///   · 通过事件对外广播（开始 / 命中 / 结束），VFX、伤害、AI 都订阅事件，而不是被这里调用。
///
/// 用法：
///   玩家：PlayerAttackInput 每帧检测鼠标 → TryAttack()
///   敌人：AI 在射程内 → TryAttack()
///   命中判定：在攻击动画的命中帧上放 Animation Event，调用 OnHitEvent()
/// </summary>
[DisallowMultipleComponent]
public class AttackController : MonoBehaviour
{
    [Header("动画")]
    [Tooltip("留空则自动在子物体中查找 Animator")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator 里的触发器参数名")]
    [SerializeField] private string attackTrigger = "Attack";

    [Tooltip("Animator 里的攻击状态名（用于判断动画是否播完）")]
    [SerializeField] private string attackStateName = "Attack";

    [Tooltip("找不到状态时长时的兜底时长（秒），防止卡在攻击中")]
    [SerializeField, Min(0.1f)] private float fallbackDuration = 1.5f;

    [Header("规则")]
    [Tooltip("两次攻击之间的最小间隔（秒）")]
    [SerializeField, Min(0f)] private float cooldown = 0.35f;

    [Tooltip("攻击中再次按攻击键时，缓存这次输入并在当前攻击结束后立刻接上（连招手感）")]
    [SerializeField] private bool bufferNextAttack = true;

    /// <summary>是否正在攻击。</summary>
    public bool IsAttacking { get; private set; }

    /// <summary>冷却是否结束（不代表不在攻击中）。</summary>
    public bool IsReady => Time.time >= _readyTime;

    /// <summary>攻击开始（动画已触发）。</summary>
    public event System.Action AttackStarted;

    /// <summary>命中帧（由 Animation Event 触发）。</summary>
    public event System.Action HitFrame;

    /// <summary>攻击结束（动画播完或超时）。</summary>
    public event System.Action AttackFinished;

    private int _triggerHash;
    private float _readyTime;
    private float _attackTimeout;
    private bool _queued;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _triggerHash = Animator.StringToHash(attackTrigger);
    }

    private void Update()
    {
        if (!IsAttacking) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        // 动画播完（normalizedTime 过 1）→ 结束
        if (state.IsName(attackStateName) && state.normalizedTime >= 1f)
        {
            FinishAttack();
            return;
        }

        // 兜底：动画事件丢失 / 状态名不匹配时，别把角色永远锁在攻击中
        if (Time.time >= _attackTimeout) FinishAttack();
    }

    /// <summary>
    /// 尝试发起一次攻击。返回是否真的开始播放。
    /// 处于冷却或攻击中时返回 false（若开启缓冲，会记下这次请求）。
    /// </summary>
    public bool TryAttack()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;

        if (IsAttacking || !IsReady)
        {
            if (bufferNextAttack) _queued = true;
            return false;
        }

        StartAttack();
        return true;
    }

    private void StartAttack()
    {
        IsAttacking = true;
        _queued = false;

        float duration = GetAttackDuration();
        _attackTimeout = Time.time + duration + 0.3f;
        _readyTime = Time.time + Mathf.Max(cooldown, 0f);

        animator.ResetTrigger(_triggerHash);
        animator.SetTrigger(_triggerHash);

        AttackStarted?.Invoke();
    }

    private void FinishAttack()
    {
        if (!IsAttacking) return;
        IsAttacking = false;
        AttackFinished?.Invoke();

        if (_queued)
        {
            _queued = false;
            TryAttack();
        }
    }

    /// <summary>动画中的命中帧调用（Animation Event）。</summary>
    public void OnHitEvent()
    {
        HitFrame?.Invoke();
    }

    /// <summary>从当前控制器里找攻击状态的 clip 时长（仅编辑器；打包后走兜底值）。</summary>
    private float GetAttackDuration()
    {
#if UNITY_EDITOR
        var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (controller != null)
        {
            foreach (var s in controller.layers[0].stateMachine.states)
            {
                if (s.state.name != attackStateName) continue;
                if (s.state.motion != null) return s.state.motion.averageDuration;
            }
        }
#endif
        return fallbackDuration;
    }

    private void OnDisable()
    {
        IsAttacking = false;
        _queued = false;
    }
}
