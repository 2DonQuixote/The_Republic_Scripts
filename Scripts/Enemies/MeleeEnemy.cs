using UnityEngine;
using System.Collections;

public class MeleeEnemy : BaseEnemy
{
    [Header("=== 核心判定设置 ===")]
    public float hitTolerance = 0.5f;

    [Header("=== 战斗手感优化 ===")]
    [Tooltip("突进时的最小贴身距离。距离小于此值时怪物会动态刹车，防止像推土机一样推着玩家走")]
    public float pushPreventDistance = 1.0f;

    [Header("=== 🩸 双手前抓 (投技) 配置 ===")]
    [Tooltip("发动投技的概率")]
    [Range(0f, 1f)] public float grabChance = 0.2f;
    [Tooltip("注意：因为会咬很多口，这里的伤害是【每一口】的伤害！建议调低，比如 10")]
    public float grabDamage = 10f;
    public float grabLungeDistance = 2.0f; // 前抓的短促突进
    public float grabLungeDelay = 0.15f;
    public float grabLungeDuration = 0.2f;

    [Space]
    [Tooltip("抓取成功后，撕咬持续的时间 (秒)")]
    public float siYaoDuration = 5.0f;
    [Tooltip("撕咬结束后，向后滑行脱离的距离")]
    public float detachDistance = 2.5f;

    // 🔥 保留：后撤延时
    [Tooltip("触发挣脱动画后，延迟多久才向后弹开 (配合动画发力点)")]
    public float detachDelay = 0.2f;

    [Tooltip("向后脱离耗时")]
    public float detachDuration = 0.2f;

    [Header("=== 常规轻/重击配置 ===")]
    public float lightDamage = 20f;
    public float lightLungeDistance = 1.0f;
    public float lightLungeDelay = 0.1f;
    public float lightLungeDuration = 0.15f;

    [Space]
    [Range(0f, 1f)] public float heavyAttackChance = 0.3f;
    public float heavyDamage = 40f;
    public float heavyLungeDistance = 3.5f;
    public float heavyLungeDelay = 0.5f;
    public float heavyLungeDuration = 0.25f;

    // 内部逻辑标记
    private enum AttackType { Light, Heavy, Grab }
    private AttackType currentType = AttackType.Light;

    // ==========================================
    // 1. 攻击决策逻辑
    // ==========================================
    protected override void PerformAttack()
    {
        float roll = Random.value;

        // 优先出投技
        if (roll <= grabChance) ExecuteGrabAttack();
        else if (Random.value <= heavyAttackChance) ExecuteHeavyAttack();
        else ExecuteLightAttack();
    }

    // ==========================================
    // 2. 具体执行器
    // ==========================================
    private void ExecuteGrabAttack()
    {
        currentType = AttackType.Grab;
        if (anim != null) anim.SetTrigger("QianZhua"); // 触发起手式
        StartCoroutine(LungeForwardCoroutine(grabLungeDistance, grabLungeDelay, grabLungeDuration));
        Debug.Log("<color=orange>【投技警告】怪物双手前扑！</color>");
    }

    private void ExecuteHeavyAttack()
    {
        currentType = AttackType.Heavy;
        if (anim != null) anim.SetTrigger("Attack2");
        StartCoroutine(LungeForwardCoroutine(heavyLungeDistance, heavyLungeDelay, heavyLungeDuration));
    }

    private void ExecuteLightAttack()
    {
        currentType = AttackType.Light;
        if (anim != null) anim.SetTrigger("Attack");
        StartCoroutine(LungeForwardCoroutine(lightLungeDistance, lightLungeDelay, lightLungeDuration));
    }

    // ==========================================
    // 3. 🌟 升级版：带动态刹车功能的突进引擎
    // ==========================================
    IEnumerator LungeForwardCoroutine(float distance, float delay, float duration)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        if (isDead) yield break;

        float speed = distance / duration;
        float timer = 0f;

        while (timer < duration)
        {
            if (isDead || agent == null || !agent.isActiveAndEnabled) break;

            // ------------------------------------------------
            // 🔥 核心优化：动态刹车检测
            // ------------------------------------------------
            bool shouldMove = true;
            if (player != null)
            {
                float currentDist = Vector3.Distance(transform.position, player.position);
                // 如果怪物离玩家太近了，就踩死刹车（不执行位移代码）
                if (currentDist <= pushPreventDistance)
                {
                    shouldMove = false;
                }
            }

            // 只有在允许移动（没贴脸）的情况下才往前冲
            if (shouldMove)
            {
                agent.Move(transform.forward * speed * Time.deltaTime);
            }

            // ⚠️ 极其重要：无论是否移动，时间照常流逝！这样才能保证动画与状态不脱节
            timer += Time.deltaTime;
            yield return null;
        }
    }

    // ==========================================
    // 4. 🔥 投技专属判定：尝试抓取
    // ==========================================
    public void TryGrab()
    {
        if (player == null || isDead) return;

        if (anim != null)
        {
            anim.ResetTrigger("GrabSuccess");
            anim.ResetTrigger("GrabFail");
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= grabLungeDistance + hitTolerance)
        {
            Debug.Log("<color=red>【抓取成功！】进入长达 5 秒的残酷撕咬！</color>");

            if (agent != null) agent.isStopped = true;
            if (anim != null) anim.SetTrigger("GrabSuccess");

            // ==========================================
            // 🔥 新增：通知玩家被抓了！并传递撕咬时长
            // ==========================================
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                // 把怪物的撕咬时长传给玩家，让玩家配合演出
                pc.ApplyGrab(siYaoDuration);
            }
            // ==========================================

            StartCoroutine(SiYaoCoroutine());
        }
        else
        {
            Debug.Log("<color=grey>抓取落空，发送 GrabFail 指令，准备恢复...</color>");
            if (anim != null) anim.SetTrigger("GrabFail");
        }
    }

    // 🔥 撕咬计时与挣脱控制中心
    IEnumerator SiYaoCoroutine()
    {
        // 1. 无情地咬 5 秒钟
        yield return new WaitForSeconds(siYaoDuration);
        if (isDead) yield break;

        // 2. 5 秒时间到！触发挣脱动画
        if (anim != null) anim.SetTrigger("ZhengTuo");
        Debug.Log("<color=yellow>撕咬结束，准备挣脱！</color>");

        // 3. 🔥 等待挣脱动画的发力点 (比如推开玩家的那一瞬间)
        if (detachDelay > 0) yield return new WaitForSeconds(detachDelay);
        if (isDead) yield break;

        // 4. 配合动画发力，往后滑行拉开距离
        float speed = detachDistance / detachDuration;
        float timer = 0f;
        while (timer < detachDuration)
        {
            if (isDead || agent == null || !agent.isActiveAndEnabled) break;
            agent.Move(-transform.forward * speed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    // ==========================================
    // 5. 伤害判定
    // ==========================================
    public void DealDamage()
    {
        if (player == null || isDead) return;

        float distance = Vector3.Distance(transform.position, player.position);

        // 如果是投技撕咬，判定范围可以极其宽松（因为已经抓到了）
        float currentHitRange = (currentType == AttackType.Grab) ? 5.0f : attackRange;

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
                    case AttackType.Grab: finalDamage = grabDamage; break;
                }
                target.TakeDamage(finalDamage);
            }
        }
    }
}