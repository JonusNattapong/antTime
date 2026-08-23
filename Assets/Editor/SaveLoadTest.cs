using UnityEditor;
using UnityEngine;

namespace AntTime.EditorTools
{
    // เซฟแล้วโหลดกลับมาต้องได้รังเดิมเป๊ะ ๆ และเล่นต่อได้ปกติ
    //   Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod AntTime.EditorTools.SaveLoadTest.Run
    public static class SaveLoadTest
    {
        const string Slot = "test_roundtrip.dat";

        [MenuItem("AntTime/Run Save-Load Test")]
        public static void Run()
        {
            Random.InitState(4242);
            var world = new World(Game.WorldW, Game.WorldH, 4242);
            var colony = new Colony(world);

            for (int x = world.NestX - 20; x <= world.NestX + 20; x++)
                for (int y = world.NestY + 2; y <= world.NestY + 6; y++)
                    colony.ApplyMark(x, y, Mark.Dig);

            const float step = 1f / 30f;
            for (int i = 0; i < (int)(400f / step); i++) colony.Tick(step);

            if (!SaveGame.Save(Slot, world, colony)) { Debug.Log("[SaveLoadTest] FAIL — เซฟไม่สำเร็จ"); return; }

            var info = new System.IO.FileInfo(SaveGame.PathFor(Slot));
            World loadedWorld;
            Colony loadedColony;
            if (!SaveGame.Load(Slot, out loadedWorld, out loadedColony))
            {
                Debug.Log("[SaveLoadTest] FAIL — โหลดไม่สำเร็จ");
                return;
            }

            bool ok = true;
            ok &= Check("world size", loadedWorld.W == world.W && loadedWorld.H == world.H);
            ok &= Check("nest position", loadedWorld.NestX == world.NestX && loadedWorld.NestY == world.NestY);

            int tileDiff = 0, markDiff = 0, storedDiff = 0, decorDiff = 0, surfaceDiff = 0;
            for (int i = 0; i < world.tiles.Length; i++)
            {
                if (world.tiles[i] != loadedWorld.tiles[i]) tileDiff++;
                if (world.marks[i] != loadedWorld.marks[i]) markDiff++;
                if (world.stored[i] != loadedWorld.stored[i]) storedDiff++;
                if (world.decor[i] != loadedWorld.decor[i]) decorDiff++;
            }
            for (int x = 0; x < world.W; x++)
                if (world.surfaceY[x] != loadedWorld.surfaceY[x]) surfaceDiff++;

            ok &= Check("tiles", tileDiff == 0);
            ok &= Check("marks", markDiff == 0);
            ok &= Check("stored food", storedDiff == 0);
            ok &= Check("decor", decorDiff == 0);
            ok &= Check("surface heights recomputed", surfaceDiff == 0);

            ok &= Check("ant count", loadedColony.ants.Count == colony.ants.Count);
            ok &= Check("queen restored", loadedColony.Queen != null && loadedColony.Queen.tile == colony.Queen.tile);
            ok &= Check("brood count", loadedColony.broods.Count == colony.broods.Count);
            ok &= Check("food items", loadedColony.foods.Count == colony.foods.Count);
            ok &= Check("day", loadedColony.day == colony.day);
            ok &= Check("stored total", loadedWorld.TotalStoredFood() == world.TotalStoredFood());
            ok &= Check("dig jobs", loadedColony.digJobs.Count == colony.digJobs.Count);

            // เล่นต่อจากเซฟได้จริงไหม
            int antsBefore = loadedColony.ants.Count;
            int soilBefore = loadedColony.soilMoved;
            for (int i = 0; i < (int)(300f / step); i++) loadedColony.Tick(step);
            ok &= Check("รังยังรอดหลังโหลด", loadedColony.ants.Count >= antsBefore);
            ok &= Check("มดยังขุดต่อได้", loadedColony.soilMoved > soilBefore || loadedColony.digJobs.Count == 0);

            Debug.Log(string.Format(
                "[SaveLoadTest] ไฟล์เซฟ {0} KB | ก่อนเซฟ: มด {1} ไข่ {2} เสบียง {3} วัน {4}\n" +
                "หลังโหลดแล้วเล่นต่ออีก 300 วิ: มด {5} ไข่ {6} เสบียง {7}",
                info.Length / 1024, colony.ants.Count, colony.broods.Count, world.TotalStoredFood(), colony.day,
                loadedColony.ants.Count, loadedColony.broods.Count, loadedWorld.TotalStoredFood()));

            System.IO.File.Delete(SaveGame.PathFor(Slot));
            Debug.Log(ok ? "[SaveLoadTest] PASS" : "[SaveLoadTest] FAIL");
        }

        static bool Check(string what, bool condition)
        {
            if (!condition) Debug.LogError("[SaveLoadTest] ไม่ตรง: " + what);
            return condition;
        }
    }
}
