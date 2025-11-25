using UnityEngine;
using System;
using System.Collections;

public class Enemiegoattck : MonoBehaviour
{
    public Transform monster;             // ลาก transform ของ Monster ใน Inspector
    public Vector3 startPosition;
    public float speed = 10f;

    void Start()
    {
        if (monster == null)
        {
            Debug.LogError("Enemiegoattck: monster is null!");
            return;
        }
        startPosition = monster.position;
    }

    // รับ target player และ callback
    public void MonsterAttack(IMonsterStat monsterStat, GameObject targetPlayer, Action onFinished)
    {
        Debug.Log("MonsterAttack called!");
        StartCoroutine(MoveAndAttack(monsterStat, targetPlayer, onFinished));
    }

    IEnumerator MoveAndAttack(IMonsterStat monsterStat, GameObject targetPlayer, Action onFinished)
    {
        Debug.Log("กำลังเดินไปตี");
        // เดินไปหา Player
        while (targetPlayer != null && Vector3.Distance(monster.position, targetPlayer.transform.position) > 1f)
        {
            monster.position = Vector3.MoveTowards(monster.position, targetPlayer.transform.position, speed * Time.deltaTime);
            yield return null;
        }

        // ถ้า targetPlayer ถูก Destroy ระหว่างเดิน ให้จบเทิร์น
        if (targetPlayer == null)
        {
            Debug.LogWarning("Enemiegoattck: targetPlayer ถูก Destroy ระหว่างเดินไปตี!");
            onFinished?.Invoke();
            yield break;
        }

        Debug.Log("กำลังโจมตี");
        ICharacterStat playerStat = targetPlayer.GetComponent<ICharacterStat>();
        if (monsterStat != null && playerStat != null)
        {
            monsterStat.AttackPlayer(playerStat); // โจมตีผ่าน interface
            yield return new WaitForSeconds(1f);
        }
        else
        {
            Debug.LogWarning("Enemiegoattck: playerStat หรือ monsterStat เป็น null!");
        }

        Debug.Log("กำลังเดินกลับ");
        // เดินกลับ
        while (Vector3.Distance(monster.position, startPosition) > 0.1f)
        {
            monster.position = Vector3.MoveTowards(monster.position, startPosition, speed * Time.deltaTime);
            yield return null;
        }

        Debug.Log("MonsterAttack finished! Calling callback");
        onFinished?.Invoke();
    }
}