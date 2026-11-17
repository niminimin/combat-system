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
    // Animator 里的状态名
    private const string ExecutedStateName = "Executed";

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

        // ③ 播"被处决"动画（Animator 里的 Executed 状态，无出口过渡，播完保持最后一帧）
        if (owner.Animator != null) owner.Animator.CrossFade(ExecutedStateName, 0.1f);
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
