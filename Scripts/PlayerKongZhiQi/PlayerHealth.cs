using UnityEngine;
using System.Collections;
using System; // 1. 引入 System 以使用 Action

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("属性设置")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    private bool isDead = false;

    // 🔥 1. 删除旧的 UI 引用
    // [SerializeField] private GhostBarAnimator healthBarAnimator; 

    // 🔥 2. 新增：事件广播 (像 EnemyHealth 那样)
    public event Action<float, float> OnHealthChanged;

    [Header("依赖引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Animator animator;
    private PlayerReaction playerReaction;

    private void Start()
    {
        currentHealth = maxHealth;

        playerReaction = GetComponent<PlayerReaction>();
        if (animator == null) animator = GetComponent<Animator>();
        if (playerController == null) playerController = GetComponent<PlayerController>();

        // 🔥 3. 初始广播：告诉 UI 我现在的血量
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount, bool triggerHitReaction = true)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        // 🔥 4. 受伤广播：喊一声 "我血变了"
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Debug.Log($"受到 {amount} 点伤害，剩余血量: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (triggerHitReaction && playerReaction != null)
            {
                playerReaction.ApplyHit();
            }
        }
    }

    // 复活/坐火逻辑
    public void HealToFull()
    {
        currentHealth = maxHealth;

        // 🔥 5. 回血广播
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        var statusMgr = GetComponent<StatusManager>();
        if (statusMgr != null)
        {
            statusMgr.ClearDebuffsOnRest();
        }
    }

    // ... Die() 和 RespawnRoutine() 保持不变 ...
    private void Die()
    {
        if (isDead) return;
        isDead = true;
        if (playerController != null) playerController.enabled = false;
        if (animator != null) animator.SetTrigger("Die");
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(3.0f);
        HealToFull();
        if (GameFlowManager.Instance != null) transform.position = GameFlowManager.Instance.currentRespawnPoint;
        if (animator != null) { animator.Rebind(); animator.Play("Movement"); }
        if (playerController != null) playerController.enabled = true;
        isDead = false;
    }
}