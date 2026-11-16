using UnityEngine;

/// <summary>
/// 战斗待机：停在玩家附近，摆战斗姿势、始终面向玩家。
///
/// 这是"进入战斗但还没出手"的状态。
/// 后续会在这里接：
///   · 环绕（ORBIT 状态）—— 隔一段时间换个位置
///   · 令牌（CombatDirector）—— 拿到令牌后切 ATTACK
///   · 后退（RETREAT）—— 攻击完退回这个状态
///
/// 现在只做最基础的部分：停住、面向、玩家跑远就重新追。
/// </summary>
public class CombatIdleState : BaseState
{
    public override EnemyState Id => EnemyState.COMBAT_IDLE;

    public override void OnEnter(EnemyController owner)
    {
        owner.Locomotion.Stop();
        owner.SetCombatStance(true);     // Animator 切到战斗站姿
    }

    public override void OnExit(EnemyController owner)
    {
        owner.SetCombatStance(false);    // 回到移动动画
    }

    public override void LogicUpdate(EnemyController owner)
    {
        if (owner.Player == null)
        {
            owner.SwitchState(EnemyState.RETURN);
            return;
        }

        float distance = Vector3.Distance(owner.transform.position, owner.Player.position);

        // 玩家跑出脱离距离 → 重新追击（用 disengageRange 做迟滞，避免临界点抖动）
        if (distance > owner.disengageRange)
        {
            owner.SwitchState(EnemyState.CHASE);
            return;
        }

        // 原地转身，始终面向玩家
        owner.FaceTarget(owner.Player.position);

        // 暂时不进入 ATTACK —— 等令牌系统接入
    }
}
