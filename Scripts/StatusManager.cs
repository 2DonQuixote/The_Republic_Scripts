using UnityEngine;
using System.Collections; // 🔥 必须引用这个，才能用协程
using System.Collections.Generic;

public class StatusManager : MonoBehaviour
{
    [System.Serializable]
    public class ActiveBuff
    {
        public BuffData data;
        public float timer;
        public float tickTimer;

        public ActiveBuff(BuffData data)
        {
            this.data = data;
            this.timer = data.duration;
            this.tickTimer = data.triggerImmediately ? data.tickInterval : 0f;
        }
    }

    public List<ActiveBuff> currentBuffs = new List<ActiveBuff>();

    private IDamageable targetHealth;

    private void Awake()
    {
        targetHealth = GetComponent<IDamageable>();
    }

    private void Update()
    {
        for (int i = currentBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = currentBuffs[i];
            buff.timer -= Time.deltaTime;
            if (buff.timer <= 0) { currentBuffs.RemoveAt(i); continue; }

            if (buff.data.damagePerTick > 0)
            {
                buff.tickTimer += Time.deltaTime;
                if (buff.tickTimer >= buff.data.tickInterval)
                {
                    buff.tickTimer = 0f;
                    if (targetHealth != null) targetHealth.TakeDamage(buff.data.damagePerTick);
                }
            }
        }
    }

    public void ApplyBuff(BuffData newData)
    {
        if (!newData.isStackable)
        {
            var existingBuff = currentBuffs.Find(x => x.data == newData);
            if (existingBuff != null)
            {
                existingBuff.timer = newData.duration;
                if (newData.triggerImmediately) existingBuff.tickTimer = newData.tickInterval;
            }
            else
            {
                currentBuffs.Add(new ActiveBuff(newData));
            }
        }
        else
        {
            currentBuffs.Add(new ActiveBuff(newData));
        }

        // 通知 UI (仅限玩家)
        if (GameStatusUI.Instance != null && gameObject.CompareTag("Player"))
        {
            GameStatusUI.Instance.ShowStatus(newData.uiMessage, newData.duration, newData.uiColor, newData.isStackable);
        }
    }

    // 🔥🔥🔥【核心修复】清除逻辑 🔥🔥🔥
    public void ClearDebuffsOnRest()
    {
        // 1. 先清除数据 (把该删的删了)
        for (int i = currentBuffs.Count - 1; i >= 0; i--)
        {
            if (currentBuffs[i].data.clearOnRest)
            {
                currentBuffs.RemoveAt(i);
            }
        }

        // 2. 刷新 UI (仅限玩家)
        // 如果我们只是 HideUI()，那些不需要清除的 Buff (比如诅咒) 也会消失，
        // 所以我们需要“先全清，再把幸存者画回去”。
        if (gameObject.CompareTag("Player") && GameStatusUI.Instance != null)
        {
            GameStatusUI.Instance.HideUI(); // 视觉上全部移除

            // 启动协程：等一帧再重画
            // 为什么要等？因为 Unity 的 Destroy 不是立刻生效的，
            // 如果不等一帧直接画，UI 系统可能会复用那些“正在死亡”的图标，导致显示错误。
            StartCoroutine(RebuildUI());
        }
    }

    // 重绘 UI 的协程
    IEnumerator RebuildUI()
    {
        // 等待当前帧结束 (让旧的图标彻底销毁)
        yield return null;

        // 把剩下的 Buff (那些没被清除的) 重新显示出来
        foreach (var buff in currentBuffs)
        {
            if (GameStatusUI.Instance != null)
            {
                GameStatusUI.Instance.ShowStatus(
                    buff.data.uiMessage,
                    buff.timer, // 注意：这里传剩余时间，不要传总时间
                    buff.data.uiColor,
                    buff.data.isStackable
                );
            }
        }
    }
}