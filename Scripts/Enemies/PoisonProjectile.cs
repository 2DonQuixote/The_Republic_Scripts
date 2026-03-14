using UnityEngine;

/// <summary>
/// 完美抛物线投射物：可调高度、飞行时间，自带精准制导、范围爆炸与叠毒。
/// </summary>
public class PoisonProjectile : MonoBehaviour
{
    [Header("=== 🏹 抛物线飞行配置 ===")]
    [Tooltip("飞到玩家脸上需要几秒？")]
    public float flightDuration = 1.0f;
    [Tooltip("抛物线最高点的高度（调大就是高抛雷，调小就是直球）")]
    public float arcHeight = 3.0f;

    [Header("=== 💥 爆炸与基础伤害 ===")]
    public float damage = 15f;
    [Tooltip("爆炸的波及范围 (半径)")]
    public float explosionRadius = 3.0f;
    public GameObject hitVFX; // 砸中玩家或地面的爆炸特效

    [Header("=== 🧪 毒性附加配置 ===")]
    public BuffData poisonBuff; // 拖入您配置好的中毒 BuffData
    [Tooltip("炸到一次给玩家增加多少中毒积累值？")]
    public float buildupAmount = 40f; 

    private Vector3 startPoint;
    private Vector3 targetPoint;
    private float elapsedTime = 0f;
    private bool isLaunched = false;
    private bool hasExploded = false; // 🔥 防重复爆炸锁

    public void LaunchToPoint(Vector3 targetPosition)
    {
        startPoint = transform.position;
        // 瞄准玩家的胸口/肚子（稍微往上抬一点，不至于砸脚趾头）
        targetPoint = targetPosition + Vector3.up * 1.0f;
        elapsedTime = 0f;
        isLaunched = true;
        hasExploded = false;
    }

    private void Update()
    {
        if (!isLaunched || hasExploded) return;

        elapsedTime += Time.deltaTime;
        float percent = elapsedTime / flightDuration; // 当前飞行进度 0.0 ~ 1.0

        if (percent >= 1f)
        {
            // 飞到终点了，强制落地爆炸
            Explode();
            return;
        }

        // 🔥 抛物线核心算法
        Vector3 currentPos = Vector3.Lerp(startPoint, targetPoint, percent);
        float heightModifier = Mathf.Sin(percent * Mathf.PI) * arcHeight;
        currentPos.y += heightModifier;

        // 让毒球的模型在空中顺着抛物线的弧度旋转，视觉效果更好
        Vector3 moveDir = currentPos - transform.position;
        if (moveDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(moveDir);

        transform.position = currentPos;
    }

    // 碰撞检测：如果是飞行途中撞到了墙、玩家、地面
    private void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;

        // 忽略怪物自己和发射出来的其他毒球，防止刚出手就炸
        if (other.CompareTag("Enemy") || other.GetComponent<PoisonProjectile>()) return;

        // 碰到任何有效物体，立刻引爆！
        Explode();
    }

    // 🔥 核心修改：范围爆炸逻辑
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        isLaunched = false;

        // 1. 范围检测：找出爆炸半径内所有的碰撞体
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (var hit in hits)
        {
            // 如果不想炸伤别的怪物队友，跳过他们
            if (hit.CompareTag("Enemy")) continue;

            // 2. 造成基础伤害并触发受击硬直
            IDamageable targetHealth = hit.GetComponent<IDamageable>();
            if (targetHealth != null)
            {
                // true 代表被炸到会出硬直
                targetHealth.TakeDamage(damage, true);
            }

            // 3. 附加中毒积累值 (接入您的 StatusManager 体系)
            StatusManager statusMgr = hit.GetComponent<StatusManager>();
            if (statusMgr != null && poisonBuff != null)
            {
                statusMgr.AddStatusBuildup(poisonBuff, buildupAmount);
            }
        }

        // 4. 生成爆炸/毒雾特效
        if (hitVFX != null)
        {
            Instantiate(hitVFX, transform.position, Quaternion.identity);
        }

        // 5. 销毁毒球本体
        Destroy(gameObject);
    }

    // 🛠️ 辅助线：在 Scene 窗口里画出爆炸范围，方便您调试
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f); // 半透明绿色球体
        Gizmos.DrawSphere(transform.position, explosionRadius);
        Gizmos.color = Color.green; // 绿色边缘线
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}