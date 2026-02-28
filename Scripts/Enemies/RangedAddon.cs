using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// 🔥 纯正的外挂配件：只继承 MonoBehaviour
[RequireComponent(typeof(BaseEnemy))]
public class RangedAddon : MonoBehaviour
{
    [Header("=== 🏹 远程外挂配置 ===")]
    public GameObject projectilePrefab;     // 毒球预制体
    public Transform throwPoint;            // 发射点 (手部骨骼)

    [Tooltip("最小射程：玩家距离小于此值时，外挂休眠，把控制权还给近战老父亲")]
    public float minRange = 3.0f;
    [Tooltip("最大射程：在这个距离内才扔毒球")]
    public float maxRange = 10.0f;

    public float cooldown = 4.0f;
    public float throwEndDelay = 1.0f;      // 扔完发呆的硬直，防平移

    private float _timer = 0f;

    // 依赖的身体组件
    private BaseEnemy _baseAI;
    private NavMeshAgent _agent;
    private Animator _anim;
    private Transform _player;

    private void Start()
    {
        _baseAI = GetComponent<BaseEnemy>();
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponentInChildren<Animator>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;
    }

    private void Update()
    {
        if (_baseAI == null || _player == null) return;

        // 如果怪物死了，直接罢工
        if (_baseAI.currentState == BaseEnemy.AIState.Dead) return;

        // 冷却计时
        if (_timer > 0) _timer -= Time.deltaTime;

        // 🔥 核心冲突避免：
        // 如果老父亲已经被劫持，或者老父亲正在施展近战动作 (Attack 状态)，
        // 外挂绝不插手，乖乖闭嘴！
        if (_baseAI.isAIHijacked || _baseAI.currentState == BaseEnemy.AIState.Attack) return;

        // 测距
        float dist = Vector3.Distance(transform.position, _player.position);

        // 如果在“甜点距离”内，且冷却完毕，开始劫持大脑！
        if (dist >= minRange && dist <= maxRange && _timer <= 0)
        {
            ExecuteThrow();
        }
    }

    private void ExecuteThrow()
    {
        // 1. 🔒 劫持大脑！BaseEnemy 现在变成植物人了，停留在原地
        _baseAI.isAIHijacked = true;
        _timer = cooldown;

        // 2. 踩死物理刹车
        if (_agent != null && _agent.isActiveAndEnabled)
        {
            _agent.velocity = Vector3.zero;
            _agent.isStopped = true;
        }

        // 3. 转身看玩家
        FaceTarget(_player.position);

        // 4. 播放丢毒动画
        if (_anim != null) _anim.SetTrigger("Throw");

        // 5. 开启硬直等待协程
        StartCoroutine(ThrowWaitCoroutine());
    }

    private IEnumerator ThrowWaitCoroutine()
    {
        // 等待动画后摇结束
        yield return new WaitForSeconds(throwEndDelay);

        // 如果怪物在此期间没被打死，就把控制权还给老父亲
        if (_baseAI.currentState != BaseEnemy.AIState.Dead)
        {
            _baseAI.isAIHijacked = false; // 🔓 解除劫持！老父亲重新接管移动和近战
        }
    }

    // ==========================================
    // 🎯 供 Animation Event 调用的生成方法
    // ==========================================
    public void SpawnProjectile()
    {
        if (projectilePrefab == null || throwPoint == null || _player == null) return;

        GameObject ball = Instantiate(projectilePrefab, throwPoint.position, throwPoint.rotation);
        var script = ball.GetComponent<PoisonProjectile>();
        if (script != null)
        {
            Vector3 targetPos = _player.position + Vector3.up * 1.0f; // 瞄准胸口
            Vector3 dir = (targetPos - throwPoint.position).normalized;
            script.Launch(dir);
        }
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}