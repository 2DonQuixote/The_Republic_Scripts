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
    public float animDampTime = 0.25f;

    [Header("=== 🧠 战术走位 (随机攻击冷却) ===")]
    public bool enableTacticalMovement = true;
    public float retreatSpeed = 3.0f;
    public float retreatDistance = 3.5f;
    public float strafeSpeed = 2.0f;

    [Tooltip("最短绕圈时间 (最少冷却)")]
    public float minStrafeTime = 1.0f;
    [Tooltip("最长绕圈时间 (最多冷却)")]
    public float maxStrafeTime = 3.0f;

    [Header("=== 🩸 受击与死亡配置 (动画驱动) ===")]
    [Tooltip("受击动画的总长度(秒)，在此期间怪物无法行动")]
    public float hitAnimDuration = 0.5f; // 🔥 现在的受击仅仅是一个计时器

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

    public bool isCodeMoving = false;
    protected bool skipTacticalThisTime = false;

    protected Vector3 startPosition;
    protected float patrolTimer = 0f;
    protected bool isWaiting = false;
    protected float strafeTimer = 0f;
    protected float retreatTimer = 0f;
    protected int strafeDirection = 1;
    protected Vector3 tacticalTargetPos;

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

        // 🔥 如果大脑没被劫持，且【没有处于受击硬直中】，才允许思考！
        if (!isAIHijacked && !isKnockedBack)
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
        // 🔥 删除了 isDead 的拦截，让怪物死了也能继续播放真实的“倒地滑行位移”！
        if (!useRootMotion || agent == null || anim == null) return;

        // 🔥 删除了 isKnockedBack，这意味着受击时，动画位移会接管怪物的后退！
        if (isAIHijacked || isCodeMoving)
        {
            if (agent.isActiveAndEnabled) agent.updatePosition = true;
            return;
        }

        if (agent.isActiveAndEnabled) agent.updatePosition = false;
        Vector3 animDeltaPosition = anim.deltaPosition;
        animDeltaPosition.y = 0;

        if (isRotationLocked)
        {
            float forwardMove = Vector3.Dot(animDeltaPosition, transform.forward);
            animDeltaPosition = transform.forward * forwardMove;
        }

        transform.position += animDeltaPosition;
        if (agent.isActiveAndEnabled) agent.nextPosition = transform.position;
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
        StopAllCoroutines();

        // 🔥 彻底删除了强制击退的代码
        // 仅仅是物理停住，让真正的死亡动画来掌管它到底怎么倒地
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }
    }

    public virtual void OnHitInterrupt()
    {
        isAttacking = false;
        isRotationLocked = false;
        skipTacticalThisTime = false;
        isCodeMoving = false;

        // 打断时立刻物理刹车
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.velocity = Vector3.zero;
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

        // 🔥 替换为纯动画等待协程
        if (gameObject.activeInHierarchy && !isDead) StartCoroutine(HitRecoverCoroutine());
    }

    // 🔥 纯计时器：等挨打动画播完
    protected IEnumerator HitRecoverCoroutine()
    {
        isKnockedBack = true; // 开启大脑停机锁

        // 我们不写任何强行移动的代码了，因为动画在播，OnAnimatorMove 会根据动画把它往后退！
        yield return new WaitForSeconds(hitAnimDuration);

        isKnockedBack = false; // 挨打结束，大脑开机

        if (!isDead)
        {
            ChangeState(AIState.Chase);
        }
    }
}