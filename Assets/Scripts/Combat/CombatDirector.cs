using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗导演：决定"同一时刻谁能攻击"。
///
/// 注意它**不做**战斗逻辑，只做三件事：
///   ① 记着谁在参战（注册表）
///   ② 按"等得最久优先"挑一个能出手的
///   ③ 控制全局攻击节奏（冷却）并防止令牌卡死（超时）
///
/// 敌人不"申请"，而是被动接收：
///   · 进战斗 → Register
///   · 离开战斗 → Unregister
///   · 被选中 → OnTokenGranted()（敌人自己切 ATTACK）
///   · 打完 → Release()，导演开始计冷却
/// </summary>
[DisallowMultipleComponent]
public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    [Header("节奏")]
    [Tooltip("两次攻击之间的最小间隔（从上一个打完开始算，秒）")]
    [SerializeField, Min(0f)] private float attackCooldown = 1.6f;

    [Tooltip("令牌持有超过这个时间还没交还，视为卡住，强制回收（秒）")]
    [SerializeField, Min(0.5f)] private float tokenTimeout = 6f;

    [Header("调试")]
    [SerializeField] private bool logEvents = true;

    /// <summary>当前持有令牌的敌人。</summary>
    public EnemyController CurrentHolder { get; private set; }

    /// <summary>参战人数（调试/UI 用）。</summary>
    public int CombatantCount => _combatants.Count;

    private class Combatant
    {
        public EnemyController Enemy;
        public float LastGrantTime;   // 上次拿到令牌的时刻，越小越优先
    }

    private readonly List<Combatant> _combatants = new List<Combatant>();
    private float _nextGrantTime;
    private float _grantTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(CombatDirector)}] 场景里存在多个，销毁多余的：{name}", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ───────────────────────── 注册 ─────────────────────────

    public void Register(EnemyController enemy)
    {
        if (enemy == null) return;
        foreach (var c in _combatants)
            if (c.Enemy == enemy) return;      // 已在名单里

        _combatants.Add(new Combatant { Enemy = enemy, LastGrantTime = Time.time });
        if (logEvents) Debug.Log($"[CombatDirector] 注册 {enemy.name}（参战 {_combatants.Count}）");
    }

    public void Unregister(EnemyController enemy)
    {
        if (enemy == null) return;

        for (int i = _combatants.Count - 1; i >= 0; i--)
        {
            if (_combatants[i].Enemy != enemy) continue;
            _combatants.RemoveAt(i);
            if (logEvents) Debug.Log($"[CombatDirector] 注销 {enemy.name}（参战 {_combatants.Count}）");
            break;
        }

        if (CurrentHolder == enemy) CurrentHolder = null;
    }

    /// <summary>敌人刚进入"能出手"的位置（COMBAT_IDLE）：重新排队，避免刚赶到的插队。</summary>
    public void MarkJoined(EnemyController enemy)
    {
        foreach (var c in _combatants)
            if (c.Enemy == enemy) { c.LastGrantTime = Time.time; return; }
    }

    // ───────────────────────── 发放 ─────────────────────────

    private void Update()
    {
        // ① 有人持牌 → 只检查是否失效/超时
        if (CurrentHolder != null)
        {
            if (!CurrentHolder.isActiveAndEnabled || CurrentHolder.IsDead)
            {
                Revoke(CurrentHolder);
                return;
            }

            if (Time.time - _grantTime > tokenTimeout)
            {
                if (logEvents) Debug.Log($"[CombatDirector] {CurrentHolder.name} 持牌超时，回收");
                Revoke(CurrentHolder);
            }
            return;
        }

        // ② 没人持牌 → 冷却到了就挑一个
        if (Time.time < _nextGrantTime) return;

        EnemyController best = null;
        float bestTime = float.MaxValue;

        foreach (var c in _combatants)
        {
            if (c.Enemy == null || !c.Enemy.CanReceiveToken) continue;
            if (c.LastGrantTime >= bestTime) continue;

            bestTime = c.LastGrantTime;
            best = c.Enemy;
        }

        if (best != null) Grant(best);
    }

    private void Grant(EnemyController enemy)
    {
        CurrentHolder = enemy;
        _grantTime = Time.time;

        foreach (var c in _combatants)
            if (c.Enemy == enemy) c.LastGrantTime = Time.time;

        if (logEvents) Debug.Log($"[CombatDirector] 授予 {enemy.name}");
        enemy.OnTokenGranted();
    }

    /// <summary>敌人交还令牌。</summary>
    public void Release(EnemyController enemy)
    {
        if (CurrentHolder != enemy) return;

        CurrentHolder = null;
        _nextGrantTime = Time.time + attackCooldown;
        if (logEvents) Debug.Log($"[CombatDirector] {enemy.name} 释放，下次最早 {attackCooldown:F1}s 后");
    }

    /// <summary>强制收回（超时/死亡/脱战/被打断）。</summary>
    public void Revoke(EnemyController enemy)
    {
        if (CurrentHolder != enemy) return;

        CurrentHolder = null;
        _nextGrantTime = Time.time + attackCooldown * 0.5f;
        enemy.OnTokenRevoked();
    }

    /// <summary>被打时插队（反击权，第 2 步接入）。</summary>
    public void ForceGrant(EnemyController enemy)
    {
        if (enemy == null) return;

        if (CurrentHolder != null && CurrentHolder != enemy)
            Revoke(CurrentHolder);

        Grant(enemy);
    }
}
