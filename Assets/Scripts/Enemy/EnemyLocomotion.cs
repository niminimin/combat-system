using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人的移动封装（NavMeshAgent）。
///
/// 状态机里只调 MoveTo / Stop / HasArrived，
/// 换成别的移动方式（直线、A*）时只改这个类，状态不用动。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyLocomotion : MonoBehaviour
{
    public NavMeshAgent Agent { get; private set; }

    [Header("速度")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 4.5f;

    [Tooltip("距离目标多近算到达")]
    public float stopDistance = 1.2f;

    [Header("转向")]
    public float turnSpeed = 540f;

    /// <summary>当前实际移动速度（用于驱动动画混合）。</summary>
    public float CurrentSpeed => Agent != null ? Agent.velocity.magnitude : 0f;

    /// <summary>是否已经走到目的地。</summary>
    public bool HasArrived
    {
        get
        {
            if (Agent == null || !Agent.isOnNavMesh) return true;
            return !Agent.pathPending
                   && Agent.remainingDistance <= Agent.stoppingDistance + 0.1f;
        }
    }

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Agent.updateRotation = true;
        Agent.angularSpeed = turnSpeed;
        Agent.acceleration = 14f;
        Agent.stoppingDistance = stopDistance;
        Agent.speed = walkSpeed;
    }

    /// <summary>
    /// 朝目标移动（每帧调用无妨，SetDestination 会自己复用路径）。
    /// <paramref name="overrideStoppingDistance"/> 传负数表示用默认的 stopDistance。
    ///
    /// 环绕时必须传一个很小的值（如 0.1），因为环绕的目标点每帧只往前挪一两厘米，
    /// 若沿用 stopDistance(1.2)，Agent 会认为"早已到达"而原地不动。
    /// </summary>
    public void MoveTo(Vector3 destination, float speed, float overrideStoppingDistance = -1f)
    {
        if (Agent == null || !Agent.isOnNavMesh) return;

        Agent.isStopped = false;
        Agent.speed = speed;
        Agent.stoppingDistance = overrideStoppingDistance >= 0f
            ? overrideStoppingDistance
            : stopDistance;
        Agent.SetDestination(destination);
    }

    /// <summary>停止移动并清掉当前路径。</summary>
    public void Stop()
    {
        Freeze();
    }

    /// <summary>
    /// 立刻停住：停用寻路 + 清路径 + 把速度清零，
    /// 避免"已经停了但还带着残余速度滑一小段"。
    /// </summary>
    public void Freeze()
    {
        if (Agent == null || !Agent.isOnNavMesh) return;

        Agent.isStopped = true;
        Agent.ResetPath();
        Agent.velocity = Vector3.zero;
    }

    /// <summary>
    /// 开关 Agent 的自动转向。
    /// 侧移（Strafe）时必须关掉：否则角色会转向"移动方向"，
    /// 而侧移动画要求身体始终面向玩家。
    /// </summary>
    public void SetRotationEnabled(bool enabled)
    {
        if (Agent != null) Agent.updateRotation = enabled;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
