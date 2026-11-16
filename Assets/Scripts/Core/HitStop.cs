using UnityEngine;

/// <summary>
/// 顿帧（hit stop）：命中瞬间把时间"冻住"几十毫秒，让打击感更扎实。
///
/// 原理就是临时把 Time.timeScale 压到很小，再用**不受时间缩放影响**的
/// Time.unscaledTime 计时恢复。
///
/// 注意：
///   · 顿帧期间所有 Time.deltaTime 都会变小 → 角色、动画、AI 一起"卡住"，这正是想要的效果
///   · 如果别处也在改 timeScale（比如暂停），用 _restoreScale 还原成进入前的值，不会覆盖成 1
/// </summary>
[DisallowMultipleComponent]
public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    [Header("默认参数")]
    [Tooltip("默认冻结时长（真实时间，秒）")]
    [SerializeField, Min(0.01f)] private float defaultDuration = 0.06f;

    [Tooltip("冻结时的 timeScale（越小越卡顿）")]
    [SerializeField, Range(0.01f, 0.5f)] private float defaultScale = 0.05f;

    private bool _frozen;
    private float _restoreAt;      // unscaledTime
    private float _restoreScale = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>触发一次顿帧。重复调用会延长冻结时间。</summary>
    public void Freeze(float duration = -1f, float scale = -1f)
    {
        if (!_frozen)
        {
            _restoreScale = Time.timeScale;
            _frozen = true;
        }

        Time.timeScale = scale > 0f ? scale : defaultScale;
        _restoreAt = Time.unscaledTime + (duration > 0f ? duration : defaultDuration);
    }

    private void Update()
    {
        if (!_frozen) return;
        if (Time.unscaledTime < _restoreAt) return;

        Time.timeScale = _restoreScale;
        _frozen = false;
    }

    private void OnDisable()
    {
        // 组件被禁用时别把时间留在冻结状态
        if (_frozen)
        {
            Time.timeScale = _restoreScale;
            _frozen = false;
        }
    }
}
