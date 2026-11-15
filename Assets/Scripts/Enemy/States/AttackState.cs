using UnityEngine;

/// <summary>
/// 攻击：停下、面向玩家、发起攻击；打完有机会**立刻接下一段连招**。
///
/// 连招的关键：必须在上一段结束后的 comboResetDelay 窗口内再次 TryAttack，
/// 否则 AttackController 会把索引归零。所以这里是"攻击一结束就立刻接"，
/// 而不是回 CHASE 再切回来（那要等 attackInterval，早就超时了）。
///
/// 一轮连招打完后才回 CHASE，下一轮的间隔由 NextAttackTime 控制。
/// </summary>
public class AttackState : BaseState
{
    // 兜底：动画卡在攻击状态时，别让敌人永远站着
    private const float MaxWaitForAnimation = 2.5f;

    private float enterTime;
    private int comboStep;      // 本轮连招已经打了几段

    public override EnemyState Id => EnemyState.ATTACK;

    public override void OnEnter(EnemyController owner)
    {
        enterTime = Time.time;
        comboStep = 0;

        owner.Locomotion.Stop();
        if (owner.Player != null) owner.FaceTarget(owner.Player.position);

        // 本轮连招结束后的间隔（下一轮攻击才用得上）
        owner.NextAttackTime = Time.time + Mathf.Max(owner.attackInterval, 0f);

        if (owner.Attack != null) owner.Attack.TryAttack();
    }

    public override void LogicUpdate(EnemyController owner)
    {
        // 攻击逻辑还在进行 → 等
        if (owner.Attack != null && owner.Attack.IsAttacking) return;

        // 攻击动作还在播（含收招混合）→ 等，避免边挥刀边移动
        if (owner.IsAttackAnimationPlaying() && Time.time - enterTime < MaxWaitForAnimation) return;

        // 被打断过 → 这次不要再接连招，直接回到追击
        if (owner.StaggeredThisState)
        {
            owner.SwitchState(EnemyState.CHASE);
            return;
        }

        // 尝试连招：没打满段数、且随机判定通过 → 立刻接下一段
        if (owner.Attack != null
            && comboStep + 1 < owner.maxComboCount
            && Random.value <= owner.comboChance)
        {
            comboStep++;
            enterTime = Time.time;
            if (owner.Player != null) owner.FaceTarget(owner.Player.position);

            owner.Attack.TryAttack();   // 间隔≈0，落在 comboResetDelay 窗口内 → 索引前进

            if (owner.Attack.IsAttacking) return;   // 接上了，继续留在 ATTACK
        }

        owner.SwitchState(EnemyState.CHASE);
    }
}
