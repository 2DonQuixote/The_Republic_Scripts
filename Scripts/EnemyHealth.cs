using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("属性设置")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("组件引用")]
    public EnemyHealthBar healthBar;

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

        if (healthBar != null) healthBar.UpdateHealth(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;

        if (healthBar != null) healthBar.UpdateHealth(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // 1. 物理停机：关闭碰撞体，防止尸体阻挡玩家或被继续攻击
        if (myCollider != null) myCollider.enabled = false;

        // 2. 大脑停机：通知 AI 大脑彻底停止逻辑
        GetComponent<BaseEnemy>()?.TriggerDeath();

        // 3. 播放死亡动画
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }
        else
        {
            ToggleVisuals(false);
        }

        // 4. 这里的销毁和复活逻辑全部删掉
        // 尸体将一直留在场景中，直到你以后写好“坐篝火刷新”的逻辑来统一处理
        Debug.Log($"{gameObject.name} 倒下了，等待篝火刷新。");
    }

    private void ToggleVisuals(bool isActive)
    {
        if (allRenderers != null)
        {
            foreach (var r in allRenderers) r.enabled = isActive;
        }
    }
}