using UnityEngine;

/// <summary>
/// 攻击：停下、面向玩家、发起一次攻击（复用 AttackController + AttackDefinitionSO）。
///
/// 两个关键点：
///   ① 进入时就排好"下一次允许攻击的时间"（attackInterval），
///      所以攻击结束后 CHASE 会先和玩家对峙一会儿，而不是立刻再砍。
///   ② 必须等**攻击动画真正离开 Attack 状态**才切回 CHASE，
///      否则 FSM 已经开始移动、动画还在收招 → 表现为"挥着刀平移"。
/// </summary>
public class AttackState : BaseState
{
    // 兜底：动画卡在 Attack 状态时，别让敌人永远站着
    private const float MaxWaitForAnimation = 2.5f;
    private float enterTime;

    public override EnemyState Id => EnemyState.ATTACK;

    public override void OnEnter(EnemyController owner)
    {
        enterTime = Time.time;
        owner.Locomotion.Stop();

        if (owner.Player != null)
            owner.FaceTarget(owner.Player.position);

        // 无论这次是否真的出手，都把下一次的间隔排上
        owner.NextAttackTime = Time.time + Mathf.Max(owner.attackInterval, 0f);

        if (owner.Attack != null)
            owner.Attack.TryAttack();
    }

    public override void LogicUpdate(EnemyController owner)
    {
        // 攻击逻辑还在进行 → 等
        if (owner.Attack != null && owner.Attack.IsAttacking) return;

        // 攻击动作还在播（含收招混合）→ 继续等，避免边挥刀边移动
        if (owner.IsAttackAnimationPlaying() && Time.time - enterTime < MaxWaitForAnimation) return;

        owner.SwitchState(EnemyState.CHASE);
    }
}
