using UnityEngine;

/// <summary>
/// 相机抖动。挂在主相机上（不是玩家身上）。
///
/// 执行顺序很关键：ThirdPersonOrbitCamera 每帧在 LateUpdate 里
/// 用 SetPositionAndRotation 完全重写相机的位置和朝向，
/// 所以这里必须**排它后面**执行（DefaultExecutionOrder 100），
/// 否则抖动的位移和旋转会当场被覆盖掉。
///
/// 抖动模型：
///   · 用"trauma"（0~1）表示强度，每次 Shake 累加
///   · 实际偏移 = trauma² × 最大值（平方让衰减更有"劲"）
///   · 用 Perlin 噪声做位移，比每帧随机更顺滑
///   · 用 unscaledTime，顿帧期间抖动依然在动
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("强度")]
    [Tooltip("强度为 1 时的最大位移（米）")]
    [SerializeField, Min(0f)] private float maxPositionOffset = 0.12f;

    [Tooltip("强度为 1 时的最大俯仰偏移（度）")]
    [SerializeField, Min(0f)] private float maxPitchAngle = 0.8f;

    [Tooltip("强度为 1 时的最大滚转偏移（度）")]
    [SerializeField, Min(0f)] private float maxRollAngle = 1.2f;

    [Header("衰减")]
    [Tooltip("每秒衰减多少强度（越大抖得越短）")]
    [SerializeField, Min(0.1f)] private float decaySpeed = 2.2f;

    [Tooltip("噪声频率，越大抖得越碎")]
    [SerializeField, Min(1f)] private float noiseFrequency = 30f;

    private float _trauma;

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

    /// <summary>叠加一次抖动。amount 0~1（0.3 轻震，0.6 重击）。</summary>
    public void Shake(float amount)
    {
        if (amount <= 0f) return;
        _trauma = Mathf.Clamp01(_trauma + amount);
    }

    private void LateUpdate()
    {
        if (_trauma <= 0f) return;

        float strength = _trauma * _trauma;      // 平方：衰减尾段更柔和
        float t = Time.unscaledTime * noiseFrequency;

        // 三个通道用不同的噪声采样点，避免位移和旋转完全同步（那样很假）
        float nx = Mathf.PerlinNoise(t, 0f) - 0.5f;
        float ny = Mathf.PerlinNoise(0f, t) - 0.5f;
        float nr = Mathf.PerlinNoise(t, 17.3f) - 0.5f;

        // 位移用相机自身的左右/上方向，绕视线的偏移最自然
        float offset = 2f * maxPositionOffset * strength;
        transform.position += transform.right * (nx * offset) + transform.up * (ny * offset);

        // 旋转：轻微俯仰 + 滚转
        float pitch = ny * 2f * maxPitchAngle * strength;
        float roll = nr * 2f * maxRollAngle * strength;
        transform.rotation *= Quaternion.Euler(pitch, 0f, roll);

        _trauma = Mathf.Max(0f, _trauma - decaySpeed * Time.unscaledDeltaTime);
    }
}
