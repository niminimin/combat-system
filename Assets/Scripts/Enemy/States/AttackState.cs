using UnityEngine;

/// <summary>
/// 攻击：停下、面向玩家、发起攻击；打完可接下一段连招。
///
/// 三个时间参数的分工：
///   · comboInterval（本状态用）：同一轮连招里，两刀之间的间隔
///   · comboResetDelay（AttackController）：超过这个时间就不再算续招
///       —— 所以 comboInterval 必须比它小，否则第二刀会被判定为"重新开始"
///   · attackInterval（离开本状态时计时）：两轮攻击之间的停顿
///
/// 还必须等攻击动画真正离开 Attack/Attack2 状态才切走，
/// 否则 FSM 先移动、动画还在收招 → "挥着刀平移"。
/// </summary>
public class AttackState : BaseState
{
    // 兜底：动画卡在攻击状态时，别让敌人永远站着
    private const float MaxWaitForAnimation = 2.5f;

    private float enterTime;
    private int comboStep;               // 本轮连招已经打了几段
    private float attackEndedAt = -1f;   // 上一刀完全播完的时刻

    public override EnemyState Id => EnemyState.ATTACK;

    public override void OnEnter(EnemyController owner)
    {
        enterTime = Time.time;
        comboStep = 0;
        attackEndedAt = -1f;

        owner.Locomotion.Stop();
        if (owner.Player != null) owner.FaceTarget(owner.Player.position);

        if (owner.Attack != null) owner.Attack.TryAttack();
    }

    public override void OnExit(EnemyController owner)
    {
        // 连招真正结束时才开始计间隔（否则连招本身就把间隔吃掉了）
        owner.NextAttackTime = Time.time + Mathf.Max(owner.attackInterval, 0f);
    }

    public override void LogicUpdate(EnemyController owner)
    {
        // 攻击逻辑还在进行 → 等
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

        // 被打断过 → 这次不要再接连招
        if (owner.StaggeredThisState)
        {
            owner.SwitchState(EnemyState.CHASE);
            return;
        }

        // 记下"这一刀播完"的时刻（只记一次）
        if (attackEndedAt < 0f) attackEndedAt = Time.time;

        // 等够两刀之间的间隔
        float wait = owner.comboInterval;
        if (owner.Attack != null)
            wait = Mathf.Min(wait, Mathf.Max(0f, owner.Attack.ComboResetDelay - 0.05f));

        if (Time.time - attackEndedAt < wait) return;

        // 尝试连招
        if (owner.Attack != null
            && comboStep + 1 < owner.maxComboCount
            && Random.value <= owner.comboChance)
        {
            comboStep++;
            enterTime = Time.time;
            attackEndedAt = -1f;
            if (owner.Player != null) owner.FaceTarget(owner.Player.position);

            owner.Attack.TryAttack();

            if (owner.Attack.IsAttacking) return;   // 接上了，继续留在 ATTACK
        }

        owner.SwitchState(EnemyState.CHASE);
    }
}
