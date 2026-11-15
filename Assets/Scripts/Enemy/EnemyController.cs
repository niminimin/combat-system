using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人状态机核心。
///
/// 结构沿用 3DRPG 项目的做法：
///   · 能力拆成组件：Locomotion（移动）/ Perception（感知）/ Attack（复用 AttackController）
///   · 状态是 BaseState 的子类实例，由子类通过工厂方法决定用哪套实现
///   · SwitchState(枚举) 是**唯一**的状态切换入口
///   · 子类只回答两个问题：初始状态是什么、脱战回位后回到哪个状态
///
/// 本项目的差异：没有 CharacterStats / GameManager / 存档系统，
/// 攻击直接用 AttackController + AttackDefinitionSO，不重复造一套战斗逻辑。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyController : MonoBehaviour
{
    public EnemyLocomotion Locomotion { get; private set; }
    public EnemyPerception Perception { get; private set; }
    public AttackController Attack { get; private set; }
    public EnemyHitReaction Reaction { get; private set; }
    public Animator Animator { get; private set; }

    /// <summary>玩家 Transform（由 Perception 找到）。</summary>
    public Transform Player => Perception != null ? Perception.Player : null;

    [Header("行为参数")]
    [Tooltip("进入这个距离就发起攻击")]
    public float attackRange = 1.8f;

    [Tooltip("玩家超过这个距离就放弃追击、回位")]
    public float loseTargetDistance = 14f;

    [Tooltip("追击时的移动速度")]
    public float chaseSpeed = 4.5f;

    [Tooltip("回位时的移动速度")]
    public float returnSpeed = 2.5f;

    [Tooltip("站岗时原地转身的速度（度/秒），纯表现")]
    public float guardTurnSpeed = 20f;

    [Tooltip("两次攻击之间的间隔（从上次攻击结束开始算，秒）")]
    public float attackInterval = 1.2f;

    [Header("脱战条件")]
    [Tooltip("拴绳半径：离出生点超过这个距离就放弃追击、回位")]
    public float leashRange = 12f;

    [Tooltip("丢失视野多久后放弃追击（秒）。绕柱子/躲掩体可以甩脱")]
    public float loseSightDelay = 2.5f;

    [Header("受击警觉")]
    [Tooltip("被攻击后多久内无视视线规则、始终知道玩家位置（秒）")]
    public float alertMemory = 6f;

    [Tooltip("Animator 里攻击状态的 Tag（Attack/Attack2 都打这个 tag），用于判断'攻击动作是否还在播'")]
    public string attackStateTag = "Attack";

    [Tooltip("Animator 里受击状态的名字，用于判断'受击动画是否还在播'")]
    public string reactionStateName = "Impact";

    [Header("连招")]
    [Tooltip("一次连招最多连续打几段（受 AttackController.combo 长度限制）")]
    [Min(1)] public int maxComboCount = 2;

    [Tooltip("打完一段后继续接下一段的概率（1 = 必定连）")]
    [Range(0f, 1f)] public float comboChance = 1f;

    [Tooltip("连招内两刀之间的间隔（秒）。必须小于 AttackController 的 comboResetDelay(0.8s)，否则连招会断开")]
    [Min(0f)] public float comboInterval = 0.15f;

    /// <summary>下一次允许攻击的时间（时间戳）。由 AttackState 写入，ChaseState 读取。</summary>
    public float NextAttackTime { get; set; }

    /// <summary>攻击动作是否还在播放（含收招过渡）——用来避免"边挥刀边移动"。</summary>
    public bool IsAttackAnimationPlaying()
    {
        if (Animator == null) return false;
        return Animator.GetCurrentAnimatorStateInfo(0).IsTag(attackStateTag);
    }

    /// <summary>出生点快照（存值，不存 Transform 引用）。</summary>
    public Vector3 HomePosition { get; private set; }
    public Quaternion HomeRotation { get; private set; }

    protected BaseState currentState;

    public BaseState guardState { get; private set; }
    public BaseState chaseState { get; private set; }
    public BaseState attackState { get; private set; }
    public BaseState returnState { get; private set; }
    public BaseState deadState { get; private set; }

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // 被攻击后的"强制警觉"截止时间
    private float _alertUntil;

    /// <summary>子类指定出生时进入哪个状态。</summary>
    protected abstract EnemyState InitialState { get; }

    /// <summary>回位到达后进入哪个状态。</summary>
    public abstract EnemyState StateAfterReturn { get; }

    protected virtual void Awake()
    {
        Animator = GetComponent<Animator>();

        Locomotion = GetComponent<EnemyLocomotion>();
        if (Locomotion == null) Locomotion = gameObject.AddComponent<EnemyLocomotion>();

        Perception = GetComponent<EnemyPerception>();
        if (Perception == null) Perception = gameObject.AddComponent<EnemyPerception>();

        Attack = GetComponent<AttackController>();
        Reaction = GetComponent<EnemyHitReaction>();

        HomePosition = transform.position;
        HomeRotation = transform.rotation;

        guardState = CreateGuardState();
        chaseState = CreateChaseState();
        attackState = CreateAttackState();
        returnState = CreateReturnState();
        deadState = CreateDeadState();

        SwitchState(InitialState);
    }

    protected virtual void OnEnable()
    {
        // Reaction 在 Awake 里赋值，OnEnable 一定在其后执行
        if (Reaction != null) Reaction.Hit += HandleHit;
    }

    protected virtual void OnDisable()
    {
        if (Reaction != null) Reaction.Hit -= HandleHit;
    }

    /// <summary>
    /// 被攻击：立刻"知道"玩家在哪，并打断站岗/回位。
    /// 这里是逻辑上的发现 —— 视觉上的转身由 NavMeshAgent 自然完成。
    /// </summary>
    private void HandleHit(GameObject source)
    {
        _alertUntil = Time.time + Mathf.Max(0f, alertMemory);

        if (CurrentStateId == EnemyState.GUARD || CurrentStateId == EnemyState.RETURN)
            SwitchState(EnemyState.CHASE);

        // TODO: 令牌系统接入后在这里给"反击权"
        // CombatDirector.Instance?.ForceGrant(this);
    }

    /// <summary>是否处于"被打了"的强制警觉期。</summary>
    public bool IsAlerted => Time.time < _alertUntil;

    /// <summary>
    /// 是否知道玩家位置。三种来源：
    ///   ① 被攻击后的警觉期（无视视线）
    ///   ② 正常视野判断
    /// 状态机统一用这个，而不是直接调 Perception。
    /// </summary>
    public bool KnowsPlayer(out float distance)
    {
        distance = float.MaxValue;
        if (Player == null) return false;

        if (IsAlerted)
        {
            distance = Vector3.Distance(transform.position, Player.position);
            return true;
        }

        return Perception != null && Perception.CanSeePlayer(out distance);
    }

    /// <summary>状态工厂：子类可以换成自己的状态实现。</summary>
    protected virtual BaseState CreateGuardState() { return new GuardState(); }
    protected virtual BaseState CreateChaseState() { return new ChaseState(); }
    protected virtual BaseState CreateAttackState() { return new AttackState(); }
    protected virtual BaseState CreateReturnState() { return new ReturnState(); }
    protected virtual BaseState CreateDeadState() { return new DeadState(); }

    protected virtual void Update()
    {
        // 受击硬直：冻结状态逻辑并停下移动。
        // 否则 Impact 动画还在播、Agent 却继续走 → 表现为"受击动画平移"。
        if (IsStaggered)
        {
            StaggeredThisState = true;   // 记录"当前状态里挨过打"，供状态自己判断
            Locomotion.Stop();
            if (Animator != null) Animator.SetFloat(SpeedHash, 0f, 0.1f, Time.deltaTime);
            return;
        }

        if (currentState != null) currentState.LogicUpdate(this);

        // 攻击动作播放期间禁止位移：即使 CHASE 刚下过 MoveTo、或者动画还在收招混合，
        // 也强制冻结，避免"挥着刀滑行"。
        if (IsAttackAnimationPlaying())
        {
            Locomotion.Freeze();
            if (Animator != null) Animator.SetFloat(SpeedHash, 0f, 0.1f, Time.deltaTime);
            return;
        }

        // 用 Agent 的实际速度驱动动画（和玩家那套 Speed 参数同名，方便复用混合树）
        if (Animator != null)
            Animator.SetFloat(SpeedHash, Locomotion.CurrentSpeed, 0.1f, Time.deltaTime);
    }

    /// <summary>
    /// 是否处于受击硬直：受击组件还在计时，或者 Impact 动画还在播。
    /// （Impact clip 比 reactionDuration 长，所以要同时看动画状态。）
    /// </summary>
    public bool IsStaggered
    {
        get
        {
            if (Reaction != null && Reaction.IsReacting) return true;
            if (Animator == null) return false;
            return Animator.GetCurrentAnimatorStateInfo(0).IsName(reactionStateName);
        }
    }

    /// <summary>当前状态里是否挨过打（用于状态自己决定"这次不要再接连招"）。</summary>
    public bool StaggeredThisState { get; private set; }

    /// <summary>唯一的状态切换入口。</summary>
    public void SwitchState(EnemyState next)
    {
        BaseState target = GetState(next);
        if (target == null) return;
        if (currentState != null && currentState.Id == next) return;

        if (currentState != null) currentState.OnExit(this);
        currentState = target;
        StaggeredThisState = false;      // 进入新状态，清掉"挨打"标记
        currentState.OnEnter(this);
    }

    /// <summary>当前状态（调试/外部查询用）。</summary>
    public EnemyState CurrentStateId => currentState != null ? currentState.Id : EnemyState.GUARD;

    protected BaseState GetState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.GUARD: return guardState;
            case EnemyState.CHASE: return chaseState;
            case EnemyState.ATTACK: return attackState;
            case EnemyState.RETURN: return returnState;
            case EnemyState.DEAD: return deadState;
            default: return null;
        }
    }

    /// <summary>立刻转向某个世界坐标（只在水平面上）。</summary>
    public void FaceTarget(Vector3 worldPosition)
    {
        Vector3 dir = worldPosition - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    /// <summary>死亡时关掉碰撞，避免尸体挡路/继续被砍。</summary>
    public void DisableCollider()
    {
        foreach (Collider c in GetComponents<Collider>())
            c.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(0.6f, 0.6f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, loseTargetDistance);
    }
}
