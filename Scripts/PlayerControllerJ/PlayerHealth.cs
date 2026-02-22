using UnityEngine;
using System.Collections;

// 保持继承关系不变
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("属性设置")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    private bool isDead = false; // 防止尸体重复扣血

    [Header("依赖引用")]
    [SerializeField] private GhostBarAnimator healthBarAnimator;

    // 直接明确类型为 PlayerController
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Animator animator;

    // 🔥 新增：专门处理受击表现的组件
    private PlayerReaction playerReaction;

    private void Start()
    {
        // 初始化逻辑
        currentHealth = maxHealth;
        if (healthBarAnimator != null)
            healthBarAnimator.Initialize(currentHealth, maxHealth);

        // 尝试自动获取组件，防止忘记拖拽
        if (animator == null) animator = GetComponent<Animator>();
        if (playerController == null) playerController = GetComponent<PlayerController>();

        // 获取受击反应组件
        playerReaction = GetComponent<PlayerReaction>();
    }

    // 🔥 注意：这里的参数要和接口保持一致
    public void TakeDamage(float amount, bool triggerHitReaction = true)
    {
        // 如果已经死了，就不要再扣血了
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        // UI 更新逻辑
        if (healthBarAnimator != null)
        {
            healthBarAnimator.SetValue(currentHealth, maxHealth);
        }

        Debug.Log($"受到 {amount} 点伤害，剩余血量: {currentHealth}");

        // 触发死亡
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // 🔥 核心修改：只有在允许触发硬直时，才通知 Reaction 脚本！
            // 流血和中毒传的是 false，所以会跳过这里
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

        // 1. 禁止移动 (直接关掉 Controller 是最保险的做法)
        if (playerController != null) playerController.enabled = false;

        // 2. 播放死亡动画 (确保你的 Animator 有 "Die" 这个 Trigger)
        if (animator != null) animator.SetTrigger("Die");

        // 3. 启动复活协程
        StartCoroutine(RespawnRoutine());
    }

    // 复活协程
    IEnumerator RespawnRoutine()
    {
        // 等待 3 秒（给死亡动画和黑屏留时间）
        yield return new WaitForSeconds(3.0f);

        // --- 开始复活 ---

        // 1. 调用回满状态方法
        HealToFull();

        // 2. 传送回存档点
        if (GameFlowManager.Instance != null)
        {
            transform.position = GameFlowManager.Instance.currentRespawnPoint;
        }

        // 3. 恢复动画状态
        if (animator != null) animator.Play("Movement"); // 或者 "Idle"

        // 4. 恢复移动控制
        if (playerController != null) playerController.enabled = true;

        isDead = false;
        Debug.Log(">>> 玩家已在存档点复活 <<<");
    }

    public void HealToFull()
    {
        // 1. 数值回满
        currentHealth = maxHealth;

        // 2. UI 强制刷新
        if (healthBarAnimator != null)
        {
            healthBarAnimator.SetValue(currentHealth, maxHealth);
        }

        // 3. 净化负面状态
        var statusMgr = GetComponent<StatusManager>();
        if (statusMgr != null)
        {
            statusMgr.ClearDebuffsOnRest();
        }

        Debug.Log("状态已完全恢复 (已根据配置清除负面状态)");
    }
}