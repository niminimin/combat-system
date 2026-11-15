using UnityEngine;

/// <summary>
/// 站岗：原地缓慢环顾，发现玩家就进入追击。
/// 这是"守卫型"敌人的初始状态，也是脱战回位后回到的状态。
/// </summary>
public class GuardState : BaseState
{
    public override EnemyState Id => EnemyState.GUARD;

    public override void OnEnter(EnemyController owner)
    {
        owner.Locomotion.Stop();
    }

    public override void LogicUpdate(EnemyController owner)
    {
        // 原地缓慢转身，显得"在警戒"（纯表现，不影响逻辑）
        owner.transform.Rotate(0f, owner.guardTurnSpeed * Time.deltaTime, 0f, Space.World);

        // 视觉发现，或被打后（警觉期）立刻进入战斗
        float distance;
        if (owner.KnowsPlayer(out distance))
            owner.SwitchState(EnemyState.CHASE);
    }
}
