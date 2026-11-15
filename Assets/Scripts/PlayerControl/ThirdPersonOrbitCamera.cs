using UnityEngine;

/// <summary>
/// 第三人称轨道相机：鼠标左右控制 yaw（360° 无限制），上下控制 pitch（带限位）。
///
/// 为什么不用 Transform.RotateAround / 不直接"用四元数转"：
///   · RotateAround 每帧直接改 transform.position 与 rotation，状态是隐式的：
///     拿不到当前 pitch，限位只能反推 eulerAngles（±90° 附近分解不稳定，易翻转），
///     且逐帧累加会漂移，帧率不稳时抖动。
///   · 四元数适合表示与插值，不适合作为可读、可钳制的角度状态（不能 Clamp）。
///   · 因此采用「角度驱动」：内部只维护 yaw / pitch 两个标量，
///     每帧由它们生成 Quaternion.Euler(pitch, yaw, 0)，
///     位置 = 枢轴 + 旋转 * (Vector3.back * distance)。位置是纯函数，不累积误差。
///
/// 鼠标输入走旧版 Input Manager（本项目 activeInputHandler = 0）。
/// </summary>
[DisallowMultipleComponent]
public class ThirdPersonOrbitCamera : MonoBehaviour
{
    [Header("目标")]
    [SerializeField] private Transform target;

