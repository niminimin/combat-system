using UnityEngine;

/// <summary>
/// 受击反应：被命中时播放受击动画。
///
/// 刻意保持极简（这是最小验证版本）：
///   · 只依赖 Animator，不关心伤害数值、血量、击退
///   · 任何"能被打"的角色都可以挂它（敌人、可破坏物）
/// 之后要扩展血量/死亡/击退，都往这个组件里加即可，命中方不用改。
/// </summary>
[DisallowMultipleComponent]
public class EnemyHitReaction : MonoBehaviour, IDamageReceiver
{
    [Tooltip("留空则自动在子物体中查找 Animator")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator 里的受击触发器参数名")]
    [SerializeField] private string hitTrigger = "Impact";

    [Tooltip("受击期间要不要忽略后续命中（避免动画被连续打断）")]
    [SerializeField] private bool ignoreHitsWhileReacting = true;

    [Tooltip("受击反应持续时间（秒）")]
    [SerializeField, Min(0f)] private float reactionDuration = 0.6f;

    [SerializeField] private bool logHits = true;

    private int _triggerHash;
    private float _reactionUntil;

    public bool IsReacting => Time.time < _reactionUntil;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _triggerHash = Animator.StringToHash(hitTrigger);
    }

    /// <summary>被命中时调用（由 MeleeHitbox 触发）。</summary>
    public void OnHit(GameObject source)
    {
        if (ignoreHitsWhileReacting && IsReacting) return;

        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(_triggerHash);

        _reactionUntil = Time.time + reactionDuration;

        if (logHits)
            Debug.Log($"[{nameof(EnemyHitReaction)}] {name} 被 {source.name} 命中", this);
    }
}
