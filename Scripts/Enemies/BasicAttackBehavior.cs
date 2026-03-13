using UnityEngine;
using System.Collections.Generic;

// ==========================================
// 📦 专为怪物打造的【纯净攻击判定配置】(带可视化偏移)
// ==========================================
[System.Serializable]
public class EnemyAttackConfig
{
    [Header("⚔️ 伤害数值")]
    public float damageMultiplier = 20f;

    [Header("📐 几何判定形状")]
    public HitShape shapeType = HitShape.Sector;

    [Tooltip("🔥 判定框的中心点偏移 (X:左右, Y:上下, Z:前后)")]
    public Vector3 hitOffset = new Vector3(0f, 1f, 1f);

    [Tooltip("攻击半径 (适用于 圆形 和 扇形)")]
    public float attackRadius = 2.0f;

    [Tooltip("扇形角度 (仅 扇形 适用)")]
    [Range(0, 360)] public float attackAngle = 90f;

    [Tooltip("矩形长宽 (仅 矩形 适用)。X是宽度，Z是往前捅的长度")]
    public Vector3 boxSize = new Vector3(1.5f, 1f, 3f);

    [Header("✨ 表现反馈")]
    public GameObject hitVFX;

    public List<Collider> GetHitTargets(Transform attacker)
    {
        List<Collider> validHits = new List<Collider>();
        Vector3 realCenter = attacker.position + attacker.rotation * hitOffset;

        float maxRange = shapeType == HitShape.Rectangle ? Mathf.Max(boxSize.x, boxSize.z) : attackRadius;
        Collider[] potentialHits = Physics.OverlapSphere(realCenter, maxRange);

        foreach (var hit in potentialHits)
        {
            if (hit.gameObject == attacker.gameObject) continue;

            Vector3 dirToTarget = hit.transform.position - realCenter;
            dirToTarget.y = 0;
            bool isHit = false;

            switch (shapeType)
            {
                case HitShape.Circle:
                    if (dirToTarget.magnitude <= attackRadius) isHit = true;
                    break;
                case HitShape.Sector:
                    if (dirToTarget.magnitude <= attackRadius)
                    {
                        float angle = Vector3.Angle(attacker.forward, dirToTarget.normalized);
                        if (angle <= attackAngle * 0.5f) isHit = true;
                    }
                    break;
                case HitShape.Rectangle:
                    Vector3 localPos = Quaternion.Inverse(attacker.rotation) * (hit.transform.position - realCenter);
                    if (Mathf.Abs(localPos.x) <= boxSize.x * 0.5f && localPos.z >= -boxSize.z * 0.5f && localPos.z <= boxSize.z * 0.5f)
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

// ==========================================
// 🧠 基础攻击行为芯片
// ==========================================
[RequireComponent(typeof(EnemyBrain))]
public class BasicAttackBehavior : MonoBehaviour
{
    [System.Serializable]
    public class AttackSkill
    {
        [Header("✏️ 招式名称 (如: 左手抓击)")]
        public string skillName = "普通攻击";

        [Header("🎬 动画核心 (Animator里的Trigger名字)")]
        public string animTriggerName = "Attack";

        [Header("⚖️ 触发权重 (填7和3就是70%和30%)")]
        [Tooltip("数值越大，被抽中的概率越高。总和任意")]
        public float weight = 10f; // 🔥 改成了权重！

        [Header("⚔️ 伤害与判定配置")]
        public EnemyAttackConfig attackConfig;
    }

    [Header("=== 📏 触发距离 ===")]
    public float attackRange = 2.0f;

    [Header("=== 📜 招式列表 ===")]
    public List<AttackSkill> attackSkills = new List<AttackSkill>();

    private EnemyBrain brain;
    private AttackSkill currentSkill;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
    }

    private void Update()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        if (brain.currentState == EnemyBrain.BrainState.Chase)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, brain.Player.position);

            if (distanceToPlayer <= attackRange)
            {
                TryStartAttack();
            }
        }
    }