    [Tooltip("相机围绕的枢轴点，相对目标原点的偏移；Y 通常取角色胸口/肩部高度")]
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("未手动指定目标时，启动时按标签自动查找玩家")]
    [SerializeField] private bool autoFindPlayerByTag = true;
    [SerializeField] private string playerTag = "Player";

    [Header("轨道")]
    [Tooltip("相机到枢轴的距离（米）")]
    [SerializeField, Min(0.1f)] private float distance = 5f;

    [Tooltip("鼠标水平灵敏度（度 / 输入单位），常用 2~5")]
    [SerializeField, Min(0f)] private float yawSensitivity = 3.5f;

    [Tooltip("鼠标垂直灵敏度（度 / 输入单位），一般略低于水平")]
    [SerializeField, Min(0f)] private float pitchSensitivity = 2.5f;

    [Tooltip("俯仰下限（负值 = 相机放低、向上仰视目标）。-90 表示完全垂直仰视")]
    [SerializeField] private float minPitch = -35f;

    [Tooltip("俯仰上限（正值 = 相机抬高、向下俯视目标）。90 表示正上方垂直下看")]
    [SerializeField] private float maxPitch = 70f;

    [Tooltip("反转垂直方向（Y 轴）")]
    [SerializeField] private bool invertY;

    [Header("初始朝向")]
    [SerializeField] private float startYaw;
    [SerializeField] private float startPitch = 15f;

    [Header("平滑")]
    [Tooltip("角度平滑时间，0 表示完全跟手；手感偏硬可适当加大")]
    [SerializeField, Min(0f)] private float angleSmoothTime = 0.04f;

    [Header("光标")]
    [Tooltip("由本脚本管理光标锁定（运行时锁定并隐藏，Esc 释放）")]
    [SerializeField] private bool controlCursor = true;

    [Tooltip("需要按住鼠标右键才能转视角")]
    [SerializeField] private bool requireRightMouse;

    [SerializeField] private KeyCode releaseCursorKey = KeyCode.Escape;

    [Header("防穿墙")]
    [Tooltip("相机被墙体挡住时自动拉近，避免看到物体内部")]
    [SerializeField] private bool avoidClipping = true;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField, Min(0.05f)] private float collisionRadius = 0.28f;
    [Tooltip("拉近的极限距离，建议不小于角色半径")]
    [SerializeField, Min(0.1f)] private float minDistance = 1f;
    [Tooltip("被挡后恢复距离的速度")]
    [SerializeField, Min(0f)] private float zoomOutSpeed = 8f;

    // 当前（用于渲染的）角度，经平滑
    private float _yaw;
    private float _pitch;

    // 输入累积的目标角度
    private float _targetYaw;
    private float _targetPitch;

    private float _yawVelocity;
    private float _pitchVelocity;

    // <= 0 表示尚未初始化（例如编辑模式下 AddComponent 不会调用 Awake）
    private float _currentDistance = -1f;

    // 玩家"希望"光标被锁住的状态（与"实际是否锁住"分开，方便焦点切换时恢复）
    private bool _lockWanted;

    // 锁定光标的那一帧要丢弃鼠标增量（此刻鼠标会被挪到屏幕中央，delta 是跳变值）
    private bool _skipMouseFrame;

    // 剩余的重试帧数：游戏视图刚获得焦点时，锁定请求可能被系统忽略，需要多试几帧
    private int _lockRetryFrames;

    /// <summary>相机环绕的目标。</summary>
    public Transform Target
    {
        get => target;
        set => target = value;
    }

    /// <summary>当前水平角（度）。</summary>
    public float Yaw => _yaw;

    /// <summary>当前俯仰角（度）。正值 = 相机在目标上方俯视，负值 = 相机在下方仰视。</summary>
    public float Pitch => _pitch;

    /// <summary>水平朝向，供角色移动方向换算使用。</summary>
    public Quaternion PlanarRotation => Quaternion.Euler(0f, _yaw, 0f);

    /// <summary>当前环绕的枢轴点（世界坐标）。</summary>
    public Vector3 Pivot => target ? target.position + pivotOffset : transform.position;

    private void Awake()
    {
        if (!target && autoFindPlayerByTag)
        {
            GameObject found = GameObject.FindWithTag(playerTag);
            if (found) target = found.transform;
        }

        _targetYaw = _yaw = startYaw;
        _targetPitch = _pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
        _currentDistance = distance;

        if (!target)
            Debug.LogWarning($"[{nameof(ThirdPersonOrbitCamera)}] 未指定 target，相机不会跟随。", this);
    }

    private void Start()
    {
        // 进游戏立刻隐藏并锁住光标
        _lockWanted = controlCursor && !requireRightMouse;
        TryApplyCursorLock();
    }

    private void OnDisable()
    {
        _lockWanted = false;
        _lockRetryFrames = 0;
        ApplyCursorLock(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!controlCursor) return;

        // 切出窗口必须放开，否则鼠标会被困在游戏里；切回来再锁上
        if (hasFocus) TryApplyCursorLock();
        else ApplyCursorLock(false);
    }

    private void Update()
    {
        HandleCursor();

        // 关键：编辑器里点 Play 的瞬间，游戏视图往往还没有焦点，
        // 此时 Cursor.lockState = Locked 会被系统忽略（光标照样露着）。
        // 所以不能只尝试一次，要在随后约两秒内反复重试，锁上就停。
        if (_lockRetryFrames > 0)
        {
            _lockRetryFrames--;

            // 用户中途按了 Esc / 松开右键，就不再重试
            if (!_lockWanted)
            {
                _lockRetryFrames = 0;
            }
            else if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
            {
                ApplyCursorLock(true);
                _skipMouseFrame = true;
            }
            else
            {
                _lockRetryFrames = 0;
            }
        }

        // 锁定/解锁的瞬间，鼠标会被挪到屏幕中央，这一帧的 Mouse X/Y 是跳变值，
        // 直接吃进去会让镜头猛甩一下，所以丢掉当帧
        if (_skipMouseFrame)
        {
            _skipMouseFrame = false;
            return;
        }

        if (!CanLook()) return;

        // 鼠标轴本身就是帧间增量，不需要再乘 Time.deltaTime
        _targetYaw += Input.GetAxis("Mouse X") * yawSensitivity;

        float pitchDelta = Input.GetAxis("Mouse Y") * pitchSensitivity;
        if (invertY) pitchDelta = -pitchDelta;

        // 限位的唯一入口：夹紧"目标"角度，平滑过程不会越界
        _targetPitch = Mathf.Clamp(_targetPitch - pitchDelta, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (!target) return;

        // 1) 角度平滑。SmoothDampAngle 正确处理 359°↔0° 环绕，且与帧率无关
        if (angleSmoothTime > 0f)
        {
            _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVelocity, angleSmoothTime);
            _pitch = Mathf.SmoothDampAngle(_pitch, _targetPitch, ref _pitchVelocity, angleSmoothTime);
        }
        else
        {
            _yaw = _targetYaw;
            _pitch = _targetPitch;
        }

        // 2) 由角度直接生成朝向：四元数只做表示，不累积状态
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        // 3) 位置 = 枢轴 + 旋转后的反向偏移，纯函数，无误差累积
        Vector3 pivot = target.position + pivotOffset;
        float wantedDistance = avoidClipping ? ResolveDistance(pivot, rotation, distance) : distance;

        // 首次运行（Awake 未执行）时直接落到目标距离，避免从 0 弹开
        if (_currentDistance <= 0f) _currentDistance = wantedDistance;

        // 拉近立即生效（防穿墙），推远平滑（避免闪跳）
        _currentDistance = wantedDistance < _currentDistance
            ? wantedDistance
            : Mathf.Lerp(_currentDistance, wantedDistance, 1f - Mathf.Exp(-zoomOutSpeed * Time.deltaTime));

        transform.SetPositionAndRotation(pivot + rotation * (Vector3.back * _currentDistance), rotation);
    }

    /// <summary>检测枢轴到相机之间的遮挡，返回允许的相机距离。</summary>
    private float ResolveDistance(Vector3 pivot, Quaternion rotation, float wishDistance)
    {
        Vector3 back = rotation * Vector3.back;

        // SphereCast 不会命中与起始球体重叠的碰撞体，因此角色自身的胶囊不会挡住相机
        if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, wishDistance,
                collisionMask, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Clamp(hit.distance, minDistance, wishDistance);
        }

        return wishDistance;
    }

    /// <summary>直接指定视角角度（过场、重生、锁定目标时使用）。</summary>
    public void SetAngles(float yaw, float pitch, bool immediate = false)
    {
        _targetYaw = yaw;
        _targetPitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (immediate)
        {
            _yaw = _targetYaw;
            _pitch = _targetPitch;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
        }
    }

    private void HandleCursor()
    {
        if (!controlCursor) return;

        if (Input.GetKeyDown(releaseCursorKey))
        {
            _lockWanted = false;
            ApplyCursorLock(false);
            return;
        }

        if (requireRightMouse)
        {
            if (Input.GetMouseButtonDown(1))
            {
                _lockWanted = true;
                TryApplyCursorLock();
            }
            else if (Input.GetMouseButtonUp(1))
            {
                _lockWanted = false;
                ApplyCursorLock(false);
            }
        }
        else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
        {
            // 点了画面就重新接管光标
            _lockWanted = true;
            TryApplyCursorLock();
        }
    }

    /// <summary>请求锁定光标，并在随后若干帧内反复重试（应对"游戏视图刚获得焦点"时锁定被忽略）。</summary>
    private void TryApplyCursorLock()
    {
        if (!controlCursor || !_lockWanted)
        {
            ApplyCursorLock(false);
            return;
        }

        ApplyCursorLock(true);
        _skipMouseFrame = true;
        _lockRetryFrames = 120;   // 约 2 秒（60fps），锁上后立即停止
    }

    /// <summary>外部（菜单、对话、暂停）统一控制光标锁定。</summary>
    public void SetCursorLocked(bool locked)
    {
        _lockWanted = locked;
        if (locked) TryApplyCursorLock();
        else
        {
            _lockRetryFrames = 0;
            ApplyCursorLock(false);
        }
    }

    private bool CanLook()
    {
        if (!controlCursor) return true;
        if (Cursor.lockState != CursorLockMode.Locked) return false;
        if (requireRightMouse && !Input.GetMouseButton(1)) return false;
        return true;
    }

    private static void ApplyCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnValidate()
    {
        if (maxPitch < minPitch) maxPitch = minPitch;
        if (distance < 0.1f) distance = 0.1f;
    }
}
