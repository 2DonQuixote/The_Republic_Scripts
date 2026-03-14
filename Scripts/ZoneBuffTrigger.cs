using UnityEngine;
using System.Collections.Generic;

public class ZoneBuffTrigger : MonoBehaviour
{
    [Header("=== 烟雾/毒圈配置 ===")]
    public BuffData buffToApply;

    [Tooltip("每次跳字，增加多少积累值？")]
    public float buildupAmount = 15f;

    [Tooltip("多长时间跳一次？(比如 0.5 秒)")]
    public float tickInterval = 0.5f;

    // 🛡️ 核心防抖字典：记录每个玩家【上一次被毒的绝对时间】
    private Dictionary<StatusManager, float> lastTickTimes = new Dictionary<StatusManager, float>();

    // 使用 OnTriggerStay 持续检测，但通过时间锁来控制频率
    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StatusManager statusMgr = other.GetComponent<StatusManager>();
            if (statusMgr != null && buffToApply != null)
            {
                // 🔥 核心时间锁：
                // 条件1：玩家刚进来，字典里没他（立刻给个下马威）
                // 条件2：玩家一直在里面，且现在的时间 > 他上次挨毒的时间 + 间隔时间
                if (!lastTickTimes.ContainsKey(statusMgr) || Time.time >= lastTickTimes[statusMgr] + tickInterval)
                {
                    // 1. 狠狠叠毒！
                    statusMgr.AddStatusBuildup(buffToApply, buildupAmount);

                    // 2. 刷新“作案时间”，接下来 0.5 秒内这把锁绝对打不开！
                    lastTickTimes[statusMgr] = Time.time;
                }
            }
        }
    }

    // 玩家彻底离开烟雾时，把他的秒表收回，等他下次再进来时重新发秒表
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StatusManager statusMgr = other.GetComponent<StatusManager>();
            if (statusMgr != null && lastTickTimes.ContainsKey(statusMgr))
            {
                lastTickTimes.Remove(statusMgr);
            }
        }
    }
}