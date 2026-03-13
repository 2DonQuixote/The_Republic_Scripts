using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 【组件化架构】怪物大脑：负责基础感知、最高级状态机、以及行为的调度与夺权。
/// 完美修复版：已禁用 Agent 自动旋转防抽风，并在交出控制权时彻底清空寻路路径！
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
    public float detectionRange = 15f; // 玩家进入这个距离，大脑就会开始追击
    public float moveSpeed = 4.5f;

    [Tooltip("硬直恢复时间")]
    public float stunDuration = 0.5f;

    public NavMeshAgent Agent { get; private set; }
    public Animator Anim { get; private set; }
    public Transform Player { get; private set; }

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
            // 默认开启 Root Motion
            Agent.updatePosition = false;

            // 🔥 终极防抽风修复 1：彻底关闭自动旋转，怪物的朝向100%由我们的代码(FaceTarget)决定！
            Agent.updateRotation = false;
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

        // 🔥 核心修复：夺取控制权的瞬间，强行把混合树的残留参数归零！
        // 这样它打完人的瞬间，混合树绝不会以为自己还要往前跑！
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

    // ==========================================
    // 💥 核心接口：受击与死亡
    // ==========================================
    public void OnHitInterrupt()
    {
        if (currentState == BrainState.Dead) return;

        currentState = BrainState.Stunned;

        if (Agent != null && Agent.isActiveAndEnabled)
        {
            // 🔥 终极防前顶修复 2：挨打时也要彻底清空路线！
            Agent.ResetPath();
            Agent.velocity = Vector3.zero;
        }

        if (Anim != null)
        {
            Anim.ResetTrigger("Attack");
            Anim.ResetTrigger("Attack2");
            Anim.ResetTrigger("Attack3");
            Anim.ResetTrigger("QianZhua");
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

    private void UpdateAnimatorLocomotion()
    {
        if (Anim == null || Agent == null) return;

        bool inCombat = (currentState != BrainState.Idle);
        Anim.SetBool("InCombat", inCombat);

        // 如果大脑处于 Chase(追击) 状态，且确实产生了速度
        if (currentState == BrainState.Chase && Agent.velocity.magnitude > 0.1f)
        {
            // 将全局速度转换为相对于怪物自身的局部方向
            Vector3 localVelocity = transform.InverseTransformDirection(Agent.velocity);

            // 传给 Animator 的 Blend Tree
            Anim.SetFloat("MoveX", localVelocity.x / Agent.speed, 0.1f, Time.deltaTime);
            Anim.SetFloat("MoveZ", localVelocity.z / Agent.speed, 0.1f, Time.deltaTime);
            Anim.SetFloat("Speed", Agent.velocity.magnitude, 0.1f, Time.deltaTime);
        }
        else if (currentState != BrainState.ExecutingAction)
        {
            // 发呆归零
            Anim.SetFloat("MoveX", 0f, 0.1f, Time.deltaTime);
            Anim.SetFloat("MoveZ", 0f, 0.1f, Time.deltaTime);
            Anim.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
        }
    }

    private void OnAnimatorMove()
    {
        if (Agent == null || Anim == null || currentState == BrainState.Dead) return;

        if (Agent.isActiveAndEnabled && !Agent.updatePosition)
        {
            Vector3 animDeltaPosition = Anim.deltaPosition;
            animDeltaPosition.y = 0;
            transform.position += animDeltaPosition;
            Agent.nextPosition = transform.position;
        }
    }
}