using UnityEngine;

/// <summary>
/// 被处决：冻结 AI，等玩家的处决动作播完再结算死亡。
///
/// 这个状态存在的意义是"把敌人按住"：
///   · 不移动、不转向、不还手
///   · 不响应受击（否则会被打断，处决动作和受击动作抢 Animator）
///   · 仍然留在场上（碰撞体不关，玩家的攻击判定还能命中，但不会掉血）
///
/// 目前是**无专用动画**的占位版本：播一次受击动作表示"被制住了"。
/// 以后接真正的被处决动画时，只要改 OnEnter 里那行播放即可。
/// </summary>
public class ExecutedState : BaseState
{
    public override EnemyState Id => EnemyState.EXECUTED;

    public override void OnEnter(EnemyController owner)
    {
        // ① 停住
        owner.Locomotion.Stop();
        owner.Locomotion.SetRotationEnabled(false);
        owner.SetOrbiting(false, 0f);

        // ② 关掉攻击：处决期间它不能再挥刀
        if (owner.Attack != null)
        {
            owner.Attack.CancelAttack();
            owner.Attack.enabled = false;
        }

        // ③ 占位表现：播一次受击动作（以后换成"被处决"动画）
        if (owner.Reaction != null) owner.Reaction.PlayImpact();
    }

    public override void OnExit(EnemyController owner)
    {
        owner.Locomotion.SetRotationEnabled(true);
        if (owner.Attack != null) owner.Attack.enabled = true;
    }

    public override void LogicUpdate(EnemyController owner)
    {
        // 冻结：处决期间不做任何决策，等玩家的 PlayerFinisher 来结束
    }
}
