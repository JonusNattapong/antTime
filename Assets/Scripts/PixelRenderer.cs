using UnityEngine;

namespace AntTime
{
    // วาดทั้งเกมลง Texture2D เดียว (1 tile = 1 pixel) แล้วค่อยขยายขึ้นจอ
    public class PixelRenderer
    {
        readonly World world;
        readonly Colony colony;
        readonly Color32[] buf;
        public readonly Texture2D texture;

        public PixelRenderer(World w, Colony c)
        {
            world = w;
            colony = c;
            buf = new Color32[w.W * w.H];
            texture = new Texture2D(w.W, w.H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        int Px(int x, int y) => (world.H - 1 - y) * world.W + x;

        public void Draw(float time)
        {
            DrawTerrain();
            DrawStored();
            DrawMarks(time);
            DrawFood();
            DrawBrood();
            DrawAnts();

            texture.SetPixels32(buf);
            texture.Apply(false);
        }

        void DrawTerrain()
        {
            for (int y = 0; y < world.H; y++)
            {
                for (int x = 0; x < world.W; x++)
                {
                    int i = y * world.W + x;
                    Tile t = world.tiles[i];
                    Color32 c;

                    if (t == Tile.Sky)
                    {
                        byte d = world.decor[i];
                        if (d == 1) c = Palette.Grass;
                        else if (d == 2) c = Palette.Stem;
                        else if (d == 3) c = Palette.Flower;
                        else
                        {
                            float f = Mathf.Clamp01(y / (float)Mathf.Max(1, world.surfaceY[x]));
                            c = Color32.Lerp(Palette.SkyTop, Palette.SkyLow, f);
                        }
                    }
                    else
                    {
                        switch (t)
                        {
                            case Tile.TopSoil: c = Palette.TopSoil; break;
                            case Tile.Soil: c = Palette.Soil; break;
                            case Tile.Clay: c = Palette.Clay; break;
                            case Tile.Stone: c = Palette.Stone; break;
                            case Tile.Tunnel: c = Palette.Tunnel; break;
                            case Tile.Nursery: c = Palette.Nursery; break;
                            case Tile.Pantry: c = Palette.Pantry; break;
                            default: c = Palette.Throne; break;
                        }
                        int n = (world.noise[i] % 13) - 6;
                        c = Palette.Shade(c, n);
                    }

                    buf[Px(x, y)] = c;
                }
            }
        }

        void DrawStored()
        {
            for (int k = 0; k < colony.pantryTiles.Count; k++)
            {
                int t = colony.pantryTiles[k];
                if (world.stored[t] == 0) continue;
                float f = world.stored[t] / (float)World.PantryTileCap;
                buf[Px(t % world.W, t / world.W)] = Color32.Lerp(Palette.Pantry, Palette.StoredFood, f);
            }
        }

        void DrawMarks(float time)
        {
            bool blink = Mathf.Repeat(time, 1f) < 0.6f;
            for (int k = 0; k < colony.digJobs.Count; k++)
            {
                int i = colony.digJobs[k];
                int x = i % world.W, y = i / world.W;
                if (!blink && ((x + y) & 1) == 0) continue;

                Color32 baseC = buf[Px(x, y)];
                Color32 tint;
                switch (world.marks[i])
                {
                    case Mark.Nursery: tint = new Color32(150, 220, 160, 255); break;
                    case Mark.Pantry: tint = new Color32(240, 220, 130, 255); break;
                    case Mark.Throne: tint = new Color32(230, 150, 150, 255); break;
                    default: tint = new Color32(255, 255, 255, 255); break;
                }
                buf[Px(x, y)] = Color32.Lerp(baseC, tint, 0.45f);
            }
        }

        void DrawFood()
        {
            for (int i = 0; i < colony.foods.Count; i++)
            {
                int t = colony.foods[i].tile;
                buf[Px(t % world.W, t / world.W)] = Palette.Food;
            }
        }

        void DrawBrood()
        {
            for (int i = 0; i < colony.broods.Count; i++)
            {
                var b = colony.broods[i];
                Color32 c;
                switch (b.stage)
                {
                    case BroodStage.Egg: c = Palette.Egg; break;
                    case BroodStage.Larva: c = Palette.Larva; break;
                    default: c = Palette.Pupa; break;
                }
                buf[Px(b.tile % world.W, b.tile / world.W)] = c;
            }
        }

        void DrawAnts()
        {
            for (int i = 0; i < colony.ants.Count; i++)
            {
                var a = colony.ants[i];
                int x = a.tile % world.W, y = a.tile / world.W;

                Color32 c;
                if (a.role == Role.Queen) c = Palette.Queen;
                else if (a.carry == Carry.Soil) c = Palette.AntCarrySoil;
                else if (a.carry == Carry.Food) c = Palette.AntCarryFood;
                else c = Palette.Ant;

                buf[Px(x, y)] = c;

                // ราชินีตัวใหญ่กว่า วาดเป็น 2x2
                if (a.role == Role.Queen)
                {
                    if (x + 1 < world.W) buf[Px(x + 1, y)] = c;
                    if (y + 1 < world.H) buf[Px(x, y + 1)] = c;
                    if (x + 1 < world.W && y + 1 < world.H) buf[Px(x + 1, y + 1)] = c;
                }
            }
        }
    }
}
