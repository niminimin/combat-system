using UnityEngine;

/// <summary>
/// 玩家攻击输入层：只做一件事——把"按下攻击键"翻译成 AttackController.TryAttack()。
///
/// 这一层把"玩家"和"攻击逻辑"隔开：
///   · AttackController 不知道鼠标的存在，敌人 AI 可以直接调用同一个 TryAttack()
///   · 想改成手柄/触摸，只改这个文件，攻击逻辑和动画完全不动
/// </summary>
[RequireComponent(typeof(AttackController))]
[DisallowMultipleComponent]
public class PlayerAttackInput : MonoBehaviour
{
    [SerializeField] private AttackController attackController;

    [Tooltip("弹反组件：弹反动作期间禁止攻击（留空自动查找）")]
    [SerializeField] private PlayerParry parry;

    [Tooltip("攻击键（鼠标左键 / 手柄按钮可在 Input Manager 里映射）")]
    [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;

    private void Reset()
    {
        attackController = GetComponent<AttackController>();
    }

    private void Awake()
    {
        if (attackController == null) attackController = GetComponent<AttackController>();
        if (parry == null) parry = GetComponent<PlayerParry>();
    }

    private void Update()
    {
        // 弹反动作期间：攻击优先级低于弹反，直接忽略输入
        if (parry != null && parry.IsParryLocked) return;

        if (!Input.GetKeyDown(attackKey)) return;

        // 不满足条件时 TryAttack 会自行处理冷却/缓冲
        attackController.TryAttack();
    }
}
