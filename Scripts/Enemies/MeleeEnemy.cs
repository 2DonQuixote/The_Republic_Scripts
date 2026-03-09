using UnityEngine;
using System.Collections.Generic;

public class MeleeEnemy : BaseEnemy
{
    [Header("=== 🚀 初见杀 (飞扑投技) 配置 ===")]
    public bool enableAmbush = true;
    public float ambushTriggerDistance = 6.0f;

    [Header("=== 🩸 双手前抓 (常规投技) 配置 ===")]
    [Range(0f, 1f)] public float grabChance = 0.2f;
    public float siYaoDuration = 5.0f;
    public float detachDistance = 2.5f;
    public float detachDuration = 0.4f;
    public float grabSuccessEndDelay = 1.5f;

    [Tooltip("投技抓空后，在原地发呆喘气几秒？")]
    public float grabFailPenaltyTime = 2.0f;

    [Header("=== ⚔️ 怪物攻击动作配置 (复用玩家的判定体系) ===")]
    [Range(0f, 1f)] public float heavyAttackChance = 0.3f;

    public AttackAction lightAttackConfig;
    public AttackAction heavyAttackConfig;
    public AttackAction grabAttackConfig;
    public AttackAction ambushAttackConfig;

    private AttackAction currentAction;
    private bool hasAmbushed = false;
    private bool isGrabSuccess = false;

    // 🔥 防呆锁：防止动画融合导致多次触发结束标签
    private bool isRecovering = false;
    // 🔥 防复读机制：防止在100%概率下无限连抓
    private bool lastAttackWasGrab = false;

