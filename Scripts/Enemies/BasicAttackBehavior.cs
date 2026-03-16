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

// 🔥 连招派生分支
[System.Serializable]
public class ComboBranch
{
    public string branchName = "派生招式";
    [Tooltip("Animator里动画节点的名字")]
    public string animStateName = "Attack2";

    [Header("触发条件")]
    [Range(0f, 1f)] public float triggerChance = 0.5f;

    public bool useHealthThreshold = false;
    [Range(0f, 1f)] public float maxHealthThreshold = 0.5f;

    [Header("派生招式的独立判定")]
    public EnemyAttackConfig attackConfig;
}

[RequireComponent(typeof(EnemyBrain))]
public class BasicAttackBehavior : MonoBehaviour
{
    [System.Serializable]
    public class AttackSkill
    {
        public string skillName = "普通攻击";
        [Tooltip("Animator里动画节点的名字")]
        public string animStateName = "Attack1";
        public float weight = 10f;

        [Header("=== 🩸 阶段解锁 (二阶段专用) ===")]
        public bool useHealthThreshold = false;
        [Range(0f, 1f)] public float maxHealthThreshold = 0.5f;

        [Header("=== ⚔️ 基础判定与派生连招 ===")]
        public EnemyAttackConfig attackConfig;
        public List<ComboBranch> comboBranches = new List<ComboBranch>();
    }

    [Header("=== 📏 触发距离 ===")]
    public float attackRange = 2.0f;

    [Header("=== 📜 招式卡池 ===")]
    public List<AttackSkill> attackSkills = new List<AttackSkill>();

    private EnemyBrain brain;
    private ComboGrabBehavior grabBehavior;
    private EnemyHealth myHealth;

    private AttackSkill currentSkill;
    private EnemyAttackConfig currentActiveHitbox;

    private float CurrentHpPercent => myHealth != null ? myHealth.GetCurrentHealthPercent() : 1f;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
        grabBehavior = GetComponent<ComboGrabBehavior>();
        myHealth = GetComponent<EnemyHealth>();
    }

    private void Update()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        if (brain.currentState == EnemyBrain.BrainState.Chase)
        {
            if (Vector3.Distance(transform.position, brain.Player.position) <= attackRange)
            {
                TryStartAttack();
            }
        }
    }

    private void TryStartAttack()
    {
        if (!brain.RequestActionExecution()) return;

        brain.FaceTarget(brain.Player.position);
        float distanceToPlayer = Vector3.Distance(transform.position, brain.Player.position);

        float totalWeight = 0f;
        List<AttackSkill> availableSkills = new List<AttackSkill>();
        float currentHp = CurrentHpPercent;

        foreach (var skill in attackSkills)
        {
            if (skill.useHealthThreshold && currentHp > skill.maxHealthThreshold) continue;
            availableSkills.Add(skill);
            totalWeight += skill.weight;
        }

        bool isGrabReady = grabBehavior != null && grabBehavior.IsReady(distanceToPlayer);
        if (isGrabReady) totalWeight += grabBehavior.weight;

        if (totalWeight <= 0f)
        {
            brain.FinishAction();
            return;
        }

        float randomVal = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        if (isGrabReady)
        {
            currentWeight += grabBehavior.weight;
            if (randomVal <= currentWeight)
            {
                currentSkill = null;
                currentActiveHitbox = null;
                grabBehavior.ExecuteGrab();
                return;
            }
        }

        foreach (var skill in availableSkills)
        {
            currentWeight += skill.weight;
            if (randomVal <= currentWeight)
            {
                currentSkill = skill;
                currentActiveHitbox = skill.attackConfig;

                if (brain.Anim != null)
                {
                    // 恢复 CrossFadeInFixedTime，这是不会卡死大脑的做法！
                    brain.Anim.CrossFadeInFixedTime(currentSkill.animStateName, 0.15f);
                }
                return;
            }
        }
    }

    // 💥【关键事件1】在动画中武器击中玩家的瞬间打上：DealDamage
    public void DealDamage()
    {
        if (currentActiveHitbox == null || brain.Player == null || brain.currentState == EnemyBrain.BrainState.Dead) return;

        List<Collider> hits = currentActiveHitbox.GetHitTargets(transform);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                IDamageable target = hit.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(currentActiveHitbox.damageMultiplier);
                    if (currentActiveHitbox.hitVFX != null)
                        Instantiate(currentActiveHitbox.hitVFX, hit.transform.position + Vector3.up, Quaternion.identity);
                }
            }
        }
    }

    // 🔄【关键事件2】在动画后摇阶段打上：AnimEvent_ComboCheck
    public void AnimEvent_ComboCheck()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead || currentSkill == null) return;
        if (currentSkill.comboBranches == null || currentSkill.comboBranches.Count == 0) return;

        float currentHp = CurrentHpPercent;

        foreach (var branch in currentSkill.comboBranches)
        {
            if (branch.useHealthThreshold && currentHp > branch.maxHealthThreshold) continue;

            if (Random.value <= branch.triggerChance)
            {
                currentActiveHitbox = branch.attackConfig;
                brain.FaceTargetInstantly(brain.Player.position);

                if (brain.Anim != null)
                {
                    // 连招也恢复 CrossFadeInFixedTime
                    brain.Anim.CrossFadeInFixedTime(branch.animStateName, 0.15f);
                }
                return;
            }
        }
    }

    // 🛑【关键事件3】在动画最后一帧打上：AnimEvent_AttackEnd
    public void AnimEvent_AttackEnd()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;
        if (currentSkill == null) return;

        // 1. 攻击打完了，先归还大脑控制权
        brain.FinishAction();

        // 2. 尝试寻找战术撤退芯片
        TacticalRetreatBehavior retreatBehavior = GetComponent<TacticalRetreatBehavior>();
        if (retreatBehavior != null)
        {
            // 如果撤退芯片接管了身体，就直接返回，不触发后续逻辑
            if (retreatBehavior.TryStartRetreat())
            {
                return;
            }
        }

        // 3. 如果没有撤退芯片，或者没摇中撤退概率，正常结束回合
        brain.TriggerActionFinished();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (attackSkills == null) return;
        foreach (var skill in attackSkills)
        {
            if (skill == null) continue;
            if (skill.attackConfig != null) DrawActionGizmo(skill.attackConfig, Color.red);
            if (skill.comboBranches != null)
            {
                foreach (var branch in skill.comboBranches)
                {
                    if (branch != null && branch.attackConfig != null)
                        DrawActionGizmo(branch.attackConfig, new Color(1f, 0.5f, 0f));
                }
            }
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