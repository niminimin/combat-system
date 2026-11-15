using UnityEngine;

/// <summary>
/// 挂在玩家身上，把自己注册给 GameManager。
///
/// 为什么单独一个脚本，而不是写进 ThirdPersonMotor：
///   · 移动组件不应该关心"全局上下文"
///   · 玩家可能是预制体，注册逻辑要跟着预制体走
///   · 以后玩家有多个（联机/切换角色）时，这层可以直接扩展
///
/// 注册时机同时放在 Awake 和 Start：
///   · GameManager 先 Awake → 这里注册成功
///   · 这里先 Awake → GameManager.Start 会按 Tag 兜底
/// 两条路都能通，不依赖执行顺序。
/// </summary>
[DisallowMultipleComponent]
public class PlayerIdentity : MonoBehaviour
{
    private void Awake()
    {
        if (GameManager.Instance != null) GameManager.Instance.RegisterPlayer(transform);
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.RegisterPlayer(transform);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.ClearPlayer(transform);
    }
}
