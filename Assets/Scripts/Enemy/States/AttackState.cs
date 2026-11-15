using UnityEngine;

/// <summary>
/// 攻击：停下、面向玩家、发起一次攻击（复用 AttackController + AttackDefinitionSO）。
/// 攻击结束后回到 CHASE —— 如果还在攻击距离内，CHASE 会立刻再切回 ATTACK，
/// 于是形成"打一下 → 判断距离 → 再打"的循环，节奏由 AttackDefinitionSO.cooldown 控制。
/// </summary>
public class AttackState : BaseState
{
    // 兜底：万一 AttackController 没配好，也不能永远卡在攻击状态
    private float enterTime;

    public override EnemyState Id => EnemyState.ATTACK;

    public override void OnEnter(EnemyController owner)
    {
        enterTime = Time.time;
        owner.Locomotion.Stop();

        if (owner.Player != null)
            owner.FaceTarget(owner.Player.position);

        if (owner.Attack != null)
            owner.Attack.TryAttack();
    }

    public override void LogicUpdate(EnemyController owner)
    {
        // 没有攻击组件：直接回追击（避免卡死）
        if (owner.Attack == null)
        {
            if (Time.time - enterTime > 0.3f) owner.SwitchState(EnemyState.CHASE);
            return;
        }

        // 攻击还在进行 → 继续等
        if (owner.Attack.IsAttacking) return;

        // 攻击结束 → 回追击，由 CHASE 决定是"再打一次"还是"继续追"
        owner.SwitchState(EnemyState.CHASE);
    }
}
