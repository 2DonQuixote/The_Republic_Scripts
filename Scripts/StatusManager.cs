using UnityEngine;
using System.Collections;
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

    public class BuildupTracker
    {
        public float currentValue;
        public float decayPauseTimer;
    }

    // 玩家的额外抗性字典
    private Dictionary<StatusType, float> resistanceBonuses = new Dictionary<StatusType, float>();

    public List<ActiveBuff> currentBuffs = new List<ActiveBuff>();
    private Dictionary<BuffData, BuildupTracker> buildupTrackers = new Dictionary<BuffData, BuildupTracker>();
    private IDamageable targetHealth;

    private void Awake() { targetHealth = GetComponent<IDamageable>(); }

    private void Update()
    {
        HandleActiveBuffs();
        HandleBuildupDecay();
    }

    // 🔥 核心修复：计算最终阈值 (这里用的是 baseThreshold)
    public float GetRealThreshold(BuffData data)
    {
        float bonus = 0f;
        if (resistanceBonuses.ContainsKey(data.type))
        {
            bonus = resistanceBonuses[data.type];
        }
        // 👇 之前报错就是因为没改成 baseThreshold
        return data.baseThreshold + bonus;
    }

    // 外部调用：增加抗性
    public void AddResistance(StatusType type, float amount)
    {
        if (!resistanceBonuses.ContainsKey(type)) resistanceBonuses[type] = 0;
        resistanceBonuses[type] += amount;
    }

    private void HandleActiveBuffs()
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
                    if (targetHealth != null) targetHealth.TakeDamage(buff.data.damagePerTick, false);
                }
            }
        }
    }

    private void HandleBuildupDecay()
    {
        List<BuffData> keys = new List<BuffData>(buildupTrackers.Keys);
        foreach (var key in keys)
        {
            BuildupTracker tracker = buildupTrackers[key];

            // 🔥 修复：动态获取当前阈值
            float maxThreshold = GetRealThreshold(key);

            if (tracker.decayPauseTimer > 0)
            {
                tracker.decayPauseTimer -= Time.deltaTime;
            }
            else if (tracker.currentValue > 0)
            {
                tracker.currentValue -= key.decayRate * Time.deltaTime;
                if (tracker.currentValue < 0) tracker.currentValue = 0;
            }

            if (gameObject.CompareTag("Player") && GameStatusUI.Instance != null)
            {
                // 🔥 修复：传入计算好的 maxThreshold
                GameStatusUI.Instance.UpdateBuildupUI(key.uiMessage, tracker.currentValue, maxThreshold, key.uiColor);
            }
        }
    }

    public void AddStatusBuildup(BuffData data, float amount)
    {
        if (data == null) return;
        if (!buildupTrackers.ContainsKey(data)) buildupTrackers[data] = new BuildupTracker();

        BuildupTracker tracker = buildupTrackers[data];

        // 🔥 修复：动态获取阈值
        float maxThreshold = GetRealThreshold(data);

        tracker.currentValue += amount;
        tracker.decayPauseTimer = 2.0f;

        if (gameObject.CompareTag("Player") && GameStatusUI.Instance != null)
        {
            GameStatusUI.Instance.UpdateBuildupUI(data.uiMessage, tracker.currentValue, maxThreshold, data.uiColor);
        }

        // 🔥 修复：这里也用 maxThreshold 判定
        if (tracker.currentValue >= maxThreshold)
        {
            ActivateBuff(data);
            tracker.currentValue = 0f;

            if (gameObject.CompareTag("Player") && GameStatusUI.Instance != null)
            {
                GameStatusUI.Instance.UpdateBuildupUI(data.uiMessage, 0, maxThreshold, data.uiColor);
            }
        }
    }

    private void ActivateBuff(BuffData newData)
    {
        if (!newData.isStackable)
        {
            var existingBuff = currentBuffs.Find(x => x.data == newData);
            if (existingBuff != null) existingBuff.timer = newData.duration;
            else currentBuffs.Add(new ActiveBuff(newData));
        }
        else currentBuffs.Add(new ActiveBuff(newData));

        if (GameStatusUI.Instance != null && gameObject.CompareTag("Player"))
        {
            GameStatusUI.Instance.ShowStatus(newData.uiMessage, newData.duration, newData.uiColor, newData.isStackable);
        }
    }

    public void ClearDebuffsOnRest()
    {
        for (int i = currentBuffs.Count - 1; i >= 0; i--)
        {
            if (currentBuffs[i].data.clearOnRest) currentBuffs.RemoveAt(i);
        }
        buildupTrackers.Clear();

        if (gameObject.CompareTag("Player") && GameStatusUI.Instance != null)
        {
            GameStatusUI.Instance.HideUI();
            StartCoroutine(RebuildUI());
        }
    }

    IEnumerator RebuildUI()
    {
        yield return null;
        foreach (var buff in currentBuffs)
        {
            if (GameStatusUI.Instance != null)
                GameStatusUI.Instance.ShowStatus(buff.data.uiMessage, buff.timer, buff.data.uiColor, buff.data.isStackable);
        }
    }
}