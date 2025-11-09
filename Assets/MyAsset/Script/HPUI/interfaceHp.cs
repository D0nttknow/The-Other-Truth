using System;

public interface IHpProvider
{
    // event แจ้งเมื่อ HP เปลี่ยน (current, max)
    event Action<int, int> OnHpChanged;

    // property สำหรับอ่านค่า HP ปัจจุบันและ Max
    int CurrentHp { get; }
    int MaxHp { get; }
}