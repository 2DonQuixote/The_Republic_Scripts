using UnityEngine;

public enum StatusType
{
    Poison,
    Bleed,
    Curse,
    Fire,
    Buff
}

[CreateAssetMenu(fileName = "NewBuff", menuName = "Game/Buff Data")]
public class BuffData : ScriptableObject
{
    [Header("=== 🆔 类型定义 ===")]
    public StatusType type;

    [Header("=== 🧪 积累机制 ===")]
    [Tooltip("基础阈值：达到这个值触发 Buff")]
    public float baseThreshold = 100f;

    [Tooltip("衰减速率：每秒自动减少多少积累值")]
    public float decayRate = 5f;

    [Space]
    [Header("=== ⏳ 持续/重置逻辑 ===")]
    public float duration = 10f;

    [Tooltip("勾选：再次触发时，刷新持续时间 (Buff续杯)。\n不勾：再次触发无效，直到当前Buff结束 (毒药模式)。")]
    public bool refreshTimeOnHit = false;

    // 🗑️ isStackable 已经被彻底删除了！

    [Header("=== ☠️ 伤害配置 ===")]
    public int damagePerTick = 10;
    public float tickInterval = 1f;

    [Header("=== 🎨 UI 表现 ===")]
    public string uiMessage = "中毒了！！！";
    public Color uiColor = Color.green;

    [Header("=== 清除规则 ===")]
    public bool clearOnRest = true;
}