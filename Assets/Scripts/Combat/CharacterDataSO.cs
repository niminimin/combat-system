using UnityEngine;

/// <summary>
/// 角色基础数据（血量等）。
///
/// 把它做成资产的好处：
///   · 三个敌人都指向同一个资产 → 改一处，全部生效
///   · 数值和场景解耦，方便做难度预设（简单/普通/困难各一套资产）
///
/// ⚠️ 只读：运行时**绝对不要**往这里写（比如受击后把 maxHealth 改小），
/// 那会把项目里的资产改脏，而且编辑器里看不出是谁改的。
/// 运行时状态一律存在 CharacterStats 里。
/// </summary>
[CreateAssetMenu(menuName = "Combat/Character Data", fileName = "NewCharacterData")]
public class CharacterDataSO : ScriptableObject
{
    [Header("基础")]
    [Tooltip("显示名（以后接 UI / Boss 血条用）")]
    public string displayName = "角色";

    [Tooltip("最大生命值")]
    [Min(1f)] public float maxHealth = 100f;
}
