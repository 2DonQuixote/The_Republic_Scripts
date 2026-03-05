using UnityEngine;
using System.Collections;

public class MeleeEnemy : BaseEnemy
{
    [Header("=== 核心判定设置 ===")]
    public float hitTolerance = 0.5f;

    [Header("=== 🚀 初见杀 (飞扑投技) 配置 ===")]
    public bool enableAmbush = true;
    public float ambushTriggerDistance = 6.0f;
    [Tooltip("飞扑动画的总长度(秒)，播完后恢复自由")]
    public float ambushTotalTime = 1.5f;

    [Header("=== 🩸 双手前抓 (常规投技) 配置 ===")]
    public float grabHitRange = 1.5f;
    [Range(0f, 1f)] public float grabChance = 0.2f;
    public float grabDamage = 10f;
    [Tooltip("前抓动画的总长度(秒)")]
    public float grabTotalTime = 1.2f;

    [Space]
    public float siYaoDuration = 5.0f;
    public float detachDistance = 2.5f;
    public float detachDuration = 0.4f; // 合并了 detachDelay，统一为后退耗时
    public float grabSuccessEndDelay = 1.5f;

    [Header("=== 常规轻/重击配置 ===")]
    public float lightDamage = 20f;
    [Tooltip("轻击动画的总长度(秒)")]
    public float lightTotalTime = 1.0f;

    [Space]
    [Range(0f, 1f)] public float heavyAttackChance = 0.3f;
    public float heavyDamage = 40f;
    [Tooltip("重击动画的总长度(秒)")]
    public float heavyTotalTime = 1.5f;

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
        StartCoroutine(WaitAttackEndCoroutine(ambushTotalTime, true));
    }

    private void ExecuteGrabAttack()
    {
        currentType = AttackType.Grab;
        if (anim != null) anim.SetTrigger("QianZhua");
        StartCoroutine(WaitAttackEndCoroutine(grabTotalTime, true));
    }

    private void ExecuteHeavyAttack()
    {
        currentType = AttackType.Heavy;
        if (anim != null) anim.SetTrigger("Attack2");
        StartCoroutine(WaitAttackEndCoroutine(heavyTotalTime, false));
    }

    private void ExecuteLightAttack()
    {
        currentType = AttackType.Light;
        if (anim != null) anim.SetTrigger("Attack");
        StartCoroutine(WaitAttackEndCoroutine(lightTotalTime, false));
    }

    // 🔥 终极形态：它现在只是一个单纯的“等动画播完”的计时器，但加上了方向锁！
    IEnumerator WaitAttackEndCoroutine(float totalTime, bool isGrabType)
    {
        if (agent != null && agent.isActiveAndEnabled) agent.velocity = Vector3.zero;

        // 🌟 核心修复：攻击一旦开始，立刻锁死怪物的方向！
        // 这样它的 Root Motion 冲刺就是一条完美的直线，再也不会在半空画弯路了！
        isRotationLocked = true;

        // 直接等待整个动画播完
        yield return new WaitForSeconds(totalTime);

        if (isDead) yield break;
        if (isGrabType && isGrabSuccess) yield break;

        // BaseEnemy 里的 OnAttackAnimEnd() 会自动把 isRotationLocked 恢复为 false，所以不用担心它以后一直瞎眼
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

        // 🔥 脱离后倒滑 (代码强推)
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

        isCodeMoving = false; // 🔥 倒滑结束，归还权限给动画

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