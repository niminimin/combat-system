using UnityEngine;

/// <summary>
/// 追击：朝玩家移动，进入攻击距离就切攻击；跟丢或跑太远就回位。
/// </summary>
public class ChaseState : BaseState
{
    public override EnemyState Id => EnemyState.CHASE;

    public override void OnEnter(EnemyController owner)
    {
        // 进入追击时如果离得太远（比如玩家跑出视野），直接判定为丢失
    }

    public override void LogicUpdate(EnemyController owner)
    {
        if (owner.Player == null)
        {
            owner.SwitchState(EnemyState.RETURN);
            return;
        }

        float distance = Vector3.Distance(owner.transform.position, owner.Player.position);

        // 超出追击范围 → 回位
        if (distance > owner.loseTargetDistance)
        {
            owner.SwitchState(EnemyState.RETURN);
            return;
        }

        // 进入攻击距离 → 攻击
        if (distance <= owner.attackRange)
        {
            owner.Locomotion.Stop();
            owner.SwitchState(EnemyState.ATTACK);
            return;
        }

        // 还在追：每帧刷新目标位置（玩家会移动）
        owner.Locomotion.MoveTo(owner.Player.position, owner.chaseSpeed);
    }
}
