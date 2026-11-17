using UnityEngine;

/// <summary>
/// 玩家处决（无专用动画版）。
///
/// 流程：
///   血量低于阈值的敌人 → 头顶显示 F → 按 F →
///   ① 玩家吸附到敌人正前方、双方转身面对面
///   ② 锁住玩家输入（移动/攻击/弹反/受击）
///   ③ 敌人进入 EXECUTED（冻结 AI、不还手）
///   ④ 导演开启处决锁（其他敌人不发牌，观战）
///   ⑤ 播放占位动画（复用 Attack），到点结算致命伤害
///   ⑥ 解锁，恢复正常
///
/// 无敌是"关掉 PlayerHitReaction"实现的——处决期间玩家完全不吃伤害。
/// </summary>
[DisallowMultipleComponent]
public class PlayerFinisher : MonoBehaviour
{
    [Header("检测")]
    [Tooltip("哪些层算可处决目标")]
    [SerializeField] private LayerMask enemyMask = ~0;

    [Tooltip("处决距离（米）")]
    [SerializeField, Min(0.5f)] private float executeRange = 2.4f;

    [Tooltip("目标必须在这个角度范围内（1 = 正前方，0 = 90°）")]
    [SerializeField, Range(-1f, 1f)] private float minFacingDot = 0.2f;

    [Header("时机")]
    [Tooltip("处决动作总时长（秒）")]
    [SerializeField, Min(0.2f)] private float executeDuration = 1.6f;

    [Tooltip("第几秒结算致命伤害（秒）")]
    [SerializeField, Min(0f)] private float damageTime = 0.9f;

    [Tooltip("玩家吸附到敌人前方的距离（米）")]
    [SerializeField, Min(0.1f)] private float snapDistance = 0.9f;

    [Header("表现")]
    [Tooltip("占位用的动画状态名（以后换成真正的处决动作）")]
    [SerializeField] private string finisherStateName = "Attack";

    [Tooltip("结算时的相机震动")]
    [SerializeField, Range(0f, 1f)] private float shakeAmount = 0.7f;

    [Header("调试")]
    [SerializeField] private bool logEvents = true;

    /// <summary>是否正在处决中（UI / 受击组件据此判断无敌）。</summary>
    public bool IsExecuting { get; private set; }

    /// <summary>当前可处决的目标（UI 图标据此显示）。</summary>
    public EnemyController Candidate { get; private set; }

    private CharacterController controller;
    private Animator animator;
    private ThirdPersonMotor motor;
    private AttackController attack;
    private PlayerAttackInput attackInput;
    private PlayerParry parry;
    private PlayerHitReaction hitReaction;

    private EnemyController _target;
    private float _elapsed;
    private bool _damageApplied;

