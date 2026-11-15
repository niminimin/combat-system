using UnityEngine;

/// <summary>
/// 死亡：停止移动、关掉碰撞与感知，给动画留出播放时间。
/// 目前只做"停下来"，等接入血量系统后再补死亡动画与销毁。
/// </summary>
public class DeadState : BaseState
{
    public override EnemyState Id => EnemyState.DEAD;

    public override void OnEnter(EnemyController owner)
    {
        owner.Locomotion.Stop();
        owner.DisableCollider();

        // 死亡动画接入后在这里播放（需要在 Enemy.controller 里加对应状态/触发器）
        // owner.Animator.SetTrigger("Die");
    }

    public override void LogicUpdate(EnemyController owner)
    {
        // 死亡状态不再切换
    }
}
