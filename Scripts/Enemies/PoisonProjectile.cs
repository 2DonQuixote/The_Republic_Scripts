using UnityEngine;

public class PoisonProjectile : MonoBehaviour
{
    [Header("=== 🚀 飞行配置 ===")]
    public float speed = 10f;
    public float arcHeight = 2.5f;
    public float directDamage = 5f;

    [Header("=== 💥 爆炸配置 ===")]
    [Tooltip("爆炸半径。直径 = 半径 * 2")]
    public float explosionRadius = 3.0f; // 🔥 这里就是您要的可调直径（半径）

    [Header("=== 🧪 毒性配置 ===")]
    public BuffData poisonBuff;
    public float buildupAmount = 40f;

    [Header("=== ✨ 表现 ===")]
    public GameObject hitVFX;

    // 内部计算变量
    private Vector3 startPos;
    private Vector3 targetPos;
    private float flightDuration;
    private float flightTimer = 0f;
    private bool isLaunched = false;
    private bool hasExploded = false; // 防止重复触发爆炸

    public void LaunchToPoint(Vector3 target)
    {
        startPos = transform.position;
        targetPos = target;

        float distance = Vector3.Distance(new Vector3(startPos.x, 0, startPos.z), new Vector3(targetPos.x, 0, targetPos.z));
        flightDuration = distance / speed;

        isLaunched = true;
    }

    void Update()
    {
        if (!isLaunched || hasExploded) return;

        flightTimer += Time.deltaTime;
        float percent = flightTimer / flightDuration;

        if (percent >= 1f)
        {
            Explode(); // 落地爆炸
            return;
        }

        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, percent);
        currentPos.y += arcHeight * 4f * percent * (1f - percent);

        Vector3 moveDir = currentPos - transform.position;
        if (moveDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(moveDir);

        transform.position = currentPos;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 忽略怪物自己和别的毒球
        if (other.CompareTag("Enemy") || other.GetComponent<PoisonProjectile>()) return;

        // 只要撞到任何非怪物的物体（墙、玩家、地面装饰），立刻引爆
        Explode();
    }

    // 🔥 核心修改：范围爆炸逻辑
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // 1. 范围检测：找出爆炸半径内所有的碰撞体
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (var hit in hits)
        {
            // 🛡️ 依然遵守之前的规则：爆炸不伤队友（Enemy 标签）
            if (hit.CompareTag("Enemy")) continue;

            // 2. 对范围内所有带 IDamageable 的物体造成伤害
            IDamageable targetHealth = hit.GetComponent<IDamageable>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(directDamage);
            }

            // 3. 对范围内所有带 StatusManager 的物体叠毒
            StatusManager statusMgr = hit.GetComponent<StatusManager>();
            if (statusMgr != null && poisonBuff != null)
            {
                statusMgr.AddStatusBuildup(poisonBuff, buildupAmount);
            }
        }

        // 4. 生成爆炸特效
        if (hitVFX != null)
        {
            GameObject vfx = Instantiate(hitVFX, transform.position, Quaternion.identity);
            // 💡 进阶小贴士：如果您的特效本身支持缩放，可以根据半径动态调整特效大小
            // vfx.transform.localScale = Vector3.one * (explosionRadius / 3f);
        }

        // 5. 销毁自己
        Destroy(gameObject);
    }

    // 🛠️ 辅助线：在 Scene 窗口里画出爆炸范围，方便您调试
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}