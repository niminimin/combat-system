using UnityEngine;
using UnityEngine.AI;

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
///
/// 三道保险（避免"原地播放走路动画"）：
///   ① 目标点吸附到 NavMesh —— 玩家站在地图边缘时目标点可能落在网格外
///   ② SetDestination 返回值 / pathStatus —— 路径不可达就别演了
///   ③ 卡住检测 —— 实际速度持续接近 0 就放弃（障碍、拥挤、寻路 bug 都能兜住）
/// </summary>
public class OrbitState : BaseState
{
    // 目标点沿弧线往前偏多少（弧度）。太小会被 Agent.stoppingDistance 判成"已到达"
    private const float LookAheadAngle = 0.5f;

    // 目标点吸附到 NavMesh 的搜索半径
    private const float TargetSampleRadius = 1.5f;

    // 低于这个速度视为"没在动"
    private const float StuckSpeedThreshold = 0.1f;

    // 持续"没在动"多久就放弃这次环绕
    private const float StuckTimeout = 0.4f;

    // 有槽位时，偏离槽位超过这个角度就结束环绕（度）
    private const float maxDriftDegrees = 30f;

    private float enterTime;
    private float duration;      // 本次环绕持续多久
    private float direction;     // +1 / -1，绕行方向
    private float angle;         // 当前角度（弧度，相对玩家）
    private float stuckTime;     // 连续"没动"的累计时间

    public override EnemyState Id => EnemyState.ORBIT;

    public override void OnEnter(EnemyController owner)
    {
        enterTime = Time.time;
        stuckTime = 0f;
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
        float radius = owner.HasSlot ? owner.SlotRadius : owner.orbitRadius;
        float angularSpeed = owner.orbitSpeed / Mathf.Max(radius, 0.1f);
        angle += direction * angularSpeed * Time.deltaTime;

        // 有编队槽位时，偏离槽位超过 maxDrift 就结束（只在自己的小扇形里活动）
        if (owner.HasSlot)
        {
            float drift = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg,
                                                     owner.SlotAngle * Mathf.Rad2Deg));
            if (drift > maxDriftDegrees)
            {
                owner.SwitchState(EnemyState.COMBAT_IDLE);
                return;
            }
        }

        // 目标点取"沿弧线往前一点"的位置（约 29°），而不是贴着当前位置
        float aheadAngle = angle + direction * LookAheadAngle;
        Vector3 rawTarget = owner.Player.position +
                            new Vector3(Mathf.Cos(aheadAngle), 0f, Mathf.Sin(aheadAngle)) * radius;
        rawTarget.y = owner.transform.position.y;   // 保持自身高度，避免上下坡抖动

        // 保险①：目标点必须在 NavMesh 上（玩家站地图边缘时可能落在网格外）
        NavMeshHit navHit;
        if (!NavMesh.SamplePosition(rawTarget, out navHit, TargetSampleRadius, NavMesh.AllAreas))
        {
            owner.SwitchState(EnemyState.COMBAT_IDLE);
            return;
        }

        // 保险②：寻路失败 / 路径不可达（被墙挡死、死角）
        if (!owner.Locomotion.MoveTo(navHit.position, owner.orbitSpeed, 0.1f) ||
            !owner.Locomotion.IsPathValid)
        {
            owner.SwitchState(EnemyState.COMBAT_IDLE);
            return;
        }

        // 保险③：卡住检测（障碍、其他敌人挤住、寻路异常都能兜住）
        if (owner.Locomotion.CurrentSpeed < StuckSpeedThreshold)
            stuckTime += Time.deltaTime;
        else
            stuckTime = 0f;

        if (stuckTime >= StuckTimeout)
        {
            owner.SwitchState(EnemyState.COMBAT_IDLE);
            return;
        }

        // 身体始终面向玩家（Agent 转向已关闭）
        owner.FaceTarget(owner.Player.position);
    }
}
