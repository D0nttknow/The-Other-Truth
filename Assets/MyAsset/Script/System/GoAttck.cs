using UnityEngine;
using System;
using System.Collections;
using System.Linq;

public class GoAttck : MonoBehaviour
{
    public Vector3 startPosition;
    public float speed = 10f;
    public GameObject playerObject;

    void Start()
    {
        startPosition = transform.position;
    }

    public void ResetStartPosition()
    {
        startPosition = transform.position;
        Debug.Log("[GoAttck] Reset startPosition: " + startPosition + " (on " + gameObject.name + ")");
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
        string info = (target != null) ? (target.name + " id=" + target.GetInstanceID()) : "null";
        Debug.Log("[GoAttck] StrongAttackMonster called on " + gameObject.name + " (id=" + gameObject.GetInstanceID() + ") with target=" + info);

        if (target == null)
        {
            Debug.LogWarning("[GoAttck] StrongAttackMonster called with null target");
            onComplete?.Invoke();
            return;
        }

        Transform targetTransform = target.transform;
        GameObject targetObj = target;

        StartCoroutine(StrongAttackCoroutine(targetTransform, targetObj, onComplete));
    }

    private IEnumerator StrongAttackCoroutine(Transform targetTransform, GameObject targetObj, Action onComplete)
    {
        string tinfo = (targetObj != null) ? (targetObj.name + " id=" + targetObj.GetInstanceID()) : "null";
        Debug.Log("[GoAttck] StrongAttackCoroutine started: attacker=" + gameObject.name + " id=" + gameObject.GetInstanceID() + " target=" + tinfo);

        float localSpeed = Mathf.Max(0.01f, speed);
        float stopDistance = 1.0f;

        float initialDist = (targetObj != null && targetTransform != null) ? Vector3.Distance(transform.position, targetTransform.position) : -1f;
        Debug.Log("[GoAttck] initial distance to target = " + initialDist);

        while (targetObj != null && Vector3.Distance(transform.position, targetTransform.position) > stopDistance)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, localSpeed * Time.deltaTime);
            yield return null;
        }

        float distNow = (targetObj != null && targetTransform != null) ? Vector3.Distance(transform.position, targetTransform.position) : -1f;
        Debug.Log("[GoAttck] movement loop ended. currentPos=" + transform.position + " distNow=" + distNow);

        if (targetObj == null)
        {
            Debug.LogWarning("[GoAttck] Target was destroyed before attack could land.");
            onComplete?.Invoke();
            yield break;
        }

        int damage = 10; // adjust skill damage as needed
        Debug.Log("[GoAttck] Applying damage to target=" + targetObj.name + " id=" + targetObj.GetInstanceID() + " dmg=" + damage);
        ApplyDamageToTarget(targetObj, damage);

        // wait one frame for animation / effects if needed
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
            Debug.Log("[GoAttck] MoveAndAttack: invoking playerStat.AttackMonster");
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
            Debug.Log("[GoAttck] MoveAndStrongAttack: invoking playerStat.StrongAttackMonster");
            playerStat.StrongAttackMonster(targetMonsterStat);
            yield return new WaitForSeconds(1f);
        }

        onAttackFinished?.Invoke();
    }

    public void ReturnToStart(Action onFinished)
    {
        Debug.Log("[GoAttck] Start ReturnToStart for " + gameObject.name + " to " + startPosition);
        StartCoroutine(ReturnCoroutine(onFinished));
    }

    IEnumerator ReturnCoroutine(Action onFinished)
    {
        while (Vector3.Distance(transform.position, startPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, startPosition, speed * Time.deltaTime);
            yield return null;
        }
        Debug.Log("[GoAttck] Returned to startPosition: " + startPosition + " for " + gameObject.name);
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

        // Log components on target to help debugging
        var comps = targetObj.GetComponents<MonoBehaviour>() ?? new MonoBehaviour[0];
        Debug.Log("[GoAttck] Target '" + targetObj.name + "' components: " + string.Join(", ", comps.Select(c => c != null ? c.GetType().Name : "null")));

        // 0) Direct known stat classes (call TakeDamage if present)
        var mokou = targetObj.GetComponent<StatEnemieMokou>();
        if (mokou != null)
        {
            Debug.Log("[GoAttck] Found StatEnemieMokou, calling TakeDamage");
            mokou.TakeDamage(damage);
            return;
        }
        var e1 = targetObj.GetComponent<StatEnemie1Monster>();
        if (e1 != null)
        {
            Debug.Log("[GoAttck] Found StatEnemie1Monster, calling TakeDamage");
            e1.TakeDamage(damage);
            return;
        }

        // 1) IDamageable
        var dmgComp = targetObj.GetComponent<IDamageable>();
        if (dmgComp != null)
        {
            try { dmgComp.TakeDamage(damage); Debug.Log("[GoAttck] Applied " + damage + " via IDamageable to " + targetObj.name); return; }
            catch (Exception ex) { Debug.LogWarning("[GoAttck] IDamageable.TakeDamage threw: " + ex.Message); }
        }

        // 2) EnemyStats
        var es = targetObj.GetComponent<EnemyStats>();
        if (es != null)
        {
            try { es.TakeDamage(damage); Debug.Log("[GoAttck] Applied " + damage + " via EnemyStats to " + targetObj.name); return; }
            catch (Exception ex) { Debug.LogWarning("[GoAttck] EnemyStats.TakeDamage threw: " + ex.Message); }
        }

        // 3) IMonsterStat (try common methods via reflection, or adjust hp fields)
        var im = targetObj.GetComponent<IMonsterStat>();
        if (im != null)
        {
            var statType = im.GetType();
            Debug.Log("[GoAttck] Found IMonsterStat runtime type " + statType.Name + " on " + targetObj.name);

            string[] methodCandidates = new string[] { "TakeDamage", "ReceiveDamage", "ApplyDamage", "Damage", "Hit" };
            foreach (var mname in methodCandidates)
            {
                var m = statType.GetMethod(mname, new Type[] { typeof(int) });
                if (m != null)
                {
                    try { m.Invoke(im, new object[] { damage }); Debug.Log("[GoAttck] Applied " + damage + " via " + statType.Name + "." + mname); return; }
                    catch (Exception ex) { Debug.LogWarning("[GoAttck] Invocation " + statType.Name + "." + mname + " failed: " + ex.Message); }
                }
            }

            var hpField = statType.GetField("monsterHp") ?? statType.GetField("hp") ?? statType.GetField("currentHp");
            if (hpField != null)
            {
                try
                {
                    object val = hpField.GetValue(im);
                    if (val is int cur)
                    {
                        int newHp = Mathf.Max(0, cur - damage);
                        hpField.SetValue(im, newHp);
                        Debug.Log("[GoAttck] Reduced " + hpField.Name + " by " + damage + " on " + targetObj.name + " (new " + hpField.Name + "=" + newHp + ")");
                        return;
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[GoAttck] Failed modifying " + hpField.Name + " on " + targetObj.name + ": " + ex.Message); }
            }
        }

        // 4) PlayerStat fallback
        var ps = targetObj.GetComponent<PlayerStat>();
        if (ps != null)
        {
            var ptype = ps.GetType();
            var method = ptype.GetMethod("TakeDamage", new Type[] { typeof(int) });
            if (method != null)
            {
                try { method.Invoke(ps, new object[] { damage }); Debug.Log("[GoAttck] Applied " + damage + " via PlayerStat.TakeDamage to " + targetObj.name); return; }
                catch (Exception ex) { Debug.LogWarning("[GoAttck] PlayerStat.TakeDamage invocation failed: " + ex.Message); }
            }
            var hpField = ptype.GetField("hp");
            if (hpField != null)
            {
                try { object v = hpField.GetValue(ps); if (v is int cur) { int newHp = Mathf.Max(0, cur - damage); hpField.SetValue(ps, newHp); Debug.Log("[GoAttck] Reduced hp on " + ps.name + " to " + newHp); return; } }
                catch (Exception ex) { Debug.LogWarning("[GoAttck] Failed to reduce hp field on " + ps.name + ": " + ex.Message); }
            }
        }

        // 5) Last resort: scan for any component method that accepts (int) and contains Take/Damage/Hit in the name
        foreach (var c in comps)
        {
            if (c == null) continue;
            var t = c.GetType();
            var methods = t.GetMethods().Where(m => (m.Name.IndexOf("Take", StringComparison.OrdinalIgnoreCase) >= 0
                                                  || m.Name.IndexOf("Damage", StringComparison.OrdinalIgnoreCase) >= 0
                                                  || m.Name.IndexOf("Hit", StringComparison.OrdinalIgnoreCase) >= 0)
                                                  && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(int));
            foreach (var m in methods)
            {
                try { m.Invoke(c, new object[] { damage }); Debug.Log("[GoAttck] Applied " + damage + " via " + t.Name + "." + m.Name + " to " + targetObj.name); return; }
                catch (Exception ex) { Debug.LogWarning("[GoAttck] Fallback invoke " + t.Name + "." + m.Name + " threw: " + ex.Message); }
            }
        }

        Debug.LogWarning("[GoAttck] No damage API found on target: " + targetObj.name);
    }
}