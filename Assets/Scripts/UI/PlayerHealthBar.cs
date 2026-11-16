using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家血条（3DRPG 风格：绿条 + 延迟收缩的红条）。
///
/// 分层：
///   底色（深灰）
///   ├── 红层：受伤后先"留住"一段时间的旧血量，再慢慢缩
///   └── 绿层：当前血量，快速跟上
/// 掉血时的观感是：绿条立刻掉 → 露出红色 → 红色延迟一会儿再慢慢缩下去。
///
/// 只做玩家血条 —— 普通敌人不显示血量。
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealthBar : MonoBehaviour
{
    [Tooltip("玩家的血量组件。留空则通过 GameManager.Player 自动查找")]
    [SerializeField] private CharacterStats stats;

    [Header("图层")]
    [Tooltip("绿条：当前血量")]
    [SerializeField] private Image greenFill;

    [Tooltip("红条：延迟收缩的旧血量（视觉上垫在绿条下面）")]
    [SerializeField] private Image redFill;

    [Header("节奏")]
    [Tooltip("绿条追赶速度")]
    [SerializeField, Min(0.1f)] private float greenSpeed = 12f;

    [Tooltip("红条开始收缩前的停顿（秒）")]
    [SerializeField, Min(0f)] private float redDelay = 0.35f;

    [Tooltip("红条收缩速度（越小越慢）")]
    [SerializeField, Min(0.1f)] private float redSpeed = 1.2f;

    [Header("颜色")]
    [SerializeField] private Color greenColor = new Color(0.28f, 0.75f, 0.32f);
    [SerializeField] private Color redColor = new Color(0.82f, 0.2f, 0.2f);

    private float _target = 1f;      // 真实血量比例
    private float _green = 1f;       // 绿条当前显示值
    private float _red = 1f;         // 红条当前显示值
    private float _redResumeTime;    // 红条可以开始收缩的时间
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
        float ratio = max > 0.001f ? Mathf.Clamp01(current / max) : 0f;

        // 扣血：红条先停一下再缩；回血：红条立刻跟上，不留空档
        if (ratio < _target) _redResumeTime = Time.time + redDelay;
        else _red = Mathf.Max(_red, ratio);

        _target = ratio;
    }

    private void Refresh()
    {
        if (stats == null) return;

        _target = stats.MaxHealth > 0.001f
            ? Mathf.Clamp01(stats.CurrentHealth / stats.MaxHealth)
            : 0f;

        _green = _target;
        _red = _target;
    }

    private void Update()
    {
        // 绿条：快速追上真实值
        _green = Mathf.MoveTowards(_green, _target, greenSpeed * Time.deltaTime);

        // 红条：延迟后慢慢缩下去；永远不低于绿条（否则会盖住绿条）
        if (Time.time >= _redResumeTime)
            _red = Mathf.MoveTowards(_red, _target, redSpeed * Time.deltaTime);

        if (_red < _green) _red = _green;

        if (greenFill != null)
        {
            greenFill.fillAmount = _green;
            greenFill.color = greenColor;
        }

        if (redFill != null)
        {
            redFill.fillAmount = _red;
            redFill.color = redColor;
        }
    }
}
