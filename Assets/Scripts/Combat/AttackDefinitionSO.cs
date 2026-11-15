using UnityEngine;

/// <summary>
/// 一段攻击的数据定义（数据驱动）。
///
/// 把"这一招是什么样"从代码里搬出来：
///   · 播哪个动画状态
///   · 判定的时间窗口（占动画时长的百分比）
///   · 伤害、冷却
///
/// 好处：
///   · 加新招式 = 新建一个资产，不用写代码
///   · 玩家和敌人可以引用同一个资产
///   · 数值集中管理，方便平衡
///
/// 注意：判定窗口用**百分比**而不是秒，
/// 这样换动画（快版/慢版）、改播放速度时都不用重新调。
/// </summary>
[CreateAssetMenu(menuName = "Combat/Attack Definition", fileName = "NewAttack")]
public class AttackDefinitionSO : ScriptableObject
{
    [Header("动画")]
    [Tooltip("Animator 里的状态名，攻击时直接 Play 这个状态")]
    public string animatorState = "Attack";

    [Tooltip("判定窗口开始（0~1，占动画时长）")]
    [Range(0f, 1f)] public float hitWindowStart = 0.36f;

    [Tooltip("判定窗口结束（0~1）。必须大于开始，且小于状态机切回待机的 exitTime")]
    [Range(0f, 1f)] public float hitWindowEnd = 0.55f;

    [Header("数值")]
    [Tooltip("这一招的伤害")]
    [Min(0f)] public float damage = 10f;

    [Tooltip("两次这一招之间的最小间隔（秒）")]
    [Min(0f)] public float cooldown = 0.35f;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(animatorState)) animatorState = "Attack";
        if (hitWindowEnd < hitWindowStart) hitWindowEnd = hitWindowStart;
    }
}
