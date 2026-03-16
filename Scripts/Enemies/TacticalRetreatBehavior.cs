using UnityEngine;

[RequireComponent(typeof(EnemyBrain))]
public class TacticalRetreatBehavior : MonoBehaviour
{
    [System.Serializable]
    public class RetreatConfig
    {
        [Header("⏱️ 撤退时间控制")]
        public float retreatDuration = 1.2f;

        [Header("🏃 撤退混合树控制 (Blend Tree)")]
        [Tooltip("控制前后移动的Float参数名（比如 MoveZ 或 MoveY）")]
        public string moveFloatParam = "MoveZ";
        [Tooltip("后退时该Float设为多少？（通常混合树里后退是 -1）")]
        public float retreatFloatValue = -1f;

        [Header("🛡️ 防御参数 (HitReaction Layer)")]
        [Range(0f, 1f)]
        public float defendChance = 0.4f;
        public string defendBoolName = "IsDefending";
    }

    [Header("=== 🎲 触发概率 ===")]
    [Range(0f, 1f)]
    public float retreatChance = 1.0f;

    [Header("=== 🟢 一阶段配置 (默认) ===")]
    public RetreatConfig phase1Config;

    [Header("=== 🩸 二阶段配置 (残血/狂暴) ===")]
    public bool useHealthThreshold = true;
    [Range(0f, 1f)] public float healthThreshold = 0.5f;
    public RetreatConfig phase2Config;

    private EnemyBrain brain;
    private EnemyHealth myHealth;

    private string currentActiveDefendBool = "";
    private RetreatConfig currentActiveConfig;

    // 用于在 Update 里控制时间的两个变量
    private bool isRetreating = false;
    private float retreatTimer = 0f;

    private float CurrentHpPercent => myHealth != null ? myHealth.GetCurrentHealthPercent() : 1f;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
        myHealth = GetComponent<EnemyHealth>();
    }

    // 核心逻辑：每一帧给混合树发送 -1，让怪物持续向后迈步
    private void Update()
    {
        if (!isRetreating) return;

        if (brain.currentState == EnemyBrain.BrainState.Dead)
        {
            isRetreating = false;
            return;
        }

        // 保持面朝玩家
        if (brain.Player != null)
        {
            brain.FaceTargetInstantly(brain.Player.position);
        }

        // 🎯 【精髓就在这里】每帧持续赋值，驱动下半身后退动画！
        if (brain.Anim != null && currentActiveConfig != null)
        {
            // 添加一点平滑缓冲（0.1f），让动作不会过于僵硬
            brain.Anim.SetFloat(currentActiveConfig.moveFloatParam, currentActiveConfig.retreatFloatValue, 0.1f, Time.deltaTime);
        }

        // 倒计时
        retreatTimer -= Time.deltaTime;

        if (retreatTimer <= 0f)
        {
            EndRetreat();
        }
    }

    public bool TryStartRetreat()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return false;
        if (isRetreating) return false;
        if (Random.value > retreatChance) return false;
        if (!brain.RequestActionExecution()) return false;

        currentActiveConfig = phase1Config;
        if (useHealthThreshold && CurrentHpPercent <= healthThreshold)
        {
            currentActiveConfig = phase2Config;
        }

        bool isDefending = Random.value <= currentActiveConfig.defendChance;

        if (brain.Anim != null)
        {
            // 【重要】不再使用 CrossFade，彻底抛弃节点播放！

            // 如果随到了防御，呼叫上半身覆盖动作
            if (isDefending && !string.IsNullOrEmpty(currentActiveConfig.defendBoolName))
            {
                brain.Anim.SetBool(currentActiveConfig.defendBoolName, true);
                currentActiveDefendBool = currentActiveConfig.defendBoolName;
            }
        }

        retreatTimer = currentActiveConfig.retreatDuration;
        isRetreating = true;

        return true;
    }

    private void EndRetreat()
    {
        isRetreating = false;

        if (brain.Anim != null)
        {
            // 1. 关闭防御
            if (!string.IsNullOrEmpty(currentActiveDefendBool))
            {
                brain.Anim.SetBool(currentActiveDefendBool, false);
                currentActiveDefendBool = "";
            }

            // 2. 🎯 【关键收尾】撤退结束，把混合树的后退 Float 归零，停下脚步！
            if (currentActiveConfig != null)
            {
                brain.Anim.SetFloat(currentActiveConfig.moveFloatParam, 0f);
            }
        }

        // 3. 解锁大脑
        brain.FinishAction();
        brain.TriggerActionFinished();
    }
}