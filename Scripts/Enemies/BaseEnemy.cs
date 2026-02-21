using UnityEngine;
using UnityEngine.AI;

// 【架构规范】抽象基类：作为所有怪物的模板，承载寻路、状态切换等通用逻辑
public abstract class BaseEnemy : MonoBehaviour
{
    // ==========================================
    // 状态枚举
    // ==========================================
    public enum AIState
    {
        Idle,       // 站着发呆 / 巡逻
        Chase,      // 追逐玩家
        Attack,     // 正在攻击
        Dead        // 死亡
    }

    [Header("=== 基础 AI 属性 (儿子们继承后都能改) ===")]
    [Tooltip("发现玩家的距离")]
    public float detectionRange = 10f;
    [Tooltip("放弃追击的距离 (通常比发现距离大一点)")]
    public float loseAggroRange = 15f;
    [Tooltip("攻击距离 (决定何时进入攻击状态)")]
    public float attackRange = 1.5f;
    [Tooltip("保持距离：离玩家多远就停止前进（建议设为1.2左右，解决紧贴问题）")]
    public float stopDistance = 1.2f;
    [Tooltip("移动速度")]
    public float moveSpeed = 3.5f;
    [Tooltip("攻击冷却时间")]
    public float attackCooldown = 2.0f;

    [Header("=== 状态监控 (仅供查看) ===")]
    public AIState currentState = AIState.Idle;

    // --- 内部核心组件引用 ---
    protected Transform player;
    protected NavMeshAgent agent;
    protected Animator anim;

    // --- 内部计时器与控制锁 ---
    protected float lastAttackTime = -100f;
    protected bool isAttacking = false;
    protected bool isDead = false;

    // ==========================================
    // 1. 初始化阶段
    // ==========================================
    protected virtual void Start()
    {
        // 自动获取组件 (带 Children 保证兼容性)
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        // 自动寻找主角
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        // 应用属性设置
        if (agent != null)
        {
            agent.speed = moveSpeed;
            // 我们手动用代码控制停止逻辑，所以把组件自带的设为0，防止冲突
            agent.stoppingDistance = 0f;
        }
    }

    // ==========================================
    // 2. 状态机中枢 (每帧驱动)
    // ==========================================
    protected virtual void Update()
    {
        if (isDead || player == null) return;

        switch (currentState)
        {
            case AIState.Idle:
                UpdateIdleState();
                break;
            case AIState.Chase:
                UpdateChaseState();
                break;
            case AIState.Attack:
                UpdateAttackState();
                break;
        }

        // 统一处理动画速度同步
        if (anim != null && !isDead)
        {
            anim.SetFloat("Speed", agent.velocity.magnitude);
        }
    }

    // ==========================================
    // 3. 各状态具体逻辑
    // ==========================================
    protected virtual void UpdateIdleState()
    {
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= detectionRange)
        {
            ChangeState(AIState.Chase);
        }
    }

    protected virtual void UpdateChaseState()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        // 情况 A：进入攻击范围，开打
        if (distance <= attackRange)
        {
            ChangeState(AIState.Attack);
            return;
        }

        // 情况 B：追丢了，回老家
        if (distance > loseAggroRange)
        {
            agent.isStopped = true;
            ChangeState(AIState.Idle);
            return;
        }

        // 情况 C：🔥 保持距离逻辑 (解决紧贴问题的核心)
        // 如果虽然还没到攻击时机，但已经离玩家很近了(小于stopDistance)，就刹车停住
        if (distance <= stopDistance)
        {
            agent.isStopped = true;
        }
        else
        {
            // 否则，继续追
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
    }

    protected virtual void UpdateAttackState()
    {
        agent.isStopped = true; // 攻击时必须刹车

        // 锁定目标：面朝玩家
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);
        }

        float distance = Vector3.Distance(transform.position, player.position);

        // 如果挥完手了，且玩家走远了，切回追击
        if (!isAttacking && distance > attackRange)
        {
            ChangeState(AIState.Chase);
            return;
        }

        // 冷却判定与攻击触发
        if (!isAttacking && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            isAttacking = true;
            PerformAttack();
        }
    }

    // ==========================================
    // 4. 派生接口
    // ==========================================
    protected virtual void PerformAttack()
    {
        Debug.LogWarning("BaseEnemy 的 PerformAttack 未被子类重写！");
    }

    // ==========================================
    // 5. 辅助与联动
    // ==========================================
    protected void ChangeState(AIState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
    }

    public virtual void OnAttackAnimEnd()
    {
        isAttacking = false;
    }

    public virtual void TriggerDeath()
    {
        isDead = true;
        currentState = AIState.Dead;
        if (agent != null) agent.isStopped = true;
    }

    // ==========================================
    // 6. 策划可视化
    // ==========================================
    private void OnDrawGizmosSelected()
    {
        // 黄色：警戒
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 蓝色：停止追击距离 (保持距离)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        // 红色：攻击
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}