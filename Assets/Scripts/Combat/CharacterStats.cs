using UnityEngine;

/// <summary>
/// 角色血量（简化的 CharacterStats）。
///
/// 相比 3DRPG 那套，这里刻意去掉了：
///   · 防御 / 暴击 / 经验 / 金币 / 升级加成
///   · CharacterDataSO 数据资产
/// 因为原型阶段只需要"能掉血、会死、能被订阅"。
///
/// 设计上保留了 3DRPG 的两个关键点：
///   ① 致死的一击只触发 Died，**不再触发 Damaged** —— 避免死亡动画和受击动画抢
///   ② 死后不再结算任何伤害
/// </summary>
[DisallowMultipleComponent]
public class CharacterStats : MonoBehaviour
{
    [Header("生命")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("调试")]
    [SerializeField] private bool logDamage = true;

    /// <summary>最大生命。</summary>
    public float MaxHealth => maxHealth;

    /// <summary>当前生命。</summary>
    public float CurrentHealth { get; private set; }

    /// <summary>是否已死亡（保证只死一次）。</summary>
    public bool IsDead { get; private set; }

    /// <summary>血量变化时通知（当前, 最大）。UI 血条订阅。</summary>
    public event System.Action<float, float> HealthChanged;

    /// <summary>受到非致死伤害时通知（伤害值, 攻击者）。受击表现订阅。</summary>
    public event System.Action<float, GameObject> Damaged;

    /// <summary>死亡时通知一次（攻击者）。死亡流程订阅。</summary>
    public event System.Action<GameObject> Died;

    private void Awake()
    {
        ResetStats();
    }

    /// <summary>恢复满血、清除死亡标记。</summary>
    public void ResetStats()
    {
        IsDead = false;
        CurrentHealth = maxHealth;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>扣血。<paramref name="source"/> 是造成伤害的物体。</summary>
    public void TakeDamage(float amount, GameObject source = null)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

        if (logDamage)
            Debug.Log($"[{name}] 受到 {amount:F0} 伤害，剩余 {CurrentHealth:F0}/{maxHealth:F0}", this);

        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        // 致死：只走死亡流程，不再通知受击
        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            Died?.Invoke(source);
            return;
        }

        Damaged?.Invoke(amount, source);
    }

    /// <summary>回血，返回实际回复量。</summary>
    public float Heal(float amount)
    {
        if (amount <= 0f || IsDead) return 0f;

        float before = CurrentHealth;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
        float healed = CurrentHealth - before;

        if (healed > 0f) HealthChanged?.Invoke(CurrentHealth, maxHealth);
        return healed;
    }

    /// <summary>复活。</summary>
    public void Revive(float health)
    {
        IsDead = false;
        CurrentHealth = Mathf.Clamp(health, 1f, maxHealth);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}
