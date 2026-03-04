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
        Dead,       // 死亡
        Retreat,    // 🔥 战术动作：打完后撤
        Strafe      // 🔥 战术动作：左右试探游走
    }

    [Header("=== 基础 AI 属性 ===")]
    public float detectionRange = 10f;
    public float loseAggroRange = 15f;
    public float attackRange = 1.5f;
    public float stopDistance = 1.2f;
    [Tooltip("追击玩家时的奔跑速度")]
    public float moveSpeed = 4.5f;

    [Header("=== 🚶 巡逻 (Patrol) 配置 ===")]
    public bool enablePatrol = true;
    public float patrolSpeed = 1.5f;
    public float patrolRadius = 6.0f;
    public float patrolWaitTime = 2.5f;

    // 🔥🔥🔥 新增：Root Motion 配置 🔥🔥🔥
    [Header("=== 🏃 动画驱动 (Root Motion) ===")]
    [Tooltip("是否使用动画的真实步伐来控制移动？(极大提升真实感，告别滑步)")]
    public bool useRootMotion = true;

    [Header("=== 🧠 战术走位 (替代攻击冷却) ===")]
    [Tooltip("是否在近战攻击结束后拉开距离并游走观察？(如果不勾选，怪物将变成没有冷却的疯狗)")]
    public bool enableTacticalMovement = true;
    [Tooltip("后撤时的速度 (建议调快，像后跳)")]
    public float retreatSpeed = 6.0f;
    [Tooltip("后撤的安全距离")]
    public float retreatDistance = 3.5f;
    [Tooltip("左右游走时的速度 (建议调慢，充满压迫感)")]
    public float strafeSpeed = 1.5f;
    [Tooltip("🔥 游走观察的最大时间 (这个值现在就是怪物的攻击冷却时间！)")]
    public float maxStrafeTime = 2.0f;

    [Header("=== 受击与死亡配置 ===")]
    public float knockbackDistance = 0.3f;
    public float knockbackDuration = 0.3f;
    public float deathKnockbackDistance = 0.0f;
    public float deathKnockbackDuration = 0.0f;

    [Header("=== 状态监控 (仅供查看) ===")]
    public AIState currentState = AIState.Idle;

    // --- 内部组件 ---
    protected Transform player;
    protected NavMeshAgent agent;
    protected Animator anim;

    // --- 内部标记 ---
    protected bool isAttacking = false;
    protected bool isDead = false;
    public bool isAIHijacked = false;
    protected bool isRotationLocked = false;
    protected bool isKnockedBack = false; // 🔥 新增：标记是否正在被击飞

    // 用来控制“这次攻击结束后，是否跳过战术走位”(供远程子类调用)
    protected bool skipTacticalThisTime = false;

    // --- 巡逻与战术变量 ---
    protected Vector3 startPosition;
    protected float patrolTimer = 0f;
    protected bool isWaiting = false;

    protected float strafeTimer = 0f;
    protected int strafeDirection = 1; // 1 右， -1 左
    protected Vector3 tacticalTargetPos;

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

            // 🔥 Root Motion 初始化：如果是动画驱动，就剥夺大脑的平移权限
            if (useRootMotion)
            {
                agent.updatePosition = false;
            }
        }
    }

    protected virtual void Update()
    {
        if (isDead || player == null) return;

        if (!isAIHijacked)
        {
            switch (currentState)
            {
                case AIState.Idle: UpdateIdleState(); break;
                case AIState.Chase: UpdateChaseState(); break;
                case AIState.Attack: UpdateAttackState(); break;
                case AIState.Retreat: UpdateRetreatState(); break;
                case AIState.Strafe: UpdateStrafeState(); break;
            }
        }

        if (anim != null && !isDead)
        {
            anim.SetFloat("Speed", agent.velocity.magnitude, 0.1f, Time.deltaTime);

            if (agent.velocity.magnitude > 0.1f)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);
                anim.SetFloat("MoveX", localVelocity.x / agent.speed, 0.1f, Time.deltaTime);
                anim.SetFloat("MoveZ", localVelocity.z / agent.speed, 0.1f, Time.deltaTime);
            }
            else
            {
                anim.SetFloat("MoveX", 0, 0.1f, Time.deltaTime);
                anim.SetFloat("MoveZ", 0, 0.1f, Time.deltaTime);
            }

            bool inCombat = (currentState == AIState.Chase || currentState == AIState.Attack ||
                             currentState == AIState.Retreat || currentState == AIState.Strafe || isAIHijacked);
            anim.SetBool("InCombat", inCombat);
        }
    }

    // 🔥🔥🔥 核心：Root Motion 动画物理位移引擎 🔥🔥🔥
    protected virtual void OnAnimatorMove()
    {
        // 如果死了、没开选项、或者组件不全，不执行
        if (isDead || !useRootMotion || agent == null || anim == null) return;

        // 如果处于外挂接管（站桩施法）或者正在被击飞，暂时将权限还给 NavMeshAgent/代码
        if (isAIHijacked || isKnockedBack)
        {
            agent.updatePosition = true;
            return;
        }

        // 平时保证 Agent 不要自己乱跑
        agent.updatePosition = false;

        // 1. 获取动画师做的真实的步伐位移差值
        Vector3 animDeltaPosition = anim.deltaPosition;
        animDeltaPosition.y = 0; // 锁死高度，防止走着走着飞起来

        // 2. 将动画位移叠加到真实模型上
        transform.position += animDeltaPosition;

        // 3. 将真实位置同步给寻路大脑，让大脑从这里重新算路线
        agent.nextPosition = transform.position;
    }

    protected void FaceTarget(Vector3 targetPos)
    {
        if (isRotationLocked) return;
        Vector3 direction = (targetPos - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);
        }
    }

    public void ForceEnterStrafe()
    {
        EnterStrafeState();
    }

    protected virtual void UpdateRetreatState()
    {
        FaceTarget(player.position);

        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
        {
            EnterStrafeState();
        }
    }

    protected void EnterStrafeState()
    {
        ChangeState(AIState.Strafe);
        strafeTimer = 0f;
        strafeDirection = Random.value > 0.5f ? 1 : -1;

        if (agent != null)
        {
            agent.speed = strafeSpeed;
            agent.isStopped = false;
        }
    }

    protected virtual void UpdateStrafeState()
    {
        FaceTarget(player.position);

        strafeTimer += Time.deltaTime;

        if (strafeTimer >= maxStrafeTime)
        {
            ChangeState(AIState.Chase);
            return;
        }

        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        dirToPlayer.y = 0;
        Vector3 rightDir = Vector3.Cross(Vector3.up, dirToPlayer) * strafeDirection;
        Vector3 strafeTarget = transform.position + rightDir * 2.0f;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(strafeTarget, out hit, 1.5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            strafeDirection *= -1;
        }
    }

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
            agent.speed = moveSpeed;
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
    }

    protected virtual void UpdateAttackState()
    {
        agent.isStopped = true;

        FaceTarget(player.position);

        float distance = Vector3.Distance(transform.position, player.position);

        if (!isAttacking && distance > attackRange)
        {
            agent.speed = moveSpeed;
            ChangeState(AIState.Chase);
            return;
        }

        if (!isAttacking)
        {
            isAttacking = true;
            PerformAttack();
        }
    }

    protected virtual void PerformAttack()
    {
        Debug.LogWarning("BaseEnemy 的 PerformAttack 未被子类重写！");
    }

    public void ChangeState(AIState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
    }

    public virtual void OnAttackAnimEnd()
    {
        if (isDead) return;

        isAttacking = false;
        isRotationLocked = false;

        if (enableTacticalMovement && !skipTacticalThisTime && player != null && agent != null && agent.isActiveAndEnabled)
        {
            ChangeState(AIState.Retreat);

            Vector3 dirAway = (transform.position - player.position).normalized;
            dirAway.y = 0;
            Vector3 target = transform.position + dirAway * retreatDistance;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(target, out hit, retreatDistance, NavMesh.AllAreas))
                tacticalTargetPos = hit.position;
            else
                tacticalTargetPos = transform.position;

            agent.speed = retreatSpeed;
            agent.isStopped = false;
            agent.SetDestination(tacticalTargetPos);
        }
        else
        {
            ChangeState(AIState.Chase);
        }

        skipTacticalThisTime = false;
    }

    public virtual void TriggerDeath()
    {
        isDead = true;
        currentState = AIState.Dead;

        // 🔥 死亡瞬间，恢复 Agent 控制权以便正常播放击飞滑行
        if (useRootMotion && agent != null) agent.updatePosition = true;

        StopAllCoroutines();

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            if (deathKnockbackDistance > 0)
                StartCoroutine(DeathKnockbackCoroutine());
            else
                agent.enabled = false;
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
        if (agent != null) agent.enabled = false;
    }

    public virtual void OnHitInterrupt()
    {
        isAttacking = false;
        isRotationLocked = false;
        skipTacticalThisTime = false;

        if (agent != null && agent.isActiveAndEnabled) agent.isStopped = true;
        StopAllCoroutines();

        if (anim != null)
        {
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("Attack2");
            anim.ResetTrigger("QianZhua");
            anim.ResetTrigger("GrabSuccess");
            anim.ResetTrigger("FeiPu");
        }

        agent.speed = moveSpeed;
        ChangeState(AIState.Chase);

        if (gameObject.activeInHierarchy && !isDead)
            StartCoroutine(KnockbackCoroutine());
    }

    protected IEnumerator KnockbackCoroutine()
    {
        if (agent == null || !agent.isActiveAndEnabled) yield break;

        isKnockedBack = true; // 🔥 标记开始击飞，防止 Root Motion 捣乱

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

        isKnockedBack = false; // 🔥 击飞结束，恢复 Root Motion
    }

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