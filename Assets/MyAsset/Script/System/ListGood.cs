public class Battler
{
    public string name;
    public int hp;
    public int atk;
    public int def;
    public int speed;
    public bool isMonster;

    public Battler(string name, int hp, int atk, int def, int speed, bool isMonster)
    {
        this.name = name;
        this.hp = hp;
        this.atk = atk;
        this.def = def;
        this.speed = speed;
        this.isMonster = isMonster;
    }
}