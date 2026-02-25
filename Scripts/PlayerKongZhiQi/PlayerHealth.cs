using UnityEngine;
using System.Collections;
using System; // 引用 System 以使用 Action

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("属性设置")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    private bool isDead = false;

    // 🔥 1. 定义事件：告诉 UI 我血量变了
    public event Action<float, float> OnHealthChanged;

    [Header("依赖引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Animator animator;
    private PlayerReaction playerReaction;

    private void Start()
    {
        currentHealth = maxHealth;
        playerReaction = GetComponent<PlayerReaction>();

        // 自动获取组件
        if (animator == null) animator = GetComponent<Animator>();
        if (playerController == null) playerController = GetComponent<PlayerController>();

        // 初始广播，确保 UI 满血
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount, bool triggerHitReaction = true)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        // 🔥 2. 广播受伤事件 (UI 会自己动)
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Debug.Log($"受到 {amount} 点伤害，剩余血量: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // 受击硬直逻辑
            if (triggerHitReaction && playerReaction != null)
            {
                playerReaction.ApplyHit();
            }
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log(">>> 角色死亡，进入复活流程 <<<");

        // A. 禁止移动 (防止尸体滑步)
        if (playerController != null) playerController.enabled = false;

        // B. 播放死亡动画
        if (animator != null) animator.SetTrigger("Die");

        // C. 启动复活倒计时
        StartCoroutine(RespawnRoutine());
    }

    // ==========================================
    // 💀 复活的核心逻辑在这里！
    // ==========================================
    IEnumerator RespawnRoutine()
    {
        // 1. 等待 3 秒 (看着尸体发呆)
        yield return new WaitForSeconds(3.0f);

        // 2. 数值回满 & 广播 UI
        HealToFull();

        // 3. 传送回存档点 (修复：不传送的问题)
        // 确保你的场景里有 GameFlowManager (挂在 Don't Destroy 物体上)
        if (GameFlowManager.Instance != null)
        {
            transform.position = GameFlowManager.Instance.currentRespawnPoint;
            Debug.Log("已传送到重生点: " + GameFlowManager.Instance.currentRespawnPoint);
        }
        else
        {
            Debug.LogError("复活失败：找不到 GameFlowManager！玩家将原地复活。");
        }

        // 4. 恢复动画状态 (修复：尸体爬起来)
        if (animator != null)
        {
            animator.Rebind(); // 重置动画状态机
            animator.Play("Movement"); // 强制切回移动状态
        }

        // 5. 恢复移动控制 (修复：无法移动的问题)
        if (playerController != null) playerController.enabled = true;

        isDead = false;
        Debug.Log(">>> 玩家复活完成 <<<");
    }

    public void HealToFull()
    {
        currentHealth = maxHealth;

        // 🔥 广播回满血事件
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // 清除负面状态
        var statusMgr = GetComponent<StatusManager>();
        if (statusMgr != null)
        {
            statusMgr.ClearDebuffsOnRest();
        }
    }
}