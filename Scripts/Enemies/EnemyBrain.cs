using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;

/// <summary>
/// 【组件化架构】怪物大脑：负责基础感知、最高级状态机、以及行为的调度与夺权。
/// 完美修复版：增加了“根运动软墙阻挡”机制，杜绝怪物攻击时推挤玩家！
/// </summary>
public class EnemyBrain : MonoBehaviour
{
    public enum BrainState
    {
        Idle,               // 发呆/巡逻中
        Chase,              // 追击玩家中
        ExecutingAction,    // 正在执行特定行为（攻击、战术走位等）
        Stunned,            // 被打出硬直（瘫痪中）
        Dead                // 死亡
    }

    [Header("=== 🧠 大脑基础感知配置 ===")]
    public BrainState currentState = BrainState.Idle;
    public float detectionRange = 15f;
    public float moveSpeed = 4.5f;

    [Tooltip("硬直恢复时间")]
    public float stunDuration = 0.5f;

    [Header("=== 🛡️ 物理防推挤设置 ===")]
    [Tooltip("防推墙距离：当怪物距离玩家小于此值时，动画往前冲的位移将被没收！(建议1.0~1.5)")]
    public float pushBlockDistance = 1.2f;

    public NavMeshAgent Agent { get; private set; }
    public Animator Anim { get; private set; }
    public Transform Player { get; private set; }

    public event Action OnActionFinishedSignal;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Anim = GetComponentInChildren<Animator>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) Player = playerObj.transform;
    }

    private void Start()
    {
        if (Agent != null)
        {
            Agent.speed = moveSpeed;
            Agent.updatePosition = false;
            Agent.updateRotation = false; // 防抽风转身
        }
    }

    private void Update()
    {
        if (currentState == BrainState.Dead || Player == null) return;

        switch (currentState)
        {
            case BrainState.Idle:
                UpdateIdle();
                break;
            case BrainState.Chase:
                UpdateChase();
                break;
        }

        UpdateAnimatorLocomotion();
    }

    // ==========================================
    // 🚦 核心接口：行为调度
    // ==========================================
    public bool RequestActionExecution()
    {
        if (currentState == BrainState.Stunned || currentState == BrainState.Dead || currentState == BrainState.ExecutingAction)
        {
            return false;
        }

        currentState = BrainState.ExecutingAction;

        if (Agent != null && Agent.isActiveAndEnabled)
        {
            Agent.ResetPath();
            Agent.velocity = Vector3.zero;
        }

        if (Anim != null)
        {
            Anim.SetFloat("MoveX", 0f);
            Anim.SetFloat("MoveZ", 0f);
        }

        return true;
    }

    public void FinishAction()
    {
        if (currentState == BrainState.Dead) return;
        currentState = BrainState.Chase;
    }

    public void TriggerActionFinished()
    {
        OnActionFinishedSignal?.Invoke();
    }

    // ==========================================
    // 💥 核心接口：受击与死亡
    // ==========================================
    public void OnHitInterrupt()
    {
        if (currentState == BrainState.Dead) return;

        currentState = BrainState.Stunned;

        if (Agent != null && Agent.isActiveAndEnabled)
        {
            Agent.ResetPath();
            Agent.velocity = Vector3.zero;
        }

        if (Anim != null)
        {
            foreach (AnimatorControllerParameter param in Anim.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger)
                {
                    if (param.name != "Hit" && param.name != "Twitch" && param.name != "Die" && param.name != "Frenzy")
                    {
                        Anim.ResetTrigger(param.name);
                    }
                }
            }
        }

        StopAllCoroutines();
        StartCoroutine(StunRecoveryRoutine());
    }

    private IEnumerator StunRecoveryRoutine()
    {
        yield return new WaitForSeconds(stunDuration);
        if (currentState != BrainState.Dead)
        {
            currentState = BrainState.Chase;
        }
    }

    public void TriggerDeath()
    {
        currentState = BrainState.Dead;
        StopAllCoroutines();
        if (Agent != null && Agent.isActiveAndEnabled)
        {
            Agent.ResetPath();
            Agent.velocity = Vector3.zero;
            Agent.isStopped = true;
        }
    }

    // ==========================================
    // 🏃 基础本能逻辑 (发现与追击)
    // ==========================================
    private void UpdateIdle()
    {
        float distance = Vector3.Distance(transform.position, Player.position);
        if (distance <= detectionRange)
        {
            currentState = BrainState.Chase;
        }
    }

    private void UpdateChase()
    {
        if (Agent != null && Agent.isActiveAndEnabled)
        {
            Agent.speed = moveSpeed;
            Agent.SetDestination(Player.position);
        }
        FaceTarget(Player.position);
    }

    public void FaceTarget(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);
        }
    }

    public void FaceTargetInstantly(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void UpdateAnimatorLocomotion()
    {
        if (Anim == null || Agent == null) return;

        bool inCombat = (currentState != BrainState.Idle);
        Anim.SetBool("InCombat", inCombat);

        if ((currentState == BrainState.Chase || currentState == BrainState.Idle) && Agent.velocity.magnitude > 0.1f)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(Agent.velocity);
            Anim.SetFloat("MoveX", localVelocity.x / Agent.speed, 0.1f, Time.deltaTime);
            Anim.SetFloat("MoveZ", localVelocity.z / Agent.speed, 0.1f, Time.deltaTime);
            Anim.SetFloat("Speed", Agent.velocity.magnitude, 0.1f, Time.deltaTime);
        }
        else if (currentState != BrainState.ExecutingAction)
        {
            Anim.SetFloat("MoveX", 0f, 0.1f, Time.deltaTime);
            Anim.SetFloat("MoveZ", 0f, 0.1f, Time.deltaTime);
            Anim.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
        }
    }

    // ==========================================
    // 🚶 动画位移接管 (Root Motion)
    // ==========================================
    private void OnAnimatorMove()
    {
        if (Agent == null || Anim == null || currentState == BrainState.Dead) return;

        if (Agent.isActiveAndEnabled && !Agent.updatePosition)
        {
            Vector3 animDeltaPosition = Anim.deltaPosition;
            animDeltaPosition.y = 0;

            // 🔥 ==============================================
            // 🛡️ 软墙拦截机制：如果离玩家太近，且正在往玩家脸上撞，没收位移！
            // =================================================
            if (Player != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, Player.position);

                if (distanceToPlayer <= pushBlockDistance)
                {
                    // 算出从怪物指向玩家的向量
                    Vector3 dirToPlayer = (Player.position - transform.position).normalized;
                    dirToPlayer.y = 0;
                    dirToPlayer.Normalize();

                    // 算出这一帧动画想要往玩家方向走多少
                    float pushAmount = Vector3.Dot(animDeltaPosition, dirToPlayer);

                    // 如果确实是在往前挤 (>0)，我们把这段位移从 Root Motion 里减去
                    if (pushAmount > 0)
                    {
                        animDeltaPosition -= dirToPlayer * pushAmount;
                    }
                }
            }

            transform.position += animDeltaPosition;
            Agent.nextPosition = transform.position;
        }
    }
}