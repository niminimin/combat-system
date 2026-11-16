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
    public CharacterStats Stats { get; private set; }
    public Animator Animator { get; private set; }

    /// <summary>玩家 Transform（由 Perception 找到）。</summary>
    public Transform Player => Perception != null ? Perception.Player : null;

    [Header("行为参数")]
    [Tooltip("进入这个距离就发起攻击")]
    public float attackRange = 1.8f;

    [Tooltip("进入战斗的距离：CHASE 到这么近就切 COMBAT_IDLE")]
    public float combatRange = 3f;

    [Tooltip("脱离战斗的距离：COMBAT_IDLE 时玩家跑出这个距离就重新追击（必须大于 combatRange，避免抖动）")]
    public float disengageRange = 4.5f;

    [Header("令牌")]
    [Tooltip("距离玩家超过这个距离不会拿到攻击令牌")]
    public float tokenRange = 5f;

    [Tooltip("拿到令牌后、走向玩家的速度")]
    public float approachSpeed = 3f;

    [Header("后撤")]
    [Tooltip("攻击结束后向后退开多远（米）。退到 combatRange 就会提前停住，不需要退太远")]
    public float retreatDistance = 1.2f;

    [Tooltip("后撤速度（米/秒）。贴着 Back Walk 的步频设（约 1.1）才不滑步")]
    public float retreatSpeed = 1.1f;

    [Tooltip("玩家超过这个距离就放弃追击、回位")]
    public float loseTargetDistance = 14f;

    [Tooltip("追击时的移动速度")]
    public float chaseSpeed = 4.5f;

    [Tooltip("回位时的移动速度")]
    public float returnSpeed = 2.5f;

    [Header("环绕")]
    [Tooltip("环绕时保持的半径（米）")]
    public float orbitRadius = 3f;

    [Tooltip("环绕移动速度（米/秒）。贴着动画步频设（1.0~1.2）才不滑步")]
    public float orbitSpeed = 1.1f;

    [Tooltip("环绕持续时间的范围（秒）")]
    public Vector2 orbitDurationRange = new Vector2(0.8f, 1.5f);

    [Tooltip("站住多久之后做一次决定（秒）")]
    public Vector2 idleTimeRange = new Vector2(1.5f, 3f);

    [Tooltip("做决定时选择环绕的概率（其余情况继续站着）")]
    [Range(0f, 1f)] public float orbitChance = 0.6f;

    [Tooltip("原地转身的速度（度/秒），纯表现")]
    public float guardTurnSpeed = 20f;

    [Header("脱战条件")]
    [Tooltip("拴绳半径：离出生点超过这个距离就放弃追击、回位")]
    public float leashRange = 12f;

    [Tooltip("丢失视野多久后放弃追击（秒）。绕柱子/躲掩体可以甩脱")]
    public float loseSightDelay = 2.5f;

    [Header("受击警觉")]
    [Tooltip("被攻击后多久内无视视线规则、始终知道玩家位置（秒）")]
    public float alertMemory = 6f;

    [Tooltip("被弹反后的硬直时长（秒）。期间令牌仍在他手里，别的敌人不会上来抢")]
    public float parryStunDuration = 1.2f;

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

    /// <summary>攻击动作是否还在播放（含收招过渡）——用来避免"边挥刀边移动"。</summary>
    public bool IsAttackAnimationPlaying()
    {
        if (Animator == null) return false;
        return Animator.GetCurrentAnimatorStateInfo(0).IsTag(attackStateTag);
    }

    // ───────────────────── 编队槽位（由 CombatFormation 写入） ─────────────────────

    /// <summary>槽位索引，-1 表示没有分配。</summary>
    public int SlotIndex { get; set; } = -1;

    /// <summary>槽位角度（弧度，世界固定）。</summary>
    public float SlotAngle { get; set; }

    /// <summary>槽位半径（米）。0 表示没有分配槽位。</summary>
    public float SlotRadius { get; set; }

    /// <summary>是否分配到了编队槽位。</summary>
    public bool HasSlot => SlotRadius > 0.01f;

    /// <summary>出生点快照（存值，不存 Transform 引用）。</summary>
    public Vector3 HomePosition { get; private set; }
    public Quaternion HomeRotation { get; private set; }

    protected BaseState currentState;

    public BaseState guardState { get; private set; }
    public BaseState chaseState { get; private set; }
    public BaseState combatIdleState { get; private set; }
    public BaseState orbitState { get; private set; }
    public BaseState attackState { get; private set; }
    public BaseState retreatState { get; private set; }
    public BaseState returnState { get; private set; }
    public BaseState deadState { get; private set; }

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int InCombatHash = Animator.StringToHash("InCombat");
    private static readonly int OrbitingHash = Animator.StringToHash("Orbiting");
    private static readonly int StrafeDirHash = Animator.StringToHash("StrafeDir");
    private static readonly int RetreatingHash = Animator.StringToHash("Retreating");

    // 被攻击后的"强制警觉"截止时间
    private float _alertUntil;

    // 拿到令牌时正处在受击硬直里 → 等硬直结束再出手
    private bool _pendingAttack;

    // 被弹反后的硬直截止时间（比普通受击更长，期间令牌不释放）
    private float _parryStunUntil;

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
        Stats = GetComponent<CharacterStats>();

        HomePosition = transform.position;
        HomeRotation = transform.rotation;

        guardState = CreateGuardState();
        chaseState = CreateChaseState();
        combatIdleState = CreateCombatIdleState();
        orbitState = CreateOrbitState();
        attackState = CreateAttackState();
        retreatState = CreateRetreatState();
        returnState = CreateReturnState();
        deadState = CreateDeadState();

        SwitchState(InitialState);
    }

    protected virtual void OnEnable()
    {
        // Reaction / Stats 在 Awake 里赋值，OnEnable 一定在其后执行
        if (Reaction != null) Reaction.Hit += HandleHit;
        if (Stats != null) Stats.Died += HandleDied;
    }

    protected virtual void OnDisable()
    {
        if (Reaction != null) Reaction.Hit -= HandleHit;
        if (Stats != null) Stats.Died -= HandleDied;

        // 兜底：被禁用/销毁时从导演名单里移除，否则会永远占着令牌
        if (CombatDirector.Instance != null) CombatDirector.Instance.Unregister(this);
    }

    /// <summary>血量归零：切到死亡状态。</summary>
    private void HandleDied(GameObject killer)
    {
        _pendingAttack = false;
        SwitchState(EnemyState.DEAD);
    }

    /// <summary>
    /// 被攻击：立刻"知道"玩家在哪，并打断站岗/回位。
    /// 这里是逻辑上的发现 —— 视觉上的转身由 NavMeshAgent 自然完成。
    /// </summary>
    private void HandleHit(GameObject source)
    {
        _alertUntil = Time.time + Mathf.Max(0f, alertMemory);

        // 站岗/回位途中被打 → 立刻进入追击（先入战斗，才有资格抢令牌）
        if (CurrentStateId == EnemyState.GUARD || CurrentStateId == EnemyState.RETURN)
            SwitchState(EnemyState.CHASE);

        // 反击权：抢走当前持有者的令牌（无视冷却）
        // 用 Request 而不是直接授予：一次横扫可能命中多个敌人，
        // 由导演在本帧末尾按"离玩家最近"挑一个，避免一帧内反复收回/授予。
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.RequestForceGrant(this);
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
    protected virtual BaseState CreateCombatIdleState() { return new CombatIdleState(); }
    protected virtual BaseState CreateOrbitState() { return new OrbitState(); }
    protected virtual BaseState CreateAttackState() { return new AttackState(); }
    protected virtual BaseState CreateRetreatState() { return new RetreatState(); }
    protected virtual BaseState CreateReturnState() { return new ReturnState(); }
    protected virtual BaseState CreateDeadState() { return new DeadState(); }

    /// <summary>切换 Animator 的战斗站姿（COMBAT_IDLE 时 true，其余 false）。</summary>
    public void SetCombatStance(bool inCombat)
    {
        if (Animator != null) Animator.SetBool(InCombatHash, inCombat);
    }

    /// <summary>切换 Animator 的侧移动画（ORBIT 时 true）。</summary>
    public void SetOrbiting(bool orbiting, float direction)
    {
        if (Animator == null) return;
        Animator.SetFloat(StrafeDirHash, Mathf.Sign(direction));
        Animator.SetBool(OrbitingHash, orbiting);
    }

    /// <summary>切换 Animator 的后撤动画。</summary>
    public void SetRetreating(bool retreating)
    {
        if (Animator != null) Animator.SetBool(RetreatingHash, retreating);
    }

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

        // 硬直结束：如果被打时抢到了令牌，现在才真正出手
        if (_pendingAttack)
        {
            _pendingAttack = false;
            if (CombatDirector.Instance == null || CombatDirector.Instance.CurrentHolder == this)
            {
                SwitchState(EnemyState.ATTACK);
                return;
            }
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

    /// <summary>是否处于"弹反硬直"（玩家的奖励时间）。</summary>
    public bool IsParryStunned => Time.time < _parryStunUntil;

    /// <summary>
    /// 是否"被打得动不了"（**不含弹反**）。
    /// 导演用它累计"被压制时长"：弹反是玩家的收益，不该让敌人丢令牌，
    /// 否则玩家弹反后根本来不及接连招。
    /// </summary>
    public bool IsHitStunned
    {
        get
        {
            if (IsParryStunned) return false;                       // 弹反期间不算
            if (Reaction != null && Reaction.IsReacting) return true;

            if (Animator == null) return false;
            return Animator.GetCurrentAnimatorStateInfo(0).IsName(reactionStateName);
        }
    }

    /// <summary>
    /// 是否处于受击硬直（含弹反）：受击组件还在计时，或者 Impact 动画还在播。
    /// 用于锁移动/冻结逻辑，是 IsHitStunned 与 IsParryStunned 的并集。
    /// </summary>
    public bool IsStaggered
    {
        get
        {
            if (Time.time < _parryStunUntil) return true;      // 被弹反：更长的硬直
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

        UpdateCombatRegistration();
    }

    /// <summary>
    /// 注册边界是"是否参战"，不是"是否摆战斗姿势"：
    /// CHASE / COMBAT_IDLE / ORBIT / ATTACK 都算参战；GUARD / RETURN / DEAD 不算。
    /// </summary>
    private void UpdateCombatRegistration()
    {
        EnemyState id = CurrentStateId;
        bool inCombat = id == EnemyState.CHASE
                        || id == EnemyState.COMBAT_IDLE
                        || id == EnemyState.ORBIT
                        || id == EnemyState.ATTACK
                        || id == EnemyState.RETREAT;

        // 令牌名单
        if (CombatDirector.Instance != null)
        {
            if (inCombat) CombatDirector.Instance.Register(this);
            else CombatDirector.Instance.Unregister(this);
        }

        // 编队槽位（和令牌名单共用同一个"进出战斗"判断点）
        if (CombatFormation.Instance != null)
        {
            if (inCombat && !HasSlot) CombatFormation.Instance.ClaimSlot(this);
            else if (!inCombat && HasSlot) CombatFormation.Instance.ReleaseSlot(this);
        }
    }

    // ───────────────────── 令牌（由 CombatDirector 调用） ─────────────────────

    /// <summary>是否已经死亡（导演据此回收令牌）。</summary>
    public bool IsDead => CurrentStateId == EnemyState.DEAD;

    /// <summary>
    /// 现在能不能把攻击令牌交给它。
    /// 导演每帧问这个来筛候选人。
    /// </summary>
    public bool CanReceiveToken
    {
        get
        {
            if (!isActiveAndEnabled) return false;
            if (Attack == null) return false;
            if (IsStaggered) return false;
            if (Player == null) return false;

            // 只有在"站桩/环绕"时才可能出手（正在追的路上不行）
            EnemyState id = CurrentStateId;
            if (id != EnemyState.COMBAT_IDLE && id != EnemyState.ORBIT) return false;

            return Vector3.Distance(transform.position, Player.position) <= tokenRange;
        }
    }

    /// <summary>
    /// 导演把令牌交给它：进入攻击状态。
    /// 如果正处在受击硬直里，先记下来，等硬直结束再出手
    /// （否则会立刻 TryAttack，把受击动画覆盖掉）。
    /// </summary>
    public void OnTokenGranted()
    {
        if (IsStaggered)
        {
            _pendingAttack = true;
            return;
        }

        SwitchState(EnemyState.ATTACK);
    }

    /// <summary>导演收回令牌（超时/被打断）。</summary>
    public void OnTokenRevoked()
    {
        _pendingAttack = false;

        if (CurrentStateId == EnemyState.ATTACK)
            SwitchState(EnemyState.COMBAT_IDLE);
    }

    /// <summary>
    /// 被玩家弹反：播受击 + 进入更长的硬直。
    /// **故意不释放令牌** —— 持牌本身就是锁，别的敌人在此期间拿不到令牌，
    /// 玩家才有一段安全的输出窗口。硬直结束时由 AttackState 离场统一归还。
    /// </summary>
    public void OnParried()
    {
        _parryStunUntil = Time.time + Mathf.Max(0f, parryStunDuration);

        if (Reaction != null) Reaction.PlayImpact();
        else if (Animator != null) Animator.SetTrigger("Impact");
    }

    /// <summary>攻击结束，交还令牌。</summary>
    public void ReleaseToken()
    {
        if (CombatDirector.Instance != null) CombatDirector.Instance.Release(this);
    }

    /// <summary>当前状态（调试/外部查询用）。</summary>
    public EnemyState CurrentStateId => currentState != null ? currentState.Id : EnemyState.GUARD;

    protected BaseState GetState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.GUARD: return guardState;
            case EnemyState.CHASE: return chaseState;
            case EnemyState.COMBAT_IDLE: return combatIdleState;
            case EnemyState.ORBIT: return orbitState;
            case EnemyState.ATTACK: return attackState;
            case EnemyState.RETREAT: return retreatState;
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