    private void TryStartAttack()
    {
        if (attackSkills == null || attackSkills.Count == 0) return;
        if (!brain.RequestActionExecution()) return;

        currentSkill = ChooseSkill();

        brain.FaceTarget(brain.Player.position);
        if (brain.Anim != null)
        {
            brain.Anim.SetTrigger(currentSkill.animTriggerName);
        }
    }

    // 🔥 核心修改：使用你设计的“权重算法”抽招
    private AttackSkill ChooseSkill()
    {
        // 1. 计算所有招式的权重总和 (比如 7 + 3 = 10)
        float totalWeight = 0f;
        foreach (var skill in attackSkills)
        {
            totalWeight += skill.weight;
        }

        // 2. 在 0 到 总和 之间掷骰子 (比如抽到了 8)
        float randomVal = Random.Range(0f, totalWeight);

        // 3. 寻找这根签落在了哪个招式的区间里
        float currentWeight = 0f;
        foreach (var skill in attackSkills)
        {
            currentWeight += skill.weight;
            if (randomVal <= currentWeight)
            {
                return skill; // 抽中了！
            }
        }

        // 兜底防错
        return attackSkills[0];
    }

    public void DealDamage()
    {
        if (brain.Player == null || brain.currentState == EnemyBrain.BrainState.Dead || currentSkill == null || currentSkill.attackConfig == null) return;

        List<Collider> hits = currentSkill.attackConfig.GetHitTargets(transform);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                IDamageable target = hit.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(currentSkill.attackConfig.damageMultiplier);
                    if (currentSkill.attackConfig.hitVFX != null)
                        Instantiate(currentSkill.attackConfig.hitVFX, hit.transform.position + Vector3.up, Quaternion.identity);
                }
            }
        }
    }

    public void AnimEvent_AttackEnd()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        brain.FinishAction(); // 1. 先把控制权还给大脑

        // 🔥 2. 新增这一行：向怪物身上的所有芯片广播“我打完了”的信号！
        SendMessage("OnAttackCompleted", SendMessageOptions.DontRequireReceiver);
    }

    // ==========================================
    // 🎨 Scene 窗口可视化辅助线
    // ==========================================
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (attackSkills == null) return;

        foreach (var skill in attackSkills)
        {
            if (skill == null || skill.attackConfig == null) continue;
            DrawActionGizmo(skill.attackConfig, Color.red);
        }
    }

    private void DrawActionGizmo(EnemyAttackConfig action, Color drawColor)
    {
        if (action == null) return;

        Gizmos.color = new Color(drawColor.r, drawColor.g, drawColor.b, 0.8f);
        Vector3 realCenter = transform.position + transform.rotation * action.hitOffset;

        switch (action.shapeType)
        {
            case HitShape.Circle:
                Gizmos.DrawWireSphere(realCenter, action.attackRadius);
                break;
            case HitShape.Sector:
                Gizmos.color = new Color(drawColor.r, drawColor.g, drawColor.b, 0.2f);
                Gizmos.DrawWireSphere(realCenter, action.attackRadius);
                Gizmos.color = new Color(drawColor.r, drawColor.g, drawColor.b, 0.8f);
                Vector3 forward = transform.forward;
                Vector3 leftRay = Quaternion.AngleAxis(-action.attackAngle * 0.5f, Vector3.up) * forward;
                Vector3 rightRay = Quaternion.AngleAxis(action.attackAngle * 0.5f, Vector3.up) * forward;
                Gizmos.DrawRay(realCenter, leftRay * action.attackRadius);
                Gizmos.DrawRay(realCenter, rightRay * action.attackRadius);
                break;
            case HitShape.Rectangle:
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(realCenter, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, action.boxSize);
                Gizmos.matrix = oldMatrix;
                break;
        }
    }
#endif
}