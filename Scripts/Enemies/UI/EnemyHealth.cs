using UnityEngine;
using System; // 1. 引入 System 以使用 Action

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("=== 基础属性设置 ===")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("=== 精英怪/韧性配置 ===")]
    [Tooltip("勾选后，该怪物变为精英怪，拥有霸体和架势条")]
    public bool isElite = false;

    [Tooltip("掉多少比例的血才会出大硬直 (例如 0.2 = 掉20%血破一次防)")]
    public float poiseThresholdPercent = 0.2f;

    // 内部变量：记录当前累积了多少削韧伤害
    private float currentPoiseDamage = 0f;

    // 🔥 广播血量变化事件 (当前血量, 最大血量)
    public event Action<float, float> OnHealthChanged;

    [Header("=== 组件引用 ===")]
    private Animator animator;
    private Collider myCollider;
    private Renderer[] allRenderers;
    private bool isDead = false;

    private void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        myCollider = GetComponent<Collider>();
        allRenderers = GetComponentsInChildren<Renderer>();

        // 🔥 初始广播：告诉所有监听者（UI），我现在是满血
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount, bool triggerHitReaction = true)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        // 🔥 广播：我掉血了！
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
        else if (triggerHitReaction)
        {
            // ==========================================
            // 🔥 核心分流：普通怪 和 精英怪 的不同受击逻辑
            // ==========================================
            if (isElite)
            {
                HandleEliteHitReaction(amount);
            }
            else
            {
                // 普通怪：一打一个硬直，直接打断大脑
                if (animator != null) animator.SetTrigger("Hit");
                GetComponent<EnemyBrain>()?.OnHitInterrupt();
            }
        }
    }

    // 🔥 新增：专门处理精英怪受击的方法
    private void HandleEliteHitReaction(float amount)
    {
        // 1. 每次受击，累加削韧伤害
        currentPoiseDamage += amount;

        // 2. 计算具体的破防阈值 (比如 1000血 * 0.2 = 200)
        float poiseThreshold = maxHealth * poiseThresholdPercent;

        if (currentPoiseDamage >= poiseThreshold)
        {
            // 💥【大硬直：破防了！】💥
            currentPoiseDamage = 0f; // 清空累积池，重新计算下一次破防

            Debug.Log("<color=yellow>精英怪破防！触发大硬直！</color>");

            // 播放大受击动画 (这里你可以共用 Hit，也可以在 Animator 里做一个 HitHeavy)
            if (animator != null) animator.SetTrigger("Hit");

            // 极其关键：打出大硬直，强行中断它的 AI 动作！
            GetComponent<EnemyBrain>()?.OnHitInterrupt();
        }
        else
        {
            // ⚡【小硬直：抽搐霸体】⚡
            Debug.Log("精英怪霸体抗下攻击，仅触发小抽搐...");

            // 播放抽搐动画 (需要在 Animator 里新增 Twitch 触发器)
            if (animator != null) animator.SetTrigger("Twitch");

            // 【注意！！】这里我们 绝对不调用 OnHitInterrupt()
            // 因为没调用它，怪物的攻击协程、寻路状态全都在继续运行，这就是霸体的原理！
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (myCollider != null) myCollider.enabled = false;
        GetComponent<EnemyBrain>()?.TriggerDeath();

        if (animator != null)
        {
            animator.SetTrigger("Die");
        }
        else
        {
            ToggleVisuals(false);
        }

        Debug.Log($"{gameObject.name} 倒下了。");
    }

    private void ToggleVisuals(bool isActive)
    {
        if (allRenderers != null)
        {
            foreach (var r in allRenderers) r.enabled = isActive;
        }
    }
}