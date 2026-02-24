using UnityEngine;

// 1. 定义枚举：你的游戏里有哪些异常？
public enum StatusType
{
    Poison, // 毒
    Bleed,  // 出血
    Curse,  // 诅咒
    Fire    // 灼烧
}

[CreateAssetMenu(fileName = "NewBuff", menuName = "Game/Buff Data")]
public class BuffData : ScriptableObject
{
    [Header("=== 🆔 类型定义 ===")]
    public StatusType type; // 👈 记得在 Inspector 里选一下！

    [Header("=== 🧪 积累机制 ===")]
    [Tooltip("基础阈值：这是及格线。最终阈值 = 基础 + 玩家抗性")]
    public float baseThreshold = 100f; // 💡 变量名改了，更明确

    [Tooltip("衰减速率：每秒自动减少多少积累值")]
    public float decayRate = 5f;

    [Space]
    [Header("=== ☠️ 伤害逻辑配置 ===")]
    public float duration = 5f;
    public int damagePerTick = 10;
    public float tickInterval = 1f;
    public bool triggerImmediately = false;

    [Header("=== 机制配置 ===")]
    public bool isStackable = false;

    [Header("=== 🎨 UI 表现配置 ===")]
    public string uiMessage = "中毒了！！！";
    public Color uiColor = Color.green;

    [Header("=== 清除规则 ===")]
    public bool clearOnRest = true;
}