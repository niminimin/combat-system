using UnityEngine;

/// <summary>
/// 后撤：攻击结束后向后退开一段，然后回到战斗待机。
///
/// 三个要点：
///   ① **令牌在这里立刻归还** —— 不用等退完，下一个敌人可以同时开始上前
///   ② 后退时身体仍面向玩家 → 关掉 Agent 自动转向，手动 FaceTarget
///   ③ 到位 / 超时 / 玩家跑远 都会结束（避免被挡住退不动）
/// </summary>
public class RetreatState : BaseState
{
    // 兜底：退不动也不能一直站着
    private const float MaxDuration = 2.5f;

    // 到达判定用的停止距离（要小，否则只退出去一点点）
    private const float ArriveStoppingDistance = 0.15f;

    private float enterTime;
    private Vector3 target;

    public override EnemyState Id => EnemyState.RETREAT;

    public override void OnEnter(EnemyController owner)
    {
        enterTime = Time.time;

        // 令牌在"后撤开始"就归还：下一个敌人可以立刻上前
        owner.ReleaseToken();

        owner.SetCombatStance(true);          // 保持战斗姿态
        owner.SetOrbiting(false, 0f);
        owner.SetRetreating(true);            // Animator 切到 Back Walk
        owner.Locomotion.SetRotationEnabled(false);

        // 目标点：沿"背离玩家"的方向退 retreatDistance 米
        Vector3 away = owner.transform.forward * -1f;
        if (owner.Player != null)
        {
            away = owner.transform.position - owner.Player.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -owner.transform.forward;
            owner.FaceTarget(owner.Player.position);
        }

        target = owner.transform.position + away.normalized * owner.retreatDistance;
        target.y = owner.transform.position.y;

        owner.Locomotion.MoveTo(target, owner.retreatSpeed, ArriveStoppingDistance);
    }

    public override void OnExit(EnemyController owner)
    {
        owner.SetRetreating(false);
        owner.Locomotion.SetRotationEnabled(true);
    }

    public override void LogicUpdate(EnemyController owner)
    {
        if (owner.Player == null)
        {
            owner.SwitchState(EnemyState.RETURN);
            return;
        }

        float distanceToPlayer = Vector3.Distance(owner.transform.position, owner.Player.position);

        // 玩家跑远 → 直接转追击（OnExit 会关掉后撤动画）
        if (distanceToPlayer > owner.disengageRange)
        {
            owner.SwitchState(EnemyState.CHASE);
            return;
        }

        // 退到"战斗站位环"就停：不要退过头，否则会掉出 combatRange
        // （掉出去以后再被玩家拉开一点，就会触发 CHASE 白跑一趟）
        if (distanceToPlayer >= owner.combatRange)
        {
            owner.SwitchState(EnemyState.COMBAT_IDLE);
            return;
        }

        // 到位 或 超时 → 回战斗待机
        if (owner.Locomotion.HasArrived || Time.time - enterTime > MaxDuration)
        {
            owner.SwitchState(EnemyState.COMBAT_IDLE);
            return;
        }

        // 继续后退（每帧刷新，玩家移动时方向会跟着变）
        Vector3 away = owner.transform.position - owner.Player.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.0001f)
        {
            target = owner.transform.position + away.normalized * owner.retreatDistance;
            target.y = owner.transform.position.y;
        }

        owner.Locomotion.MoveTo(target, owner.retreatSpeed, ArriveStoppingDistance);

        // 始终面向玩家（Agent 转向已关闭）
        owner.FaceTarget(owner.Player.position);
    }
}
