using UnityEngine;

namespace AntTime
{
    // ชนิดของ tile ในโลก (1 tile = 1 pixel บนจอ)
    public enum Tile : byte
    {
        Sky = 0,
        TopSoil = 1,   // ดินชั้นบน ขุดง่าย
        Soil = 2,      // ดินกลาง
        Clay = 3,      // ดินเหนียว ขุดช้า
        Stone = 4,     // หิน ขุดไม่ได้
        Tunnel = 5,    // อุโมงค์ที่ขุดแล้ว
        Nursery = 6,   // ห้องอนุบาลไข่/ตัวอ่อน
        Pantry = 7,    // ห้องเก็บอาหาร
        Throne = 8,    // ห้องราชินี
    }

    // สิ่งที่ผู้เล่นสั่งไว้บน tile นั้น (ผลลัพธ์ที่อยากได้หลังขุดเสร็จ)
    public enum Mark : byte
    {
        None = 0,
        Dig = 1,
        Nursery = 2,
        Pantry = 3,
        Throne = 4,
    }

    public static class Tiles
    {
        public static bool IsOpen(Tile t) => t >= Tile.Tunnel || t == Tile.Sky;
        public static bool IsSolid(Tile t) => !IsOpen(t);
        public static bool IsDiggable(Tile t) => t == Tile.TopSoil || t == Tile.Soil || t == Tile.Clay;
        public static bool IsChamber(Tile t) => t >= Tile.Nursery;

        // เวลาที่ต้องใช้ขุด (วินาทีของเวลาในเกม)
        public static float DigTime(Tile t)
        {
            switch (t)
            {
                case Tile.TopSoil: return 0.8f;
                case Tile.Soil: return 1.3f;
                case Tile.Clay: return 2.4f;
                default: return 999f;
            }
        }

        public static Tile MarkResult(Mark m)
        {
            switch (m)
            {
                case Mark.Nursery: return Tile.Nursery;
                case Mark.Pantry: return Tile.Pantry;
                case Mark.Throne: return Tile.Throne;
                default: return Tile.Tunnel;
            }
        }
    }

    public static class Palette
    {
        public static readonly Color32 SkyTop = new Color32(150, 200, 230, 255);
        public static readonly Color32 SkyLow = new Color32(198, 226, 240, 255);
        public static readonly Color32 TopSoil = new Color32(222, 200, 152, 255);
        public static readonly Color32 Soil = new Color32(206, 180, 128, 255);
        public static readonly Color32 Clay = new Color32(168, 134, 92, 255);
        public static readonly Color32 Stone = new Color32(128, 122, 116, 255);
        public static readonly Color32 Tunnel = new Color32(96, 72, 48, 255);
        public static readonly Color32 Nursery = new Color32(120, 96, 60, 255);
        public static readonly Color32 Pantry = new Color32(110, 90, 56, 255);
        public static readonly Color32 Throne = new Color32(88, 64, 52, 255);
        public static readonly Color32 Grass = new Color32(96, 148, 62, 255);
        public static readonly Color32 Stem = new Color32(112, 160, 70, 255);
        public static readonly Color32 Flower = new Color32(232, 220, 110, 255);
        public static readonly Color32 Ant = new Color32(48, 34, 26, 255);
        public static readonly Color32 AntCarrySoil = new Color32(150, 110, 70, 255);
        public static readonly Color32 AntCarryFood = new Color32(224, 196, 96, 255);
        public static readonly Color32 Queen = new Color32(96, 40, 40, 255);
        public static readonly Color32 Egg = new Color32(244, 240, 214, 255);
        public static readonly Color32 Larva = new Color32(236, 226, 178, 255);
        public static readonly Color32 Pupa = new Color32(208, 190, 150, 255);
        public static readonly Color32 Food = new Color32(226, 190, 78, 255);
        public static readonly Color32 StoredFood = new Color32(214, 178, 74, 255);
        public static readonly Color32 MarkTint = new Color32(255, 255, 255, 255);

        public static Color32 Shade(Color32 c, int delta)
        {
            return new Color32(
                (byte)Mathf.Clamp(c.r + delta, 0, 255),
                (byte)Mathf.Clamp(c.g + delta, 0, 255),
                (byte)Mathf.Clamp(c.b + delta, 0, 255),
                255);
        }
    }
}
