using UnityEngine;

public class PoisonProjectile : MonoBehaviour
{
    [Header("配置")]
    public float speed = 15f;
    public float directDamage = 5f; // 被砸中的物理伤害

    [Header("毒性配置")]
    public BuffData poisonBuff;     // 拖入你的毒 Buff 文件
    public float buildupAmount = 40f; // 砸中一下增加 40 点积累值（假设阈值80，那就是中2下中毒）

    public GameObject hitVFX; // 爆炸特效

    private Vector3 shootDir;
    private bool isLaunched = false;

    // 怪物发射时调用这个
    public void Launch(Vector3 direction)
    {
        shootDir = direction.normalized;
        isLaunched = true;
        Destroy(gameObject, 5f); // 5秒没打中人自动销毁
    }

    void Update()
    {
        if (isLaunched)
        {
            transform.position += shootDir * speed * Time.deltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 忽略怪物自己和别的投掷物
        if (other.CompareTag("Enemy") || other.GetComponent<PoisonProjectile>()) return;

        // 1. 造成物理伤害
        IDamageable targetHealth = other.GetComponent<IDamageable>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(directDamage);
        }

        // 2. 🔥🔥🔥 增加毒积累值 🔥🔥🔥
        StatusManager statusMgr = other.GetComponent<StatusManager>();
        if (statusMgr != null && poisonBuff != null)
        {
            statusMgr.AddStatusBuildup(poisonBuff, buildupAmount);
        }

        // 3. 特效与销毁
        if (hitVFX != null) Instantiate(hitVFX, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}