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
        //    例外：被处决时"被处决动画"本身就是倒地动作，
        //    再叠一个死亡动画会变成"爬起来再倒一次"，所以要跳过。
        //    此时 Animator 会停在被处决动画的最后一帧（非循环 clip 自动保持）。
        if (owner.Animator != null)
        {
            if (owner.IsBeingExecuted)
                Debug.Log($"[DeadState] {owner.name} 被处决致死，跳过死亡动画，保持倒地姿势");
            else
                owner.Animator.SetBool("Dead", true);
        }

        // ④ 关门：不再参与感知/AI
        owner.enabled = false;
    }

    public override void LogicUpdate(EnemyController owner)
    {
        // 状态机本身已停用
    }
}
