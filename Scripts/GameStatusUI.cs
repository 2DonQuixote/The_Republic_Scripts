using UnityEngine;
using DG.Tweening;

public class GameStatusUI : MonoBehaviour
{
    public static GameStatusUI Instance;
    public GameObject statusItemPrefab;
    public Transform container;

    private void Awake() => Instance = this;

    // 1. 显示/刷新 Active Buff (倒计时模式)
    public void ShowStatus(string content, float duration, Color barColor)
    {
        // 先找找有没有同名的条子（无论是正在积累的，还是已经激活的）
        foreach (Transform child in container)
        {
            var controller = child.GetComponent<StatusItemController>();
            if (controller != null && controller.GetTitle() == content)
            {
                // 找到了！直接复用它，从积累模式切换为倒计时模式
                controller.ResetTimer(duration);
                // 💡 这里顺便可以重置一下颜色，确保颜色正确
                if (controller.durationBar != null) controller.durationBar.color = barColor;
                return;
            }
        }

        // 没找到（可能是直接获得Buff），新建一个
        GameObject newItem = Instantiate(statusItemPrefab, container);
        var ctrl = newItem.GetComponent<StatusItemController>();
        if (ctrl != null)
        {
            ctrl.Setup(content, duration, barColor);
        }
    }

    // 2. 更新积累进度 (百分比模式)
    public void UpdateBuildupUI(string buffName, float current, float max, Color color)
    {
        // 🔥 核心修改：去掉 "[积累]" 前缀！让它和 ShowStatus 用同一个名字
        string uiTitle = buffName;

        StatusItemController targetCtrl = null;

        // 寻找现有条子
        foreach (Transform child in container)
        {
            var controller = child.GetComponent<StatusItemController>();
            if (controller != null && controller.GetTitle() == uiTitle)
            {
                targetCtrl = controller;
                break;
            }
        }

        // 归零逻辑：只有当确实有一个“纯积累”条时才删除
        // 如果这个条子已经变成了 Active Buff（在倒计时），我们就不应该在这里删它
        if (current <= 0)
        {
            // 这里我们不做删除操作，交给 StatusManager 的逻辑去控制
            // 或者仅仅当它处于“非激活”状态时才删 (这个判断比较复杂，留给 Manager 控制更稳)
            if (targetCtrl != null)
            {
                // 只有当条子是满的或者空的，且没有在倒计时（很难判断），才移除
                // 简单处理：StatusManager 会在激活时接管，在衰减归零时调用这个。
                // 如果衰减归零了，说明没激活，直接删。
                targetCtrl.RemoveSelf();
            }
            return;
        }

        // 新建条子
        if (targetCtrl == null)
        {
            GameObject newItem = Instantiate(statusItemPrefab, container);
            targetCtrl = newItem.GetComponent<StatusItemController>();

        }

        // 刷新数值
        if (targetCtrl != null)
        {
            targetCtrl.UpdateBuildup(uiTitle, current, max, color);
        }
    }

    public void HideUI()
    {
        foreach (Transform child in container) Destroy(child.gameObject);
    }
}