using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class StatusItemController : MonoBehaviour
{
    public Image durationBar;
    public Text statusText;

    private Tween _barTween;

    // --- 倒计时模式 ---
    public void Setup(string content, float duration, Color barColor)
    {
        if (statusText != null) statusText.text = content;
        if (durationBar != null)
        {
            durationBar.color = barColor;
            durationBar.fillAmount = 1f;
        }

        transform.localScale = Vector3.zero;
        transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        StartCountdown(duration);
    }

    public void ResetTimer(float newDuration)
    {
        _barTween?.Kill();
        transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 10, 1);

        if (durationBar != null) durationBar.fillAmount = 1f;
        StartCountdown(newDuration);
    }

    // 🔥🔥🔥 (核心新增) 积累条模式 🔥🔥🔥
    public void UpdateBuildup(string content, float current, float max, Color barColor)
    {
        // 杀掉旧动画，改为手动控制进度
        _barTween?.Kill();

        if (statusText != null) statusText.text = content;

        if (durationBar != null)
        {
            // 半透明显示，表示还没生效
            Color ghostColor = barColor;
            ghostColor.a = 0.7f;
            durationBar.color = ghostColor;

            // 计算百分比
            float pct = Mathf.Clamp01(current / max);
            durationBar.fillAmount = pct;
        }

        // 确保它是显示的
        transform.localScale = Vector3.one;
    }

    private void StartCountdown(float duration)
    {
        if (durationBar != null)
        {
            _barTween = durationBar.DOFillAmount(0f, duration)
                .SetEase(Ease.Linear)
                .OnComplete(RemoveSelf);
        }
    }

    public void RemoveSelf()
    {
        if (this == null) return;
        transform.DOScale(0f, 0.2f).OnComplete(() => {
            if (gameObject != null) Destroy(gameObject);
        });
    }

    public string GetTitle()
    {
        return statusText != null ? statusText.text : "";
    }
}