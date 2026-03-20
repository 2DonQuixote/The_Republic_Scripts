using UnityEngine;

[RequireComponent(typeof(EnemyBrain))]
public class TacticalRetreatBehavior : MonoBehaviour
{
    [System.Serializable]
    public class RetreatConfig
    {
        [Header("⏱️ 撤退时间控制")]
        [Tooltip("撤退动作持续的时间（秒），时间一到自动结束撤退并继续攻击")]
        public float retreatDuration = 1.2f;

        // 🔥 就是这里！专属的后退过渡速度！
        [Header("🏃 撤退敏捷度控制")]
        [Tooltip("后退动作的渐变速度，数值越小，后退启动越快！(建议0.05~0.15)")]
        [Range(0.01f, 0.5f)] public float retreatDampTime = 0.15f;

        [Header("🏃 撤退混合树控制 (Blend Tree)")]
        [Tooltip("控制前后移动的Float参数名（比如 Movement）")]
        public string moveFloatParam = "Movement";
        [Tooltip("后退时该Float设为多少？（通常混合树里后退是 -1）")]
        public float retreatFloatValue = -1f;

        [Header("🛡️ 防御参数 (HitReaction Layer)")]
        [Range(0f, 1f)]
        [Tooltip("撤退时举盾/防御的概率")]
        public float defendChance = 0.4f;

        [Tooltip("HitReaction层进入防御状态所需的Bool名字")]
        public string defendBoolName = "IsDefending";
    }

    [Header("=== 🎲 触发概率 ===")]
    [Range(0f, 1f)]
    [Tooltip("打完一套攻击后，有多大概率执行撤退？")]
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

    // 获取当前血量百分比
    private float CurrentHpPercent => myHealth != null ? myHealth.GetCurrentHealthPercent() : 1f;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
        myHealth = GetComponent<EnemyHealth>();
    }

    // 每一帧给混合树发送平滑的数值，让怪物持续向后迈步
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

        // 🎯 【迅捷后退】使用配置好的 retreatDampTime 进行敏捷平滑
        if (brain.Anim != null && currentActiveConfig != null)
        {
            brain.Anim.SetFloat(currentActiveConfig.moveFloatParam, currentActiveConfig.retreatFloatValue, currentActiveConfig.retreatDampTime, Time.deltaTime);
        }

        // 倒计时
        retreatTimer -= Time.deltaTime;

        // 时间到了，结束撤退
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

        // 1. 确定当前使用哪个阶段的配置
        currentActiveConfig = phase1Config;
        if (useHealthThreshold && CurrentHpPercent <= healthThreshold)
        {
            currentActiveConfig = phase2Config;
        }

        // 2. 掷骰子决定是否防御
        bool isDefending = Random.value <= currentActiveConfig.defendChance;

        // 3. 呼叫上半身覆盖动作
        if (brain.Anim != null)
        {
            if (isDefending && !string.IsNullOrEmpty(currentActiveConfig.defendBoolName))
            {
                brain.Anim.SetBool(currentActiveConfig.defendBoolName, true);
                currentActiveDefendBool = currentActiveConfig.defendBoolName;
            }
        }

        // 4. 设置好时间，开启倒计时开关！
        retreatTimer = currentActiveConfig.retreatDuration;
        isRetreating = true;

        return true;
    }

    private void EndRetreat()
    {
        isRetreating = false;

        if (brain.Anim != null)
        {
            // 撤退结束，关闭上半身防御
            if (!string.IsNullOrEmpty(currentActiveDefendBool))
            {
                brain.Anim.SetBool(currentActiveDefendBool, false);
                currentActiveDefendBool = "";
            }
        }

        // 强行解锁大脑，归还控制权
        brain.FinishAction();
        brain.TriggerActionFinished();
    }
}