using UnityEditor;
using UnityEngine;

namespace AntTime.EditorTools
{
    // รัน simulation แบบไม่มีภาพ เพื่อเช็กว่ามดทำงานจริง (ขุด/ขนดิน/หาอาหาร/ฟักไข่)
    // เรียกจาก command line:
    //   Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod AntTime.EditorTools.SimSmokeTest.Run
    public static class SimSmokeTest
    {
        [MenuItem("AntTime/Run Sim Smoke Test")]
        public static void Run()
        {
            int[] seeds = { 12345, 777, 424242, 99, 20260823 };
            bool allOk = true;
            for (int i = 0; i < seeds.Length; i++) allOk &= RunOne(seeds[i]);
            Debug.Log(allOk ? "[SimSmokeTest] ALL PASS" : "[SimSmokeTest] SOME FAILED");
        }

        static bool RunOne(int seed)
        {
            Random.InitState(seed);
            var world = new World(Game.WorldW, Game.WorldH, seed);
            var colony = new Colony(world);

            // สั่งขุดอุโมงค์ยาว ๆ ลงไปจากห้องราชินี
            for (int x = world.NestX - 30; x <= world.NestX + 30; x++)
                for (int y = world.NestY + 2; y <= world.NestY + 8; y++)
                    colony.ApplyMark(x, y, Mark.Dig);

            int startJobs = colony.digJobs.Count;
            const float step = 1f / 30f;
            string envSec = System.Environment.GetEnvironmentVariable("ANTTIME_SECONDS");
            float seconds;
            if (!float.TryParse(envSec, out seconds) || seconds <= 0f) seconds = 300f;
            int ticks = (int)(seconds / step);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < ticks; i++) colony.Tick(step);
            sw.Stop();

            Debug.Log(string.Format(
                "[SimSmokeTest] {0} ticks in {1} ms\n" +
                "ants={2} (born {3}, died {4})  brood={5}\n" +
                "dig jobs {6} -> {7}   soil moved={8}   food gathered={9}   stored={10}\n" +
                "surface food items={11}   day={12}   pathOk={13} pathFails={14}",
                ticks, sw.ElapsedMilliseconds,
                colony.ants.Count, colony.antsBorn, colony.antsDied, colony.broods.Count,
                startJobs, colony.digJobs.Count, colony.soilMoved, colony.foodGathered,
                world.TotalStoredFood(), colony.foods.Count, colony.day, colony.pathOk, colony.pathFails));

            // งานขุดที่เหลือ ยังมีช่องให้มดยืนขุดได้กี่จุด (ถ้า 0 = ที่เหลือถูกหินปิดตาย ไม่ใช่บั๊ก)
            int reachable = 0;
            for (int k = 0; k < colony.digJobs.Count; k++)
            {
                int t = colony.digJobs[k];
                int tx = t % world.W, ty = t / world.W;
                for (int dy = -1; dy <= 1 && reachable == k; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (world.Walkable(tx + dx, ty + dy)) { reachable++; break; }
            }
            Debug.Log("[SimSmokeTest] dig jobs with a reachable digging spot: " + reachable + " / " + colony.digJobs.Count);

            // flood fill จากราชินี ดูว่ารังยังต่อกับผิวดินและอาหารอยู่ไหม
            var seen = new bool[world.W * world.H];
            var q = new System.Collections.Generic.Queue<int>();
            q.Enqueue(colony.Queen.tile); seen[colony.Queen.tile] = true;
            int reach = 0, skyReach = 0;
            while (q.Count > 0)
            {
                int c = q.Dequeue(); reach++;
                int cx = c % world.W, cy = c / world.W;
                if (world.tiles[c] == Tile.Sky) skyReach++;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (!world.Walkable(nx, ny)) continue;
                        int ni = world.Idx(nx, ny);
                        if (seen[ni]) continue;
                        seen[ni] = true; q.Enqueue(ni);
                    }
            }
            int foodReach = 0;
            for (int k = 0; k < colony.foods.Count; k++) if (seen[colony.foods[k].tile]) foodReach++;
            Debug.Log("[SimSmokeTest] reachable tiles=" + reach + "  sky tiles reachable=" + skyReach +
                      "  food reachable=" + foodReach + "/" + colony.foods.Count);

            bool ok = colony.soilMoved > 0 && colony.digJobs.Count < startJobs && colony.ants.Count > 12;
            Debug.Log(ok ? "[SimSmokeTest] PASS" : "[SimSmokeTest] FAIL");
            return ok;
        }
    }
}
