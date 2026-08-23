using System.IO;
using UnityEditor;
using UnityEngine;

namespace AntTime.EditorTools
{
    // เรนเดอร์ภาพนิ่งของรังออกมาเป็น PNG เอาไว้ดูงานศิลป์โดยไม่ต้องเปิดเกม
    //   Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod AntTime.EditorTools.PreviewShot.Run
    public static class PreviewShot
    {
        [MenuItem("AntTime/Render Preview PNG")]
        public static void Run()
        {
            Random.InitState(2026);
            var world = new World(Game.WorldW, Game.WorldH, 424242);
            var colony = new Colony(world);

            // สั่งขุดให้เห็นเป็นรังจริง ๆ: ทางลง + ห้องเพิ่ม
            for (int y = world.NestY + 2; y <= world.NestY + 26; y++)
                for (int x = world.NestX - 1; x <= world.NestX + 2; x++)
                    colony.ApplyMark(x, y, Mark.Dig);
            for (int x = world.NestX - 26; x <= world.NestX + 26; x++)
                for (int y = world.NestY + 12; y <= world.NestY + 14; y++)
                    colony.ApplyMark(x, y, Mark.Dig);
            // ทางเชื่อมลงไปห้องข้างล่าง
            for (int y = world.NestY + 14; y <= world.NestY + 18; y++)
            {
                for (int x = world.NestX - 20; x <= world.NestX - 18; x++) colony.ApplyMark(x, y, Mark.Dig);
                for (int x = world.NestX + 18; x <= world.NestX + 20; x++) colony.ApplyMark(x, y, Mark.Dig);
            }
            for (int x = world.NestX - 24; x <= world.NestX - 14; x++)
                for (int y = world.NestY + 16; y <= world.NestY + 20; y++)
                    colony.ApplyMark(x, y, Mark.Nursery);
            for (int x = world.NestX + 14; x <= world.NestX + 24; x++)
                for (int y = world.NestY + 16; y <= world.NestY + 20; y++)
                    colony.ApplyMark(x, y, Mark.Pantry);

            const float step = 1f / 30f;
            for (int i = 0; i < (int)(2400f / step); i++) colony.Tick(step);

            var render = new PixelRenderer(world, colony);
            render.Draw(0f);

            string path = Path.Combine(Directory.GetCurrentDirectory(), "preview.png");
            File.WriteAllBytes(path, render.texture.EncodeToPNG());
            Debug.Log("[PreviewShot] wrote " + path + "  ants=" + colony.ants.Count +
                      " brood=" + colony.broods.Count + " stored=" + world.TotalStoredFood() +
                      " soilMoved=" + colony.soilMoved + " digJobsLeft=" + colony.digJobs.Count +
                      " pathOk=" + colony.pathOk + " pathFails=" + colony.pathFails +
                      " foodGathered=" + colony.foodGathered);

            int digging = 0, hauling = 0, foraging = 0, idle = 0, wandering = 0, other = 0;
            for (int i = 0; i < colony.ants.Count; i++)
            {
                var a = colony.ants[i];
                if (a.state == AntState.Digging || a.state == AntState.MoveToDig) digging++;
                else if (a.state == AntState.MoveToDump) hauling++;
                else if (a.state == AntState.MoveToFood || a.state == AntState.MoveToPantry) foraging++;
                else if (a.state == AntState.Wander) wandering++;
                else if (a.state == AntState.Idle) idle++;
                else other++;
            }
            int reachableJobs = 0;
            for (int k = 0; k < colony.digJobs.Count; k++)
            {
                int t = colony.digJobs[k];
                int tx = t % world.W, ty = t / world.W;
                bool any = false;
                for (int dy = -1; dy <= 1 && !any; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (world.Walkable(tx + dx, ty + dy)) { any = true; break; }
                if (any) reachableJobs++;
            }
            Debug.Log("[PreviewShot] dig jobs with a digging spot: " + reachableJobs + " / " + colony.digJobs.Count);

            // ภาพซูมรอบรัง
            int cx0 = Mathf.Max(0, world.NestX - 60), cy0 = Mathf.Max(0, world.NestY - 30);
            int cw = Mathf.Min(120, world.W - cx0), ch = Mathf.Min(70, world.H - cy0);
            const int scale = 4;
            var crop = new Texture2D(cw * scale, ch * scale, TextureFormat.RGBA32, false);
            var src = render.texture.GetPixels32();
            var dst = new Color32[cw * scale * ch * scale];
            for (int yy = 0; yy < ch * scale; yy++)
                for (int xx = 0; xx < cw * scale; xx++)
                {
                    int sx = cx0 + xx / scale;
                    int sy = world.H - 1 - (cy0 + yy / scale);
                    dst[(ch * scale - 1 - yy) * cw * scale + xx] = src[sy * world.W + sx];
                }
            crop.SetPixels32(dst);
            crop.Apply(false);
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "preview_zoom.png"), crop.EncodeToPNG());

            Debug.Log("[PreviewShot] states dig=" + digging + " haul=" + hauling + " forage=" + foraging +
                      " wander=" + wandering + " idle=" + idle + " other=" + other);
        }
    }
}
