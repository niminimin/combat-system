using UnityEngine;

/// <summary>
/// 环绕：沿着玩家周围的一段弧线侧移，身体始终面向玩家。
///
/// 几何：以玩家为圆心、orbitRadius 为半径，
/// 从"当前所在角度"出发，每帧按 orbitSpeed 推进一点角度，算出新目标点。
///
/// 两个关键点：
///   ① 关闭 NavMeshAgent 的自动转向 —— 否则角色会转向移动方向，
///      而侧移动画要求身体面向玩家（横着走）
///   ② 时长到了就回 COMBAT_IDLE，不要一直绕
/// </summary>
public class OrbitState : BaseState
{
    private float enterTime;
    private float duration;      // 本次环绕持续多久
    private float direction;     // +1 / -1，绕行方向
    private float angle;         // 当前角度（弧度，相对玩家）

    public override EnemyState Id => EnemyState.ORBIT;

    public override void OnEnter(EnemyController owner)
    {
        enterTime = Time.time;
        duration = Random.Range(owner.orbitDurationRange.x, owner.orbitDurationRange.y);
        direction = Random.value < 0.5f ? -1f : 1f;

        if (owner.Player != null)
        {
            // 用当前位置相对玩家的角度作为起点
            Vector3 offset = owner.transform.position - owner.Player.position;
            offset.y = 0f;
            angle = Mathf.Atan2(offset.z, offset.x);
            owner.FaceTarget(owner.Player.position);
        }
        else angle = 0f;

        // 侧移时身体要面向玩家 → 关掉 Agent 自动转向
        owner.SetCombatStance(true);     // 环绕仍属于战斗姿态
        owner.Locomotion.SetRotationEnabled(false);
        owner.SetOrbiting(true, direction);
    }

    public override void OnExit(EnemyController owner)
    {
        owner.SetOrbiting(false, 0f);
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

        // 玩家跑远 → 回追击
        if (distanceToPlayer > owner.disengageRange)
        {
            owner.SwitchState(EnemyState.CHASE);
            return;
        }

        // 时间到 → 回战斗待机
        if (Time.time - enterTime >= duration)
        {
            owner.SwitchState(EnemyState.COMBAT_IDLE);
            return;
        }

        // 推进角度：角速度 = 线速度 / 半径
        float angularSpeed = owner.orbitSpeed / Mathf.Max(owner.orbitRadius, 0.1f);
        angle += direction * angularSpeed * Time.deltaTime;

        Vector3 target = owner.Player.position +
                         new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * owner.orbitRadius;
        target.y = owner.transform.position.y;   // 保持自身高度，避免上下坡抖动

        owner.Locomotion.MoveTo(target, owner.orbitSpeed);

        // 身体始终面向玩家（Agent 转向已关闭）
        owner.FaceTarget(owner.Player.position);
    }
}
