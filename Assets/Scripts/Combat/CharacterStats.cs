using UnityEngine;

/// <summary>
/// 角色血量（简化的 CharacterStats）。
///
/// 数值来源：CharacterDataSO 资产。三个敌人指向同一个资产，
/// 改一次就能同时调整它们，不用逐个改场景里的组件。
///
/// 相比 3DRPG 那套，这里刻意去掉了：
///   · 防御 / 暴击 / 经验 / 金币 / 升级加成
/// 因为原型阶段只需要"能掉血、会死、能被订阅"。
///
/// 设计上保留了 3DRPG 的两个关键点：
///   ① 致死的一击只触发 Died，**不再触发 Damaged** —— 避免死亡动画和受击动画抢
///   ② 死后不再结算任何伤害
/// </summary>
[DisallowMultipleComponent]
public class CharacterStats : MonoBehaviour
{
    [Header("数据")]
    [Tooltip("角色基础数据资产（血量等）。留空则使用下面的兜底值")]
    [SerializeField] private CharacterDataSO data;

    [Tooltip("没有配置 CharacterDataSO 时使用的最大生命值")]
    [SerializeField, Min(1f)] private float fallbackMaxHealth = 100f;

    [Header("调试")]
    [SerializeField] private bool logDamage = true;

    /// <summary>最大生命（优先从数据资产读）。</summary>
    public float MaxHealth => data != null ? data.maxHealth : fallbackMaxHealth;

    /// <summary>角色显示名（以后接 UI / Boss 血条）。</summary>
    public string DisplayName => data != null ? data.displayName : name;

    /// <summary>可处决的血量比例（0 表示不可处决）。</summary>
    public float ExecuteThreshold => data != null ? data.executeThreshold : 0f;

    /// <summary>当前是否处于"可处决"状态（血量低于阈值）。</summary>
    public bool IsExecutable =>
        !IsDead && ExecuteThreshold > 0f && CurrentHealth <= MaxHealth * ExecuteThreshold;

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
        CurrentHealth = MaxHealth;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>扣血。<paramref name="source"/> 是造成伤害的物体。</summary>
    public void TakeDamage(float amount, GameObject source = null)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

        if (logDamage)
            Debug.Log($"[{name}] 受到 {amount:F0} 伤害，剩余 {CurrentHealth:F0}/{MaxHealth:F0}", this);

        HealthChanged?.Invoke(CurrentHealth, MaxHealth);

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
        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        float healed = CurrentHealth - before;

        if (healed > 0f) HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        return healed;
    }

    /// <summary>复活。</summary>
    public void Revive(float health)
    {
        IsDead = false;
        CurrentHealth = Mathf.Clamp(health, 1f, MaxHealth);
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }
}
