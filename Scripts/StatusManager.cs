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
            this.tickTimer = 0f;
        }
    }

    public class BuildupTracker
    {
        public float currentValue;
        public float decayPauseTimer;
    }

    private Dictionary<StatusType, float> resistanceBonuses = new Dictionary<StatusType, float>();
    private Dictionary<BuffData, BuildupTracker> buildupTrackers = new Dictionary<BuffData, BuildupTracker>();
    public List<ActiveBuff> currentBuffs = new List<ActiveBuff>();

    private IDamageable targetHealth;

    private void Awake() { targetHealth = GetComponent<IDamageable>(); }

    private void Update()
    {
        HandleActiveBuffs();
        HandleBuildupDecay();
    }

    // ==========================================
    // 增加积累值
    // ==========================================
    public void AddStatusBuildup(BuffData data, float amount)
    {
        if (data == null) return;

        // 1. 如果 Buff 已经激活
        var activeBuff = currentBuffs.Find(x => x.data == data);
        if (activeBuff != null)
        {
            // 如果允许刷新时间
            if (data.refreshTimeOnHit)
            {
                activeBuff.timer = data.duration;
                // UI 也会复用同一个条子，看起来就是倒计时瞬间回满
                if (GameStatusUI.Instance != null && gameObject.CompareTag("Player"))
                {
                    GameStatusUI.Instance.ShowStatus(data.uiMessage, data.duration, data.uiColor);
                }
            }
            return; // 只要激活了，就不再处理积累值
        }

        // 2. 如果还没激活，处理积累条
        if (!buildupTrackers.ContainsKey(data)) buildupTrackers[data] = new BuildupTracker();
        BuildupTracker tracker = buildupTrackers[data];

        float maxThreshold = GetRealThreshold(data);

        tracker.currentValue += amount;
        tracker.decayPauseTimer = 2.0f;

        // 更新 UI：条子上涨
        if (gameObject.CompareTag("Player") && GameStatusUI.Instance != null)
        {
            GameStatusUI.Instance.UpdateBuildupUI(data.uiMessage, tracker.currentValue, maxThreshold, data.uiColor);
        }

        // 3. 判定爆发
        if (tracker.currentValue >= maxThreshold)
        {
            ActivateBuff(data);        // 激活！UI 会无缝切换成倒计时
            tracker.currentValue = 0f; // 清空后台数据

            // 🔥🔥🔥 核心修改：删掉了这里让 UI 消失的代码 🔥🔥🔥
            // 我们不删除 UI，而是让 ActivateBuff -> ShowStatus 去接管它
        }
    }

    private void ActivateBuff(BuffData newData)
    {
        currentBuffs.Add(new ActiveBuff(newData));

        if (GameStatusUI.Instance != null && gameObject.CompareTag("Player"))
        {
            // 这里会找到刚刚那个积累条，把它重置为满状态，并开始倒计时
            GameStatusUI.Instance.ShowStatus(newData.uiMessage, newData.duration, newData.uiColor);
        }
    }

    private void HandleActiveBuffs()
    {
        for (int i = currentBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = currentBuffs[i];
            buff.timer -= Time.deltaTime;

            if (buff.data.damagePerTick > 0)
            {
                buff.tickTimer += Time.deltaTime;
                if (buff.tickTimer >= buff.data.tickInterval)
                {
                    buff.tickTimer = 0f;
                    if (targetHealth != null) targetHealth.TakeDamage(buff.data.damagePerTick, false);
                }
            }

            if (buff.timer <= 0) currentBuffs.RemoveAt(i);
        }
    }

    private void HandleBuildupDecay()
    {
        List<BuffData> keys = new List<BuffData>(buildupTrackers.Keys);
        foreach (var key in keys)
        {
            // 🔥🔥🔥 核心修改：如果这个 Buff 已经激活了，就不要再管积累条了 🔥🔥🔥
            // 这样防止后台的衰减逻辑去干扰前台正在倒计时的 UI
            if (currentBuffs.Exists(x => x.data == key)) continue;

            BuildupTracker tracker = buildupTrackers[key];
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

            // 只有积累值 > 0 才更新 UI
            if (tracker.currentValue > 0)
            {
                if (gameObject.CompareTag("Player") && GameStatusUI.Instance != null)
                {
                    GameStatusUI.Instance.UpdateBuildupUI(key.uiMessage, tracker.currentValue, maxThreshold, key.uiColor);
                }
            }
            else
            {
                // 如果衰减归零了，且没激活，说明玩家躲过一劫，移除 UI
                if (gameObject.CompareTag("Player") && GameStatusUI.Instance != null)
                {
                    GameStatusUI.Instance.UpdateBuildupUI(key.uiMessage, 0, maxThreshold, key.uiColor);
                }
            }
        }
    }

    public float GetRealThreshold(BuffData data)
    {
        float bonus = 0f;
        if (resistanceBonuses.ContainsKey(data.type)) bonus = resistanceBonuses[data.type];
        return data.baseThreshold + bonus;
    }

    public void AddResistance(StatusType type, float amount)
    {
        if (!resistanceBonuses.ContainsKey(type)) resistanceBonuses[type] = 0;
        resistanceBonuses[type] += amount;
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
                GameStatusUI.Instance.ShowStatus(buff.data.uiMessage, buff.timer, buff.data.uiColor);
        }
    }
}