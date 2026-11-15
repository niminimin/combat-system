using UnityEngine;

/// <summary>
/// 敌人感知：判断"现在能不能看到玩家"。
///
/// 三个条件同时满足才算看到：
///   ① 距离在视野范围内
///   ② 玩家在视野夹角内（不是背后）
///   ③ 没有墙体遮挡（Line of Sight）
///
/// 注意：追击过程中**不依赖**这里的视野判断（ChaseState 用距离判断），
/// 否则玩家绕到背后会让敌人立刻"失忆"，手感很蠢。
/// </summary>
public class EnemyPerception : MonoBehaviour
{
    [Header("视野")]
    public float viewDistance = 12f;

    [Range(10f, 360f)]
    public float viewAngle = 140f;

    [Tooltip("眼睛高度（相对敌人脚下）")]
    public float eyeHeight = 1.6f;

    [Tooltip("瞄准玩家身体的高度（相对玩家脚下）")]
    public float targetHeight = 1.2f;

    [Tooltip("遮挡检测层：默认检测所有层，命中非玩家就算被挡住")]
    public LayerMask obstacleMask = ~0;

    /// <summary>场景里的玩家（按 Tag 查找，只找一次）。</summary>
    public Transform Player { get; private set; }

    private void Awake()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) Player = playerObject.transform;
        else Debug.LogWarning($"[{nameof(EnemyPerception)}] 场景里找不到 Tag=Player 的物体，敌人不会主动发现玩家。", this);
    }

    /// <summary>能否看到玩家；<paramref name="distance"/> 输出平面距离。</summary>
    public bool CanSeePlayer(out float distance)
    {
        distance = float.MaxValue;
        if (Player == null) return false;

        Vector3 from = transform.position + Vector3.up * eyeHeight;
        Vector3 to = Player.position + Vector3.up * targetHeight;
        Vector3 delta = to - from;

        distance = delta.magnitude;
        if (distance > viewDistance) return false;

        // 夹角判断（只在水平面上比较，避免高低差影响）
        Vector3 flat = new Vector3(delta.x, 0f, delta.z);
        if (flat.sqrMagnitude > 0.0001f &&
            Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f)
            return false;

        // 视线遮挡
        RaycastHit hit;
        if (Physics.Raycast(from, delta.normalized, out hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.root != Player.root)
                return false;   // 先撞到墙/道具 → 看不见
        }

        distance = Vector3.Distance(transform.position, Player.position);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.5f);
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Gizmos.DrawWireSphere(eye, viewDistance);

        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(eye, eye + left * viewDistance);
        Gizmos.DrawLine(eye, eye + right * viewDistance);
    }
}
