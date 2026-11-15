using UnityEngine;

/// <summary>
/// 通用攻击控制器：播放攻击动画 + 冷却 + 连招缓冲 + 进度查询。
///
/// 数据驱动：每段攻击的参数（动画状态、判定窗口、伤害、冷却）来自
/// AttackDefinitionSO 资产，代码里不再硬编码这些值。
///
/// 设计原则（为敌人复用）：
///   · 不读任何 Input —— 玩家输入在 PlayerAttackInput，敌人由 AI 调用 TryAttack()
///   · 只依赖 Animator + AttackDefinitionSO
///   · 通过事件广播开始/结束；判定窗口由外部查询 CurrentNormalizedTime 得到
/// </summary>
[DisallowMultipleComponent]
public class AttackController : MonoBehaviour
{
    [Header("动画")]
    [Tooltip("留空则自动在子物体中查找 Animator")]
    [SerializeField] private Animator animator;

    [Header("攻击数据（多段则按数组顺序连招）")]
    [Tooltip("每个元素是一段攻击的 ScriptableObject 定义")]
    [SerializeField] private AttackDefinitionSO[] combo;

    [Tooltip("找不到状态时长时的兜底时长（秒），防止卡在攻击中")]
    [SerializeField, Min(0.1f)] private float fallbackDuration = 1.5f;

    [Header("规则")]
    [Tooltip("攻击中再次按攻击键时，缓存这次输入并在当前攻击结束后立刻接上")]
    [SerializeField] private bool bufferNextAttack = true;

    [Tooltip("动画播到该进度（0~1）就解除攻击状态，允许移动")]
    [SerializeField, Range(0.1f, 1f)] private float releaseNormalizedTime = 0.95f;

    /// <summary>是否正在攻击。</summary>
    public bool IsAttacking { get; private set; }

    /// <summary>当前（或最近一次）攻击的数据定义。</summary>
    public AttackDefinitionSO CurrentAttack { get; private set; }

    /// <summary>
    /// 当前攻击动画的播放进度（0~1）。不在攻击状态时为 0。
    /// 实时读取 Animator，不缓存，避免 Update 执行顺序造成的"晚一帧"。
    /// </summary>
    public float CurrentNormalizedTime
    {
        get
        {
            if (animator == null || CurrentAttack == null) return 0f;
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            return st.IsName(CurrentAttack.animatorState) ? st.normalizedTime : 0f;
        }
    }

    /// <summary>冷却是否结束（不代表不在攻击中）。</summary>
    public bool IsReady => Time.time >= _readyTime;

    /// <summary>攻击开始（动画已触发）。</summary>
    public event System.Action AttackStarted;

    /// <summary>攻击结束（动画播完或超时）。</summary>
    public event System.Action AttackFinished;

    /// <summary>
    /// 表现用事件：动画里的 Animation Event "OnHitEvent" 会转发到这里。
    /// 注意：判定不再依赖它，判定窗口由 AttackDefinitionSO 决定。
    /// 保留它是为了以后放刀光/音效时，仍能在动画帧上精确挂表现。
    /// </summary>
    public event System.Action HitFrame;

    private float _readyTime;
    private float _attackTimeout;
    private bool _queued;
    private bool _sawAttackState;
    private int _comboIndex;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (!IsAttacking) return;

        string stateName = CurrentAttack != null ? CurrentAttack.animatorState : string.Empty;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool inAttackState = !string.IsNullOrEmpty(stateName) && state.IsName(stateName);

        if (inAttackState) _sawAttackState = true;

        // ① 播到指定进度 → 放行移动
        if (inAttackState && state.normalizedTime >= releaseNormalizedTime)
        {
            FinishAttack();
            return;
        }

        // ② 状态机已切出攻击状态（exitTime 到点）→ 立即结束
        if (_sawAttackState && !inAttackState)
        {
            FinishAttack();
            return;
        }

        // ③ 兜底：状态名配错 / 动画被中断
        if (Time.time >= _attackTimeout) FinishAttack();
    }

    /// <summary>尝试发起一次攻击。返回是否真的开始播放。</summary>
    public bool TryAttack()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        if (GetCurrentDefinition() == null) return false;

        if (IsAttacking || !IsReady)
        {
            if (bufferNextAttack) _queued = true;
            return false;
        }

        StartAttack();
        return true;
    }

    private AttackDefinitionSO GetCurrentDefinition()
    {
        if (combo == null || combo.Length == 0) return null;
        int index = Mathf.Clamp(_comboIndex, 0, combo.Length - 1);
        return combo[index] != null ? combo[index] : combo[0];
    }

    private void StartAttack()
    {
        AttackDefinitionSO def = GetCurrentDefinition();
        if (def == null) return;

        CurrentAttack = def;
        IsAttacking = true;
        _queued = false;
        _sawAttackState = false;

        float duration = GetAttackDuration(def.animatorState);
        _attackTimeout = Time.time + duration + 0.3f;
        _readyTime = Time.time + Mathf.Max(def.cooldown, 0f);

        // 用 Play 强制从头播放：连招时若仍在攻击状态，Trigger 不会重播，
        // 会导致 normalizedTime 停在旧值、下一帧被误判为"攻击结束"。
        animator.Play(def.animatorState, 0, 0f);

        AttackStarted?.Invoke();

        // 连招推进：按数组顺序循环
        if (combo != null && combo.Length > 1)
            _comboIndex = (_comboIndex + 1) % combo.Length;
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

    /// <summary>表现用：由 AnimationEventRelay 转发动画事件。</summary>
    public void OnHitEvent()
    {
        HitFrame?.Invoke();
    }

    private float GetAttackDuration(string stateName)
    {
#if UNITY_EDITOR
        var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (controller != null)
        {
            foreach (var s in controller.layers[0].stateMachine.states)
            {
                if (s.state.name != stateName) continue;
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
