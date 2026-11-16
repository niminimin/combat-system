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

    // 本帧收到的"被打抢令牌"请求，LateUpdate 里统一处理
    private readonly List<EnemyController> _pendingForce = new List<EnemyController>();
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

    /// <summary>
    /// 请求"被打抢令牌"。**不立即授予**，而是攒到本帧的 LateUpdate 统一处理。
    ///
    /// 原因：一次横扫可能命中多个敌人，如果每个都立刻授予，就会在一帧内
    /// 反复"收回→授予"，最终谁拿取决于 Physics.OverlapBox 的返回顺序（不确定）。
    /// 统一处理时可以按规则挑一个，结果稳定。
    /// </summary>
    public void RequestForceGrant(EnemyController enemy)
    {
        if (enemy == null || enemy.IsDead) return;
        if (_pendingForce.Contains(enemy)) return;
        _pendingForce.Add(enemy);
    }

    private void LateUpdate()
    {
        if (_pendingForce.Count == 0) return;

        // 同一帧只授予一个：取离玩家最近的（你正对着砍的那个）
        EnemyController best = null;
        float bestDistance = float.MaxValue;

        foreach (var e in _pendingForce)
        {
            if (e == null || e.IsDead) continue;

            float distance = e.Player != null
                ? Vector3.Distance(e.transform.position, e.Player.position)
                : 0f;

            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = e;
        }

        _pendingForce.Clear();

        if (best != null) ForceGrant(best);
    }

    /// <summary>
    /// 被打时插队抢令牌（反击权）。
    /// · 无视全局冷却
    /// · 如果当前有人持牌，先收回
    /// · 如果被打的就是持牌者，不重复授予（他本来就该反击）
    /// </summary>
    public void ForceGrant(EnemyController enemy)
    {
        if (enemy == null) return;
        if (enemy.IsDead) return;

        if (CurrentHolder == enemy) return;   // 已经是它，无需重复

        if (CurrentHolder != null)
        {
            if (logEvents) Debug.Log($"[CombatDirector] {enemy.name} 被打，收回 {CurrentHolder.name} 的令牌");
            Revoke(CurrentHolder);
        }

        if (logEvents) Debug.Log($"[CombatDirector] {enemy.name} 被打，插队获得令牌");
        Grant(enemy);
    }
}
