using UnityEngine;
using System.Collections;

public class MeleeEnemy : BaseEnemy
{
    [Header("=== 核心判定设置 ===")]
    public float hitTolerance = 0.5f;

    [Header("=== 战斗手感优化 ===")]
    public float pushPreventDistance = 1.0f;

    [Header("=== 🚀 初见杀 (飞扑投技) 配置 ===")]
    public bool enableAmbush = true;
    public float ambushTriggerDistance = 6.0f;
    public float ambushLungeDistance = 6.5f;
    public float ambushLungeDelay = 0.1f;
    public float ambushLungeDuration = 0.35f;
    public float ambushEndDelay = 0f;

    [Header("=== 🩸 双手前抓 (常规投技) 配置 ===")]
    public float grabHitRange = 1.5f;
    [Range(0f, 1f)] public float grabChance = 0.2f;
    public float grabDamage = 10f;
    public float grabLungeDistance = 2.0f;
    public float grabLungeDelay = 0.15f;
    public float grabLungeDuration = 0.2f;
    public float grabEndDelay = 0f;

    [Space]
    public float siYaoDuration = 5.0f;
    public float detachDistance = 2.5f;
    public float detachDelay = 0.2f;
    public float detachDuration = 0.2f;
    public float grabSuccessEndDelay = 1.5f;

    [Header("=== 常规轻/重击配置 ===")]
    public float lightDamage = 20f;
    public float lightLungeDistance = 1.0f;
    public float lightLungeDelay = 0.1f;
    public float lightLungeDuration = 0.15f;
    public float lightEndDelay = 0f;

    [Space]
    [Range(0f, 1f)] public float heavyAttackChance = 0.3f;
    public float heavyDamage = 40f;
    public float heavyLungeDistance = 3.5f;
    public float heavyLungeDelay = 0.5f;
    public float heavyLungeDuration = 0.25f;
    public float heavyEndDelay = 0f;

    private enum AttackType { Light, Heavy, Grab, Ambush }
    private AttackType currentType = AttackType.Light;

    private bool hasAmbushed = false;
    private bool isGrabSuccess = false;

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
                ExecuteAmbushGrab();
                return;
            }
        }
        base.UpdateChaseState();
    }

    protected override void PerformAttack()
    {
        isGrabSuccess = false;
        float roll = Random.value;
        if (roll <= grabChance) ExecuteGrabAttack();
        else if (Random.value <= heavyAttackChance) ExecuteHeavyAttack();
        else ExecuteLightAttack();
    }

    private void ExecuteAmbushGrab()
    {
        currentType = AttackType.Ambush;
        if (anim != null) anim.SetTrigger("FeiPu");
        StartCoroutine(LungeForwardCoroutine(ambushLungeDistance, ambushLungeDelay, ambushLungeDuration, ambushEndDelay, true));
    }

    private void ExecuteGrabAttack()
    {
        currentType = AttackType.Grab;
        if (anim != null) anim.SetTrigger("QianZhua");
        StartCoroutine(LungeForwardCoroutine(grabLungeDistance, grabLungeDelay, grabLungeDuration, grabEndDelay, true));
    }

    private void ExecuteHeavyAttack()
    {
        currentType = AttackType.Heavy;
        if (anim != null) anim.SetTrigger("Attack2");
        StartCoroutine(LungeForwardCoroutine(heavyLungeDistance, heavyLungeDelay, heavyLungeDuration, heavyEndDelay, false));
    }

    private void ExecuteLightAttack()
    {
        currentType = AttackType.Light;
        if (anim != null) anim.SetTrigger("Attack");
        StartCoroutine(LungeForwardCoroutine(lightLungeDistance, lightLungeDelay, lightLungeDuration, lightEndDelay, false));
    }

    IEnumerator LungeForwardCoroutine(float distance, float delay, float moveDuration, float endDelay, bool isGrabType)
    {
        if (agent != null && agent.isActiveAndEnabled) agent.velocity = Vector3.zero;

        if (delay > 0) yield return new WaitForSeconds(delay);
        if (isDead) yield break;

        // 🔥 开启红灯：代码准备强行推送怪物了，叫停动画 Root Motion！
        isCodeMoving = true;

        float speed = distance / moveDuration;
        float timer = 0f;
        bool hasBraked = false;

        while (timer < moveDuration)
        {
            if (isDead || agent == null || !agent.isActiveAndEnabled) break;
            if (player != null && !hasBraked)
            {
                if (Vector3.Distance(transform.position, player.position) <= pushPreventDistance)
                    hasBraked = true;
            }

            if (!hasBraked) agent.Move(transform.forward * speed * Time.deltaTime);

            timer += Time.deltaTime;
            yield return null;
        }

        // 🔥 绿灯：代码推完了，恢复动画接管
        isCodeMoving = false;

        if (endDelay > 0) yield return new WaitForSeconds(endDelay);
        if (isDead) yield break;
        if (isGrabType && isGrabSuccess) yield break;

        OnAttackAnimEnd();
    }

    public void TryGrab()
    {
        if (player == null || isDead || isGrabSuccess) return;

        if (anim != null)
        {
            anim.ResetTrigger("GrabSuccess");
            anim.ResetTrigger("GrabFail");
        }

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= grabHitRange + hitTolerance)
        {
            isGrabSuccess = true;
            if (agent != null) agent.isStopped = true;
            if (anim != null) anim.SetTrigger("GrabSuccess");

            PlayerReaction reaction = player.GetComponent<PlayerReaction>();
            if (reaction != null) reaction.ApplyGrab(siYaoDuration);

            StartCoroutine(SiYaoCoroutine());
        }
        else
        {
            if (anim != null) anim.SetTrigger("GrabFail");
        }
    }

    IEnumerator SiYaoCoroutine()
    {
        yield return new WaitForSeconds(siYaoDuration);
        if (isDead) yield break;

        if (anim != null) anim.SetTrigger("ZhengTuo");

        if (detachDelay > 0) yield return new WaitForSeconds(detachDelay);
        if (isDead) yield break;

        // 🔥 脱离后倒滑同样属于“代码强推”，挂起RootMotion
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

        isCodeMoving = false; // 🔥 倒滑结束，归还权限

        if (grabSuccessEndDelay > 0) yield return new WaitForSeconds(grabSuccessEndDelay);
        if (isDead) yield break;

        OnAttackAnimEnd();
    }

    public void DealDamage()
    {
        if (player == null || isDead) return;
        float distance = Vector3.Distance(transform.position, player.position);
        bool isGrabType = (currentType == AttackType.Grab || currentType == AttackType.Ambush);
        float currentHitRange = isGrabType ? 5.0f : attackRange;

        if (distance <= currentHitRange + hitTolerance)
        {
            IDamageable target = player.GetComponent<IDamageable>();
            if (target != null)
            {
                float finalDamage = 0;
                switch (currentType)
                {
                    case AttackType.Light: finalDamage = lightDamage; break;
                    case AttackType.Heavy: finalDamage = heavyDamage; break;
                    case AttackType.Grab:
                    case AttackType.Ambush: finalDamage = grabDamage; break;
                }
                target.TakeDamage(finalDamage);
            }
        }
    }
}