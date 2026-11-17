using UnityEngine;

/// <summary>
/// 处决提示：在"按 F 会处决的那个敌人"头上显示一个 F 图标。
///
/// 用屏幕空间实现（一个图标对象），每帧把世界坐标转成屏幕坐标。
/// 只显示"最近的合格目标"，所以图标本身就是目标指示器。
/// </summary>
[DisallowMultipleComponent]
public class ExecutePromptUI : MonoBehaviour
{
    [Tooltip("玩家处决组件（提供当前目标）")]
    [SerializeField] private PlayerFinisher finisher;

    [Tooltip("图标根节点（会被移动到目标头顶）")]
    [SerializeField] private RectTransform icon;

    [Tooltip("渲染用的相机，留空则用 Camera.main")]
    [SerializeField] private Camera worldCamera;

    [Tooltip("图标相对敌人脚底的高度偏移（米）")]
    [SerializeField, Min(0f)] private float heightOffset = 2.0f;

    [Tooltip("上下浮动的幅度（像素）")]
    [SerializeField, Min(0f)] private float bobAmplitude = 5f;

    [Tooltip("上下浮动的频率（Hz）")]
    [SerializeField, Min(0f)] private float bobFrequency = 1.2f;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
    }

    private void LateUpdate()
    {
        EnemyController target = finisher != null ? finisher.Candidate : null;

        if (target == null || target.IsDead || worldCamera == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 world = target.transform.position + Vector3.up * heightOffset;
        Vector3 screen = worldCamera.WorldToScreenPoint(world);

        // z <= 0 表示目标在相机背后
        if (screen.z <= 0f)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        icon.position = new Vector3(screen.x, screen.y + bob, 0f);
    }

    private void SetVisible(bool visible)
    {
        if (icon != null && icon.gameObject.activeSelf != visible)
            icon.gameObject.SetActive(visible);
    }
}
