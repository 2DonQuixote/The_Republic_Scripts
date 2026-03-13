using UnityEngine;
using System.Collections;

/// <summary>
/// 【行为芯片】战术走位模块：接管身体进行后退和左右平移。
/// 完美版：支持被硬直(Stunned)瞬间完美打断并清零，无视中毒等软伤害。
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
public class TacticalMovementBehavior : MonoBehaviour
{
    [Header("=== ⏱️ 战术时间 (即攻击CD) ===")]
    public float minTacticalTime = 1.5f;
    public float maxTacticalTime = 3.0f;

    [Header("=== 🚶 走位阶段划分 ===")]
    public float retreatDuration = 1.0f;

    [Header("=== ⚙️ 位移控制模式 ===")]
    public bool useCodeMovement = true;
    public float retreatSpeed = 2.5f;
    public float strafeSpeed = 2.0f;

    private EnemyBrain brain;
    private Coroutine tacticalRoutine;
    private bool originalRootMotion = false;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
    }

    // 🔥 1. 新增：脚本启用时订阅大脑的信号
    private void Start()
    {
        if (brain != null)
        {
            brain.OnActionFinishedSignal += ExecuteTacticalMove;
        }
    }

    // 🔥 2. 新增：脚本销毁时记得取消订阅，防止内存泄漏报错
    private void OnDestroy()
    {
        if (brain != null)
        {
            brain.OnActionFinishedSignal -= ExecuteTacticalMove;
        }
    }

    // 🔥 3. 核心修改：这个方法现在由事件自动触发了，可以改成 private，且无需叫特定名字
    private void ExecuteTacticalMove()
    {
        if (brain.RequestActionExecution())
        {
            if (brain.Anim != null)
            {
                brain.Anim.SetFloat("MoveX", 0f);
                brain.Anim.SetFloat("MoveZ", -1f);
                brain.Anim.CrossFadeInFixedTime("Combat_Locomotion", 0.1f);
            }

            if (tacticalRoutine != null) StopCoroutine(tacticalRoutine);
            tacticalRoutine = StartCoroutine(TacticalRoutine());
        }
    }

    private IEnumerator TacticalRoutine()
    {
        float totalTime = Random.Range(minTacticalTime, maxTacticalTime);
        float currentTimer = 0f;
        float strafeDir = Random.value > 0.5f ? 1f : -1f;

        if (brain.Anim != null)
        {
            originalRootMotion = brain.Anim.applyRootMotion;
            if (useCodeMovement) brain.Anim.applyRootMotion = false;
        }

        while (currentTimer < totalTime)
        {
            if (brain.currentState == EnemyBrain.BrainState.Stunned || brain.currentState == EnemyBrain.BrainState.Dead)
            {
                ResetAnimParams();
                if (brain.Anim != null && useCodeMovement)
                {
                    brain.Anim.applyRootMotion = originalRootMotion;
                }
                yield break;
            }

            brain.FaceTarget(brain.Player.position);

            Vector3 moveDir = Vector3.zero;
            float animMoveX = 0f;
            float animMoveZ = 0f;
            float currentSpeed = 0f;

            if (currentTimer < retreatDuration)
            {
                moveDir = -transform.forward;
                animMoveZ = -1f;
                currentSpeed = retreatSpeed;
            }
            else
            {
                moveDir = transform.right * strafeDir;
                animMoveX = strafeDir;
                currentSpeed = strafeSpeed;
            }

            if (useCodeMovement && brain.Agent != null && brain.Agent.isActiveAndEnabled)
            {
                brain.Agent.Move(moveDir * currentSpeed * Time.deltaTime);
                transform.position = brain.Agent.nextPosition;
            }

            if (brain.Anim != null)
            {
                brain.Anim.SetFloat("MoveX", animMoveX, 0.1f, Time.deltaTime);
                brain.Anim.SetFloat("MoveZ", animMoveZ, 0.1f, Time.deltaTime);
            }

            currentTimer += Time.deltaTime;
            yield return null;
        }

        ResetAnimParams();

        if (brain.Anim != null && useCodeMovement)
        {
            brain.Anim.applyRootMotion = originalRootMotion;
        }

        brain.FinishAction();
    }

    private void ResetAnimParams()
    {
        if (brain.Anim != null)
        {
            brain.Anim.SetFloat("MoveX", 0f);
            brain.Anim.SetFloat("MoveZ", 0f);
        }
    }
}