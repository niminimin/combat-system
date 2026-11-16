using UnityEngine;

/// <summary>
/// 追击。三个脱战条件（任一满足就回位）：
///   ① 拴绳：离出生点超过 leashRange
///   ② 距离：离玩家超过 loseTargetDistance
///   ③ 跟丢：连续 loseSightDelay 秒看不到玩家（视线被挡）
///
/// 进入攻击距离时停下、面向玩家，等 attackInterval 到了再 ATTACK。
/// </summary>
public class ChaseState : BaseState
{
    // 每个敌人有自己的状态实例，所以这个计时器是"每敌人独立"的
    private float lostSightTimer;

    public override EnemyState Id => EnemyState.CHASE;

    public override void OnEnter(EnemyController owner)
    {
        lostSightTimer = 0f;
    }

    public override void LogicUpdate(EnemyController owner)
    {
        if (owner.Player == null)
        {
            owner.SwitchState(EnemyState.RETURN);
            return;
        }

        // ① 拴绳：追出家门太远就放弃
        if (Vector3.Distance(owner.transform.position, owner.HomePosition) > owner.leashRange)
        {
            owner.SwitchState(EnemyState.RETURN);
            return;
        }

        float distance = Vector3.Distance(owner.transform.position, owner.Player.position);

        // ② 距离太远
        if (distance > owner.loseTargetDistance)
        {
            owner.SwitchState(EnemyState.RETURN);
            return;
        }

        // ③ 跟丢计时：看不见玩家就累计，重新看见就清零
        //    （被攻击后的警觉期内 KnowsPlayer 恒为 true，所以不会跟丢）
        float seenDistance;
        if (owner.KnowsPlayer(out seenDistance))
            lostSightTimer = 0f;
        else
            lostSightTimer += Time.deltaTime;

        if (lostSightTimer >= owner.loseSightDelay)
        {
            owner.SwitchState(EnemyState.RETURN);
            return;
        }

        // 进入战斗距离 → 停下摆战斗姿势
        // （ATTACK 暂时不可达，等令牌系统接入后再从这里/COMBAT_IDLE 进入）
        if (distance <= owner.combatRange)
        {
            owner.Locomotion.Stop();
            owner.SwitchState(EnemyState.COMBAT_IDLE);
            return;
        }

        owner.Locomotion.MoveTo(owner.Player.position, owner.chaseSpeed);
    }
}
