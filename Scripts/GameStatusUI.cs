using UnityEngine;
using DG.Tweening; // 记得引用这个，我们要用 DOShake 或者 DOScale

public class GameStatusUI : MonoBehaviour
{
    public static GameStatusUI Instance;
    public GameObject statusItemPrefab;
    public Transform container;

    private void Awake() => Instance = this;

    // 1. 显示已激活 Buff (倒计时模式)
    public void ShowStatus(string content, float duration, Color barColor, bool isStackable)
    {
        if (!isStackable)
        {
            foreach (Transform child in container)
            {
                var controller = child.GetComponent<StatusItemController>();
                if (controller != null && controller.GetTitle() == content)
                {
                    controller.ResetTimer(duration);
                    return;
                }
            }
        }

        GameObject newItem = Instantiate(statusItemPrefab, container);
        var ctrl = newItem.GetComponent<StatusItemController>();
        if (ctrl != null)
        {
            ctrl.Setup(content, duration, barColor);
        }
    }

    // 2. 显示积累条 (百分比模式)
    public void UpdateBuildupUI(string buffName, float current, float max, Color color)
    {
        string uiTitle = $"[积累] {buffName}";

        StatusItemController targetCtrl = null;

        // 寻找现有的条子
        foreach (Transform child in container)
        {
            var controller = child.GetComponent<StatusItemController>();
            if (controller != null && controller.GetTitle() == uiTitle)
            {
                targetCtrl = controller;
                break;
            }
        }

        // 逻辑 A：积累值归零，且条子存在 -> 删掉
        if (current <= 0)
        {
            if (targetCtrl != null) targetCtrl.RemoveSelf();
            return;
        }

        // 逻辑 B：积累值 > 0，但没有条子 -> 新建一个
        if (targetCtrl == null)
        {
            GameObject newItem = Instantiate(statusItemPrefab, container);
            targetCtrl = newItem.GetComponent<StatusItemController>();

            // 🔥 简单的进场动画 (这里就是之前报错的地方，现在修好了)
            newItem.transform.localScale = Vector3.zero;
            newItem.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
        }

        // 逻辑 C：刷新数值
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