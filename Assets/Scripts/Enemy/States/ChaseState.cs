using UnityEngine;

/// <summary>
/// 追击：朝玩家移动，进入攻击距离且"间隔到了"就切攻击；跑太远就回位。
///
/// 两个距离判断的区别：
///   · 进入攻击距离 → 停下、面向玩家，等 attackInterval 到了再 ATTACK
///     （等待期间不往前走，否则会一直顶着玩家推）
///   · 超过 loseTargetDistance → RETURN
/// </summary>
public class ChaseState : BaseState
{
    public override EnemyState Id => EnemyState.CHASE;

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

        // 进入攻击距离
        if (distance <= owner.attackRange)
        {
            owner.Locomotion.Stop();
            owner.FaceTarget(owner.Player.position);

            // 攻击间隔没到 → 站在原地对峙，既不打也不往前走
            if (Time.time < owner.NextAttackTime) return;

            owner.SwitchState(EnemyState.ATTACK);
            return;
        }

        // 还在追：每帧刷新目标位置（玩家会移动）
        owner.Locomotion.MoveTo(owner.Player.position, owner.chaseSpeed);
    }
}
