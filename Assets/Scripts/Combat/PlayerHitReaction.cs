using UnityEngine;

/// <summary>
/// 玩家受击表现：播受击动画 + 短暂锁定移动。
///
/// 目前只做"表现"，不做伤害 —— 等接入血量系统后，
/// 在同一处调用 TakeDamage 即可。
/// </summary>
[DisallowMultipleComponent]
public class PlayerHitReaction : MonoBehaviour, IDamageReceiver
{
    [Tooltip("留空则从子物体里找（玩家的 Animator 在 Visual 上）")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator 里的受击触发器名")]
    [SerializeField] private string hitTrigger = "Impact";

    [Tooltip("受击后多久解除移动锁定（秒）")]
    [SerializeField, Min(0f)] private float reactionDuration = 0.45f;

    [Tooltip("是否在控制台打印受击日志")]
    [SerializeField] private bool logHits = true;

    private int _triggerHash;
    private float _reactingUntil;

    /// <summary>是否处于受击硬直（ThirdPersonMotor 据此锁输入）。</summary>
    public bool IsReacting => Time.time < _reactingUntil;

    /// <summary>受击事件（以后接血量/UI 抖动/音效）。</summary>
    public event System.Action<GameObject> Hit;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _triggerHash = Animator.StringToHash(hitTrigger);
    }

    public void OnHit(GameObject source)
    {
        if (animator == null)
        {
            Debug.LogWarning($"[{nameof(PlayerHitReaction)}] 找不到 Animator，受击动画不会播放。", this);
            return;
        }

        animator.SetTrigger(_triggerHash);
        _reactingUntil = Time.time + reactionDuration;

        if (logHits)
            Debug.Log($"[PlayerHitReaction] 被 {(source != null ? source.name : "未知")} 命中", this);

        Hit?.Invoke(source);
    }
}
