using UnityEngine;

/// <summary>
/// 玩家死亡处理：停掉所有控制、播死亡动画、通知 GameManager。
///
/// 之所以单独一个组件而不是塞进 PlayerHitReaction：
/// 死亡要停掉的东西很多（移动/攻击/弹反），属于"整体状态切换"，
/// 和"挨了一下"不是同一层的事。
/// </summary>
[DisallowMultipleComponent]
public class PlayerDeath : MonoBehaviour
{
    [Tooltip("血量组件（留空自动查找）")]
    [SerializeField] private CharacterStats stats;

    [Tooltip("玩家的 Animator（留空自动从子物体查找）")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator 里的死亡布尔参数名")]
    [SerializeField] private string deadParam = "Dead";

    private void Awake()
    {
        if (stats == null) stats = GetComponent<CharacterStats>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (stats != null) stats.Died += HandleDied;
    }

    private void OnDisable()
    {
        if (stats != null) stats.Died -= HandleDied;
    }

    private void HandleDied(GameObject killer)
    {
        // ① 取消正在进行的攻击（未结算的伤害作废）
        var attack = GetComponent<AttackController>();
        if (attack != null)
        {
            attack.CancelAttack();
            attack.enabled = false;
        }

        // ② 关掉所有玩家控制
        Disable<ThirdPersonMotor>();
        Disable<PlayerAttackInput>();
        Disable<PlayerParry>();

        // ③ 播死亡动画
        if (animator != null) animator.SetBool(deadParam, true);

        // ④ 全局状态
        if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.GameOver);

        Debug.Log($"[PlayerDeath] 玩家死亡（击杀者：{(killer != null ? killer.name : "未知")}）", this);
    }

    private void Disable<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component != null) component.enabled = false;
    }
}
