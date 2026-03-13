using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System; // 🔥 1. 引入 System 命名空间以使用 Action

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

    // 🔥 2. 定义一个公共事件：当怪物的某个动作（如攻击）完全结束时广播
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
            // 默认开启 Root Motion
            Agent.updatePosition = false;

            // 终极防抽风修复 1：彻底关闭自动旋转，怪物的朝向100%由我们的代码(FaceTarget)决定！
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

        // 核心修复：夺取控制权的瞬间，强行把混合树的残留参数归零！
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

    // 🔥 3. 供其他行为芯片调用的方法：触发“动作完成”广播
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

        // ==========================================
        // ✅ 智能清理机制：遍历并清除所有 Trigger
        // 不再硬编码名字，无论什么怪、什么招式，统统打断！
        // ==========================================
        if (Anim != null)
        {
            foreach (AnimatorControllerParameter param in Anim.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger)
                {
                    // 🔥 核心修改：只清除攻击类的 Trigger，绝对不能清除受击和死亡的 Trigger！
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

    private void UpdateAnimatorLocomotion()
    {
        if (Anim == null || Agent == null) return;

        bool inCombat = (currentState != BrainState.Idle);
        Anim.SetBool("InCombat", inCombat);

        if (currentState == BrainState.Chase && Agent.velocity.magnitude > 0.1f)
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