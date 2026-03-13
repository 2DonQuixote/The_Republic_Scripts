using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(EnemyBrain))]
public class ComboGrabBehavior : MonoBehaviour
{
    [Header("=== ⚖️ 权重与触发 ===")]
    public float weight = 15f;
    public float grabRange = 2.5f;

    [Header("=== 🎬 动画参数 ===")]
    public string animStartGrab = "QianZhua";
    public string animBiteLoop = "GrabSuccess";
    public string animRelease = "ZhengTuo";

    [Header("=== ⚙️ 判定配置 ===")]
    public EnemyAttackConfig grabHitbox;

    [Header("=== ⏱️ 时间设置 (4秒发呆) ===")]
    public float biteDuration = 3.0f;
    public float releaseAnimDuration = 1.5f;
    public float postGrabDazeDuration = 2.0f; // 啃完后的发呆
    public float missPenaltyDuration = 4.0f; // 🔥 您要求的：抓空后原地发呆4秒

    [Header("=== 🩸 伤害 ===")]
    public float biteDamage = 15f;
    public GameObject bloodVFX;

    private EnemyBrain brain;
    public bool isGrabSuccess { get; private set; } = false;
    private bool isHandlingGrab = false; // 🛡️ 状态锁，防止逻辑重复触发
    private Coroutine currentRoutine;
    private Transform caughtPlayer;

    private void Awake() => brain = GetComponent<EnemyBrain>();

    public bool IsReady(float distanceToPlayer) => distanceToPlayer <= grabRange;

    public void ExecuteGrab()
    {
        isGrabSuccess = false;
        isHandlingGrab = true; // 开启逻辑保护
        brain.FaceTarget(brain.Player.position);
        if (brain.Anim != null) brain.Anim.SetTrigger(animStartGrab);
    }

    // ==========================================
    // 🎭 动画事件：抓取判定 (打在合拢那一帧)
    // ==========================================
    public void TryGrab()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        List<Collider> hits = grabHitbox.GetHitTargets(transform);
        bool caught = false;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                caught = true;
                caughtPlayer = hit.transform;
                break;
            }
        }

        if (caught)
        {
            isGrabSuccess = true;
            if (brain.Anim != null) brain.Anim.SetTrigger(animBiteLoop);

            PlayerReaction reaction = caughtPlayer.GetComponent<PlayerReaction>();
            if (reaction != null) reaction.ApplyGrab(biteDuration + releaseAnimDuration + 0.5f);

            if (currentRoutine != null) StopCoroutine(currentRoutine);
            currentRoutine = StartCoroutine(BiteTimerRoutine());
        }
        // ❌ 注意：抓空了不要在这里写逻辑，交给下面的 AnimEvent_GrabEnd 处理
    }

    // ==========================================
    // 🎭 动画事件：前抓动作结束 (打在 QianZhua 最后一帧)
    // ==========================================
    public void AnimEvent_GrabEnd()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        // 如果已经抓成功了，让 BiteTimerRoutine 协程接管
        if (isGrabSuccess) return;

        // 🎯 抓空逻辑：启动 4 秒惩罚协程
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(MissPenaltyRoutine());
    }

    // --- 内部计时逻辑 ---

    private IEnumerator BiteTimerRoutine()
    {
        yield return new WaitForSeconds(biteDuration);
        if (CheckDead()) yield break;

        if (brain.Anim != null) brain.Anim.SetTrigger(animRelease);
        yield return new WaitForSeconds(releaseAnimDuration);
        if (CheckDead()) yield break;

        // 啃完后的发呆
        yield return new WaitForSeconds(postGrabDazeDuration);
        FinishGrabBehavior();
    }

    private IEnumerator MissPenaltyRoutine()
    {
        // 🔥 强制执行 4 秒发呆
        Debug.Log($"<color=orange>抓空了！原地硬直 {missPenaltyDuration} 秒...</color>");

        // 1. 确保动画机回到 Idle 或原地不动的状态参数
        if (brain.Anim != null)
        {
            brain.Anim.SetFloat("Speed", 0);
            brain.Anim.SetFloat("MoveX", 0);
            brain.Anim.SetFloat("MoveZ", 0);
        }

        yield return new WaitForSeconds(missPenaltyDuration);

        FinishGrabBehavior();
    }

    private void FinishGrabBehavior()
    {
        Debug.Log("<color=green>硬直结束，重新搜索目标！</color>");
        isGrabSuccess = false;
        isHandlingGrab = false;
        caughtPlayer = null;
        brain.FinishAction(); // 交还控制权，由于没发信号，不会触发后跳
    }

    private bool CheckDead() => brain.currentState == EnemyBrain.BrainState.Dead || brain.currentState == EnemyBrain.BrainState.Stunned;

    public void DealDamage()
    {
        if (isGrabSuccess && caughtPlayer != null)
        {
            IDamageable playerHealth = caughtPlayer.GetComponent<IDamageable>();
            playerHealth?.TakeDamage(biteDamage, false);
            if (bloodVFX != null) Instantiate(bloodVFX, caughtPlayer.position + Vector3.up, Quaternion.identity);
        }
    }
}