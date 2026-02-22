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

    // 🔥🔥🔥 新增：巡逻专属配置 🔥🔥🔥
    [Header("=== 🚶 巡逻 (Patrol) 配置 ===")]
    [Tooltip("勾选则会在 Idle 状态下四处溜达，不勾则原地站岗")]
    public bool enablePatrol = true;
    [Tooltip("巡逻时的慢走速度 (需要与你的 Walk 动画匹配)")]
    public float patrolSpeed = 1.5f;
    [Tooltip("围绕出生点巡逻的最大活动半径")]
    public float patrolRadius = 6.0f;
    [Tooltip("走到目标点后，停下来发呆思考人生的时间")]
    public float patrolWaitTime = 2.5f;

    [Header("=== 受击反馈设置 ===")]
    public float knockbackDistance = 1.5f;
    public float knockbackDuration = 0.15f;

    // 🔥🔥🔥 新增：死亡击飞配置 🔥🔥🔥
    [Header("=== 💀 死亡表现 (Death Knockback) ===")]
    [Tooltip("死亡瞬间被击飞的距离。设为0则原地软倒")]
    public float deathKnockbackDistance = 3.0f;
    [Tooltip("在地上滑行退后的时间 (建议匹配死亡动画的前半段落地时间)")]
    public float deathKnockbackDuration = 0.4f;

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

    // 🔥 新增：转向锁。出招后锁死，禁止原地转盘！
    protected bool isRotationLocked = false;

    // 🔥 巡逻内部状态
    protected Vector3 startPosition; // 领地中心（出生点）
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

        // 🔥 记住出生地，作为巡逻领地中心
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
            // 1. 保留我们之前加的平滑阻尼
            anim.SetFloat("Speed", agent.velocity.magnitude, 0.1f, Time.deltaTime);

            // 🔥 2. 核心新增：告诉 Animator 现在是不是战斗状态？
            // 只要不是在 Idle（发呆/巡逻），就是在战斗中！
            bool inCombat = (currentState == AIState.Chase || currentState == AIState.Attack);
            anim.SetBool("InCombat", inCombat);
        }
    }

    // ==========================================
    // 3. 🌟 重写：待机与巡逻逻辑
    // ==========================================
    protected virtual void UpdateIdleState()
    {
        // 1. 索敌逻辑：优先看玩家在不在视野内
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= detectionRange)
        {
            // 发现玩家，切入战斗！
            agent.speed = moveSpeed; // 切换为狂奔速度
            ChangeState(AIState.Chase);
            return;
        }

        // 2. 巡逻溜达逻辑
        if (enablePatrol && agent != null && agent.isActiveAndEnabled)
        {
            agent.speed = patrolSpeed; // 强制切换为散步速度

            if (isWaiting)
            {
                // 发呆中，累计时间
                patrolTimer += Time.deltaTime;
                if (patrolTimer >= patrolWaitTime)
                {
                    isWaiting = false;
                    SetNewPatrolDestination(); // 时间到，找下一个目标点
                }
            }
            else
            {
                // 正在散步中，检查是否走到目的地了
                // pathPending 表示还在算路，remainingDistance 是剩余距离
                if (!agent.pathPending && agent.remainingDistance <= 0.2f)
                {
                    // 走到了！开始发呆
                    isWaiting = true;
                    patrolTimer = 0f;
                }
            }
        }
    }

    // 🔥 生成安全的随机巡逻点
    private void SetNewPatrolDestination()
    {
        // 在出生点周围生成一个随机的二维圆内坐标
        Vector2 randomDir = Random.insideUnitCircle * patrolRadius;
        Vector3 randomPos = startPosition + new Vector3(randomDir.x, 0, randomDir.y);

        // ⚠️ 极其重要：验证这个随机点是不是在合法的寻路网格上
        NavMeshHit hit;
        // 采样半径设为 patrolRadius，如果在范围内找到合法的地板，就赋值给 hit
        if (NavMesh.SamplePosition(randomPos, out hit, patrolRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            agent.isStopped = false;
        }
        else
        {
            // 万一真随到了不可达的地方（比如死角），干脆原地再等一会儿
            isWaiting = true;
            patrolTimer = 0f;
        }
    }

    // ==========================================
    // 追逐逻辑
    // ==========================================
    protected virtual void UpdateChaseState()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= attackRange)
        {
            ChangeState(AIState.Attack);
            return;
        }

        // 脱战逻辑
        if (distance > loseAggroRange)
        {
            agent.isStopped = true;
            // 脱战后，重置巡逻状态，让它先发一会儿呆再回去巡逻
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

    protected virtual void UpdateAttackState()
    {
        agent.isStopped = true;

        // 🔥 核心修复：如果方向没被锁死，才允许怪物转身盯着玩家
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
            agent.speed = moveSpeed; // 确保切回追逐时是跑动速度
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
        isRotationLocked = false; // 🔥 打完收招结束了，解开脖子的锁
    }

    // ==========================================
    // 💀 死亡指令接收与击飞处理
    // ==========================================
    public virtual void TriggerDeath()
    {
        isDead = true;
        currentState = AIState.Dead;

        // 🔪 核心保护：强行叫停可能正在突进/飞扑的协程，防止尸体自己往前飞
        StopAllCoroutines();

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;

            // 判断需不需要击飞表现
            if (deathKnockbackDistance > 0)
            {
                StartCoroutine(DeathKnockbackCoroutine());
            }
            else
            {
                // 如果距离填 0，则原地倒下，彻底关闭寻路防止挡路
                agent.enabled = false;
            }
        }
    }

    // 💨 死亡击飞物理滑行协程
    protected IEnumerator DeathKnockbackCoroutine()
    {
        float timer = 0f;
        float speed = deathKnockbackDistance / deathKnockbackDuration;

        // 智能计算被击飞的方向：远离玩家
        Vector3 pushDir = -transform.forward;
        if (player != null)
        {
            pushDir = (transform.position - player.position).normalized;
            pushDir.y = 0; // 贴地滑行
        }

        while (timer < deathKnockbackDuration)
        {
            if (agent == null || !agent.isActiveAndEnabled) break;

            agent.Move(pushDir * speed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }

        // 滑行彻底结束，让出寻路网格，防止地上的尸体卡住其他活着的怪物
        if (agent != null)
        {
            agent.enabled = false;
        }
    }

    // ==========================================
    // 受击打断
    // ==========================================
    public virtual void OnHitInterrupt()
    {
        isAttacking = false;
        isRotationLocked = false; // 🔥 挨打中断了，也要解开锁

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
        }

        agent.speed = moveSpeed; // 挨打醒来后肯定是想跑着追你
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