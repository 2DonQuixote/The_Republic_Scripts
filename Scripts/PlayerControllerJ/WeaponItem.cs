using System.Collections.Generic;
using UnityEngine;

// 🔥 新增：定义三种近战极其常用的判定形状
public enum HitShape { Sector, Circle, Rectangle }

[CreateAssetMenu(menuName = "Combat/New Weapon")]
public class WeaponItem : ScriptableObject
{
    [Header("基础信息")]
    public string weaponName;
    public GameObject modelPrefab;
    public AnimatorOverrideController weaponAnimator;

    [Header("连招配置")]
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

    [Header("数值配置")]
    public float damageDelay = 0.0f;
    public float damageMultiplier = 20f;

    // 🔥🔥🔥 核心修改：几何判定配置 🔥🔥🔥
    [Header("几何判定设置 (由该动作自行决定)")]
    public HitShape shapeType = HitShape.Sector; // 默认扇形

    [Tooltip("攻击半径 (适用于 Circle 和 Sector)")]
    public float attackRadius = 2.0f;

    [Tooltip("扇形角度 (仅 Sector 适用)")]
    [Range(0, 360)] public float attackAngle = 90f;

    [Tooltip("矩形长宽 (仅 Rectangle 适用)。X是宽度，Z是往前捅的长度")]
    public Vector3 boxSize = new Vector3(1.5f, 1f, 3f);

    [Header("表现反馈")]
    public GameObject hitVFX;

    // ===============================================
    // 🔥 将数学扫描逻辑直接封装在这个类里！
    // ===============================================
    public List<Collider> GetHitTargets(Transform attacker)
    {
        List<Collider> validHits = new List<Collider>();

        // 无论什么形状，先用一个能包住它们的最大圆把附近的物体全捞出来
        float maxRange = shapeType == HitShape.Rectangle ? Mathf.Max(boxSize.x, boxSize.z) : attackRadius;
        Collider[] potentialHits = Physics.OverlapSphere(attacker.position, maxRange);

        foreach (var hit in potentialHits)
        {
            if (hit.gameObject == attacker.gameObject) continue;

            Vector3 dirToTarget = hit.transform.position - attacker.position;
            dirToTarget.y = 0; // 忽略高度差

            bool isHit = false;

            // 策略模式：根据当前动作配置的形状，执行不同数学算法
            switch (shapeType)
            {
                case HitShape.Circle:
                    // 圆形：只要在半径内就算打中 (例如大锤砸地)
                    if (dirToTarget.magnitude <= attackRadius) isHit = true;
                    break;

                case HitShape.Sector:
                    // 扇形：在半径内，且夹角在扇形范围内 (例如大剑横扫)
                    if (dirToTarget.magnitude <= attackRadius)
                    {
                        float angle = Vector3.Angle(attacker.forward, dirToTarget.normalized);
                        if (angle <= attackAngle * 0.5f) isHit = true;
                    }
                    break;

                case HitShape.Rectangle:
                    // 矩形：将目标坐标转换到玩家的局部坐标系 (例如长枪刺击)
                    Vector3 localPos = attacker.InverseTransformPoint(hit.transform.position);
                    // 判断是否在正前方的矩形框内 (x左右宽，z前后长)
                    if (Mathf.Abs(localPos.x) <= boxSize.x * 0.5f && localPos.z >= 0 && localPos.z <= boxSize.z)
                    {
                        isHit = true;
                    }
                    break;
            }

            if (isHit) validHits.Add(hit);
        }

        return validHits;
    }
}