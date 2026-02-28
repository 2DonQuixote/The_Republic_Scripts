using UnityEngine;

public class PoisonProjectile : MonoBehaviour
{
    [Header("=== 🚀 飞行配置 ===")]
    public float speed = 10f;       // 水平飞行的速度
    public float arcHeight = 2.5f;  // 抛物线的最高点高度 (越高弧度越大)
    public float directDamage = 5f; // 被直接砸中的物理伤害

    [Header("=== 🧪 毒性配置 ===")]
    public BuffData poisonBuff;     // 拖入你的毒 Buff 文件
    public float buildupAmount = 40f;

    [Header("=== ✨ 表现 ===")]
    public GameObject hitVFX;       // 落地/砸中人的爆炸特效

    // 内部计算变量
    private Vector3 startPos;
    private Vector3 targetPos;
    private float flightDuration;
    private float flightTimer = 0f;
    private bool isLaunched = false;

    // 🔥 新版发射方法：传入目标落点
    public void LaunchToPoint(Vector3 target)
    {
        startPos = transform.position;
        targetPos = target; // 记住发射这一刻的落点目标

        // 计算水平面的总距离
        float distance = Vector3.Distance(new Vector3(startPos.x, 0, startPos.z), new Vector3(targetPos.x, 0, targetPos.z));

        // 根据距离和速度，算出这趟飞行需要几秒
        flightDuration = distance / speed;

        isLaunched = true;
    }

    void Update()
    {
        if (!isLaunched) return;

        flightTimer += Time.deltaTime;

        // 计算当前飞行的进度百分比 (0 到 1)
        float percent = flightTimer / flightDuration;

        if (percent >= 1f)
        {
            // 进度达到 100%，说明刚好落在目标地上，触发爆炸！
            Explode(null);
            return;
        }

        // 1. 计算水平方向的直线移动 (匀速靠近目标)
        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, percent);

        // 2. 加上垂直方向的抛物线高度！
        // 核心公式：4 * height * p * (1-p)。当 percent=0.5 (飞到一半) 时，高度正好是 arcHeight
        currentPos.y += arcHeight * 4f * percent * (1f - percent);

        // 3. 让毒球的朝向跟着飞行轨迹改变 (如果有长条状模型，会有箭头飞行的感觉)
        Vector3 moveDir = currentPos - transform.position;
        if (moveDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(moveDir);

        // 实际移动物体
        transform.position = currentPos;
    }

    // 半空中如果撞到玩家、墙壁等物体，提前引爆
    private void OnTriggerEnter(Collider other)
    {
        // 忽略怪物自己和别的毒球
        if (other.CompareTag("Enemy") || other.GetComponent<PoisonProjectile>()) return;

        Explode(other.gameObject);
    }

    // 统一的爆炸销毁逻辑
    private void Explode(GameObject hitTarget)
    {
        // 如果是因为撞到了玩家而爆炸，就造成直击伤害和叠毒
        if (hitTarget != null)
        {
            IDamageable targetHealth = hitTarget.GetComponent<IDamageable>();
            if (targetHealth != null) targetHealth.TakeDamage(directDamage);

            StatusManager statusMgr = hitTarget.GetComponent<StatusManager>();
            if (statusMgr != null && poisonBuff != null)
            {
                statusMgr.AddStatusBuildup(poisonBuff, buildupAmount);
            }
        }

        // 播放地上的那一滩毒气爆炸特效
        if (hitVFX != null) Instantiate(hitVFX, transform.position, Quaternion.identity);

        // 功成身退，销毁自己
        Destroy(gameObject);
    }
}