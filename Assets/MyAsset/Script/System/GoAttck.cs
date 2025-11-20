using UnityEngine;
using System;
using System.Collections;

public class GoAttck : MonoBehaviour
{
    public Vector3 startPosition;
    public float speed = 10f;
    // optional: reference to the Player GameObject, only needed if this script is on a child object
    public GameObject playerObject;

    void Start()
    {
        startPosition = transform.position;
    }

    public void ResetStartPosition()
    {
        startPosition = transform.position;
        Debug.Log($"[GoAttck] Reset startPosition: {startPosition} (on {gameObject.name})");
    }

    // Normal attack entry (keeps old interface)
    public void AttackMonster(GameObject monsterObject, Action onAttackFinished = null)
    {
        if (monsterObject == null)
        {
            Debug.LogWarning("[GoAttck] AttackMonster: monsterObject is null!");
            onAttackFinished?.Invoke();
            return;
        }
        IMonsterStat targetMonsterStat = monsterObject.GetComponent<IMonsterStat>();
        if (targetMonsterStat == null)
        {
            Debug.LogWarning("[GoAttck] AttackMonster: targetMonsterStat (IMonsterStat) is null!");
            onAttackFinished?.Invoke();
            return;
        }

        StartCoroutine(MoveAndAttack(monsterObject.transform, targetMonsterStat, onAttackFinished));
    }

    // Strong attack: accept explicit GameObject target and callback
    public void StrongAttackMonster(GameObject target, Action onComplete)
    {
        string targetInfo = target != null ? $"{target.name} id={target.GetInstanceID()}" : "null";
        Debug.Log($"[GoAttck] StrongAttackMonster called on {gameObject.name} (this id={gameObject.GetInstanceID()}) with target={targetInfo}");

        if (target == null)
        {
            Debug.LogWarning("[GoAttck] StrongAttackMonster called with null target");
            onComplete?.Invoke();
            return;
        }

        Transform targetTransform = target.transform;
        GameObject targetObj = target;

        // Start coroutine that uses the passed reference only
        StartCoroutine(StrongAttackCoroutine(targetTransform, targetObj, onComplete));
    }

