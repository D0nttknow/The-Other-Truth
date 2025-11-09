using System;

public interface ICharacterStat
{
    string Name { get; }
    int hp { get; }
    int maxHp { get; }   // <-- เพิ่มบรรทัดนี้
    int atk { get; }
    int def { get; }
    int speed { get; }
    int level { get; }

    void AttackMonster(IMonsterStat monster);
    void TakeDamage(int damage);
    void StrongAttackMonster(IMonsterStat monster);
}