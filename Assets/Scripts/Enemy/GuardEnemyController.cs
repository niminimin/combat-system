/// <summary>
/// 守卫型敌人：出生先在原地站岗，脱战后回到站岗位置。
///
/// 需要巡逻型时，新建一个 PatrolEnemyController：
///   InitialState => EnemyState.PATROL, StateAfterReturn => EnemyState.PATROL
/// 并补一个 PatrolState。
/// </summary>
public class GuardEnemyController : EnemyController
{
    protected override EnemyState InitialState => EnemyState.GUARD;

    public override EnemyState StateAfterReturn => EnemyState.GUARD;
}
