using UnityEngine;
using System; // 1. 引入 System 以使用 Action

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("属性设置")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    // 🔥 删除：不再直接持有 UI 引用，彻底解耦
    // public EnemyHealthBar healthBar;

    // 🔥 新增：广播血量变化事件 (当前血量, 最大血量)
    public event Action<float, float> OnHealthChanged;

    [Header("组件引用")]
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

        // 🔥 广播：我掉血了！
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (triggerHitReaction && animator != null)
            {
                animator.SetTrigger("Hit");
                GetComponent<BaseEnemy>()?.OnHitInterrupt();
            }
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (myCollider != null) myCollider.enabled = false;
        GetComponent<BaseEnemy>()?.TriggerDeath();

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