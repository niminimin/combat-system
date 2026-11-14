using UnityEditor;
using UnityEngine;

/// <summary>
/// 把选中角色的骨骼复位到"绑定姿势"（T-pose）。
///
/// 用途：在编辑模式下调试动画时（例如手动调用 Animator.Update 预览动作），
/// Unity 会把当时的骨骼旋转记成预制体覆盖，退出 Play 或重载场景都不会自动恢复，
/// 角色就一直保持动画的最后一帧，导致没法在 T-pose 下对齐武器/装备。
///
/// 原理：删除这些 Transform 上的预制体覆盖，让它们重新取自 FBX 预制体（绑定姿势）。
/// </summary>
public static class ModelPoseReset
{
    [MenuItem("Tools/Combat System/重置选中模型到绑定姿势")]
    public static void ResetSelectedPose()
    {
        GameObject target = Selection.activeGameObject;
        if (target == null)
        {
            Debug.LogWarning("[ModelPoseReset] 请先在 Hierarchy 里选中角色（通常是带 Animator 的 Visual 节点）。");
            return;
        }

        int reverted = 0;
        var overrides = PrefabUtility.GetObjectOverrides(target, true);
        foreach (var entry in overrides)
        {
            var t = entry.instanceObject as Transform;
            if (t == null) continue;

            PrefabUtility.RevertObjectOverride(t, InteractionMode.AutomatedAction);
            reverted++;
        }

        if (reverted == 0)
            Debug.Log("[ModelPoseReset] 该对象没有 Transform 覆盖，已经是绑定姿势。");
        else
            Debug.Log($"[ModelPoseReset] 已回滚 {reverted} 个 Transform 覆盖，角色回到绑定姿势。");
    }
}
