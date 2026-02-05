using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("属性设置")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("组件引用")]
    // 🔥 新增：拖入挂着 EnemyHealthBar 的那个物体
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

        // 初始化血条（确保刚开始是满血隐藏状态）
        if (healthBar != null) healthBar.UpdateHealth(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;

        // 🔥 新增：通知血条更新
        if (healthBar != null) healthBar.UpdateHealth(currentHealth, maxHealth);

        Debug.Log($"{gameObject.name} 剩余血量: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        if (myCollider != null) myCollider.enabled = false;

        if (animator != null)
        {
            animator.SetTrigger("Die");
        }
        else
        {
            ToggleVisuals(false);
        }

        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(3.0f);

        // 复活逻辑
        currentHealth = maxHealth;
        isDead = false;

        // 🔥 新增：复活满血后，通知血条（这会让血条自动隐藏）
        if (healthBar != null) healthBar.UpdateHealth(currentHealth, maxHealth);

        if (myCollider != null) myCollider.enabled = true;
        ToggleVisuals(true);
        if (animator != null)
        {
            animator.Play("Idle");
            animator.ResetTrigger("Die");
        }
    }

    private void ToggleVisuals(bool isActive)
    {
        if (allRenderers != null)
        {
            foreach (var r in allRenderers) r.enabled = isActive;
        }
    }
}