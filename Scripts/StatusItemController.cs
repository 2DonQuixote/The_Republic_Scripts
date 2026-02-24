using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class StatusItemController : MonoBehaviour
{
    public Image durationBar;
    public Text statusText;

    private Tween _barTween;
    private Tween _animTween; // 统一管理所有缩放动画

    private bool _isFirstUpdate = true;

    [Header("✨ 震动参数")]
    public float punchScale = 0.2f;
    public float punchDuration = 0.15f;
    public int punchVibrato = 8;
    public float punchElasticity = 0.6f;

    // =========================================================
    // 1. 激活/倒计时模式
    // =========================================================
    public void Setup(string content, float duration, Color barColor)
    {
        InitCommon(content, barColor);

        // 进场：播放一次震动，或者你可以改成 PlayEntryAnim()
        PlayPunchEffect();

        StartCountdown(duration);
    }

    public void ResetTimer(float newDuration)
    {
        _barTween?.Kill();
        PlayPunchEffect(); // 刷新时间震一下
        if (durationBar != null) durationBar.fillAmount = 1f;
        StartCountdown(newDuration);
    }

    // =========================================================
    // 2. 积累模式
    // =========================================================
    public void UpdateBuildup(string content, float current, float max, Color barColor)
    {
        if (statusText != null) statusText.text = content;

        if (durationBar != null)
        {
            Color ghostColor = barColor;
            ghostColor.a = 0.7f;
            durationBar.color = ghostColor;

            float targetPct = Mathf.Clamp01(current / max);

            // --- 核心逻辑分流 ---
            if (_isFirstUpdate)
            {
                _isFirstUpdate = false;
                durationBar.fillAmount = 0f;

                // 🔥 第一次出现：播放进场动画 (从小变大)
                // 这样就不会有 "震动" 和 "进场" 打架的情况了
                PlayEntryAnim();
            }
            else if (targetPct > durationBar.fillAmount + 0.001f)
            {
                // 🔥 后续涨条：播放受击震动
                PlayPunchEffect();
            }

            // 进度条动画 (这个不冲突，可以一直播)
            _barTween?.Kill();
            _barTween = durationBar.DOFillAmount(targetPct, 0.2f).SetEase(Ease.OutQuad);
        }

        // 双重保险
        if (transform.localScale.x == 0 && !_isFirstUpdate) transform.localScale = Vector3.one;
    }

    // =========================================================
    // 🎨 动画区 (互斥管理)
    // =========================================================

    // 🌟 进场动画：Q弹地冒出来
    private void PlayEntryAnim()
    {
        _animTween?.Kill();
        transform.localScale = Vector3.zero; // 先变没
        _animTween = transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
    }

    // 🥊 受击动画：原地哆嗦
    private void PlayPunchEffect()
    {
        // 如果正在播进场动画，就别震动了，防止打断进场显得鬼畜
        if (_animTween != null && _animTween.IsActive() && _animTween.Elapsed() < 0.2f) return;

        _animTween?.Kill();
        transform.localScale = Vector3.one; // 强制归位
        _animTween = transform.DOPunchScale(Vector3.one * punchScale, punchDuration, punchVibrato, punchElasticity);
    }

    // =========================================================
    // 内部逻辑
    // =========================================================
    private void InitCommon(string content, Color color)
    {
        if (statusText != null) statusText.text = content;
        if (durationBar != null)
        {
            durationBar.color = color;
            durationBar.fillAmount = 1f;
        }
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
        // 退场动画：缩小消失
        transform.DOScale(0f, 0.2f).SetEase(Ease.InBack).OnComplete(() => {
            if (gameObject != null) Destroy(gameObject);
        });
    }

    public string GetTitle()
    {
        return statusText != null ? statusText.text : "";
    }
}