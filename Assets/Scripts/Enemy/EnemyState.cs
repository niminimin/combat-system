/// <summary>
/// 敌人状态枚举。状态机用枚举作为"唯一入口"的标识，
/// 具体逻辑在对应的 BaseState 子类里。
/// </summary>
public enum EnemyState
{
    GUARD,        // 站岗：原地环顾，发现玩家进入 CHASE
    CHASE,        // 追击：走向玩家，进入战斗距离切 COMBAT_IDLE
    COMBAT_IDLE,  // 战斗待机：停在玩家附近摆战斗姿势、面向玩家
    ORBIT,        // 环绕：沿着玩家周围的一段弧线侧移
    ATTACK,       // 攻击：接近 + 出手（持令牌期间）
    RETREAT,      // 后撤：攻击后退开一段，再回战斗待机
    RETURN,       // 回位：走回出生点
    DEAD          // 死亡：停止一切行为（后续接死亡动画）
}
