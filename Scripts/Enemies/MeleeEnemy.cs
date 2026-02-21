using UnityEngine;
using System.Collections;

// 继承自 BaseEnemy，保留基础寻路与轻/重击决策
public class MeleeEnemy : BaseEnemy
{
    [Header("=== 核心判定设置 ===")]
    public float hitTolerance = 0.5f;

    [Header("=== 轻击配置 (Light) ===")]
    public float lightDamage = 20f;
    public float lightLungeDistance = 1.5f;
    public float lightLungeDelay = 0.1f;
    public float lightLungeDuration = 0.15f;

    [Header("=== 重击配置 (Heavy) ===")]
    [Range(0f, 1f)] public float heavyAttackChance = 0.3f;
    public float heavyDamage = 40f;
    public float heavyLungeDistance = 3.5f;
    public float heavyLungeDelay = 0.5f;
    public float heavyLungeDuration = 0.25f;

    // 内部逻辑标记
    private bool isCurrentAttackHeavy = false;

    // ==========================================
    // 1. 攻击决策逻辑
    // ==========================================
    protected override void PerformAttack()
    {
        // 简单的二选一决策
        if (Random.value <= heavyAttackChance)
        {
            ExecuteHeavyAttack();
        }
        else
        {
            ExecuteLightAttack();
        }
    }

    // ==========================================
    // 2. 具体执行器
    // ==========================================
    private void ExecuteHeavyAttack()
    {
        isCurrentAttackHeavy = true;
        if (anim != null) anim.SetTrigger("Attack2");
        StartCoroutine(LungeForwardCoroutine(heavyLungeDistance, heavyLungeDelay, heavyLungeDuration));
    }

    private void ExecuteLightAttack()
    {
        isCurrentAttackHeavy = false;
        if (anim != null) anim.SetTrigger("Attack");
        StartCoroutine(LungeForwardCoroutine(lightLungeDistance, lightLungeDelay, lightLungeDuration));
    }

    // ==========================================
    // 3. 通用突进引擎
    // ==========================================
    IEnumerator LungeForwardCoroutine(float distance, float delay, float duration)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        if (isDead) yield break; //

        float speed = distance / duration;
        float timer = 0f;

        while (timer < duration)
        {
            if (isDead || agent == null || !agent.isActiveAndEnabled) break; //

            // 使用 NavMeshAgent.Move 执行爆发位移
            agent.Move(transform.forward * speed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    // ==========================================
    // 4. 伤害判定 (由动画事件调用)
    // ==========================================
    public void DealDamage()
    {
        if (player == null || isDead) return;

        float distance = Vector3.Distance(transform.position, player.position);

        // 判定玩家是否在攻击范围内
        if (distance <= attackRange + hitTolerance) //
        {
            IDamageable target = player.GetComponent<IDamageable>();
            if (target != null)
            {
                float finalDamage = isCurrentAttackHeavy ? heavyDamage : lightDamage;
                target.TakeDamage(finalDamage);
            }
        }
    }
}