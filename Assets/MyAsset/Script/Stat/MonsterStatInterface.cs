using System;

public interface IMonsterStat
{
    string monsterName { get; }
    int monsterHp { get; }
    int monsterMaxHp { get; }    // current max HP
    int monsterAtk { get; }
    int monsterDef { get; }
    int monsterSpeed { get; }

    // EXP value awarded when this monster is defeated
    int expValue { get; }

    void TakeDamage(int damage);
    void AttackPlayer(ICharacterStat player);

    // debuff APIs (optional)
    void ApplyBleed(int damagePerTurn, int duration);
    void ApplyStun(int duration);
}