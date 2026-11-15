using UnityEngine;

/// <summary>
/// 回位：走回出生点，到了以后进入子类指定的"脱战后状态"（守卫型 = GUARD）。
/// 途中又看到玩家就重新追击。
/// </summary>
public class ReturnState : BaseState
{
    public override EnemyState Id => EnemyState.RETURN;

    public override void OnEnter(EnemyController owner)
    {
        owner.Locomotion.MoveTo(owner.HomePosition, owner.returnSpeed);
    }

    public override void LogicUpdate(EnemyController owner)
    {
        float distance;
        if (owner.KnowsPlayer(out distance))
        {
            owner.SwitchState(EnemyState.CHASE);
            return;
        }

        if (owner.Locomotion.HasArrived)
        {
            owner.FaceTarget(owner.HomePosition);
            owner.SwitchState(owner.StateAfterReturn);
        }
    }
}
