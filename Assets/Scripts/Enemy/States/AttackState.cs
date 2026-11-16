using UnityEngine;

/// <summary>
/// 攻击（持令牌期间）。三个阶段：
///   A. 接近：不在攻击距离内就先走过去
///   B. 出手：1 或 2 段连招（概率由 comboChance / maxComboCount 控制）
///   C. 结束：回 COMBAT_IDLE，离场时交还令牌
///
/// 关键约束：
///   · 必须等攻击动画真正离开 Attack/Attack2 才切走，否则会"挥着刀平移"
///   · 被打断（StaggeredThisState）→ 立刻放弃连招
///   · 玩家跑出 disengageRange → 放弃这次攻击（令牌在 OnExit 还掉）
/// </summary>
public class AttackState : BaseState
{
    // 兜底：动画卡在攻击状态时，别让敌人永远站着
    private const float MaxWaitForAnimation = 2.5f;

    private float enterTime;
    private int comboStep;               // 本轮已经打了几段
    private float attackEndedAt = -1f;   // 上一段完全播完的时刻
    private bool approaching;            // 是否还处在"接近"阶段

    public override EnemyState Id => EnemyState.ATTACK;

    public override void OnEnter(EnemyController owner)
    {
        enterTime = Time.time;
        comboStep = 0;
        attackEndedAt = -1f;

        owner.SetCombatStance(true);          // 保持战斗姿态
        owner.SetOrbiting(false, 0f);
        owner.Locomotion.SetRotationEnabled(true);

        if (owner.Player == null) { approaching = false; return; }

        float distance = Vector3.Distance(owner.transform.position, owner.Player.position);
        if (distance > owner.attackRange)
        {
            // 阶段 A：先走过去
            approaching = true;
            owner.Locomotion.MoveTo(owner.Player.position, owner.approachSpeed);
        }
        else
        {
            approaching = false;
            BeginSwing(owner);
        }
    }

    public override void OnExit(EnemyController owner)
    {
        // 无论从哪条路离开（正常结束/被打断/死亡），都确保令牌归还。
        // Release 内部会判断"我是不是当前持有者"，重复调用无害。
        owner.ReleaseToken();
    }

    /// <summary>停下、面向玩家、发起一段攻击。</summary>
    private void BeginSwing(EnemyController owner)
    {
        owner.Locomotion.Stop();
        if (owner.Player != null) owner.FaceTarget(owner.Player.position);

        enterTime = Time.time;
        attackEndedAt = -1f;

        if (owner.Attack != null) owner.Attack.TryAttack();
    }

    public override void LogicUpdate(EnemyController owner)
    {
        if (owner.Player == null)
        {
            owner.SwitchState(EnemyState.RETURN);
            return;
        }

        float distance = Vector3.Distance(owner.transform.position, owner.Player.position);

        // 玩家跑太远 → 放弃这次攻击（离场时 OnExit 会还令牌）
        if (distance > owner.disengageRange)
        {
            owner.SwitchState(EnemyState.CHASE);
            return;
        }

        // 阶段 A：接近
        if (approaching)
        {
            if (distance > owner.attackRange)
            {
                owner.Locomotion.MoveTo(owner.Player.position, owner.approachSpeed);
                return;
            }

            approaching = false;
            BeginSwing(owner);
            return;
        }

        // 阶段 B：攻击中
        if (owner.Attack != null && owner.Attack.IsAttacking)
        {
            attackEndedAt = -1f;
            return;
        }

        // 攻击动作还在播（含收招混合）→ 等，避免边挥刀边移动
        if (owner.IsAttackAnimationPlaying() && Time.time - enterTime < MaxWaitForAnimation)
        {
            attackEndedAt = -1f;
            return;
        }

        // 被打断 → 不接连招
        if (owner.StaggeredThisState)
        {
            owner.SwitchState(EnemyState.COMBAT_IDLE);
            return;
        }

        // 记下"这一段播完"的时刻（只记一次）
        if (attackEndedAt < 0f) attackEndedAt = Time.time;

        // 等够两段之间的间隔
        float wait = owner.comboInterval;
        if (owner.Attack != null)
            wait = Mathf.Min(wait, Mathf.Max(0f, owner.Attack.ComboResetDelay - 0.05f));

        if (Time.time - attackEndedAt < wait) return;

        // 阶段 C：还有段数就再打一段
        if (owner.Attack != null
            && comboStep + 1 < owner.maxComboCount
            && Random.value <= owner.comboChance)
        {
            comboStep++;
            BeginSwing(owner);
            if (owner.Attack.IsAttacking) return;
        }

        // 打完了 → 回战斗待机
        owner.SwitchState(EnemyState.COMBAT_IDLE);
    }
}
