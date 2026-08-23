using System.Collections.Generic;

namespace AntTime
{
    public enum Carry : byte { None, Soil, Food }
    public enum Role : byte { Digger, Forager, Nurse, Queen }

    public enum AntState : byte
    {
        Idle,
        MoveToDig,
        Digging,
        MoveToDump,
        MoveToFood,
        MoveToPantry,
        MoveToLarva,
        MoveToEat,
        Working,
        Wander,
        Dead,
    }

    public class Ant
    {
        public int id;
        public int tile;
        public Role role;
        public AntState state = AntState.Idle;
        public Carry carry = Carry.None;

        public List<int> path;
        public int pathStep;
        public float moveTimer;
        public float workTimer;

        public int targetTile = -1;   // ช่องปลายทางที่จะไปยืน
        public int jobTile = -1;      // ช่องเป้าหมายของงาน (ดินที่จะขุด / larva / ฯลฯ)
        public int foodItemId = -1;
        public int broodTarget = -1;  // ตัวอ่อนที่จองไว้ว่าจะไปป้อน
        public bool carryForFeeding;
        public int dumpFails;         // กันวนลูปหาที่ทิ้งดินไม่ได้  // อาหารที่ถืออยู่เอาไปป้อนตัวอ่อน ไม่ใช่เอาไปเก็บ

        public float hunger;          // 0..1 ยิ่งมากยิ่งหิว
        public float age;
        public bool alive = true;
    }

    public enum BroodStage : byte { Egg, Larva, Pupa }

    public class Brood
    {
        public int tile;
        public BroodStage stage;
        public float timer;
        public int fed;          // จำนวนครั้งที่ถูกป้อน
        public bool claimed;     // มีพี่เลี้ยงกำลังมาป้อน
    }

    public class FoodItem
    {
        public int id;
        public int tile;
        public int amount;
        public bool claimed;
    }
}
