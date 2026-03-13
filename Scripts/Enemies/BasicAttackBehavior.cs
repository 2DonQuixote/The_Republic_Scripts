using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EnemyAttackConfig
{
    [Header("⚔️ 伤害数值")]
    public float damageMultiplier = 20f;

    [Header("📐 几何判定形状")]
    public HitShape shapeType = HitShape.Sector;
    public Vector3 hitOffset = new Vector3(0f, 1f, 1f);
    public float attackRadius = 2.0f;
    [Range(0, 360)] public float attackAngle = 90f;
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

[RequireComponent(typeof(EnemyBrain))]
public class BasicAttackBehavior : MonoBehaviour
{
    [System.Serializable]
    public class AttackSkill
    {
        public string skillName = "普通攻击";
        public string animTriggerName = "Attack";
        public float weight = 10f;
        public EnemyAttackConfig attackConfig;
    }

    [Header("=== 📏 触发距离 ===")]
    public float attackRange = 2.0f;

    [Header("=== 📜 招式列表 ===")]
    public List<AttackSkill> attackSkills = new List<AttackSkill>();

    private EnemyBrain brain;
    private AttackSkill currentSkill;
    private ComboGrabBehavior grabBehavior; // 👈 引用同一物体上的投技芯片

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
        grabBehavior = GetComponent<ComboGrabBehavior>(); // 👈 获取投技组件
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
        // 1. 先向大脑申请夺权
        if (!brain.RequestActionExecution()) return;

        float distanceToPlayer = Vector3.Distance(transform.position, brain.Player.position);
        brain.FaceTarget(brain.Player.position);

        // ==========================================
        // 🎲 核心融合：普攻和投技进入同一个权重奖池进行抽卡
        // ==========================================
        float totalWeight = 0f;

        // 判断投技现在是否可用（冷却好没、距离够没）
        bool isGrabReady = grabBehavior != null && grabBehavior.IsReady(distanceToPlayer);

        // 如果可用，把投技的权重加进池子
        if (isGrabReady) totalWeight += grabBehavior.weight;

        // 把所有普通攻击的权重加进池子
        if (attackSkills != null)
        {
            foreach (var skill in attackSkills) totalWeight += skill.weight;
        }

        // 防呆保护：如果没有配置任何技能，直接交还控制权
        if (totalWeight <= 0f)
        {
            brain.FinishAction();
            return;
        }

        // 开始摇号
        float randomVal = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        // --- 抽卡阶段 1：先看有没有抽中投技 ---
        if (isGrabReady)
        {
            currentWeight += grabBehavior.weight;
            if (randomVal <= currentWeight)
            {
                currentSkill = null; // 🛡️ 极其关键：设为空，代表当前打出的不是普攻，防止后续 DealDamage 发生冲突！
                grabBehavior.ExecuteGrab(); // 让投技芯片去接管后续动画
                return;
            }
        }

        // --- 抽卡阶段 2：如果没抽中投技，接着抽普攻 ---
        if (attackSkills != null)
        {
            foreach (var skill in attackSkills)
            {
                currentWeight += skill.weight;
                if (randomVal <= currentWeight)
                {
                    currentSkill = skill;
                    if (brain.Anim != null) brain.Anim.SetTrigger(currentSkill.animTriggerName);
                    return;
                }
            }
        }
    }

    // 由 Animator 中的 "DealDamage" 事件触发
    public void DealDamage()
    {
        // 🛡️ 完美防冲突保护：
        // 如果上面抽中的是投技 (currentSkill 为 null)，直接跳过！
        // 这样普攻脚本就不会错误地把普攻的伤害和特效叠加在撕咬上了。
        if (currentSkill == null || currentSkill.attackConfig == null) return;

        if (brain.Player == null || brain.currentState == EnemyBrain.BrainState.Dead) return;

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

    // 由 Animator 中的 "AnimEvent_AttackEnd" 事件触发
    public void AnimEvent_AttackEnd()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        // 🛡️ 终极防冲突锁：
        // 如果 currentSkill 为 null，说明当前大脑正在执行的是【投技】！
        // 此时绝对不能响应普通攻击的结束事件，直接无视，防止动画标签误伤！
        if (currentSkill == null) return;

        brain.FinishAction(); // 1. 先把控制权还给大脑

        // 2. 广播动作结束，让战术走位芯片去接管
        brain.TriggerActionFinished();
    }

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
            case HitShape.Circle: Gizmos.DrawWireSphere(realCenter, action.attackRadius); break;
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