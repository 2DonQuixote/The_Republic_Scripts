using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("=== 核心配置 ===")]
    public WeaponItem currentWeapon;

    [Header("=== 模型挂载点 ===")]
    public Transform rightHandTransform;

    [Header("=== 移动参数 ===")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float crouchSpeed = 2.5f;
    public float rotateSpeed = 15f;
    public bool useRootMotion = true;

    [Header("=== 通用手感配置 ===")]
    public float inputBufferTime = 0.8f;

    [Header("=== 翻滚配置 ===")]
    public float rollForce = 15f;
    public float rollDuration = 0.6f;
    public float rollDrag = 3f;
    public float rollCooldown = 1.0f;
    [Range(0f, 1f)] public float rollAttackWindow = 0.7f;

    // --- 内部状态 ---
    private GameControls controls;
    private Vector2 moveInput;
    private Rigidbody rb;
    private Animator animator;

    private GameObject currentWeaponModel;

    private bool isCrouching;
    private bool isRunning;
    private bool isRolling;
    private bool isAttacking;
    private bool canMove = true;

    private int comboCount = 0;
    private bool currentAttackIsHeavy = false;

    private float lastInputTime = -100f;
    private float lastAttackStartTime;
    private float rollStartTime;
    private float lastRollTime = -100f;
    private float defaultDrag;
    private bool bufferedInputIsHeavy = false;
    private float lastHeavyAttackTime = -100f; // 记录上一次释放重击的时间

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        defaultDrag = rb.drag;

        controls = new GameControls();
        SetupInput();
    }

    private void Start()
    {
        EquipWeapon(currentWeapon);
    }

    // ================= 核心：武器装备逻辑 =================
    public void EquipWeapon(WeaponItem newWeapon)
    {
        currentWeapon = newWeapon;

        if (currentWeapon != null && currentWeapon.weaponAnimator != null)
        {
            animator.runtimeAnimatorController = currentWeapon.weaponAnimator;
        }

        if (currentWeaponModel != null) Destroy(currentWeaponModel);

        if (currentWeapon != null && currentWeapon.modelPrefab != null)
        {
            if (rightHandTransform != null)
            {
                currentWeaponModel = Instantiate(currentWeapon.modelPrefab, rightHandTransform);
                currentWeaponModel.transform.localPosition = Vector3.zero;
                currentWeaponModel.transform.localRotation = Quaternion.identity;
                currentWeaponModel.transform.localScale = Vector3.one;
            }
            else
            {
                Debug.LogError("请在 PlayerController 组件里把 'Right Hand Transform' 拖进去！");
            }
        }

        ResetCombo();
        CancelInvoke(nameof(ResetCombo));
    }

    // ================= 核心：输入与更新 =================
    private void SetupInput()
    {
        controls.Gameplay.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Gameplay.Run.performed += ctx => isRunning = true;
        controls.Gameplay.Run.canceled += ctx => isRunning = false;

        controls.Gameplay.Crouch.performed += ctx => ToggleCrouch(!isCrouching);
        controls.Gameplay.Roll.performed += ctx => HandleRollInput();

        controls.Gameplay.Attack.performed += ctx => BufferAttackInput(false);
        controls.Gameplay.HeavyAttack.performed += ctx => BufferAttackInput(true);
    }

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable() => controls.Gameplay.Disable();

    private void Update()
    {
        if (!canMove) return;
        CheckBufferedInput();
    }

    private void FixedUpdate()
    {
        if (!canMove) return;
        if (isRolling || isAttacking) return;

        HandleMovement();
        HandleRotation();
    }

    // ================= 核心：战斗逻辑 =================
    private void BufferAttackInput(bool isHeavy)
    {
        if (!canMove) return;
        if (!isRolling || (Time.time - rollStartTime >= rollDuration * rollAttackWindow))
        {
            lastInputTime = Time.time;
            bufferedInputIsHeavy = isHeavy;
        }
    }

    private void CheckBufferedInput()
    {
        bool hasBufferedInput = (Time.time - lastInputTime) < inputBufferTime;
        if (!hasBufferedInput) return;

        // 🔥【修改点 1】拦截重击冷却
        if (bufferedInputIsHeavy && currentWeapon != null)
        {
            AttackAction heavyAction = currentWeapon.GetHeavyAttack(0);
            if (heavyAction != null && Time.time < lastHeavyAttackTime + heavyAction.cooldown)
            {
                // 冷却没好，直接忽略这次重击输入
                return;
            }
        }

        if (isRolling)
        {
            if ((Time.time - rollStartTime) >= rollDuration * rollAttackWindow)
            {
                ExecuteAttack(bufferedInputIsHeavy);
                lastInputTime = -100f;
            }
        }
        else if (CanAttackNow())
        {
            ExecuteAttack(bufferedInputIsHeavy);
            lastInputTime = -100f;
        }
    }

    private bool CanAttackNow()
    {
        if (isRolling) return false;
        if (!isAttacking) return true;

        if (currentWeapon == null) return false;

        AttackAction currentAction = GetCurrentActionData();
        if (currentAction == null) return true;

        float timePlayed = Time.time - lastAttackStartTime;
        return timePlayed >= (currentAction.totalDuration * currentAction.comboWindowStart);
    }

    private void ExecuteAttack(bool isHeavy)
    {
        if (currentWeapon == null) return;

        CancelInvoke(nameof(ResetCombo));
        PrepareAttackState();
        currentAttackIsHeavy = isHeavy;

        AttackAction action = null;

        if (isHeavy)
        {
            comboCount = 0;
            action = currentWeapon.GetHeavyAttack(0);

            // 🔥【修改点 2】成功释放重击后，刷新冷却计时器
            lastHeavyAttackTime = Time.time;
        }
        else
        {
            if (comboCount >= currentWeapon.lightAttacks.Count) comboCount = 0;
            action = currentWeapon.GetLightAttack(comboCount);
        }

        if (action == null)
        {
            OnAttackEnd();
            return;
        }

        animator.CrossFade(action.animName, action.transitionDuration);

        StartCoroutine(EnableDamageWithDelay(action));

        Invoke(nameof(OnAttackEnd), action.totalDuration);

        if (!isHeavy) comboCount++;
    }

    IEnumerator EnableDamageWithDelay(AttackAction action)
    {
        if (action.damageDelay > 0)
        {
            yield return new WaitForSeconds(action.damageDelay);
        }

        if (!isAttacking) yield break;

        PerformHitDetection(action);
    }

    private void PerformHitDetection(AttackAction action)
    {
        List<Collider> hits = action.GetHitTargets(transform);

        foreach (var hit in hits)
        {
            IDamageable target = hit.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(action.damageMultiplier);

                if (action.hitVFX != null)
                {
                    Instantiate(action.hitVFX, hit.transform.position + Vector3.up, Quaternion.identity);
                }
            }
        }
    }

    private AttackAction GetCurrentActionData()
    {
        if (currentWeapon == null) return null;
        if (currentAttackIsHeavy) return currentWeapon.GetHeavyAttack(0);

        int index = Mathf.Clamp(comboCount - 1, 0, currentWeapon.lightAttacks.Count - 1);
        if (comboCount == 0) index = 0;

        return currentWeapon.GetLightAttack(index);
    }

    private void PrepareAttackState()
    {
        if (isRolling)
        {
            isRolling = false;
            rb.drag = defaultDrag;
            CancelInvoke(nameof(OnRollEnd));
        }

        FaceMouseInstant();

        isAttacking = true;
        rb.velocity = Vector3.zero;
        lastAttackStartTime = Time.time;

        CancelInvoke(nameof(OnAttackEnd));
    }

    public void OnAttackEnd()
    {
        isAttacking = false;
        Invoke(nameof(ResetCombo), currentWeapon.comboResetTime);
    }

    private void ResetCombo()
    {
        comboCount = 0;
    }

    // ================= 翻滚逻辑 =================
    private void HandleRollInput()
    {
        if (!canMove) return;
        if (Time.time < lastRollTime + rollCooldown) return;
        if (isRolling) return;

        if (isAttacking)
        {
            AttackAction currentAction = GetCurrentActionData();
            if (currentAction != null)
            {
                float timePlayed = Time.time - lastAttackStartTime;
                if (timePlayed < currentAction.totalDuration * currentAction.rollCancelStartTime)
                {
                    return;
                }
            }
        }

        PerformRoll();
    }

    private void PerformRoll()
    {
        isAttacking = false;
        ResetCombo();
        CancelInvoke(nameof(OnAttackEnd));

        isRolling = true;
        lastRollTime = Time.time;
        rollStartTime = Time.time;

        animator.CrossFade("Roll", 0.1f);
        rb.drag = rollDrag;

        Vector3 rollDir = transform.forward;
        if (moveInput.magnitude > 0.1f)
            rollDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;

        transform.rotation = Quaternion.LookRotation(rollDir);
        rb.AddForce(rollDir * rollForce, ForceMode.Impulse);

        Invoke(nameof(OnRollEnd), rollDuration);
    }

    public void OnRollEnd()
    {
        isRolling = false;
        rb.drag = defaultDrag;
    }

    // ================= 基础移动 =================
    private void HandleMovement()
    {
        if (moveInput.magnitude > 0.1f)
        {
            float currentSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
            Vector3 targetDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            rb.MovePosition(rb.position + targetDir * currentSpeed * Time.fixedDeltaTime);

            float animSpeed = isRunning ? 1f : 0.5f;
            if (isCrouching) animSpeed = 0.5f;
            animator.SetFloat("Speed", moveInput.magnitude * animSpeed, 0.1f, Time.fixedDeltaTime);
        }
        else
        {
            animator.SetFloat("Speed", 0, 0.1f, Time.fixedDeltaTime);
        }
    }

    private void HandleRotation()
    {
        if (moveInput.magnitude > 0.1f)
        {
            Vector3 targetDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            rb.rotation = Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(targetDir), rotateSpeed * Time.fixedDeltaTime);
        }
    }

    private void FaceMouseInstant()
    {
        if (Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (new Plane(Vector3.up, new Vector3(0, transform.position.y, 0)).Raycast(ray, out float enter))
        {
            Vector3 lookDir = (ray.GetPoint(enter) - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero) rb.rotation = Quaternion.LookRotation(lookDir);
        }
    }

    private void ToggleCrouch(bool state)
    {
        isCrouching = state;
        animator.SetBool("IsCrouch", isCrouching);
    }

    private void OnAnimatorMove()
    {
        if ((isAttacking || isRolling) && useRootMotion && canMove)
        {
            Vector3 newPos = rb.position + animator.deltaPosition;
            newPos.y = rb.position.y;
            rb.MovePosition(newPos);
        }
    }

    // ==========================================
    // 🔥 外部状态接管接口 (供 PlayerReaction 等脚本调用)
    // ==========================================

    public bool IsInvincible()
    {
        return isRolling;
    }

    public void LockControl()
    {
        canMove = false;
        isAttacking = false;
        isRolling = false;
        ResetCombo();
        CancelInvoke(); // 取消所有等待中的后摇回调

        if (rb != null) rb.velocity = Vector3.zero; // 物理急刹车
    }

    public void UnlockControl()
    {
        canMove = true;
    }

    // ==========================================

    private void OnDrawGizmosSelected()
    {
        if (currentWeapon == null) return;

        AttackAction displayAction = isAttacking ? GetCurrentActionData() : currentWeapon.GetLightAttack(0);
        if (displayAction == null) return;

        Gizmos.color = new Color(1, 0, 0, 0.3f);

        switch (displayAction.shapeType)
        {
            case HitShape.Circle:
                Gizmos.DrawWireSphere(transform.position, displayAction.attackRadius);
                break;

            case HitShape.Sector:
                Gizmos.DrawWireSphere(transform.position, displayAction.attackRadius);
                Vector3 forward = transform.forward;
                Vector3 leftRay = Quaternion.AngleAxis(-displayAction.attackAngle * 0.5f, Vector3.up) * forward;
                Vector3 rightRay = Quaternion.AngleAxis(displayAction.attackAngle * 0.5f, Vector3.up) * forward;
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, leftRay * displayAction.attackRadius);
                Gizmos.DrawRay(transform.position, rightRay * displayAction.attackRadius);
                break;

            case HitShape.Rectangle:
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(new Vector3(0, 0, displayAction.boxSize.z * 0.5f), displayAction.boxSize);
                Gizmos.matrix = Matrix4x4.identity;
                break;
        }
    }
}