/// <summary>
/// 可受击对象。武器判定命中时，通过这个接口通知对方，
/// 不关心对方到底是玩家还是敌人。
///
/// 实现方：EnemyHitReaction、PlayerHitReaction
/// </summary>
public interface IDamageReceiver
{
    /// <summary>
    /// 受到一次命中。
    /// <paramref name="source"/> 是发起攻击的物体；
    /// <paramref name="damage"/> 来自 AttackDefinitionSO 的伤害值。
    /// </summary>
    void OnHit(UnityEngine.GameObject source, float damage);
}
