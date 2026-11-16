using UnityEngine;

/// <summary>
/// 战斗待机：停在玩家附近，摆战斗姿势、始终面向玩家。
///
/// 隔一段时间做一次"要不要挪个位置"的决定：
///   命中 orbitChance → 进入 ORBIT 环绕一小段
///   否则 → 继续站着，重新计时
///
/// 后续会在这里接：
///   · 令牌（CombatDirector）—— 拿到令牌后切 ATTACK
///   · 后退（RETREAT）—— 攻击完退回这个状态
/// </summary>
public class CombatIdleState : BaseState
{
    private float nextDecisionTime;

    public override EnemyState Id => EnemyState.COMBAT_IDLE;

    // 偏差超过这个角度（度）就主动挪回槽位
    private const float RepositionThreshold = 25f;

    // 挪到偏差小于这个角度（度）就算到位
    private const float SettledAngle = 10f;

    private bool _repositioning;

    public override void OnEnter(EnemyController owner)
    {
        owner.Locomotion.Stop();
        owner.SetCombatStance(true);      // Animator 切到战斗站姿
        owner.SetOrbiting(false, 0f);     // 确保不在侧移状态

        // 重新排队：刚赶到 / 刚打完的敌人不该插队
        if (CombatDirector.Instance != null) CombatDirector.Instance.MarkJoined(owner);

        // 离自己的编队槽位太远 → 先走过去（自动散开包围）
        _repositioning = owner.HasSlot && AngleDeviation(owner) > RepositionThreshold;

        ScheduleNextDecision(owner);
    }

    // 注意：离场时不关 InCombat —— 如果是切去 ORBIT，仍处于战斗姿态。
    // "离开战斗"的关闭由 CHASE 等非战斗状态负责。

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

        // 走向自己的槽位（期间不转向玩家：Agent 会朝移动方向转）
        if (_repositioning)
        {
            if (MoveTowardsSlot(owner)) _repositioning = false;
            return;
        }

        // 站定：原地转身，始终面向玩家
        owner.FaceTarget(owner.Player.position);

        // 到点了：决定站着还是绕一段
        if (Time.time < nextDecisionTime) return;

        if (Random.value <= owner.orbitChance)
        {
            owner.SwitchState(EnemyState.ORBIT);
            return;
        }

        // 这次决定继续站着，重新排下一次
        ScheduleNextDecision(owner);
    }

    private void ScheduleNextDecision(EnemyController owner)
    {
        nextDecisionTime = Time.time +
                           Random.Range(owner.idleTimeRange.x, owner.idleTimeRange.y);
    }

    /// <summary>当前角度与槽位角度的偏差（度）。</summary>
    private static float AngleDeviation(EnemyController owner)
    {
        if (owner.Player == null) return 0f;

        Vector3 offset = owner.transform.position - owner.Player.position;
        offset.y = 0f;
        if (offset.sqrMagnitude < 0.0001f) return 0f;

        float currentDeg = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
        float slotDeg = owner.SlotAngle * Mathf.Rad2Deg;
        return Mathf.Abs(Mathf.DeltaAngle(currentDeg, slotDeg));
    }

    /// <summary>朝自己的槽位走一步，返回是否已经到位。</summary>
    private static bool MoveTowardsSlot(EnemyController owner)
    {
        if (AngleDeviation(owner) <= SettledAngle) return true;

        Vector3 target = owner.Player.position +
                         new Vector3(Mathf.Cos(owner.SlotAngle), 0f, Mathf.Sin(owner.SlotAngle)) * owner.SlotRadius;
        target.y = owner.transform.position.y;

        owner.Locomotion.MoveTo(target, owner.orbitSpeed, 0.2f);

        if (owner.Locomotion.HasArrived) return true;
        return false;
    }
}
