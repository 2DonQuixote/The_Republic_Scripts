using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Animator), typeof(PlayerController))]
public class PlayerReaction : MonoBehaviour
{
    private Animator animator;
    private PlayerController playerController;

    // 🔥 核心防线 1：记录当前正在运行的计时器，方便随时掐死它
    private Coroutine currentRecoverCoroutine;

    // 🔥 核心防线 2：记录是否正在被硬控（投技霸体）
    public bool isGrabbed { get; private set; } = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
    }

    // ==========================================
    // 1. 普通受击 (被轻重击打中)
    // ==========================================
    public void ApplyHit()
    {
        // 🛡️ 拦截：如果玩家正在翻滚（无敌帧），或者【已经被抓了】，绝对不触发受击硬直！
        // 这样怪物在咬你的时候，你只会掉血（血条扣血），但不会播挨打动画打断被咬。
        if (playerController.IsInvincible() || isGrabbed) return;

        Debug.Log("<color=orange>玩家受到普通攻击，产生硬直！</color>");

        playerController.LockControl();
        animator.SetTrigger("Hit");

        // 如果之前有正在计时的恢复协程，直接掐死，重新掐表（防连击导致的过早解锁）
        if (currentRecoverCoroutine != null) StopCoroutine(currentRecoverCoroutine);
        currentRecoverCoroutine = StartCoroutine(HitRecoverCoroutine(0.5f));
    }

    // ==========================================
    // 2. 投技受击 (被丧尸飞扑按倒)
    // ==========================================
    public void ApplyGrab(float duration)
    {
        Debug.Log($"<color=red>玩家被投技命中！失去控制 {duration} 秒！</color>");

        isGrabbed = true; // 开启投技状态！

        // 🔪 强行掐死任何企图恢复自由的常规受击协程（防止之前被打的 0.5 秒倒计时突然把你解开了）
        if (currentRecoverCoroutine != null) StopCoroutine(currentRecoverCoroutine);

        // 🧹 清理残留的受击指令，防止动画错乱
        animator.ResetTrigger("Hit");

        playerController.LockControl();
        animator.SetBool("IsGrabbed", true);

        // 开启专属的投技解绑倒计时
        currentRecoverCoroutine = StartCoroutine(GrabRecoverCoroutine(duration));
    }

    // ==========================================
    // 恢复控制的协程
    // ==========================================
    private IEnumerator HitRecoverCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        playerController.UnlockControl();
        currentRecoverCoroutine = null;
    }

    private IEnumerator GrabRecoverCoroutine(float duration)
    {
        // 1. 乖乖等怪物咬完
        yield return new WaitForSeconds(duration);

        // 2. 告诉动画器：被咬结束了
        animator.SetBool("IsGrabbed", false);

        // 🔥 起身过渡硬直：给动画器 0.2 秒的时间切回 Locomotion，防止瞬间给权限导致的滑步
        yield return new WaitForSeconds(0.2f);

        // 3. 彻底恢复自由
        isGrabbed = false; // 解除投技状态
        playerController.UnlockControl();
        currentRecoverCoroutine = null;

        Debug.Log("<color=green>玩家彻底挣脱投技，恢复移动控制！</color>");
    }
}