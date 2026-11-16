using UnityEngine;

/// <summary>
/// 玩家弹反（无动画版本）。
///
/// 机制很简单：
///   · 按下按键 → 开启一段"弹反窗口"（默认 0.25 秒）
///   · 窗口内的敌人攻击命中 → 判定弹反成功，玩家不受击，敌人被打断
///   · 窗口外命中 → 走正常的受击流程
///
/// 没有专门的格挡动画，靠"敌人踉跄"传达成功感；以后有 Block 动画再补表现。
/// </summary>
[DisallowMultipleComponent]
public class PlayerParry : MonoBehaviour
{
    [Header("输入")]
    [SerializeField] private KeyCode parryKey = KeyCode.E;

    [Header("窗口")]
    [Tooltip("按下后多久内算弹反成功（秒）")]
    [SerializeField, Min(0.05f)] private float parryWindow = 0.25f;

    [Tooltip("两次弹反之间的最小间隔（秒）")]
    [SerializeField, Min(0f)] private float parryCooldown = 0.6f;

    [Tooltip("攻击者必须在玩家正面这个角度内（度）才算弹反成功")]
    [SerializeField, Range(30f, 360f)] private float facingAngle = 160f;

    [Header("调试")]
    [SerializeField] private bool logParry = true;

    /// <summary>弹反窗口截止时间。</summary>
    private float _parryUntil;
    private float _nextParryTime;

    /// <summary>当前是否处于弹反窗口内。</summary>
    public bool IsParrying => Time.time < _parryUntil;

    private void Update()
    {
        if (Input.GetKeyDown(parryKey)) TryParry();
    }

    /// <summary>尝试开启弹反（冷却中则无效）。</summary>
    public bool TryParry()
    {
        if (Time.time < _nextParryTime) return false;

        _parryUntil = Time.time + parryWindow;
        _nextParryTime = Time.time + parryCooldown;

        if (logParry) Debug.Log($"[PlayerParry] 开启弹反窗口 {parryWindow:F2}s", this);
        return true;
    }

    /// <summary>
    /// 尝试用当前窗口弹反一次攻击。
    /// 成功时消耗窗口（一次按键只挡一下），并返回 true。
    /// </summary>
    public bool TryParryAttack(GameObject attacker, out EnemyController enemy)
    {
        enemy = null;

        if (!IsParrying) return false;
        if (attacker == null) return false;

        // 必须在正面（从背后砍过来不该被弹反）
        Vector3 toAttacker = attacker.transform.position - transform.position;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude > 0.0001f &&
            Vector3.Angle(transform.forward, toAttacker) > facingAngle * 0.5f)
            return false;

        enemy = attacker.GetComponentInParent<EnemyController>();
        if (enemy == null) return false;

        _parryUntil = 0f;   // 消耗窗口，一次按键只弹一下

        if (logParry) Debug.Log($"[PlayerParry] 弹反成功：{enemy.name}", this);
        return true;
    }
}
