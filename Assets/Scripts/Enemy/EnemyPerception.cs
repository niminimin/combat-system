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

    private Transform _player;
    private float _nextLookupTime;

    /// <summary>
    /// 玩家引用。惰性解析：
    ///   ① 优先读 GameManager.Player（由 PlayerIdentity 注册）
    ///   ② 没有 Manager 就按 Tag 查找
    ///   ③ 找不到时每秒重试一次（覆盖"玩家晚于敌人生成"的情况）
    /// </summary>
    public Transform Player
    {
        get
        {
            if (_player != null) return _player;              // 已有（Unity 假 null 会正确判 false）
            if (Time.time < _nextLookupTime) return null;     // 节流，避免每帧全局搜索
            _nextLookupTime = Time.time + 1f;
            _player = ResolvePlayer();
            return _player;
        }
    }

    // Start 在"所有 Awake 之后"执行，能拿到在 Awake 里注册的 GameManager
    private void Start()
    {
        _player = ResolvePlayer();

        if (_player == null)
            Debug.LogWarning($"[{nameof(EnemyPerception)}] 找不到玩家，敌人会一直站岗（之后每秒重试一次）。", this);
    }

    private Transform ResolvePlayer()
    {
        if (GameManager.Instance != null && GameManager.Instance.Player != null)
            return GameManager.Instance.Player;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
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
