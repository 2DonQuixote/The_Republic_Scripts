using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyBrain))]
public class PhaseChangeBehavior : MonoBehaviour
{
    [Header("=== 🩸 触发条件 ===")]
    [Range(0f, 1f)]
    [Tooltip("生命值低于此百分比时触发二阶段 (例如 0.5 就是 50%血)")]
    public float healthThreshold = 0.5f;

    [Header("=== 🎬 表现配置 ===")]
    [Tooltip("二阶段变身动画的节点名称 (动画自带后跳和嘶吼)")]
    public string phaseChangeAnimState = "Roar";

    [Tooltip("整个变身过程持续多少秒？(时间到了才允许继续追击和攻击)")]
    public float phaseChangeDuration = 3.0f;

    [Header("=== ✨ 变身特效 (可选) ===")]
    public GameObject phaseChangeVFX;
    [Tooltip("特效生成的位置（如果不填，默认生成在怪物脚下）")]
    public Transform vfxSpawnPoint;

    private EnemyBrain brain;
    private EnemyHealth myHealth;

    // 确保变身只触发一次的锁
    private bool hasTriggered = false;

    private float CurrentHpPercent => myHealth != null ? myHealth.GetCurrentHealthPercent() : 1f;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
        myHealth = GetComponent<EnemyHealth>();
    }

    private void Update()
    {
        // 如果已经爆过气了，或者死了，就不管了
        if (hasTriggered || brain.currentState == EnemyBrain.BrainState.Dead) return;

        // 【核心判断】：一旦血量跌破阈值
        if (CurrentHpPercent <= healthThreshold)
        {
            // 尝试抢占大脑的控制权！
            // 💡 亮点：如果怪物正在攻击，RequestActionExecution 会返回 false。
            // 只有等它当前攻击打完，大脑空闲了，这里才会返回 true，完美实现“等动作做完再爆气”！
            if (brain.RequestActionExecution())
            {
                // 抢占成功！立刻锁死状态，开始变身
                hasTriggered = true;
                StartCoroutine(PhaseChangeRoutine());
            }
        }
    }

    private IEnumerator PhaseChangeRoutine()
    {
        // 1. 强制转身面朝玩家
        if (brain.Player != null)
        {
            brain.FaceTargetInstantly(brain.Player.position);
        }

        // 2. 播放变身/嘶吼动画 (带0.1秒平滑过渡，位移全靠动画自带的 Root Motion)
        if (brain.Anim != null)
        {
            brain.Anim.CrossFadeInFixedTime(phaseChangeAnimState, 0.1f);
        }

        // 3. 生成爆气特效 (如果有配置的话)
        if (phaseChangeVFX != null)
        {
            Transform spawnParent = vfxSpawnPoint != null ? vfxSpawnPoint : transform;
            Instantiate(phaseChangeVFX, spawnParent.position, spawnParent.rotation, spawnParent);
        }

        float timer = 0f;

        // 4. 安全倒计时：只当秒表用，绝对不干涉物理位移
        while (timer < phaseChangeDuration)
        {
            if (brain.currentState == EnemyBrain.BrainState.Dead) yield break;

            timer += Time.deltaTime;

            // 嘶吼期间，让怪物始终盯着玩家，压迫感更强
            if (brain.Player != null)
            {
                brain.FaceTargetInstantly(brain.Player.position);
            }

            yield return null;
        }

        // 5. 时间到了！变身结束，解锁大脑！
        brain.FinishAction();
        brain.TriggerActionFinished();
    }
}