    // 每帧检测用的缓冲区（避免 OverlapSphere 的每帧 GC）
    private readonly Collider[] _hitBuffer = new Collider[16];

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        motor = GetComponent<ThirdPersonMotor>();
        attack = GetComponent<AttackController>();
        attackInput = GetComponent<PlayerAttackInput>();
        parry = GetComponent<PlayerParry>();
        hitReaction = GetComponent<PlayerHitReaction>();
    }

    private void Update()
    {
        if (IsExecuting)
        {
            TickExecution();
            return;
        }

        // 没在处决：找目标 + 听 F
        Candidate = FindCandidate();

        if (Candidate == null) return;

        if (Input.GetKeyDown(KeyCode.F)) Begin(Candidate);
    }

    // ───────────────────────── 找目标 ─────────────────────────

    /// <summary>在处决范围内挑一个"可处决 + 在正面"的最近敌人。</summary>
    private EnemyController FindCandidate()
    {
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            return null;

        // 玩家正忙（出招/弹反/硬直）时不提示
        if (attack != null && attack.IsAttacking) return null;
        if (parry != null && parry.IsParryLocked) return null;
        if (hitReaction != null && hitReaction.IsReacting) return null;

        int count = Physics.OverlapSphereNonAlloc(transform.position, executeRange, _hitBuffer,
                                                  enemyMask, QueryTriggerInteraction.Ignore);

        EnemyController best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider hit = _hitBuffer[i];
            if (hit == null) continue;

            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null || !enemy.IsExecutable) continue;

            Vector3 offset = enemy.transform.position - transform.position;
            offset.y = 0f;

            float distance = offset.magnitude;
            if (distance > executeRange || distance < 0.001f) continue;

            // 必须在正面（否则会出现"隔空转身处决"）
            if (Vector3.Dot(transform.forward, offset / distance) < minFacingDot) continue;

            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = enemy;
        }

        return best;
    }

    // ───────────────────────── 开始处决 ─────────────────────────

    private void Begin(EnemyController enemy)
    {
        if (enemy == null || !enemy.IsExecutable) return;

        IsExecuting = true;
        _target = enemy;
        _elapsed = 0f;
        _damageApplied = false;
        Candidate = null;

        AlignWithTarget(enemy);
        LockPlayer(true);

        // 敌人进入"被处决"：冻结 AI、不还手
        enemy.BeginExecution();

        // 导演停摆：其他敌人不发牌
        if (CombatDirector.Instance != null)
        {
            // 如果别人正拿着令牌，先收回来，否则它会在旁边砍你
            if (CombatDirector.Instance.CurrentHolder != null &&
                CombatDirector.Instance.CurrentHolder != enemy)
            {
                CombatDirector.Instance.Revoke(CombatDirector.Instance.CurrentHolder);
            }

            CombatDirector.Instance.SetExecutionLock(true);
        }

        // 占位动画（以后换成真正的处决动作）
        if (animator != null) animator.CrossFade(finisherStateName, 0.1f);

        if (logEvents)
            Debug.Log($"[PlayerFinisher] 开始处决 {enemy.name}", this);
    }

    /// <summary>把双方摆成面对面，并把玩家吸附到攻击距离。</summary>
    private void AlignWithTarget(EnemyController enemy)
    {
        Vector3 offset = enemy.transform.position - transform.position;
        offset.y = 0f;

        if (offset.sqrMagnitude < 0.0001f) offset = transform.forward;

        Vector3 direction = offset.normalized;

        // 玩家面朝敌人
        Quaternion playerRot = Quaternion.LookRotation(direction);
        // 敌人面朝玩家
        Quaternion enemyRot = Quaternion.LookRotation(-direction);

        // 吸附位置：敌人正前方 snapDistance 米
        Vector3 snapPosition = enemy.transform.position - direction * snapDistance;
        snapPosition.y = transform.position.y;

        // CharacterController 必须先关闭再改位置，否则会被内部状态覆盖
        if (controller != null) controller.enabled = false;
        transform.rotation = playerRot;
        transform.position = snapPosition;
        if (controller != null) controller.enabled = true;

        enemy.transform.rotation = enemyRot;
    }

    // ───────────────────────── 每帧推进 ─────────────────────────

    private void TickExecution()
    {
        _elapsed += Time.deltaTime;

        // 目标中途死掉（被别的伤害打死）→ 直接结束
        if (_target == null || _target.IsDead)
        {
            if (!_damageApplied) ApplyDamage();
            End();
            return;
        }

        if (!_damageApplied && _elapsed >= damageTime) ApplyDamage();

        if (_elapsed >= executeDuration) End();
    }

    private void ApplyDamage()
    {
        _damageApplied = true;

        if (_target == null) return;

        CharacterStats stats = _target.Stats;
        if (stats != null)
        {
            // 处决必定致死：给一个远高于剩余血量的伤害
            stats.TakeDamage(stats.MaxHealth * 2f, gameObject);
        }

        // 打击反馈：顿帧 + 震动
        if (HitStop.Instance != null) HitStop.Instance.Freeze();
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(shakeAmount);

        if (logEvents) Debug.Log($"[PlayerFinisher] 处决结算：{_target.name}", this);
    }

    private void End()
    {
        IsExecuting = false;

        if (_target != null) _target.EndExecution();
        _target = null;

        if (CombatDirector.Instance != null) CombatDirector.Instance.SetExecutionLock(false);

        LockPlayer(false);

        if (animator != null) animator.CrossFade("Locomotion", 0.15f);

        if (logEvents) Debug.Log("[PlayerFinisher] 处决结束", this);
    }

    // ───────────────────────── 锁定玩家 ─────────────────────────

    private void LockPlayer(bool locked)
    {
        if (attack != null)
        {
            if (locked) attack.CancelAttack();
            attack.enabled = !locked;
        }

        if (attackInput != null) attackInput.enabled = !locked;
        if (motor != null)
        {
            motor.enabled = !locked;
            if (!locked) motor.ResetVelocity();   // 清掉传送前的残留速度，避免滑步
        }
        if (parry != null) parry.enabled = !locked;

        // 无敌：处决期间完全不接受伤害
        if (hitReaction != null) hitReaction.enabled = !locked;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, executeRange);
    }
}
