using UnityEngine;

/// <summary>
/// 第三人称角色移动：相机相对方向 + CharacterController + 重力/跳跃。
///
/// 设计要点：
///   · 输入方向相对**相机**而不是世界轴：按 W 永远是"镜头前方"，这是第三人称的基本手感。
///   · 用相机暴露的 PlanarRotation（纯水平朝向）换算，避免相机俯仰导致前方向量被压缩。
///   · 角色只朝"移动方向"转身，不跟随相机左右转头（否则站着不动时角色会被鼠标带着扭）。
///   · 加减速分离：起步有加速、松手有减速，不会瞬间起停。
///
/// 输入使用旧版 Input Manager（本项目 activeInputHandler = 0）。
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class ThirdPersonMotor : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("为空时自动查找场景里的 ThirdPersonOrbitCamera")]
    [SerializeField] private ThirdPersonOrbitCamera orbitCamera;

    [Header("速度")]
    [Tooltip("常规移动速度（米/秒）")]
    [SerializeField, Min(0f)] private float walkSpeed = 4f;

    [Tooltip("按住冲刺键时的速度")]
    [SerializeField, Min(0f)] private float sprintSpeed = 6.5f;

    [Tooltip("加速度（米/秒²），越大起步越快")]
    [SerializeField, Min(0f)] private float acceleration = 24f;

    [Tooltip("松开输入后的减速度（米/秒²）")]
    [SerializeField, Min(0f)] private float deceleration = 28f;

    [Tooltip("转身速度（度/秒）")]
    [SerializeField, Min(0f)] private float turnSpeedDegPerSec = 720f;

    [Header("重力与跳跃")]
    [SerializeField] private float gravity = -22f;

    [Tooltip("终端下落速度（负值）。限制单帧位移，避免高速穿透薄地面")]
    [SerializeField] private float terminalVelocity = -30f;

    [SerializeField, Min(0f)] private float jumpHeight = 1.1f;
    [SerializeField] private bool enableJump = true;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

    [Header("调试")]
    [SerializeField] private bool debugLog;

    [Header("视觉对齐")]
    [Tooltip("角色模型根节点，留空则自动查找名为 Visual 的子物体")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("启动时自动把模型最低点对齐到角色脚下。注意：若模型最低点不是鞋底（比如垂下的布料），会把脚抬高，默认关闭")]
    [SerializeField] private bool alignVisualOnAwake = false;

    [Header("动画")]
    [Tooltip("角色 Animator，留空则自动在子物体中查找")]
    [SerializeField] private Animator animator;

    [Tooltip("Speed 参数的平滑时间，避免走跑切换突兀")]
    [SerializeField, Min(0f)] private float animSpeedDamp = 0.1f;

    [Header("战斗联动")]
    [Tooltip("攻击期间锁住移动（留空则自动查找同物体上的 AttackController）")]
    [SerializeField] private AttackController attackController;

    [Tooltip("受击硬直期间锁住移动（留空则自动查找同物体上的 PlayerHitReaction）")]
    [SerializeField] private PlayerHitReaction hitReaction;

    // 参数哈希：比每帧传字符串快，也避免拼写错误
    private static readonly int SpeedParamHash = Animator.StringToHash("Speed");

    private CharacterController _controller;
    private Vector3 _planarVelocity;   // 水平速度（世界坐标）
    private float _verticalVelocity;   // 竖直速度
    private bool _isGrounded;

    /// <summary>当前水平速度大小（米/秒），接动画时用来驱动走/跑混合。</summary>
    public float CurrentSpeed => new Vector2(_planarVelocity.x, _planarVelocity.z).magnitude;

    /// <summary>0~1 的归一化速度，等于 CurrentSpeed / walkSpeed。</summary>
    public float NormalizedSpeed => walkSpeed > 0.001f ? Mathf.Clamp01(CurrentSpeed / walkSpeed) : 0f;

    /// <summary>当前是否站在地面。</summary>
    public bool IsGrounded => _isGrounded;

    /// <summary>是否有移动输入（用于切换 Idle / Move 动画）。</summary>
    public bool HasMoveInput { get; private set; }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (orbitCamera == null)
            orbitCamera = FindObjectOfType<ThirdPersonOrbitCamera>();

        if (orbitCamera == null)
            Debug.LogWarning($"[{nameof(ThirdPersonMotor)}] 场景里找不到 ThirdPersonOrbitCamera，" +
                             "移动将退化为按世界坐标轴方向。", this);

        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (attackController == null) attackController = GetComponent<AttackController>();
        if (hitReaction == null) hitReaction = GetComponent<PlayerHitReaction>();

        if (alignVisualOnAwake) AlignVisualToGround();
    }

    /// <summary>
    /// 把模型原点放到角色脚下（世界 y = 角色 y）。
    ///
    /// 注意：**不要**用"所有网格包围盒的最低点"来对齐。
    /// 只要模型在脚底以下还有一点几何体（衣摆、弓梢、绑带），
    /// 最低点就不是鞋底，那样会把整个角色抬起来，看起来像悬空。
    /// Mixamo 等标准人形资源的原点就在脚底，归零即可。
    /// </summary>
    [ContextMenu("把模型原点对齐到脚下")]
    public void AlignVisualToGround()
    {
        Transform visual = visualRoot != null ? visualRoot : transform.Find("Visual");
        if (visual == null)
        {
            Debug.LogWarning($"[{nameof(ThirdPersonMotor)}] 找不到视觉子物体（Visual），无法对齐脚底。", this);
            return;
        }

        Vector3 local = visual.localPosition;
        visual.localPosition = new Vector3(local.x, 0f, local.z);

        if (visual.localPosition.y != 0f)
            Debug.LogWarning($"[{nameof(ThirdPersonMotor)}] 对齐后 localPosition.y 仍为 {visual.localPosition.y}", this);
    }

    private void Update()
    {
        Vector2 input = ReadMoveInput();
        bool sprinting = Input.GetKey(sprintKey);

        // 攻击期间不接受移动输入（重力/贴地仍然生效），
        // 判定来自通用的 AttackController，与"是不是玩家"无关
        if (attackController != null && attackController.IsAttacking)
        {
            input = Vector2.zero;
            sprinting = false;
        }

        // 受击硬直期间也锁住移动
        if (hitReaction != null && hitReaction.IsReacting)
        {
            input = Vector2.zero;
            sprinting = false;
        }

        HasMoveInput = input.sqrMagnitude > 0.0001f;

        // 1) 输入方向 → 世界方向（相对相机水平朝向）
        Vector3 wishDirection = CameraRelativeDirection(input);

        // 2) 目标速度
        float targetSpeed = 0f;
        if (HasMoveInput) targetSpeed = sprinting ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = wishDirection * targetSpeed;

        // 3) 加速 / 减速
        float rate = HasMoveInput ? acceleration : deceleration;
        _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetVelocity, rate * Time.deltaTime);

        // 4) 朝移动方向转身
        if (HasMoveInput)
        {
            Quaternion targetRotation = Quaternion.LookRotation(wishDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, turnSpeedDegPerSec * Time.deltaTime);
        }

        // 5) 重力与跳跃
        _isGrounded = _controller.isGrounded;

        if (_isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;                    // 轻微下压，保证贴地

        if (enableJump && _isGrounded && Input.GetKeyDown(jumpKey))
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);   // v = √(2gh)

        _verticalVelocity += gravity * Time.deltaTime;

        // 终端速度：防止长时间离地后单帧位移过大，穿透薄碰撞体（陷进地面）
        if (_verticalVelocity < terminalVelocity) _verticalVelocity = terminalVelocity;

        // 6) 应用位移
        Vector3 motion = _planarVelocity + Vector3.up * _verticalVelocity;
        _controller.Move(motion * Time.deltaTime);

        // 7) 驱动动画：Speed 决定 Idle / 走 / 跑的混合权重
        if (animator != null)
            animator.SetFloat(SpeedParamHash, CurrentSpeed, animSpeedDamp, Time.deltaTime);

        if (debugLog && HasMoveInput)
            Debug.Log($"[Motor] speed={CurrentSpeed:F2} grounded={_isGrounded} " +
                      $"animator={(animator != null ? animator.name : "NULL")} " +
                      $"animSpeed={(animator != null ? animator.GetFloat(SpeedParamHash).ToString("F2") : "-")}", this);
    }

    private Vector2 ReadMoveInput()
    {
        // 旧版 Input Manager 的标准轴（WASD / 方向键）
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 raw = new Vector2(h, v);
        return raw.sqrMagnitude > 1f ? raw.normalized : raw;   // 斜向不加速
    }

    /// <summary>把 -1~1 的输入向量，换成世界空间的水平方向。</summary>
    private Vector3 CameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

        // 优先用相机的纯水平朝向，避免俯视角时 forward 被压缩
        Vector3 forward;
        if (orbitCamera != null)
        {
            forward = orbitCamera.PlanarRotation * Vector3.forward;
        }
        else
        {
            forward = transform.forward;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = new Vector3(forward.z, 0f, -forward.x);   // forward 顺时针 90°

        Vector3 dir = forward * input.y + right * input.x;
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }
}