    private IEnumerator StrongAttackCoroutine(Transform targetTransform, GameObject targetObj, Action onComplete)
    {
        string targetInfo = targetObj != null ? $"{targetObj.name} id={targetObj.GetInstanceID()}" : "null";
        Debug.Log($"[GoAttck] StrongAttackCoroutine started: attacker={gameObject.name} id={gameObject.GetInstanceID()} target={targetInfo} startPos={transform.position} targetPos={(targetTransform != null ? targetTransform.position : Vector3.zero)}");

        float localSpeed = Mathf.Max(0.01f, speed); // use public speed (or override locally)
        float stopDistance = 1.0f;

        // initial distance check/log
        float initialDist = targetObj != null ? Vector3.Distance(transform.position, targetTransform.position) : -1f;
        Debug.Log($"[GoAttck] initial distance to target = {initialDist}");

        // move toward target until within range or target destroyed
        while (targetObj != null && Vector3.Distance(transform.position, targetTransform.position) > stopDistance)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, localSpeed * Time.deltaTime);
            yield return null;
        }

        Debug.Log($"[GoAttck] movement loop ended. currentPos={transform.position} targetPos={(targetTransform != null ? targetTransform.position : Vector3.zero)} distNow={(targetObj != null ? Vector3.Distance(transform.position, targetTransform.position) : -1f)}");

        if (targetObj == null)
        {
            Debug.LogWarning("[GoAttck] Target was destroyed before attack could land.");
            onComplete?.Invoke();
            yield break;
        }

        // Apply damage using robust helper
        int damage = 10; // <- adapt to actual skill damage
        Debug.Log($"[GoAttck] Applying damage to target={targetObj.name} id={targetObj.GetInstanceID()} dmg={damage}");
        ApplyDamageToTarget(targetObj, damage);

        // optional: play attack animation here and wait for it
        yield return null;

        onComplete?.Invoke();
    }

    IEnumerator MoveAndAttack(Transform targetTransform, IMonsterStat targetMonsterStat, Action onAttackFinished)
    {
        while (targetTransform != null && Vector3.Distance(transform.position, targetTransform.position) > 1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, speed * Time.deltaTime);
            yield return null;
        }

        ICharacterStat playerStat = (playerObject != null) ? playerObject.GetComponent<ICharacterStat>() : GetComponent<ICharacterStat>() as ICharacterStat;
        if (playerStat != null && targetMonsterStat != null)
        {
            Debug.Log($"[GoAttck] MoveAndAttack: invoking playerStat.AttackMonster on {playerStat} for target {targetMonsterStat}");
            playerStat.AttackMonster(targetMonsterStat);
            yield return new WaitForSeconds(1f);
        }
        else
        {
            Debug.LogWarning("[GoAttck] MoveAndAttack: missing ICharacterStat or IMonsterStat");
        }

        onAttackFinished?.Invoke();
    }

    IEnumerator MoveAndStrongAttack(Transform targetTransform, IMonsterStat targetMonsterStat, Action onAttackFinished)
    {
        while (targetTransform != null && Vector3.Distance(transform.position, targetTransform.position) > 1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, speed * Time.deltaTime);
            yield return null;
        }

        ICharacterStat playerStat = (playerObject != null) ? playerObject.GetComponent<ICharacterStat>() : GetComponent<ICharacterStat>() as ICharacterStat;
        if (playerStat != null && targetMonsterStat != null)
        {
            Debug.Log($"[GoAttck] MoveAndStrongAttack: invoking playerStat.StrongAttackMonster for target {targetMonsterStat}");
            playerStat.StrongAttackMonster(targetMonsterStat);
            yield return new WaitForSeconds(1f);
        }

        onAttackFinished?.Invoke();
    }

    public void ReturnToStart(Action onFinished)
    {
        Debug.Log($"[GoAttck] Start ReturnToStart for {gameObject.name} to {startPosition}");
        StartCoroutine(ReturnCoroutine(onFinished));
    }

    IEnumerator ReturnCoroutine(Action onFinished)
    {
        while (Vector3.Distance(transform.position, startPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, startPosition, speed * Time.deltaTime);
            yield return null;
        }
        Debug.Log($"[GoAttck] Returned to startPosition: {startPosition} for {gameObject.name}");
        onFinished?.Invoke();
    }

    // ---------------- Helper: robust damage application ----------------
    void ApplyDamageToTarget(GameObject targetObj, int damage)
    {
        if (targetObj == null)
        {
            Debug.LogWarning("[GoAttck] ApplyDamageToTarget called with null target");
            return;
        }

        // 1) IDamageable
        var dmgComp = targetObj.GetComponent<IDamageable>();
        if (dmgComp != null)
        {
            try
            {
                dmgComp.TakeDamage(damage);
                Debug.Log($"[GoAttck] Applied {damage} via IDamageable to {targetObj.name}");
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GoAttck] IDamageable.TakeDamage threw: {ex.Message}");
            }
        }

        // 2) EnemyStats
        var es = targetObj.GetComponent<EnemyStats>();
        if (es != null)
        {
            try
            {
                es.TakeDamage(damage);
                Debug.Log($"[GoAttck] Applied {damage} via EnemyStats to {targetObj.name}");
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GoAttck] EnemyStats.TakeDamage threw: {ex.Message}");
            }
        }

        // 3) PlayerStat via reflection or hp field fallback
        var ps = targetObj.GetComponent<PlayerStat>();
        if (ps != null)
        {
            var type = ps.GetType();
            var method = type.GetMethod("TakeDamage", new Type[] { typeof(int) });
            if (method != null)
            {
                try
                {
                    method.Invoke(ps, new object[] { damage });
                    Debug.Log($"[GoAttck] Applied {damage} via PlayerStat.TakeDamage (reflection) to {targetObj.name}");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GoAttck] Failed invoking PlayerStat.TakeDamage on {ps.name}: {ex.Message}");
                }
            }

            // try modify hp field directly
            var hpField = type.GetField("hp");
            if (hpField != null)
            {
                try
                {
                    object fieldVal = hpField.GetValue(ps);
                    if (fieldVal is int currentHp)
                    {
                        int newHp = Mathf.Max(0, currentHp - damage);
                        hpField.SetValue(ps, newHp);
                        Debug.Log($"[GoAttck] Reduced hp field by {damage} on {ps.name} (new hp={newHp})");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GoAttck] Failed to modify hp field on {ps.name}: {ex.Message}");
                }
            }

            Debug.LogWarning($"[GoAttck] PlayerStat found on {ps.name} but no TakeDamage/hp field could be used.");
            return;
        }

        Debug.LogWarning("[GoAttck] No IDamageable / EnemyStats / PlayerStat found on target to apply damage: " + targetObj.name);
    }
}