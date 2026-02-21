using System.Collections.Generic;
using UnityEngine;

// 这是武器配置文件
[CreateAssetMenu(menuName = "Combat/New Weapon")]
public class WeaponItem : ScriptableObject
{
    [Header("基础信息")]
    public string weaponName;
    public GameObject modelPrefab;
    public AnimatorOverrideController weaponAnimator;

    // 🔥 Base Damage 已移除
    // 现在伤害完全由下面的动作列表决定

    [Header("连招配置")]
    [Tooltip("动作结束后，几秒内不攻击则重置连招？")]
    public float comboResetTime = 2.0f;

    public List<AttackAction> lightAttacks;
    public List<AttackAction> heavyAttacks;

    public AttackAction GetLightAttack(int index)
    {
        if (lightAttacks == null || lightAttacks.Count == 0) return null;
        if (index >= lightAttacks.Count) return lightAttacks[0];
        return lightAttacks[index];
    }

    public AttackAction GetHeavyAttack(int index)
    {
        if (heavyAttacks == null || heavyAttacks.Count == 0) return null;
        if (index >= heavyAttacks.Count) return heavyAttacks[0];
        return heavyAttacks[index];
    }
}

[System.Serializable]
public class AttackAction
{
    [Header("动画核心")]
    public string animName;

    [Range(0f, 0.5f)] public float transitionDuration = 0.1f;

    [Header("节奏控制")]
    public float totalDuration = 1.0f;
    [Range(0f, 1f)] public float comboWindowStart = 0.6f;
    [Range(0f, 1f)] public float rollCancelStartTime = 0.5f;

    [Header("位移与物理")]
    public float impulseForce = 0f;
    public float impulseDelay = 0.05f;
    [Range(0f, 1f)] public float movementSpeedMultiplier = 0f;

    [Header("数值与判定")]

    [Tooltip("动作挥刀的前摇时间（秒）。比如填 0.3，就是 0.3 秒后那一瞬间进行扇形判定。")]
    public float damageDelay = 0.0f; // 默认为0 (立即开启)

    [Tooltip("⚠️ 注意：这里现在直接代表【伤害数值】！\n填 20 就是打 20 血，不再是倍率了。")]
    public float damageMultiplier = 20f;

    // 🔥🔥【新增】扇形判定参数 🔥🔥
    [Header("扇形判定设置")]
    [Tooltip("攻击半径 (米)。代表你能打多远。")]
    public float attackRadius = 2.0f;

    [Tooltip("扇形角度 (度)。例如 90度 就是前方一个直角扇形区域。")]
    [Range(0, 360)] public float attackAngle = 90f;

    // 🔥🔥【新增】打击反馈
    [Header("表现反馈")]
    [Tooltip("打中敌人时播放的特效 (可选)")]
    public GameObject hitVFX;
}