    protected override void UpdateChaseState()
    {
        if (enableAmbush && !hasAmbushed && player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance <= ambushTriggerDistance && distance > attackRange)
            {
                hasAmbushed = true;
                ChangeState(AIState.Attack);
                isAttacking = true;
                ExecuteAttack("FeiPu", ambushAttackConfig);
                return;
            }
        }
        base.UpdateChaseState();
    }

    protected override void PerformAttack()
    {
        isGrabSuccess = false;
        isRecovering = false; // 出招前重置锁

        float roll = Random.value;

        // 🔥 防复读：如果上一次是投技，这次绝对不放投技 (哪怕概率是100%)
        if (roll <= grabChance && !lastAttackWasGrab)
        {
            lastAttackWasGrab = true;
            ExecuteAttack("QianZhua", grabAttackConfig);
        }
        else if (Random.value <= heavyAttackChance)
        {
            lastAttackWasGrab = false;
            ExecuteAttack("Attack2", heavyAttackConfig);
        }
        else
        {
            lastAttackWasGrab = false;
            ExecuteAttack("Attack", lightAttackConfig);
        }
    }

    private void ExecuteAttack(string triggerName, AttackAction actionConfig)
    {
        if (agent != null && agent.isActiveAndEnabled) agent.velocity = Vector3.zero;

        currentAction = actionConfig;
        isRotationLocked = true;

        if (anim != null) anim.SetTrigger(triggerName);
    }

    // ==========================================
    // 🎭 动画事件接收区
    // ==========================================

    public void DealDamage()
    {
        if (player == null || isDead || currentAction == null) return;

        List<Collider> hits = currentAction.GetHitTargets(transform);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                IDamageable target = hit.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(currentAction.damageMultiplier);

                    if (currentAction.hitVFX != null)
                        Instantiate(currentAction.hitVFX, hit.transform.position + Vector3.up, Quaternion.identity);
                }
            }
        }
    }

    public void TryGrab()
    {
        if (player == null || isDead || isGrabSuccess || currentAction == null) return;

        if (anim != null) anim.ResetTrigger("GrabSuccess");

        List<Collider> hits = currentAction.GetHitTargets(transform);
        bool caughtPlayer = hits.Exists(c => c.CompareTag("Player"));

        if (caughtPlayer)
        {
            isGrabSuccess = true;

            if (agent != null) agent.isStopped = true;
            if (anim != null) anim.SetTrigger("GrabSuccess");

            PlayerReaction reaction = player.GetComponent<PlayerReaction>();
            if (reaction != null) reaction.ApplyGrab(siYaoDuration);

            StartCoroutine(SiYaoCoroutine());
        }
        // 💡 删除了 isGrabMissed，不再依赖完美触发 TryGrab！
    }

    public void AnimEvent_AttackEnd()
    {
        if (isDead) return;
        if (isGrabSuccess) return;
        if (isRecovering) return; // 防止重复触发

        // 🌟 终极神级防呆判断：只要当前放的是投技（或飞扑），且没成功，100%强制罚站！
        // 这样即使您在动画里忘记打 TryGrab 标签，它也绝对会罚站！
        bool wasGrabAttack = (currentAction == grabAttackConfig || currentAction == ambushAttackConfig);

        if (wasGrabAttack)
        {
            StartCoroutine(GrabMissRecoveryRoutine());
            return;
        }

        OnAttackAnimEnd();
    }

    // 🌟 重写受伤打断：防止协程被 BaseEnemy 掐死后，状态锁死变成木桩
    public override void OnHitInterrupt()
    {
        base.OnHitInterrupt(); //
        isRecovering = false;
        lastAttackWasGrab = false; // 挨打后清空复读记忆
    }

    // ==========================================
    // ⏳ 协程逻辑区
    // ==========================================

    System.Collections.IEnumerator GrabMissRecoveryRoutine()
    {
        isRecovering = true;  // 上锁！

        // 发呆期间，强行拉起手刹并清空速度
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }

        // 解除方向锁定，让它发呆时能跟着玩家转头
        isRotationLocked = false;

        float waitTime = Mathf.Max(0.1f, grabFailPenaltyTime);
        yield return new WaitForSeconds(waitTime);

        if (isDead) yield break;

        // 罚站结束，解锁
        isRecovering = false;

        // 跳过绕圈直接追击
        skipTacticalThisTime = true;
        OnAttackAnimEnd();
    }

    System.Collections.IEnumerator SiYaoCoroutine()
    {
        yield return new WaitForSeconds(siYaoDuration);
        if (isDead) yield break;

        if (anim != null) anim.SetTrigger("ZhengTuo");

        isCodeMoving = true;

        float speed = detachDistance / detachDuration;
        float slideTimer = 0f;
        while (slideTimer < detachDuration)
        {
            if (isDead || agent == null || !agent.isActiveAndEnabled) break;
            agent.Move(-transform.forward * speed * Time.deltaTime);
            slideTimer += Time.deltaTime;
            yield return null;
        }

        isCodeMoving = false;

        if (grabSuccessEndDelay > 0) yield return new WaitForSeconds(grabSuccessEndDelay);
        if (isDead) yield break;

        OnAttackAnimEnd();
    }

    // ==========================================
    // 🎨 Scene 窗口可视化辅助线
    // ==========================================
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawActionGizmo(lightAttackConfig, Color.yellow);
        DrawActionGizmo(heavyAttackConfig, Color.red);
        DrawActionGizmo(grabAttackConfig, Color.magenta);
        DrawActionGizmo(ambushAttackConfig, Color.cyan);
    }

    private void DrawActionGizmo(AttackAction action, Color drawColor)
    {
        if (action == null) return;
        Gizmos.color = new Color(drawColor.r, drawColor.g, drawColor.b, 0.8f);

        switch (action.shapeType)
        {
            case HitShape.Circle:
                Gizmos.DrawWireSphere(transform.position, action.attackRadius);
                break;

            case HitShape.Sector:
                Gizmos.color = new Color(drawColor.r, drawColor.g, drawColor.b, 0.2f);
                Gizmos.DrawWireSphere(transform.position, action.attackRadius);

                Gizmos.color = new Color(drawColor.r, drawColor.g, drawColor.b, 0.8f);
                Vector3 forward = transform.forward;
                Vector3 leftRay = Quaternion.AngleAxis(-action.attackAngle * 0.5f, Vector3.up) * forward;
                Vector3 rightRay = Quaternion.AngleAxis(action.attackAngle * 0.5f, Vector3.up) * forward;

                Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, leftRay * action.attackRadius);
                Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, rightRay * action.attackRadius);
                break;

            case HitShape.Rectangle:
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(new Vector3(0, 1f, action.boxSize.z * 0.5f), action.boxSize);
                Gizmos.matrix = oldMatrix;
                break;
        }
    }
#endif
}