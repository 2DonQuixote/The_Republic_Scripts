using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(EnemyBrain))]
public class BiteGrabBehavior : MonoBehaviour
{
    [Header("=== ⚙️ 触发与权重 ===")]
    public float weight = 15f;
    public float grabRange = 2.5f;
    public float cooldown = 8f;

    [Header("=== 🎬 动画配置 ===")]
    public string animGrab = "Attack_Grab";

    [Header("=== 📐 判定配置 ===")]
    public EnemyAttackConfig grabHitbox;

    [Header("=== 🩸 撕咬与持续伤害 (DoT) ===")]
    public float biteDuration = 3.0f;
    public float damageTickInterval = 0.5f;
    public float tickDamage = 8f;
    public GameObject bloodVFX;

    private EnemyBrain brain;
    private TacticalRetreatBehavior retreatBehavior;
    private float cooldownTimer = 0f;

    private bool isGrabbing = false;
    // 🔥 新增：幽灵事件防呆锁
    private bool isBiteFinished = false;
    private Transform caughtPlayer;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
        retreatBehavior = GetComponent<TacticalRetreatBehavior>();
    }

    private void Update()
    {
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;
    }

    public bool IsReady(float distanceToPlayer)
    {
        return distanceToPlayer <= grabRange && cooldownTimer <= 0;
    }

    public void ExecuteGrab()
    {
        cooldownTimer = cooldown;
        isGrabbing = false;
        isBiteFinished = false; // 每次出招时，把锁打开

        brain.FaceTargetInstantly(brain.Player.position);
        if (brain.Anim != null) brain.Anim.CrossFadeInFixedTime(animGrab, 0.15f);
    }

    public void AnimEvent_TryBite()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        List<Collider> hits = grabHitbox.GetHitTargets(transform);
        bool caught = false;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                caught = true;
                caughtPlayer = hit.transform;
                break;
            }
        }

        if (caught)
        {
            isGrabbing = true;
            if (brain.Anim != null) brain.Anim.speed = 0f;

            PlayerReaction reaction = caughtPlayer.GetComponent<PlayerReaction>();
            if (reaction != null) reaction.ApplyGrab(biteDuration + 0.2f);

            StartCoroutine(BiteDamageRoutine());
        }
    }

    public void AnimEvent_BiteEnd()
    {
        // 🔥 核心防线：如果吸血已经结束(isBiteFinished为true)，绝对不允许这个幽灵事件解锁大脑！
        if (brain.currentState == EnemyBrain.BrainState.Dead || isGrabbing || isBiteFinished) return;

        // 只有抓空了，才会走到这一步来正常结束
        brain.FinishAction();
        brain.TriggerActionFinished();
    }

    private IEnumerator BiteDamageRoutine()
    {
        float timer = 0f;

        while (timer < biteDuration)
        {
            if (brain.currentState == EnemyBrain.BrainState.Dead || brain.currentState == EnemyBrain.BrainState.Stunned)
            {
                if (brain.Anim != null) brain.Anim.speed = 1f;
                yield break;
            }

            if (caughtPlayer != null)
            {
                IDamageable playerHealth = caughtPlayer.GetComponent<IDamageable>();
                playerHealth?.TakeDamage(tickDamage, false);

                if (bloodVFX != null)
                    Instantiate(bloodVFX, caughtPlayer.position + Vector3.up, Quaternion.identity);
            }

            yield return new WaitForSeconds(damageTickInterval);
            timer += damageTickInterval;
        }

        // --- 撕咬3秒彻底结束 ---

        // 🔥 上锁！声明吸血过程已完成，接下来的任何旧动画事件统统作废！
        isBiteFinished = true;

        if (brain.Anim != null) brain.Anim.speed = 1f;
        isGrabbing = false;

        // 解锁大脑，让撤退芯片可以接管
        brain.FinishAction();

        if (retreatBehavior != null)
        {
            float originalChance = retreatBehavior.retreatChance;
            retreatBehavior.retreatChance = 1f;

            bool retreatSuccess = retreatBehavior.TryStartRetreat();

            retreatBehavior.retreatChance = originalChance;

            if (retreatSuccess) yield break;
        }

        brain.TriggerActionFinished();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (grabHitbox == null) return;

        Gizmos.color = new Color(1f, 0f, 1f, 0.8f);
        Vector3 realCenter = transform.position + transform.rotation * grabHitbox.hitOffset;

        switch (grabHitbox.shapeType)
        {
            case HitShape.Circle:
                Gizmos.DrawWireSphere(realCenter, grabHitbox.attackRadius);
                break;
            case HitShape.Sector:
                Gizmos.color = new Color(1f, 0f, 1f, 0.2f);
                Gizmos.DrawWireSphere(realCenter, grabHitbox.attackRadius);
                Gizmos.color = new Color(1f, 0f, 1f, 0.8f);
                Vector3 forward = transform.forward;
                Vector3 leftRay = Quaternion.AngleAxis(-grabHitbox.attackAngle * 0.5f, Vector3.up) * forward;
                Vector3 rightRay = Quaternion.AngleAxis(grabHitbox.attackAngle * 0.5f, Vector3.up) * forward;
                Gizmos.DrawRay(realCenter, leftRay * grabHitbox.attackRadius);
                Gizmos.DrawRay(realCenter, rightRay * grabHitbox.attackRadius);
                break;
            case HitShape.Rectangle:
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(realCenter, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, grabHitbox.boxSize);
                Gizmos.matrix = oldMatrix;
                break;
        }
    }
#endif
}