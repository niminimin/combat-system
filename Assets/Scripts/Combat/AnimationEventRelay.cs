using UnityEngine;

/// <summary>
/// 动画事件转发器。
///
/// 为什么需要它：
///   Unity 的 Animation Event 只会派发给 **Animator 所在的那个 GameObject** 上的组件，
///   不会向上/向下找父物体。而本项目的 Animator 挂在模型节点（Visual）上，
///   攻击逻辑 AttackController 挂在角色根节点（Player）上，
///   所以需要一个"同物体上的接收器"把事件转发过去。
///
/// 用法：挂在带 Animator 的那个物体上，动画事件函数名与这里的方法名一致。
/// </summary>
[DisallowMultipleComponent]
public class AnimationEventRelay : MonoBehaviour
{
    [Tooltip("留空则自动向上查找角色根节点上的 AttackController")]
    [SerializeField] private AttackController attackController;

    private void Awake()
    {
        if (attackController == null) attackController = GetComponentInParent<AttackController>();

        if (attackController == null)
            Debug.LogWarning($"[{nameof(AnimationEventRelay)}] 找不到 AttackController，命中事件不会被转发。", this);
    }

    /// <summary>由攻击动画的 Animation Event 调用（命中帧）。</summary>
    public void OnHitEvent()
    {
        if (attackController != null) attackController.OnHitEvent();
    }
}
