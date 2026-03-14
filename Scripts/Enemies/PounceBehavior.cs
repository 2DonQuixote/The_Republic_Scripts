using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 【行为芯片】飞扑组件：中远距离突进 -> 判定(命中转撕咬/落空趴地硬直) -> 发呆 -> 继续追击
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
public class PounceBehavior : MonoBehaviour
{
    [Header("=== 📏 雷达触发距离与冷却 ===")]
    [Tooltip("最小触发距离（防止贴脸原地起跳）")]
    public float minPounceRange = 3.5f;
    [Tooltip("最大飞扑射程")]
    public float maxPounceRange = 6.0f;
    [Tooltip("飞扑专属冷却时间（如果不是一次性的话）")]
    public float pounceCooldown = 8.0f;

    [Header("=== 🌟 一次性技能设置 ===")]
    [Tooltip("勾选后，怪物一生只能释放一次飞扑（例如开局下马威）")]
    public bool useOnlyOnce = false;              // 🔥 您要的勾选框
    private bool hasUsedPounce = false;           // 内部状态锁：记录是否已经飞扑过

    [Header("=== 🎬 动画参数 (飞扑流转) ===")]
    public string animStartPounce = "FeiPu";      // 1. 飞扑起跳
    public string animBiteLoop = "GrabSuccess";   // 2. 扑倒撕咬 (复用投技动画)
    public string animRelease = "ZhengTuo";       // 3. 松开 (复用投技动画)

    [Header("=== ⚙️ 飞扑抓取判定配置 ===")]
    public EnemyAttackConfig pounceHitbox;

    [Header("=== ⏱️ 时间轴与硬直 ===")]
    public float biteDuration = 3.0f;             // 撕咬总时间
    public float releaseAnimDuration = 1.5f;      // 松开动画保护时间
    public float postPounceDazeDuration = 2.0f;   // 啃完满足后，发呆几秒
    public float missPenaltyDuration = 3.0f;      // 扑空后，趴在地上硬直几秒

    [Header("=== 🩸 撕咬单次伤害 (复用 DealDamage 事件) ===")]
    public float biteDamage = 15f;
    public GameObject bloodVFX;

    private EnemyBrain brain;
    private float cooldownTimer = 0f;

