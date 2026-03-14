using UnityEngine;
using System.Collections;

/// <summary>
/// 【行为芯片】远程投掷组件：化身固定炮台，在范围内死死站定无限投掷！
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
public class RangedBehavior : MonoBehaviour
{
    [Header("=== 🏹 远程炮台配置 ===")]
    public GameObject projectilePrefab;
    public Transform throwPoint;

    [Header("=== 📏 触发距离与冷却 ===")]
    public float minRange = 3.0f;
    public float maxRange = 10.0f;
    [Tooltip("投掷间隔时间：冷却期间会在原地盯着玩家，绝不前进！")]
    public float cooldown = 2.0f;

    [Header("=== 🎬 动画参数与防呆保护 ===")]
    public string animThrow = "Throw";
    public float throwAnimDuration = 1.5f;

    private EnemyBrain brain;
    private float cooldownTimer = 0f;
    private Coroutine currentRoutine;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
    }

    private void Update()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        if (brain.currentState == EnemyBrain.BrainState.Chase)
        {
            float dist = Vector3.Distance(transform.position, brain.Player.position);

            // 🔥 核心修改：只要玩家在射程内，不论冷却好没好，立刻变身“固定炮台”！
            if (dist >= minRange && dist <= maxRange)
            {
                // 1. 强行拉起物理手刹，没收大脑的寻路权利，实现原地待机！
                if (brain.Agent != null && brain.Agent.isActiveAndEnabled)
                {
                    brain.Agent.isStopped = true;
                    brain.Agent.velocity = Vector3.zero;
                }

                // 2. 虽然不动，但眼神依然死死盯着玩家
                brain.FaceTarget(brain.Player.position);

                // 3. 只有冷却好了，才开火！
                if (cooldownTimer <= 0)
                {
                    ExecuteThrow();
                }
            }
            else
            {
                // 🔥 如果玩家逃出了射程 (或者靠得太近进入了近战区)，松开手刹，让大脑重新开始追击！
                if (brain.Agent != null && brain.Agent.isActiveAndEnabled)
                {
                    brain.Agent.isStopped = false;
                }
            }
        }
    }

    private void ExecuteThrow()
    {
        if (!brain.RequestActionExecution()) return;

        cooldownTimer = cooldown;
        brain.FaceTargetInstantly(brain.Player.position);

        if (brain.Anim != null)
        {
            brain.Anim.CrossFadeInFixedTime(animThrow, 0.15f);
        }

        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(ThrowFailsafeRoutine());
    }

    // ==========================================
    // 🎭 动画事件：生成毒球
    // ==========================================
    public void SpawnProjectile()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead || brain.currentState == EnemyBrain.BrainState.Stunned) return;
        if (projectilePrefab == null || throwPoint == null || brain.Player == null) return;

        brain.FaceTargetInstantly(brain.Player.position);
        GameObject ball = Instantiate(projectilePrefab, throwPoint.position, throwPoint.rotation);

        var script = ball.GetComponent<PoisonProjectile>();
        if (script != null)
        {
            script.LaunchToPoint(brain.Player.position);
        }
    }

    // ==========================================
    // 🎭 动画事件：动作结束
    // ==========================================
    public void AnimEvent_ThrowEnd()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;
        FinishThrow();
    }

    private IEnumerator ThrowFailsafeRoutine()
    {
        yield return new WaitForSeconds(throwAnimDuration);
        if (brain.currentState == EnemyBrain.BrainState.Dead || brain.currentState == EnemyBrain.BrainState.Stunned) yield break;
        FinishThrow();
    }

    private void FinishThrow()
    {
        if (currentRoutine != null) { StopCoroutine(currentRoutine); currentRoutine = null; }
        brain.FinishAction();
    }
}