using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家血条：订阅 CharacterStats.HealthChanged，驱动一个 Filled Image。
///
/// 只做玩家血条 —— 普通敌人刻意不显示血量（动作游戏靠受击动画传达状态，
/// 多目标时血条反而变成噪音）。Boss/精英以后单独做底部横条。
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealthBar : MonoBehaviour
{
    [Tooltip("玩家的血量组件。留空则通过 GameManager.Player 自动查找")]
    [SerializeField] private CharacterStats stats;

    [Tooltip("血条填充图（Image 的 Type 必须是 Filled）")]
    [SerializeField] private Image fill;

    [Tooltip("血条追上目标值的速度（越大越快）")]
    [SerializeField, Min(0.1f)] private float smoothSpeed = 8f;

    [Header("颜色")]
    [SerializeField] private Color healthyColor = new Color(0.78f, 0.22f, 0.22f);
    [SerializeField] private Color lowColor = new Color(0.95f, 0.55f, 0.15f);
    [Tooltip("低于这个比例时开始变黄")]
    [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.3f;

    private float _target = 1f;
    private bool _subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        // GameManager 可能在 OnEnable 之后才把玩家登记进来
        TrySubscribe();
        Refresh();
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;

        if (stats == null && GameManager.Instance != null && GameManager.Instance.Player != null)
            stats = GameManager.Instance.Player.GetComponent<CharacterStats>();

        if (stats == null) return;

        stats.HealthChanged += HandleHealthChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (stats != null) stats.HealthChanged -= HandleHealthChanged;
        _subscribed = false;
    }

    private void HandleHealthChanged(float current, float max)
    {
        _target = max > 0.001f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void Refresh()
    {
        if (stats != null) _target = stats.MaxHealth > 0.001f
            ? Mathf.Clamp01(stats.CurrentHealth / stats.MaxHealth)
            : 0f;
    }

    private void Update()
    {
        if (fill == null) return;

        // 平滑追赶：掉血时血条"刷"地缩下去，比瞬变更舒服
        fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, _target, smoothSpeed * Time.deltaTime);

        fill.color = _target <= lowThreshold
            ? Color.Lerp(lowColor, healthyColor, Mathf.InverseLerp(0f, lowThreshold, _target))
            : healthyColor;
    }
}
