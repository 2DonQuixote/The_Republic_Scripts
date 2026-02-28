using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class RangedAddon : MonoBehaviour
{
    [Header("=== 🏹 远程外挂配置 ===")]
    public GameObject projectilePrefab;
    public Transform throwPoint;

    public float minRange = 3.0f;
    public float maxRange = 10.0f;

    public float cooldown = 4.0f;
    public float throwEndDelay = 1.0f;

    private float _timer = 0f;
    private bool _isThrowing = false;

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
        if (_baseAI == null || _player == null || _baseAI.currentState == BaseEnemy.AIState.Dead) return;

        if (_timer > 0) _timer -= Time.deltaTime;

        float dist = Vector3.Distance(transform.position, _player.position);

        // 🔥【核心修复 1】只要玩家在远程范围内，且 AI 还在发呆，强行激活它的追击状态
        // 这样即便玩家退到 10.1 米，AI 的状态也是 Chase，它就会根据 loseAggroRange (15) 来判定了
        if (dist <= maxRange && _baseAI.currentState == BaseEnemy.AIState.Idle)
        {
            _baseAI.ChangeState(BaseEnemy.AIState.Chase);
        }

        // 判定是否在投掷区间
        if (dist >= minRange && dist <= maxRange)
        {
            if (_isThrowing) return;

            // 如果老父亲正在播近战攻击（且不是被我们劫持的），我们不插手
            if (_baseAI.currentState == BaseEnemy.AIState.Attack && !_baseAI.isAIHijacked) return;

            _baseAI.isAIHijacked = true;

            if (_agent != null && _agent.isActiveAndEnabled)
            {
                _agent.velocity = Vector3.zero;
                _agent.isStopped = true;
            }

            FaceTarget(_player.position);

            if (_timer <= 0)
            {
                ExecuteThrow();
            }
        }
        else
        {
            // 🔥【核心修复 2】走出投掷范围（太近或太远），释放控制权并恢复寻路
            if (_baseAI.isAIHijacked)
            {
                _baseAI.isAIHijacked = false;
                if (_agent != null && _agent.isActiveAndEnabled)
                {
                    _agent.isStopped = false;
                    // 如果还在追击状态，立刻重新设置目的地，防止原地发呆
                    if (_baseAI.currentState == BaseEnemy.AIState.Chase)
                    {
                        _agent.SetDestination(_player.position);
                    }
                }
            }
        }
    }

    private void ExecuteThrow()
    {
        _isThrowing = true;
        _timer = cooldown;
        if (_anim != null) _anim.SetTrigger("Throw");
        StartCoroutine(ThrowWaitCoroutine());
    }

    private IEnumerator ThrowWaitCoroutine()
    {
        yield return new WaitForSeconds(throwEndDelay);
        _isThrowing = false;
    }

    public void SpawnProjectile()
    {
        if (projectilePrefab == null || throwPoint == null || _player == null) return;
        GameObject ball = Instantiate(projectilePrefab, throwPoint.position, throwPoint.rotation);
        var script = ball.GetComponent<PoisonProjectile>();
        if (script != null)
        {
            script.LaunchToPoint(_player.position);
        }
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
    }
}