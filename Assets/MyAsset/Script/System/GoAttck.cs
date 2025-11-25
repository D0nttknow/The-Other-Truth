using UnityEngine;
using System;
using System.Collections;

public class GoAttck : MonoBehaviour
{
    public Vector3 startPosition;
    public float speed = 10f;
    public GameObject playerObject; // ลาก GameObject Player ที่มี ICharacterStat

    void Start()
    {
        startPosition = transform.position;
    }

    public void ResetStartPosition()
    {
        startPosition = transform.position;
        Debug.Log($"Reset startPosition: {startPosition}");
    }

    // ฟังก์ชันใหม่: รับ GameObject ของมอนสเตอร์ กับ IMonsterStat
    public void AttackMonster(GameObject monsterObject, Action onAttackFinished = null)
    {
        if (monsterObject == null)
        {
            Debug.LogWarning("AttackMonster: monsterObject is null!");
            onAttackFinished?.Invoke();
            return;
        }
        IMonsterStat targetMonsterStat = monsterObject.GetComponent<IMonsterStat>();
        if (targetMonsterStat == null)
        {
            Debug.LogWarning("AttackMonster: targetMonsterStat (IMonsterStat) is null!");
            onAttackFinished?.Invoke();
            return;
        }
        if (playerObject == null)
        {
            Debug.LogWarning("AttackMonster: playerObject is null!");
            onAttackFinished?.Invoke();
            return;
        }
        StartCoroutine(MoveAndAttack(monsterObject.transform, targetMonsterStat, onAttackFinished));
    }

    public void StrongAttackMonster(GameObject monsterObj, Action onAttackFinished = null)
    {
        if (monsterObj == null)
        {
            Debug.LogWarning("StrongAttackMonster: monsterObj is null!");
            onAttackFinished?.Invoke();
            return;
        }
        IMonsterStat targetMonsterStat = monsterObj.GetComponent<IMonsterStat>();
        if (targetMonsterStat == null)
        {
            Debug.LogWarning("StrongAttackMonster: targetMonsterStat (IMonsterStat) is null!");
            onAttackFinished?.Invoke();
            return;
        }
        if (playerObject == null)
        {
            Debug.LogWarning("StrongAttackMonster: playerObject is null!");
            onAttackFinished?.Invoke();
            return;
        }
        StartCoroutine(MoveAndStrongAttack(monsterObj.transform, targetMonsterStat, onAttackFinished));
    }

    IEnumerator MoveAndStrongAttack(Transform targetTransform, IMonsterStat targetMonsterStat, Action onAttackFinished)
    {
        while (targetTransform != null && Vector3.Distance(transform.position, targetTransform.position) > 1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, speed * Time.deltaTime);
            yield return null;
        }
        ICharacterStat playerStat = playerObject.GetComponent<ICharacterStat>();
        if (playerStat != null && targetMonsterStat != null)
        {
            playerStat.StrongAttackMonster(targetMonsterStat); // เรียกฟังก์ชันโจมตีแรง
            yield return new WaitForSeconds(1f);
        }
        onAttackFinished?.Invoke();
    }
    IEnumerator MoveAndAttack(Transform targetTransform, IMonsterStat targetMonsterStat, Action onAttackFinished)
    {
        while (targetTransform != null && Vector3.Distance(transform.position, targetTransform.position) > 1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, speed * Time.deltaTime);
            yield return null;
        }
        ICharacterStat playerStat = playerObject.GetComponent<ICharacterStat>();
        if (playerStat != null && targetMonsterStat != null)
        {
            playerStat.AttackMonster(targetMonsterStat); // ใช้ interface stat
            yield return new WaitForSeconds(1f);
        }
        else
        {
            Debug.LogWarning("ไม่มี ICharacterStat หรือ IMonsterStat สำหรับโจมตี");
        }
        onAttackFinished?.Invoke();
    }

    public void ReturnToStart(Action onFinished)
    {
        Debug.Log($"เริ่มเดินกลับไปที่ startPosition: {startPosition}");
        StartCoroutine(ReturnCoroutine(onFinished));
    }

    IEnumerator ReturnCoroutine(Action onFinished)
    {
        Debug.Log("กำลังเดินกลับ");
        while (Vector3.Distance(transform.position, startPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, startPosition, speed * Time.deltaTime);
            yield return null;
        }
        Debug.Log($"เดินกลับถึงจุดเริ่มแล้ว: {startPosition}");
        onFinished?.Invoke();
    }
}