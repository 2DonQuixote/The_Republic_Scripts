using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// 【架构规范】抽象基类：作为所有怪物的模板，承载寻路、状态切换等通用逻辑
public abstract class BaseEnemy : MonoBehaviour
{
    public enum AIState
    {
        Idle, Chase, Attack, Dead, Retreat, Strafe
    }

    [Header("=== 基础 AI 属性 ===")]
    public float detectionRange = 10f;
    public float loseAggroRange = 15f;
    public float attackRange = 1.5f;
    public float stopDistance = 1.2f;
    public float moveSpeed = 4.5f;

    [Header("=== 🚶 巡逻 (Patrol) 配置 ===")]
    public bool enablePatrol = true;
    public float patrolSpeed = 1.5f;
    public float patrolRadius = 6.0f;
    public float patrolWaitTime = 2.5f;

    [Header("=== 🏃 动画驱动 (Root Motion) ===")]
    public bool useRootMotion = true;
    [Tooltip("动画切换的平滑过渡时间 (越大越迟钝，越小越生硬)")]
    public float animDampTime = 0.25f; // 🔥 平滑油门缓冲时间

    [Header("=== 🧠 战术走位 (随机攻击冷却) ===")]
    public bool enableTacticalMovement = true;
    public float retreatSpeed = 3.0f;
    public float retreatDistance = 3.5f;
    public float strafeSpeed = 2.0f;

    [Tooltip("最短绕圈时间 (最少冷却)")]
    public float minStrafeTime = 1.0f;
    [Tooltip("最长绕圈时间 (最多冷却)")]
    public float maxStrafeTime = 3.0f;

    [Header("=== 受击与死亡配置 ===")]
    public float knockbackDistance = 0.3f;
    public float knockbackDuration = 0.3f;
    public float deathKnockbackDistance = 0.0f;
    public float deathKnockbackDuration = 0.0f;

    public AIState currentState = AIState.Idle;

    protected Transform player;
    protected NavMeshAgent agent;
    protected Animator anim;

    // --- 内部标记 ---
    protected bool isAttacking = false;
    protected bool isDead = false;
    public bool isAIHijacked = false;
    protected bool isRotationLocked = false;
    protected bool isKnockedBack = false;

    // 代码强制位移锁（绿灯/红灯）
    public bool isCodeMoving = false;
    protected bool skipTacticalThisTime = false;

    protected Vector3 startPosition;
    protected float patrolTimer = 0f;
    protected bool isWaiting = false;
    protected float strafeTimer = 0f;
    protected float retreatTimer = 0f;
    protected int strafeDirection = 1;
    protected Vector3 tacticalTargetPos;

    // 记录当前这一回合随机抽出的冷却时间
    protected float currentStrafeTargetTime = 2.0f;

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
            if (useRootMotion) agent.updatePosition = false;
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

        // 动画状态机同步
        if (anim != null && !isDead)
        {
            // 🔥 融入了 animDampTime：让 Blend Tree 混合过渡如丝般顺滑
            anim.SetFloat("Speed", agent.velocity.magnitude, animDampTime, Time.deltaTime);
            if (agent.velocity.magnitude > 0.1f)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);
                anim.SetFloat("MoveX", localVelocity.x / agent.speed, animDampTime, Time.deltaTime);
                anim.SetFloat("MoveZ", localVelocity.z / agent.speed, animDampTime, Time.deltaTime);
            }
            else
            {
                anim.SetFloat("MoveX", 0, animDampTime, Time.deltaTime);
                anim.SetFloat("MoveZ", 0, animDampTime, Time.deltaTime);
            }

            bool inCombat = (currentState == AIState.Chase || currentState == AIState.Attack ||
                             currentState == AIState.Retreat || currentState == AIState.Strafe || isAIHijacked);
            anim.SetBool("InCombat", inCombat);
        }
    }

    protected virtual void OnAnimatorMove()
    {
        if (isDead || !useRootMotion || agent == null || anim == null) return;

        // 如果代码接管，则取消动画位移
        if (isAIHijacked || isKnockedBack || isCodeMoving)
        {
            agent.updatePosition = true;
            return;
        }

        agent.updatePosition = false;
        Vector3 animDeltaPosition = anim.deltaPosition;
        animDeltaPosition.y = 0;

        // 🌟🌟🌟 核心正骨手术：强制直线冲刺 🌟🌟🌟
        if (isRotationLocked)
        {
            // 提取出正前方 (transform.forward) 的位移分量，把左右偏移（歪的幅度）抹杀掉
            float forwardMove = Vector3.Dot(animDeltaPosition, transform.forward);
            animDeltaPosition = transform.forward * forwardMove;
        }

        transform.position += animDeltaPosition;
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

    public void ForceEnterStrafe() { EnterStrafeState(); }

    protected virtual void UpdateRetreatState()
    {
        FaceTarget(player.position);
        retreatTimer += Time.deltaTime;

        if ((!agent.pathPending && agent.remainingDistance <= 0.5f) || retreatTimer >= 1.5f)
        {
            EnterStrafeState();
        }
    }

    protected void EnterStrafeState()
    {
        ChangeState(AIState.Strafe);
        strafeTimer = 0f;

        // 每次进入左右平移时，在最小值和最大值之间掷骰子！
        currentStrafeTargetTime = Random.Range(minStrafeTime, maxStrafeTime);

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

        // 判断条件：是否达到了本回合随机出来的思考时间
        if (strafeTimer >= currentStrafeTargetTime)
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
            agent.SetDestination(hit.position);
        else
            strafeDirection *= -1;
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
        if (distance <= stopDistance) agent.isStopped = true;
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

    protected virtual void PerformAttack() { Debug.LogWarning("BaseEnemy 的 PerformAttack 未被重写！"); }

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
            retreatTimer = 0f;

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
        else ChangeState(AIState.Chase);

        skipTacticalThisTime = false;
    }

    public virtual void TriggerDeath()
    {
        isDead = true;
        currentState = AIState.Dead;
        if (useRootMotion && agent != null) agent.updatePosition = true;
        StopAllCoroutines();
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            if (deathKnockbackDistance > 0) StartCoroutine(DeathKnockbackCoroutine());
            else agent.enabled = false;
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

        isCodeMoving = false;

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
        if (gameObject.activeInHierarchy && !isDead) StartCoroutine(KnockbackCoroutine());
    }

    protected IEnumerator KnockbackCoroutine()
    {
        if (agent == null || !agent.isActiveAndEnabled) yield break;
        isKnockedBack = true;
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
        isKnockedBack = false;
    }
}