/// <summary>
/// 状态基类。每个具体状态只实现三件事：
///   · OnEnter：刚进入时做一次的事（停移动、播 animator、重置计时）
///   · LogicUpdate：每帧判断要不要切到别的状态
///   · OnExit：离开时清理（目前多数状态不需要）
///
/// 状态实例是**共享的**（一个敌人一个实例，由 EnemyController 在 Awake 创建），
/// 所以不要把"只属于某一次进入"的数据存在状态类里 —— 存在 EnemyController 上。
/// </summary>
public abstract class BaseState
{
    /// <summary>这个状态对应的枚举值。</summary>
    public abstract EnemyState Id { get; }

    public virtual void OnEnter(EnemyController owner) { }

    public virtual void LogicUpdate(EnemyController owner) { }

    public virtual void OnExit(EnemyController owner) { }
}
