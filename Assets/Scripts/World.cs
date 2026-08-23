using UnityEngine;

namespace AntTime
{
    // โลกแบบ tile grid มองด้านข้าง (cross-section) — y=0 คือด้านบนสุดของท้องฟ้า
    public class World
    {
        public readonly int W;
        public readonly int H;

        public readonly Tile[] tiles;
        public readonly Mark[] marks;
        public readonly byte[] noise;   // ความแปรปรวนของสีต่อ tile ให้ดูเป็นเม็ดดิน
        public readonly byte[] decor;   // 0=ไม่มี 1=หญ้า 2=ก้าน 3=ดอก
        public readonly byte[] stored;  // อาหารที่เก็บใน tile ห้องเสบียง
        public readonly int[] surfaceY; // y ของผิวดินในแต่ละคอลัมน์

        public const byte PantryTileCap = 6;

        public int NestX;      // คอลัมน์ปากรัง
        public int NestY;      // ความลึกของห้องราชินีเริ่มต้น
        public int Version;    // เพิ่มขึ้นทุกครั้งที่ terrain เปลี่ยน (ให้ pathfinder รู้)

        public World(int w, int h, int seed)
        {
            W = w; H = h;
            tiles = new Tile[w * h];
            marks = new Mark[w * h];
            noise = new byte[w * h];
            decor = new byte[w * h];
            stored = new byte[w * h];
            surfaceY = new int[w];
            Generate(seed);
        }

        public int Idx(int x, int y) => y * W + x;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

        public Tile Get(int x, int y) => InBounds(x, y) ? tiles[y * W + x] : Tile.Stone;

        public void Set(int x, int y, Tile t)
        {
            if (!InBounds(x, y)) return;
            int i = y * W + x;
            if (tiles[i] == t) return;
            tiles[i] = t;
            if (Tiles.IsSolid(t)) decor[i] = 0;
            if (t != Tile.Pantry) stored[i] = 0;
            RecomputeSurface(x);
            Version++;
        }

        void RecomputeSurface(int x)
        {
            for (int y = 0; y < H; y++)
            {
                if (Tiles.IsSolid(tiles[y * W + x])) { surfaceY[x] = y; return; }
            }
            surfaceY[x] = H;
        }

        // เดินได้ไหม: ใต้ดินเดินได้ทุกช่องที่โล่ง,
        // บนฟ้าต้องมีดินติดอยู่สักด้าน (มดไต่ผิวดิน เนินดิน และปากรังได้ แต่ลอยกลางอากาศไม่ได้)
        public bool Walkable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            Tile t = tiles[y * W + x];
            if (Tiles.IsSolid(t)) return false;
            if (t != Tile.Sky) return true;

            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (Tiles.IsSolid(Get(x + dx, y + dy))) return true;
                }
            return false;
        }

        public bool Walkable(int i) => Walkable(i % W, i / W);

        void Generate(int seed)
        {
            Random.State prev = Random.state;
            Random.InitState(seed);
            float ox = Random.Range(0f, 1000f);
            float oy = Random.Range(0f, 1000f);

            int baseSurface = Mathf.RoundToInt(H * 0.26f);
            for (int x = 0; x < W; x++)
            {
                float n = Mathf.PerlinNoise(ox + x * 0.035f, oy) * 2f - 1f;
                float n2 = Mathf.PerlinNoise(ox + x * 0.11f, oy + 5.5f) * 2f - 1f;
                int sy = baseSurface + Mathf.RoundToInt(n * 5f + n2 * 2f);
                sy = Mathf.Clamp(sy, 6, H - 20);
                surfaceY[x] = sy;

                for (int y = 0; y < H; y++)
                {
                    int i = y * W + x;
                    noise[i] = (byte)Random.Range(0, 255);

                    if (y < sy) { tiles[i] = Tile.Sky; continue; }

                    int depth = y - sy;
                    Tile t;
                    if (depth < 8) t = Tile.TopSoil;
                    else if (depth < (H - sy) * 0.55f) t = Tile.Soil;
                    else t = Tile.Clay;

                    // ก้อนหินเป็นก้อนเล็ก ๆ กระจายตัว (ความถี่สูง = ก้อนเล็ก)
                    // ถ้าใช้ noise ความถี่ต่ำจะได้ "กำแพงหิน" ยาว ๆ ที่ปิดทางขุดจนมดไปต่อไม่ได้
                    float r = Mathf.PerlinNoise(ox + x * 0.34f, oy + y * 0.34f + 31.7f);
                    if (depth > 10 && r > 0.83f) t = Tile.Stone;

                    tiles[i] = t;
                }

                // ต้นไม้/หญ้าบนผิวดิน
                if (Random.value < 0.55f) decor[Idx(x, sy - 1)] = 1;
            }

            PlantVegetation();
            Random.state = prev;
        }

        void PlantVegetation()
        {
            for (int x = 2; x < W - 2; x += Random.Range(3, 9))
            {
                int sy = surfaceY[x];
                int height = Random.Range(3, 9);
                for (int k = 1; k <= height; k++)
                {
                    int y = sy - k;
                    if (y < 1) break;
                    decor[Idx(x, y)] = 2;
                    if (k >= 3 && Random.value < 0.35f && x + 1 < W) decor[Idx(x + 1, y)] = 3;
                }
                if (sy - height - 1 > 0) decor[Idx(x, sy - height - 1)] = 3;
            }
        }

        // ขุดรังเริ่มต้น: ปากรัง + ทางลง + ห้องราชินี
        public void CarveStarterNest()
        {
            NestX = W / 2;
            int sy = surfaceY[NestX];
            NestY = sy + 16;

            for (int y = sy; y <= NestY; y++)
            {
                Set(NestX, y, Tile.Tunnel);
                Set(NestX + 1, y, Tile.Tunnel);
            }
            CarveRoom(NestX, NestY, 5, 3, Tile.Throne);
            CarveRoom(NestX - 8, NestY - 5, 4, 2, Tile.Nursery);
            CarveRoom(NestX + 9, NestY - 5, 4, 2, Tile.Pantry);
            for (int x = NestX - 8; x <= NestX + 9; x++) Set(x, NestY - 5, Tile.Tunnel);
            for (int y = NestY - 5; y <= NestY; y++) { Set(NestX, y, Tile.Tunnel); Set(NestX + 1, y, Tile.Tunnel); }
        }

        public void CarveRoom(int cx, int cy, int rx, int ry, Tile t)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (!InBounds(x, y)) continue;
                    if (Get(x, y) == Tile.Sky) continue;
                    if (Get(x, y) == Tile.Stone) continue;
                    Set(x, y, t);
                }
        }

        public int TotalStoredFood()
        {
            int sum = 0;
            for (int i = 0; i < stored.Length; i++) sum += stored[i];
            return sum;
        }
    }
}
