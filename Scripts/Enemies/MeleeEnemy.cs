using UnityEngine;
using System.Collections;

public class MeleeEnemy : BaseEnemy
{
    [Header("=== 核心判定设置 ===")]
    public float hitTolerance = 0.5f;

    [Header("=== 战斗手感优化 ===")]
    [Tooltip("突进时的最小贴身距离。距离小于此值时怪物会动态刹车，防止像推土机一样推着玩家走")]
    public float pushPreventDistance = 1.0f;

    // 🔥🔥🔥 初见杀配置 🔥🔥🔥
    [Header("=== 🚀 初见杀 (飞扑投技) 配置 ===")]
    public bool enableAmbush = true;
    public float ambushTriggerDistance = 6.0f;
    public float ambushLungeDistance = 6.5f;
    public float ambushLungeDelay = 0.1f;
    public float ambushLungeDuration = 0.35f;
    [Tooltip("飞扑扑空后的原地蹲起缓冲时间(破绽)")]
    public float ambushEndDelay = 0f;

    [Header("=== 🩸 双手前抓 (常规投技) 配置 ===")]
    public float grabHitRange = 1.5f;

    [Range(0f, 1f)] public float grabChance = 0.2f;
    public float grabDamage = 10f;
    public float grabLungeDistance = 2.0f;
    public float grabLungeDelay = 0.15f;
    public float grabLungeDuration = 0.2f;
    [Tooltip("前抓扑空后的原地硬直时间(破绽)")]
    public float grabEndDelay = 0f;

    [Space]
    [Tooltip("抓到玩家后，撕咬持续的时间")]
    public float siYaoDuration = 5.0f;
    [Tooltip("撕咬结束，向后滑行脱离的距离")]
    public float detachDistance = 2.5f;
    [Tooltip("触发挣脱动画后，延迟多久开始向后滑")]
    public float detachDelay = 0.2f;
    [Tooltip("向后滑行耗时")]
    public float detachDuration = 0.2f;

    // 🔥🔥🔥 新增：挣脱后的硬直配置 🔥🔥🔥
    [Tooltip("被玩家挣脱/踢开并向后滑行结束后，原地踉跄发呆的硬直时间")]
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

    // 内部逻辑标记
    private enum AttackType { Light, Heavy, Grab, Ambush }
    private AttackType currentType = AttackType.Light;

    private bool hasAmbushed = false;
    private bool isGrabSuccess = false;

    // ==========================================
    // 🌟 核心拦截：追击逻辑
    // ==========================================
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

    // ==========================================
    // 具体执行器
    // ==========================================
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
        Debug.Log("<color=red>【初见杀警告】怪物从远处飞扑过来了！</color>");
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

    // ==========================================
    // 🔥 升级版突进引擎：【前摇 -> 突进 -> 后摇缓冲】完美闭环
    // ==========================================
    IEnumerator LungeForwardCoroutine(float distance, float delay, float moveDuration, float endDelay, bool isGrabType)
    {
        // 加一层物理急刹车保险
        if (agent != null && agent.isActiveAndEnabled) agent.velocity = Vector3.zero;

        // 1. 【前摇】等待时间
        if (delay > 0) yield return new WaitForSeconds(delay);
        if (isDead) yield break;

        // 删除了方向锁死，怪物会在突进期间继续盯着玩家

        // 2. 【突进】位移时间
        float speed = distance / moveDuration;
        float timer = 0f;

        // 🔥🔥🔥 核心新增：刹车锁！只要刹过一次车，这招就不许再往前滑了
        bool hasBraked = false;

        while (timer < moveDuration)
        {
            if (isDead || agent == null || !agent.isActiveAndEnabled) break;

            // 只要没触发过刹车，就时刻检测玩家距离
            if (player != null && !hasBraked)
            {
                float currentDist = Vector3.Distance(transform.position, player.position);
                if (currentDist <= pushPreventDistance)
                {
                    hasBraked = true; // 🔥 触发刹车！彻底焊死，这招剩余的时间里绝不许再往前动！
                }
            }

            // 只有在没刹车的情况下，才允许往前滑
            if (!hasBraked)
            {
                agent.Move(transform.forward * speed * Time.deltaTime);
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 3. 【后摇缓冲】原地不动，播放收招/缓冲动画 (针对轻重击和扑空的投技)
        if (endDelay > 0) yield return new WaitForSeconds(endDelay);
        if (isDead) yield break;

        // 4. 【结算解锁】
        // 如果是投技，并且刚刚成功抓到了玩家，那么不需要在这里解锁，交给撕咬协程去解
        if (isGrabType && isGrabSuccess) yield break;

        // 否则，彻底解除锁定，怪物开始走动追玩家
        OnAttackAnimEnd();
    }

    // ==========================================
    // 投技专属判定：尝试抓取
    // ==========================================
    public void TryGrab()
    {
        // 🔥 核心补充：加上了 isGrabSuccess，防止鬼畜动画多次触发导致重叠撕咬 Bug！
        if (player == null || isDead || isGrabSuccess) return;

        if (anim != null)
        {
            anim.ResetTrigger("GrabSuccess");
            anim.ResetTrigger("GrabFail");
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= grabHitRange + hitTolerance)
        {
            Debug.Log("<color=red>【抓取成功！】进入长达 5 秒的残酷撕咬！</color>");

            isGrabSuccess = true;

            if (agent != null) agent.isStopped = true;
            if (anim != null) anim.SetTrigger("GrabSuccess");

            PlayerReaction reaction = player.GetComponent<PlayerReaction>();
            if (reaction != null) reaction.ApplyGrab(siYaoDuration);

            StartCoroutine(SiYaoCoroutine());
        }
        else
        {
            Debug.Log("<color=grey>抓取落空，发送 GrabFail 指令，等待缓冲时间结束...</color>");
            if (anim != null) anim.SetTrigger("GrabFail");
        }
    }

    // 🔥 撕咬计时与挣脱控制中心
    IEnumerator SiYaoCoroutine()
    {
        // 1. 无情地咬 5 秒钟
        yield return new WaitForSeconds(siYaoDuration);
        if (isDead) yield break;

        // 2. 触发挣脱推开动画
        if (anim != null) anim.SetTrigger("ZhengTuo");

        // 3. 动画发力前摇
        if (detachDelay > 0) yield return new WaitForSeconds(detachDelay);
        if (isDead) yield break;

        // 4. 向后滑行脱离
        float speed = detachDistance / detachDuration;
        float slideTimer = 0f;
        while (slideTimer < detachDuration)
        {
            if (isDead || agent == null || !agent.isActiveAndEnabled) break;
            agent.Move(-transform.forward * speed * Time.deltaTime);
            slideTimer += Time.deltaTime;
            yield return null;
        }

        // 5. 滑行结束后，原地踉跄喘息！(给玩家反击的机会)
        if (grabSuccessEndDelay > 0) yield return new WaitForSeconds(grabSuccessEndDelay);
        if (isDead) yield break;

        // 6. 喘息结束，彻底解锁，大脑恢复运转！
        OnAttackAnimEnd();
    }

    // ==========================================
    // 伤害判定
    // ==========================================
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
                    case AttackType.Ambush:
                        finalDamage = grabDamage;
                        break;
                }
                target.TakeDamage(finalDamage);
            }
        }
    }
}