    // 🛡️ 状态锁，极其重要：防止和普通投技的扣血发生串台
    public bool isPounceSuccess { get; private set; } = false;
    private Coroutine currentRoutine;
    private Transform caughtPlayer;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
    }

    private void Update()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        // 🔥 核心拦截：如果是“一次性”且“已经用过”了，雷达彻底关机，不再消耗性能！
        if (useOnlyOnce && hasUsedPounce) return;

        // 跑冷却
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        // 🎯 核心逻辑：如果在追击状态，且冷却完毕，开始雷达扫描！
        if (brain.currentState == EnemyBrain.BrainState.Chase && cooldownTimer <= 0)
        {
            float distance = Vector3.Distance(transform.position, brain.Player.position);

            // 如果玩家正好在 3.5米 到 6米 之间，立刻起跳飞扑！
            if (distance >= minPounceRange && distance <= maxPounceRange)
            {
                ExecutePounce();
            }
        }
    }

    private void ExecutePounce()
    {
        // 向大脑申请最高控制权
        if (!brain.RequestActionExecution()) return;

        isPounceSuccess = false;
        hasUsedPounce = true;           // 🔥 标记：我已经把这辈子唯一的一次飞扑用掉了！
        cooldownTimer = pounceCooldown; // 进入冷却（即便是一次性，也顺手设一下）

        // 起跳前瞬间强行把脸对准玩家，杜绝转身没转完造成的空中弧线！
        brain.FaceTargetInstantly(brain.Player.position);

        if (brain.Anim != null) brain.Anim.SetTrigger(animStartPounce);
    }

    // ==========================================
    // 🎭 动画事件 1：飞扑落地抓取判定
    // 打在飞扑动画(FeiPu)落地、爪子接触地面的那一帧！
    // ==========================================
    public void TryPounceGrab()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        List<Collider> hits = pounceHitbox.GetHitTargets(transform);
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
            isPounceSuccess = true;
            if (brain.Anim != null) brain.Anim.SetTrigger(animBiteLoop);

            // 锁死玩家操作
            PlayerReaction reaction = caughtPlayer.GetComponent<PlayerReaction>();
            if (reaction != null) reaction.ApplyGrab(biteDuration + releaseAnimDuration + 0.5f);

            if (currentRoutine != null) StopCoroutine(currentRoutine);
            currentRoutine = StartCoroutine(BiteTimerRoutine());
        }
    }

    // ==========================================
    // 🎭 动画事件 2：撕咬扣血 (复用名为 DealDamage 的事件)
    // ==========================================
    public void DealDamage()
    {
        if (isPounceSuccess && caughtPlayer != null)
        {
            IDamageable playerHealth = caughtPlayer.GetComponent<IDamageable>();
            playerHealth?.TakeDamage(biteDamage, false);
            if (bloodVFX != null) Instantiate(bloodVFX, caughtPlayer.position + Vector3.up, Quaternion.identity);
        }
    }

    // ==========================================
    // 🎭 动画事件 3：飞扑动作彻底结束
    // 打在飞扑动画(FeiPu)的最后一帧！用于判定抓空。
    // ==========================================
    public void AnimEvent_PounceEnd()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead || isPounceSuccess) return;

        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(MissPenaltyRoutine());
    }

    // --- 内部终极防卡死时间轴 ---

    private IEnumerator BiteTimerRoutine()
    {
        yield return new WaitForSeconds(biteDuration);
        if (CheckDead()) yield break;

        if (brain.Anim != null) brain.Anim.SetTrigger(animRelease);
        yield return new WaitForSeconds(releaseAnimDuration);
        if (CheckDead()) yield break;

        yield return new WaitForSeconds(postPounceDazeDuration);
        if (CheckDead()) yield break;

        FinishPounceBehavior();
    }

    private IEnumerator MissPenaltyRoutine()
    {
        Debug.Log($"<color=orange>飞扑落空！怪物趴地硬直 {missPenaltyDuration} 秒...</color>");

        if (brain.Anim != null)
        {
            brain.Anim.SetFloat("Speed", 0);
            brain.Anim.SetFloat("MoveX", 0);
            brain.Anim.SetFloat("MoveZ", 0);
        }

        yield return new WaitForSeconds(missPenaltyDuration);
        if (CheckDead()) yield break;

        FinishPounceBehavior();
    }

    private void FinishPounceBehavior()
    {
        isPounceSuccess = false;
        caughtPlayer = null;
        brain.FinishAction(); // 交还控制权，继续像疯狗一样追击
    }

    private bool CheckDead() => brain.currentState == EnemyBrain.BrainState.Dead || brain.currentState == EnemyBrain.BrainState.Stunned;

    // ==========================================
    // 🎨 Scene 窗口可视化辅助线
    // ==========================================
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (pounceHitbox == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Vector3 realCenter = transform.position + transform.rotation * pounceHitbox.hitOffset;

        switch (pounceHitbox.shapeType)
        {
            case HitShape.Circle: Gizmos.DrawWireSphere(realCenter, pounceHitbox.attackRadius); break;
            case HitShape.Sector:
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Gizmos.DrawWireSphere(realCenter, pounceHitbox.attackRadius);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
                Vector3 forward = transform.forward;
                Vector3 leftRay = Quaternion.AngleAxis(-pounceHitbox.attackAngle * 0.5f, Vector3.up) * forward;
                Vector3 rightRay = Quaternion.AngleAxis(pounceHitbox.attackAngle * 0.5f, Vector3.up) * forward;
                Gizmos.DrawRay(realCenter, leftRay * pounceHitbox.attackRadius);
                Gizmos.DrawRay(realCenter, rightRay * pounceHitbox.attackRadius);
                break;
            case HitShape.Rectangle:
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(realCenter, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, pounceHitbox.boxSize);
                Gizmos.matrix = oldMatrix;
                break;
        }
    }
#endif
}