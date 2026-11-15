using UnityEngine;

/// <summary>全局游戏状态（暂时只有少数几个）。</summary>
public enum GameState
{
    Playing,
    Paused,
    GameOver
}

/// <summary>
/// 全局上下文持有者 —— 只放"谁都可能要用、但不属于某个具体系统"的东西。
///
/// 目前只有：玩家引用 + 游戏状态。
/// 刻意保持很薄：血量放 PlayerHealth、敌人生成放 Spawner、存档放 SaveService，
/// 不要往这里堆逻辑，否则会变成"改一处崩一片"的上帝对象。
/// </summary>
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>当前游戏状态。</summary>
    public GameState State { get; private set; } = GameState.Playing;

    /// <summary>场景里的玩家（由 PlayerIdentity 注册，或用 Tag 兜底查找）。</summary>
    public Transform Player { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(GameManager)}] 场景里存在多个 GameManager，销毁多余的：{name}", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 兜底：如果 PlayerIdentity 的 Awake 比这里早跑（拿不到 Instance），
        // 那就由这里按 Tag 找一次。
        if (Player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) Player = playerObject.transform;
        }

        if (Player == null)
            Debug.LogWarning($"[{nameof(GameManager)}] 场景里找不到玩家（Tag=Player），敌人不会主动发现玩家。", this);
    }

    /// <summary>由 PlayerIdentity 在自身启用时调用。</summary>
    public void RegisterPlayer(Transform player)
    {
        if (player == null) return;
        Player = player;
    }

    /// <summary>玩家被销毁/移除时调用。</summary>
    public void ClearPlayer(Transform player)
    {
        if (Player == player) Player = null;
    }

    public void SetState(GameState state) => State = state;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
