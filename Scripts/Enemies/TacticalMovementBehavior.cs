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
    private bool originalRootMotion = false; // 提取为全局，方便打断时恢复

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
    }

    public void OnAttackCompleted()
    {
        if (brain.RequestActionExecution())
        {
            if (brain.Anim != null)
            {
                // 瞬间挂倒挡，不经过发呆状态
                brain.Anim.SetFloat("MoveX", 0f);
                brain.Anim.SetFloat("MoveZ", -1f);

                // 配合你的状态机名字
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
            // 🔥 核心打断逻辑：每一帧都在监听大脑状态！
            // 只要大脑被大剑劈进了 Stunned(硬直) 或 Dead(死亡) 状态...
            if (brain.currentState == EnemyBrain.BrainState.Stunned || brain.currentState == EnemyBrain.BrainState.Dead)
            {
                // 1. 立刻清零平移参数，防止受击抽搐时还在滑步
                ResetAnimParams();

                // 2. 🔥 完美收尾：把 Root Motion 物理驱动权还给动画机，防止打断后变木桩！
                if (brain.Anim != null && useCodeMovement)
                {
                    brain.Anim.applyRootMotion = originalRootMotion;
                }

                // 3. 彻底销毁当前战术走位，退出协程！
                yield break;
            }

            // 如果大脑不是 Stunned（比如中了毒，状态依然是 ExecutingAction），它就会无视伤害，继续帅气走位！
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

        // 正常走位结束的收尾
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