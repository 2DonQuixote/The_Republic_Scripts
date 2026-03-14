using UnityEngine;
using System.Collections;

/// <summary>
/// 【行为芯片】自爆组件：靠近玩家后强行夺权，原地播放自爆动画。
/// 升级版：爆炸附带出血值，并原地留下持续的出血烟雾。
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
public class ExplosionBehavior : MonoBehaviour
{
    [Header("=== 💣 自爆触发配置 ===")]
    public float triggerRange = 2.0f;
    public string animExplode = "ZiBao";

    [Header("=== 💥 爆炸威力配置 ===")]
    public float explosionRadius = 3.5f;
    public float explosionDamage = 80f;
    public GameObject explosionVFX;

    [Header("=== 🩸 出血与烟雾配置 ===")]
    public BuffData bleedBuff;           // 拖入出血的 BuffData
    [Tooltip("瞬间炸中时，给玩家叠多少出血值？")]
    public float immediateBuildup = 50f;

    [Tooltip("爆炸后留下的持续烟雾预制体 (可以在上面挂 ZoneBuffTrigger)")]
    public GameObject bleedSmokePrefab;
    [Tooltip("烟雾持续几秒后消失？")]
    public float smokeDuration = 5.0f;

    private EnemyBrain brain;
    private bool hasTriggered = false;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
    }

    private void Update()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead || hasTriggered) return;

        if (brain.currentState == EnemyBrain.BrainState.Chase)
        {
            float dist = Vector3.Distance(transform.position, brain.Player.position);
            if (dist <= triggerRange)
            {
                StartExplosion();
            }
        }
    }

    private void StartExplosion()
    {
        if (!brain.RequestActionExecution()) return;

        hasTriggered = true;
        brain.FaceTargetInstantly(brain.Player.position);

        if (brain.Anim != null)
        {
            brain.Anim.CrossFadeInFixedTime(animExplode, 0.15f);
        }

        Debug.Log("<color=orange>【自爆警告】自爆怪开始蓄力！快跑！</color>");
    }

    // ==========================================
    // 🎭 动画事件：真正的爆炸
    // ==========================================
    public void TriggerExplosion()
    {
        if (brain.currentState == EnemyBrain.BrainState.Dead) return;

        // 1. 瞬间爆炸范围检测
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            if (hit.gameObject == this.gameObject || hit.CompareTag("Enemy")) continue;

            // 造成瞬间物理伤害
            IDamageable target = hit.GetComponent<IDamageable>();
            if (target != null) target.TakeDamage(explosionDamage, true);

            // 🔥 附加瞬间出血值
            StatusManager statusMgr = hit.GetComponent<StatusManager>();
            if (statusMgr != null && bleedBuff != null)
            {
                statusMgr.AddStatusBuildup(bleedBuff, immediateBuildup);
            }
        }

        // 2. 生成瞬间爆炸的火光/血浆特效
        if (explosionVFX != null)
        {
            Instantiate(explosionVFX, transform.position + Vector3.up, Quaternion.identity);
        }

        // 3. 🔥 生成残留的出血烟雾区域
        if (bleedSmokePrefab != null)
        {
            // 在怪物脚下生成烟雾
            GameObject smoke = Instantiate(bleedSmokePrefab, transform.position, Quaternion.identity);

            // 神奇的 API：直接让 Unity 在 smokeDuration 秒后自动销毁这个烟雾物体，不需要额外写计时器！
            Destroy(smoke, smokeDuration);
        }

        Debug.Log("<color=red>💥 轰！自爆怪已引爆并留下了出血烟雾！</color>");

        // 4. 销毁自爆怪自己
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRange);
    }
#endif
}