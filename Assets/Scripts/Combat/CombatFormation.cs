using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗编队：把参战的敌人分配到玩家周围的角度槽位上，避免全挤在一侧。
///
/// 只做一件事：**分配/释放角度槽位**。
/// 具体的"走过去""站着""环绕"由敌人自己的状态机负责，
/// 它只需要读 enemy.SlotAngle / enemy.SlotRadius。
///
/// 槽位角度是**世界坐标固定**的（0°、60°、120°…），
/// 玩家转身时敌人不会跟着一起转圈。
/// </summary>
[DisallowMultipleComponent]
public class CombatFormation : MonoBehaviour
{
    public static CombatFormation Instance { get; private set; }

    [Header("槽位")]
    [Tooltip("玩家周围分成几个角度槽")]
    [SerializeField, Range(3, 12)] private int slotCount = 6;

    [Tooltip("槽位半径（米），一般等于敌人的 combatRange")]
    [SerializeField, Min(0.5f)] private float baseRadius = 3f;

    [Tooltip("同一个槽位有多人时，每多一个人半径增加多少")]
    [SerializeField, Min(0f)] private float radiusStep = 1.2f;

    [Header("调试")]
    [SerializeField] private bool logEvents = true;

    private class Slot
    {
        public float AngleDeg;                 // 槽位角度（度，世界固定）
        public readonly List<EnemyController> Owners = new List<EnemyController>();
    }

    private Slot[] _slots;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(CombatFormation)}] 场景里存在多个，销毁多余的：{name}", this);
            Destroy(this);
            return;
        }
        Instance = this;

        _slots = new Slot[slotCount];
        for (int i = 0; i < slotCount; i++)
            _slots[i] = new Slot { AngleDeg = i * (360f / slotCount) };
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 给敌人分配一个槽位（就近原则），并把角度/半径写回它身上。
    /// 槽位满了就共用，用半径错开。
    /// </summary>
    public void ClaimSlot(EnemyController enemy)
    {
        if (enemy == null || _slots == null) return;

        float currentAngle = AngleOf(enemy);

        // ① 优先找最近的空槽
        int bestIndex = -1;
        float bestDiff = float.MaxValue;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Owners.Count > 0) continue;

            float diff = Mathf.Abs(Mathf.DeltaAngle(currentAngle, _slots[i].AngleDeg));
            if (diff >= bestDiff) continue;
            bestDiff = diff;
            bestIndex = i;
        }

        // ② 都满了 → 选人最少的槽位共用
        if (bestIndex < 0)
        {
            int minCount = int.MaxValue;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Owners.Count >= minCount) continue;
                minCount = _slots[i].Owners.Count;
                bestIndex = i;
            }
        }

        if (bestIndex < 0) return;

        Slot slot = _slots[bestIndex];
        slot.Owners.Add(enemy);

        int tier = slot.Owners.Count - 1;          // 第几个占这个槽的人
        enemy.SlotIndex = bestIndex;
        enemy.SlotAngle = slot.AngleDeg * Mathf.Deg2Rad;
        enemy.SlotRadius = baseRadius + radiusStep * tier;

        if (logEvents)
            Debug.Log($"[CombatFormation] {enemy.name} → 槽位 {bestIndex}（{slot.AngleDeg:F0}°，半径 {enemy.SlotRadius:F1}m）");
    }

    /// <summary>释放敌人的槽位（离开战斗/死亡时调用）。</summary>
    public void ReleaseSlot(EnemyController enemy)
    {
        if (enemy == null || _slots == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Owners.Remove(enemy))
            {
                if (logEvents)
                    Debug.Log($"[CombatFormation] {enemy.name} 释放槽位 {i}");
                break;
            }
        }

        enemy.SlotIndex = -1;
        enemy.SlotRadius = 0f;
    }

    /// <summary>敌人相对玩家的角度（度，0~360）。</summary>
    private float AngleOf(EnemyController enemy)
    {
        Transform player = enemy.Player;
        if (player == null) return 0f;

        Vector3 offset = enemy.transform.position - player.position;
        offset.y = 0f;
        if (offset.sqrMagnitude < 0.0001f) return 0f;

        float deg = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
        return deg < 0f ? deg + 360f : deg;
    }
}
