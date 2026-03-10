using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BeastEliteEnemy : BaseEnemy
{
    [Header("=== 🩸 阶段与概率配置 ===")]
    [Tooltip("血量低于此值时，左挥击有概率派生右挥击")]
    [Range(0f, 1f)] public float comboHpThreshold = 0.5f;
    [Range(0f, 1f)] public float comboChance = 0.4f;

    [Tooltip("血量高于此值时，战术后退有概率举起双手格挡")]
    [Range(0f, 1f)] public float defenseHpThreshold = 0.7f;
    [Range(0f, 1f)] public float defenseChance = 0.5f;

    [Tooltip("血量低于此值时，触发大跳+战吼，进入疯狗模式")]
    [Range(0f, 1f)] public float frenzyHpThreshold = 0.3f;

    [Header("=== ⚔️ 攻击与动作配置 ===")]
    [Range(0f, 1f)] public float heavyAttackChance = 0.3f;
    [Range(0f, 1f)] public float grabChance = 0.2f;

    public AttackAction leftSwipeConfig;    // 左挥击 (Attack)
    public AttackAction rightSwipeConfig;   // 右挥击 (Attack2)
    public AttackAction heavySmashConfig;   // 重击 (Attack3)
    public AttackAction grabConfig;         // 投技 (QianZhua)

    [Header("=== 🩸 投技专属表现 ===")]
    public float grabHoldDuration = 3.0f;   // 动作定格的时长
    public GameObject grabVFX;              // 抓到人后飙血的特效

    // --- 内部状态缓存 ---
    private EnemyHealth myHealth;
    private float currentHpPercent = 1f;
    private bool isFrenzied = false;
    private bool isDefendingNow = false;
    private bool lastAttackWasGrab = false;
    private AttackAction currentAction;

    protected override void Start()
    {
        base.Start();

        // 1. 获取血量组件，并订阅血量变化事件
        myHealth = GetComponent<EnemyHealth>();
        if (myHealth != null)
        {
            myHealth.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void OnDestroy()
    {
        // 记得取消订阅，防止内存泄漏
        if (myHealth != null)
        {
            myHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    // ==========================================
    // 🧠 核心 1：血量监听与狂暴触发
    // ==========================================
    private void HandleHealthChanged(float current, float max)
    {
        currentHpPercent = current / max;

        // 触发狂暴大跳：跌破 30%，且还没狂暴过，且还没死
        if (currentHpPercent <= frenzyHpThreshold && !isFrenzied && !isDead)
        {
            TriggerFrenzy();
        }
    }

    private void TriggerFrenzy()
    {
        isFrenzied = true;
        Debug.Log("<color=red>野兽精英怪：血量跌破阈值，进入狂暴阶段！</color>");

        // 1. 打断当前一切常规动作（调用 BaseEnemy 基类方法）
        OnHitInterrupt();

        // 2. 劫持大脑，锁定状态不准它乱跑
        isAIHijacked = true;

        // 3. 触发狂暴动画（状态机会自动执行: 大跳 -> 战吼）
        if (anim != null) anim.SetTrigger("Frenzy");
    }

    // ==========================================
    // ⚔️ 核心 2：攻击决策与执行
    // ==========================================
    protected override void PerformAttack()
    {
        float roll = Random.value;

        // 优先判断投技 (防复读)
        if (roll <= grabChance && !lastAttackWasGrab)
        {
            lastAttackWasGrab = true;
            ExecuteAttack("QianZhua", grabConfig);
        }
        else if (Random.value <= heavyAttackChance)
        {
            lastAttackWasGrab = false;
            ExecuteAttack("Attack3", heavySmashConfig);
        }
        else
        {
            // 默认打左挥击
            lastAttackWasGrab = false;
            ExecuteAttack("Attack", leftSwipeConfig);
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
    // 🛡️ 核心 3：战术走位与格挡举手
    // ==========================================

    // 重写动作结束方法：在这里决定退后时要不要举手
    public override void OnAttackAnimEnd()
    {
        if (isDead) return;

        isDefendingNow = false;
        // 如果血量 > 70%，且通过概率判定，则在接下来的 Retreat 中举手
        if (currentHpPercent >= defenseHpThreshold && Random.value <= defenseChance)
        {
            isDefendingNow = true;
        }

        base.OnAttackAnimEnd(); // 这会把状态切到 Retreat
    }

    // 怪物后退中...
    protected override void UpdateRetreatState()
    {
        // 持续控制 Animator 里的 IsDefending
        if (anim != null) anim.SetBool("IsDefending", isDefendingNow);
        base.UpdateRetreatState();
    }

    // 怪物切到了左右平移...
    protected override void UpdateStrafeState()
    {
        // 只要不是后退，就必须把盾放下
        if (anim != null) anim.SetBool("IsDefending", false);
        base.UpdateStrafeState();
    }

    // 怪物又扑上来了...
    protected override void UpdateChaseState()
    {
        if (anim != null) anim.SetBool("IsDefending", false);
        base.UpdateChaseState();
    }

    // 挨打被打断时，也要把盾放下
    public override void OnHitInterrupt()
    {
        base.OnHitInterrupt();
        if (anim != null) anim.SetBool("IsDefending", false);
        isDefendingNow = false;
    }

    // ==========================================
    // 🎭 动画事件接收区 (Animation Events)
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

    // ✨ 半血连招判定点（在 "Attack_Left" 动画的中后段打上此事件）
    public void AnimEvent_ComboCheck()
    {
        if (isDead || currentAction != leftSwipeConfig) return;

        // 如果半血以下，且概率判定通过，直接派生右挥击！
        if (currentHpPercent < comboHpThreshold && Random.value <= comboChance)
        {
            Debug.Log("<color=yellow>野兽触发连招：左挥击派生右挥击！</color>");
            ExecuteAttack("Attack2", rightSwipeConfig);
        }
        // 如果不连招，动画会自动继续播到最后，触发原有的 AnimEvent_AttackEnd
    }

    // 🩸 投技判定点 (在 "Attack_Grab" 双手合拢那帧打上此事件)
    public void TryGrab()
    {
        if (player == null || isDead || currentAction != grabConfig) return;

        List<Collider> hits = currentAction.GetHitTargets(transform);
        bool caughtPlayer = hits.Exists(c => c.CompareTag("Player"));

        if (caughtPlayer)
        {
            Debug.Log("<color=magenta>野兽投技命中！动作定格，疯狂飙血！</color>");

            // 1. 通知玩家被抓
            PlayerReaction reaction = player.GetComponent<PlayerReaction>();
            if (reaction != null) reaction.ApplyGrab(grabHoldDuration);

            // 2. 播放特效
            if (grabVFX != null)
            {
                Instantiate(grabVFX, player.position + Vector3.up, Quaternion.identity);
            }

            // 3. 启动定格协程
            StartCoroutine(GrabHoldRoutine());
        }
    }

    private IEnumerator GrabHoldRoutine()
    {
        // 霸道逻辑：直接把怪物自己的动画播放速度设为 0
        if (anim != null) anim.speed = 0f;

        // 等待设定的时长 (注意：anim.speed=0时不影响 WaitForSeconds)
        yield return new WaitForSeconds(grabHoldDuration);

        // 恢复动画播放
        if (anim != null) anim.speed = 1f;

        // 结束这波攻击
        OnAttackAnimEnd();
    }

    // ✨ 狂暴战吼结束点（挂在 Roar 动画的最后一帧）
    public void AnimEvent_FrenzyEnd()
    {
        if (isDead) return;

        Debug.Log("<color=red>战吼结束！疯狗出笼！</color>");

        // 1. 解锁大脑
        isAIHijacked = false;

        // 2. 疯狂数值强化！(让你绝望的疯狗模式)
        minStrafeTime = 0.2f;  // 原本可能是 1 秒
        maxStrafeTime = 0.6f;  // 原本可能是 3 秒 (现在它几乎不平移，无限追着咬)
        moveSpeed += 2.0f;     // 移速加快

        // 可选：你甚至可以顺便给它的攻击加上额外伤害倍率
        // heavySmashConfig.damageMultiplier *= 1.5f; 

        // 3. 马上转入追击状态
        ChangeState(AIState.Chase);
    }

    // 兜底的动画结束接收器（需要在所有攻击动画的结尾打上此事件）
    public void AnimEvent_AttackEnd()
    {
        // 调用基类的结束方法，进入战术走位或继续追击
        OnAttackAnimEnd();
    }
}