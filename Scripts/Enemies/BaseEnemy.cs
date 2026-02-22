using UnityEngine;
using UnityEngine.AI;
using System.Collections; // 🔥 必须加上这个才能用协程

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

    [Header("=== 基础 AI 属性 ===")]
    public float detectionRange = 10f;
    public float loseAggroRange = 15f;
    public float attackRange = 1.5f;
    public float stopDistance = 1.2f;
    public float moveSpeed = 3.5f;
    public float attackCooldown = 2.0f;

    // 🔥🔥🔥 新增：受击与击退配置 🔥🔥🔥
    [Header("=== 受击反馈设置 ===")]
    [Tooltip("每次挨打被击退的距离 (米)")]
    public float knockbackDistance = 1.5f;
    [Tooltip("击退滑行的耗时 (秒)")]
    public float knockbackDuration = 0.15f;

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
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        if (agent != null)
        {
            agent.speed = moveSpeed;
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
        if (distance <= detectionRange) ChangeState(AIState.Chase);
    }

    protected virtual void UpdateChaseState()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= attackRange)
        {
            ChangeState(AIState.Attack);
            return;
        }

        if (distance > loseAggroRange)
        {
            agent.isStopped = true;
            ChangeState(AIState.Idle);
            return;
        }

        if (distance <= stopDistance)
        {
            agent.isStopped = true;
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
    }

    protected virtual void UpdateAttackState()
    {
        agent.isStopped = true;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (!isAttacking && distance > attackRange)
        {
            ChangeState(AIState.Chase);
            return;
        }

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
    // 6. 🔥 核心：受击打断与击退处理
    // ==========================================
    public virtual void OnHitInterrupt()
    {
        isAttacking = false;

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
        }

        // 🔥 强行掐死正在进行的突进/撕咬/旧的击退协程，防止“一边挨打一边滑步咬人”或连续被连击导致速度叠加
        StopAllCoroutines();

        // 重置多余的指令，防止怪物清醒后瞎扑腾
        if (anim != null)
        {
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("Attack2");
            anim.ResetTrigger("QianZhua");
            anim.ResetTrigger("GrabSuccess");
        }

        ChangeState(AIState.Chase);

        // 🔥 启动平滑击退协程
        if (gameObject.activeInHierarchy && !isDead)
        {
            StartCoroutine(KnockbackCoroutine());
        }
    }

    // 🌟 平滑击退引擎
    protected IEnumerator KnockbackCoroutine()
    {
        if (agent == null || !agent.isActiveAndEnabled) yield break;

        float timer = 0f;
        float speed = knockbackDistance / knockbackDuration;

        // 往怪物的正后方推 (因为怪物打人时通常是面朝玩家的，所以向后退最合理)
        Vector3 pushDir = -transform.forward;

        while (timer < knockbackDuration)
        {
            if (isDead || agent == null || !agent.isActiveAndEnabled) break;

            // 使用 NavMeshAgent.Move 保证不会卡进墙里或掉出地图
            agent.Move(pushDir * speed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    // ==========================================
    // 7. 策划可视化
    // ==========================================
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}