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
    // 💡 我们把原来的 punchScale 拆分成两个变量，方便你在 Inspector 里微调
    public float buildupPunch = 0.15f; // 积累时的小跳 (轻轻哆嗦)
    public float activePunch = 0.3f;   // 爆发时的大跳 (猛得一下)

    public float punchDuration = 0.15f;
    public int punchVibrato = 8;
    public float punchElasticity = 0.6f;

    // =========================================================
    // 1. 激活/倒计时模式 (爆发！)
    // =========================================================
    public void Setup(string content, float duration, Color barColor)
    {
        InitCommon(content, barColor);

        // 🔥 爆发时刻：使用大跳力度 (activePunch)
        PlayPunchEffect(activePunch);

        StartCountdown(duration);
    }

    public void ResetTimer(float newDuration)
    {
        _barTween?.Kill();

        // 🔥 刷新时刻：也算一次强反馈，使用大跳力度
        PlayPunchEffect(activePunch);

        if (durationBar != null) durationBar.fillAmount = 1f;
        StartCountdown(newDuration);
    }

    // =========================================================
    // 2. 积累模式 (积累中...)
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

            // --- 核心逻辑 ---
            if (_isFirstUpdate)
            {
                _isFirstUpdate = false;
                durationBar.fillAmount = 0f;

                // 第一次出现：虽然是刚开始，但为了引起注意，
                // 你可以选择是用小跳(buildupPunch)还是大跳(activePunch)
                // 这里我们按你的需求，算作"积累阶段"，用小跳
                transform.localScale = Vector3.one;
                PlayPunchEffect(buildupPunch);
            }
            else if (targetPct > durationBar.fillAmount + 0.001f)
            {
                // 🔥 后续涨条：使用小跳力度 (buildupPunch)
                PlayPunchEffect(buildupPunch);
            }

            // 进度条动画
            _barTween?.Kill();
            _barTween = durationBar.DOFillAmount(targetPct, 0.2f).SetEase(Ease.OutQuad);
        }

        // 双重保险
        if (transform.localScale.x == 0 && !_isFirstUpdate) transform.localScale = Vector3.one;
    }

    // =========================================================
    // 🎨 动画区 (互斥管理)
    // =========================================================

    // 🥊 受击动画：原地哆嗦
    // 🔥 修改：增加参数 scale，允许外部指定震动大小
    private void PlayPunchEffect(float scale)
    {
        // 保护机制：如果正在震动且刚开始不久，不要频繁打断（防止高频鬼畜）
        // 但为了手感，有时候覆盖也是一种选择，这里我们允许覆盖
        _animTween?.Kill();

        transform.localScale = Vector3.one; // 强制归位，防止越震越大

        // 使用传入的 scale 参数
        _animTween = transform.DOPunchScale(Vector3.one * scale, punchDuration, punchVibrato, punchElasticity);
    }

    // =========================================================
    // 内部逻辑 (保持不变)
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
        transform.DOScale(0f, 0.2f).SetEase(Ease.InBack).OnComplete(() => {
            if (gameObject != null) Destroy(gameObject);
        });
    }

    public string GetTitle()
    {
        return statusText != null ? statusText.text : "";
    }
}