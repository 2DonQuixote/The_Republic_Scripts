using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// 【架构规范】抽象基类：作为所有怪物的模板，承载寻路、状态切换等通用逻辑
public abstract class BaseEnemy : MonoBehaviour
{
    // ==========================================
    // 状态枚举
    // ==========================================
    public enum AIState
    {
        Idle,       // 发呆 / 巡逻
        Chase,      // 追逐玩家
        Attack,     // 正在攻击
        Dead        // 死亡
    }

    [Header("=== 基础 AI 属性 ===")]
    public float detectionRange = 10f;
    public float loseAggroRange = 15f;
    public float attackRange = 1.5f;
    public float stopDistance = 1.2f;
    [Tooltip("追击玩家时的奔跑速度")]
    public float moveSpeed = 4.5f;
    public float attackCooldown = 2.0f;

    [Header("=== 🚶 巡逻 (Patrol) 配置 ===")]
    public bool enablePatrol = true;
    public float patrolSpeed = 1.5f;
    public float patrolRadius = 6.0f;
    public float patrolWaitTime = 2.5f;

    [Header("=== 受击反馈设置 ===")]
    public float knockbackDistance = 0.3f;
    public float knockbackDuration = 0.3f;

    [Header("=== 💀 死亡表现 (Death Knockback) ===")]
    public float deathKnockbackDistance = 0.0f;
    public float deathKnockbackDuration = 0.0f;

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

    // 🔥 允许外部插件接管 AI 的锁
    public bool isAIHijacked = false;

    protected bool isRotationLocked = false;

    // --- 巡逻内部状态 ---
    protected Vector3 startPosition;
    protected float patrolTimer = 0f;
    protected bool isWaiting = false;

    // ==========================================
    // 1. 初始化阶段
    // ==========================================
    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        startPosition = transform.position;

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

        // 🔥 如果没有被外挂劫持，才执行基础逻辑
        if (!isAIHijacked)
        {
            switch (currentState)
            {
                case AIState.Idle: UpdateIdleState(); break;
                case AIState.Chase: UpdateChaseState(); break;
                case AIState.Attack: UpdateAttackState(); break;
            }
        }

        if (anim != null && !isDead)
        {
            anim.SetFloat("Speed", agent.velocity.magnitude, 0.1f, Time.deltaTime);

            // 🔥 被外挂劫持了，也算是处于战斗状态
            bool inCombat = (currentState == AIState.Chase || currentState == AIState.Attack || isAIHijacked);
            anim.SetBool("InCombat", inCombat);
        }
    }

    // ==========================================
    // 3. 待机与巡逻逻辑
    // ==========================================
    protected virtual void UpdateIdleState()
    {
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= detectionRange)
        {
            agent.speed = moveSpeed;
            ChangeState(AIState.Chase);
            return;
        }

        if (enablePatrol && agent != null && agent.isActiveAndEnabled)
        {
            agent.speed = patrolSpeed;

            if (isWaiting)
            {
                patrolTimer += Time.deltaTime;
                if (patrolTimer >= patrolWaitTime)
                {
                    isWaiting = false;
                    SetNewPatrolDestination();
                }
            }
            else
            {
                if (!agent.pathPending && agent.remainingDistance <= 0.2f)
                {
                    isWaiting = true;
                    patrolTimer = 0f;
                }
            }
        }
    }

    private void SetNewPatrolDestination()
    {
        Vector2 randomDir = Random.insideUnitCircle * patrolRadius;
        Vector3 randomPos = startPosition + new Vector3(randomDir.x, 0, randomDir.y);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPos, out hit, patrolRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            agent.isStopped = false;
        }
        else
        {
            isWaiting = true;
            patrolTimer = 0f;
        }
    }

    // ==========================================
    // 4. 追逐逻辑
    // ==========================================
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
            isWaiting = true;
            patrolTimer = 0f;
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

    // ==========================================
    // 5. 攻击状态监控
    // ==========================================
    protected virtual void UpdateAttackState()
    {
        agent.isStopped = true;

        if (!isRotationLocked)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);
            }
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (!isAttacking && distance > attackRange)
        {
            agent.speed = moveSpeed;
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

    protected virtual void PerformAttack()
    {
        Debug.LogWarning("BaseEnemy 的 PerformAttack 未被子类重写！");
    }

    protected void ChangeState(AIState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
    }

    public virtual void OnAttackAnimEnd()
    {
        isAttacking = false;
        isRotationLocked = false;
    }

    // ==========================================
    // 6. 死亡处理
    // ==========================================
    public virtual void TriggerDeath()
    {
        isDead = true;
        currentState = AIState.Dead;

        StopAllCoroutines();

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;

            if (deathKnockbackDistance > 0)
            {
                StartCoroutine(DeathKnockbackCoroutine());
            }
            else
            {
                agent.enabled = false;
            }
        }
    }

    protected IEnumerator DeathKnockbackCoroutine()
    {
        float timer = 0f;
        float speed = deathKnockbackDistance / deathKnockbackDuration;

        Vector3 pushDir = -transform.forward;
        if (player != null)
        {
            pushDir = (transform.position - player.position).normalized;
            pushDir.y = 0;
        }

        while (timer < deathKnockbackDuration)
        {
            if (agent == null || !agent.isActiveAndEnabled) break;

            agent.Move(pushDir * speed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }

        if (agent != null)
        {
            agent.enabled = false;
        }
    }

    // ==========================================
    // 7. 受击打断
    // ==========================================
    public virtual void OnHitInterrupt()
    {
        isAttacking = false;
        isRotationLocked = false;

        // 🔥 核心修复：挨打中断时，强行解除外部插件的劫持！
        isAIHijacked = false;

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
        }

        StopAllCoroutines();

        if (anim != null)
        {
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("Attack2");
            anim.ResetTrigger("QianZhua");
            anim.ResetTrigger("GrabSuccess");
            anim.ResetTrigger("FeiPu");
            anim.ResetTrigger("Throw"); // 顺便把丢毒也打断
        }

        agent.speed = moveSpeed;
        ChangeState(AIState.Chase);

        if (gameObject.activeInHierarchy && !isDead)
        {
            StartCoroutine(KnockbackCoroutine());
        }
    }

    protected IEnumerator KnockbackCoroutine()
    {
        if (agent == null || !agent.isActiveAndEnabled) yield break;

        float timer = 0f;
        float speed = knockbackDistance / knockbackDuration;
        Vector3 pushDir = -transform.forward;

        while (timer < knockbackDuration)
        {
            if (isDead || agent == null || !agent.isActiveAndEnabled) break;

            agent.Move(pushDir * speed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (Application.isPlaying)
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(startPosition, patrolRadius);
        }
        else
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(transform.position, patrolRadius);
        }
    }
}