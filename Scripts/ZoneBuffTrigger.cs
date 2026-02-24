using UnityEngine;

public class ZoneBuffTrigger : MonoBehaviour
{
    [Header("配置")]
    public BuffData buffToApply;

    [Tooltip("进入区域时，瞬间增加多少积累值？(比如 20)")]
    public float buildupAmount = 20f;

    // 💡 可选：如果你希望站在里面持续增加积累值，可以用 OnTriggerStay
    // 这里我们先用 Enter 保持和你原来逻辑一致
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var statusMgr = other.GetComponent<StatusManager>();
            if (statusMgr != null && buffToApply != null)
            {
                // 🔥 修改点：不再直接 ApplyBuff，而是增加积累值！
                statusMgr.AddStatusBuildup(buffToApply, buildupAmount);
            }
        }
    }
}