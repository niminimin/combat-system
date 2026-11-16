using UnityEngine;

/// <summary>
/// 死亡：停止移动、关碰撞、还令牌、播死亡动画。
///
/// 注意顺序：先归还令牌 + 注销，再播动画——
/// 否则尸体还会在导演名单里占着位置，影响下一个敌人的节奏。
/// </summary>
public class DeadState : BaseState
{
    public override EnemyState Id => EnemyState.DEAD;

    public override void OnEnter(EnemyController owner)
    {
        // ① 立刻停手（取消正在挥的攻击，避免死后还能砍到玩家）
        owner.Locomotion.Stop();
        if (owner.Attack != null)
        {
            owner.Attack.CancelAttack();
            owner.Attack.enabled = false;
        }

        owner.ReleaseToken();
        if (CombatDirector.Instance != null) CombatDirector.Instance.Unregister(owner);

        // ② 关碰撞：尸体不挡路、也不再被砍
        owner.DisableCollider();

        // ③ 播死亡动画（Animator 里有一个 Dead 布尔 + Death 状态）
        if (owner.Animator != null) owner.Animator.SetBool("Dead", true);

        // ④ 关门：不再参与感知/AI
        owner.enabled = false;
    }

    public override void LogicUpdate(EnemyController owner)
    {
        // 状态机本身已停用
    }
}
