using UnityEngine;

[RequireComponent(typeof(EnemyBrain))]
public class TacticalRetreatBehavior : MonoBehaviour
{
    [System.Serializable]
    public class RetreatConfig
    {
        [Header("🏃 撤退动画")]
        [Tooltip("Animator中普通后撤步的节点名称")]
        public string retreatAnimState = "MoveBackward";

        [Header("🛡️ 防御参数")]
        [Range(0f, 1f)]
        [Tooltip("撤退时举盾/防御的概率")]
        public float defendChance = 0.4f;
        [Tooltip("触发防御时播放的动画节点名称(比如举盾后退)")]
        public string defendRetreatAnimState = "DefendBackward";

        [Tooltip("如果用Bool控制减伤，填在这里")]
        public string defendBoolName = "IsDefending";
    }

    [Header("=== 🎲 触发概率 ===")]
    [Range(0f, 1f)]
    [Tooltip("打完一套攻击后，有多大概率执行撤退？(1为必定撤退)")]
    public float retreatChance = 1.0f;

    [Header("=== 🟢 一阶段配置 (默认) ===")]
    public RetreatConfig phase1Config;

    [Header("=== 🩸 二阶段配置 (残血/狂暴) ===")]
    public bool useHealthThreshold = true;
    [Range(0f, 1f)] public float healthThreshold = 0.5f;
    public RetreatConfig phase2Config;

    private EnemyBrain brain;
    private EnemyHealth myHealth;

    // 缓存当前使用的防御Bool名字，方便结束时关闭
    private string currentActiveDefendBool = "";

    private float CurrentHpPercent => myHealth != null ? myHealth.GetCurrentHealthPercent() : 1f;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
        myHealth = GetComponent<EnemyHealth>();
    }

    // 由 BasicAttackBehavior 在攻击结束时调用
    public bool TryStartRetreat()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return false;

        // 掷骰子：是否触发撤退
        if (Random.value > retreatChance) return false;

        // 向大脑申请执行动作（接管身体）
        if (!brain.RequestActionExecution()) return false;

        // 1. 判断阶段
        RetreatConfig activeConfig = phase1Config;
        if (useHealthThreshold && CurrentHpPercent <= healthThreshold)
        {
            activeConfig = phase2Config;
        }

        // 2. 掷骰子：本次撤退是否防御
        bool isDefending = Random.value <= activeConfig.defendChance;
        string animToPlay = isDefending ? activeConfig.defendRetreatAnimState : activeConfig.retreatAnimState;

        // 3. 播放动画，并开启防御机制
        if (brain.Anim != null)
        {
            brain.Anim.CrossFadeInFixedTime(animToPlay, 0.15f);

            if (isDefending && !string.IsNullOrEmpty(activeConfig.defendBoolName))
            {
                brain.Anim.SetBool(activeConfig.defendBoolName, true);
                currentActiveDefendBool = activeConfig.defendBoolName;
            }
        }

        // 强制对准玩家，防止背对玩家后退
        if (brain.Player != null)
        {
            brain.FaceTargetInstantly(brain.Player.position);
        }

        return true; // 成功接管
    }

    // 🛑【关键事件】必须在撤退动画的最后几帧打上这个动画事件！
    public void AnimEvent_RetreatEnd()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        // 撤退结束，关闭防御Bool
        if (!string.IsNullOrEmpty(currentActiveDefendBool) && brain.Anim != null)
        {
            brain.Anim.SetBool(currentActiveDefendBool, false);
            currentActiveDefendBool = "";
        }

        // 归还控制权给大脑，让怪物继续追击或发呆
        brain.FinishAction();
        brain.TriggerActionFinished();
    }
}