using UnityEngine;

namespace AntTime
{
    // จุดเริ่มของเกม: สร้างทุกอย่างจากโค้ด ไม่ต้องจัด scene เอง แค่กด Play
    public class Game : MonoBehaviour
    {
        public const int WorldW = 256;
        public const int WorldH = 168;

        World world;
        Colony colony;
        PixelRenderer render;

        int zoom = 5;
        Vector2 pan = Vector2.zero;
        Vector3 lastMouse;

        Mark brush = Mark.Dig;
        int brushSize = 3;
        int speedIndex = 1;
        static readonly float[] Speeds = { 0f, 1f, 2f, 4f, 8f };

        float accumulator;
        const float SimStep = 1f / 30f;

        Vector2Int lastPaint = new Vector2Int(-1, -1);
        GUIStyle hudStyle;
        Font uiFont;
        bool showHelp = true;

        float autoSaveTimer = AutoSaveInterval;
        const float AutoSaveInterval = 120f;   // วินาทีจริง
        string toast = "";
        float toastTimer;
        float confirmNewWorldUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("AntTime");
            go.AddComponent<Game>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            SetupCamera();

            // มีเซฟอัตโนมัติค้างอยู่ก็เล่นต่อจากตรงนั้นเลย
            if (!LoadFrom(SaveGame.AutoSlot, silentIfMissing: true))
                NewGame(Random.Range(1, 999999));
            RecenterView();
        }

        void SetupCamera()
        {
            if (Camera.main != null) { Camera.main.backgroundColor = new Color(0.07f, 0.07f, 0.09f); return; }
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.09f);
            cam.orthographic = true;
            DontDestroyOnLoad(camGo);
        }

        void NewGame(int seed)
        {
            world = new World(WorldW, WorldH, seed);
            colony = new Colony(world);
            render = new PixelRenderer(world, colony);
            lastPaint = new Vector2Int(-1, -1);
        }

        void RecenterView()
        {
            zoom = Mathf.Clamp(Mathf.FloorToInt(Screen.height / (float)WorldH), 2, 8);
            pan = Vector2.zero;
            // เลื่อนให้ปากรังอยู่กลางจอ
            float nestOffsetX = (WorldW * 0.5f - world.NestX) * zoom;
            float nestOffsetY = (world.NestY - WorldH * 0.5f) * zoom;
            pan = new Vector2(nestOffsetX, nestOffsetY);
        }

        void Update()
        {
            HandleInput();
            StepSimulation();
            TickAutoSave();
            if (toastTimer > 0f) toastTimer -= Time.deltaTime;
            render.Draw(Time.time);
        }

        void TickAutoSave()
        {
            autoSaveTimer -= Time.deltaTime;
            if (autoSaveTimer > 0f) return;
            autoSaveTimer = AutoSaveInterval;
            if (SaveGame.Save(SaveGame.AutoSlot, world, colony)) Toast("บันทึกอัตโนมัติแล้ว");
        }

        void OnApplicationQuit()
        {
            SaveGame.Save(SaveGame.AutoSlot, world, colony);
        }

        void Toast(string message)
        {
            toast = message;
            toastTimer = 2.5f;
        }

        // ---------- เซฟ / โหลด ----------

        void SaveTo(string slot)
        {
            Toast(SaveGame.Save(slot, world, colony) ? "บันทึกเกมแล้ว" : "บันทึกไม่สำเร็จ (ดู Console)");
        }

        bool LoadFrom(string slot, bool silentIfMissing = false)
        {
            if (!SaveGame.Exists(slot))
            {
                if (!silentIfMissing) Toast("ยังไม่มีไฟล์เซฟ");
                return false;
            }

            World loadedWorld;
            Colony loadedColony;
            if (!SaveGame.Load(slot, out loadedWorld, out loadedColony))
            {
                if (!silentIfMissing) Toast("โหลดไม่สำเร็จ (ดู Console)");
                return false;   // เกมที่เล่นอยู่ไม่ถูกแตะ
            }

            world = loadedWorld;
            colony = loadedColony;
            render = new PixelRenderer(world, colony);
            lastPaint = new Vector2Int(-1, -1);
            if (!silentIfMissing) Toast("โหลดเกมแล้ว");
            return true;
        }

        void StepSimulation()
        {
            float speed = Speeds[speedIndex];
            if (speed <= 0f) return;

            accumulator += Time.deltaTime * speed;
            int steps = 0;
            while (accumulator >= SimStep && steps < 12)
            {
                colony.Tick(SimStep);
                accumulator -= SimStep;
                steps++;
            }
            if (accumulator > SimStep * 12) accumulator = 0f;
        }

        // ---------- input ----------

        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) brush = Mark.Dig;
            if (Input.GetKeyDown(KeyCode.Alpha2)) brush = Mark.Nursery;
            if (Input.GetKeyDown(KeyCode.Alpha3)) brush = Mark.Pantry;
            if (Input.GetKeyDown(KeyCode.Alpha4)) brush = Mark.Throne;
            if (Input.GetKeyDown(KeyCode.Alpha5)) brush = Mark.None;

            if (Input.GetKeyDown(KeyCode.LeftBracket)) brushSize = Mathf.Max(1, brushSize - 2);
            if (Input.GetKeyDown(KeyCode.RightBracket)) brushSize = Mathf.Min(11, brushSize + 2);

            if (Input.GetKeyDown(KeyCode.Space)) speedIndex = speedIndex == 0 ? 1 : 0;
            if (Input.GetKeyDown(KeyCode.Comma)) speedIndex = Mathf.Max(0, speedIndex - 1);
            if (Input.GetKeyDown(KeyCode.Period)) speedIndex = Mathf.Min(Speeds.Length - 1, speedIndex + 1);

            if (Input.GetKeyDown(KeyCode.C)) RecenterView();
            if (Input.GetKeyDown(KeyCode.H)) showHelp = !showHelp;
            if (Input.GetKeyDown(KeyCode.F5)) SaveTo(SaveGame.ManualSlot);
            if (Input.GetKeyDown(KeyCode.F9)) LoadFrom(SaveGame.ManualSlot);

            // สร้างโลกใหม่ = ทิ้งรังที่เลี้ยงมา ต้องกดยืนยันอีกครั้ง
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (Time.time < confirmNewWorldUntil)
                {
                    confirmNewWorldUntil = 0f;
                    NewGame(Random.Range(1, 999999));
                    RecenterView();
                    Toast("สร้างโลกใหม่แล้ว");
                }
                else
                {
                    confirmNewWorldUntil = Time.time + 3f;
                    Toast("กด R อีกครั้งเพื่อทิ้งรังนี้และสร้างโลกใหม่");
                }
            }

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                int newZoom = Mathf.Clamp(zoom + (wheel > 0 ? 1 : -1), 2, 12);
                if (newZoom != zoom)
                {
                    pan *= newZoom / (float)zoom;
                    zoom = newZoom;
                }
            }

            // ลากด้วยปุ่มขวา/ปุ่มกลาง = เลื่อนกล้อง
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) lastMouse = Input.mousePosition;
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                Vector3 delta = Input.mousePosition - lastMouse;
                pan += new Vector2(delta.x, delta.y);
                lastMouse = Input.mousePosition;
            }

            // WASD เลื่อนกล้อง
            float pk = 400f * Time.deltaTime;
            if (Input.GetKey(KeyCode.A)) pan.x += pk;
            if (Input.GetKey(KeyCode.D)) pan.x -= pk;
            if (Input.GetKey(KeyCode.W)) pan.y -= pk;
            if (Input.GetKey(KeyCode.S)) pan.y += pk;

            // ลากปุ่มซ้าย = สั่งงาน
            if (Input.GetMouseButton(0))
            {
                Vector2Int t = ScreenToTile(Input.mousePosition);
                if (t.x >= 0)
                {
                    if (lastPaint.x >= 0) PaintLine(lastPaint, t);
                    else PaintAt(t);
                    lastPaint = t;
                }
            }
            else lastPaint = new Vector2Int(-1, -1);
        }

        Vector2Int ScreenToTile(Vector3 mouse)
        {
            float cx = Screen.width * 0.5f + pan.x;
            float cy = Screen.height * 0.5f + pan.y;
            float u = (mouse.x - cx) / zoom + WorldW * 0.5f;
            float v = (mouse.y - cy) / zoom + WorldH * 0.5f;
            int tx = Mathf.FloorToInt(u);
            int ty = WorldH - 1 - Mathf.FloorToInt(v);
            if (!world.InBounds(tx, ty)) return new Vector2Int(-1, -1);
            return new Vector2Int(tx, ty);
        }

        void PaintLine(Vector2Int a, Vector2Int b)
        {
            int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            if (steps == 0) { PaintAt(b); return; }
            for (int i = 0; i <= steps; i++)
            {
                float f = i / (float)steps;
                PaintAt(new Vector2Int(
                    Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, f)),
                    Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, f))));
            }
        }

        void PaintAt(Vector2Int t)
        {
            int r = brushSize / 2;
            for (int y = t.y - r; y <= t.y + r; y++)
                for (int x = t.x - r; x <= t.x + r; x++)
                    colony.ApplyMark(x, y, brush);
        }

        // ---------- HUD ----------

        // วาด framebuffer ของโลกเต็มจอ (GUI y นับจากบนลงล่าง ส่วน pan นับจากล่างขึ้นบน)
        void DrawWorldTexture()
        {
            float w = WorldW * zoom;
            float h = WorldH * zoom;
            float left = Screen.width * 0.5f + pan.x - w * 0.5f;
            float top = Screen.height * 0.5f - pan.y - h * 0.5f;
            GUI.DrawTexture(new Rect(left, top, w, h), render.texture, ScaleMode.StretchToFill, false);
        }

        void OnGUI()
        {
            DrawWorldTexture();

            if (hudStyle == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont("Leelawadee UI", 15)
                         ?? Font.CreateDynamicFontFromOSFont("Tahoma", 15);
                hudStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    richText = true,
                    wordWrap = false,
                };
                if (uiFont != null) hudStyle.font = uiFont;
                hudStyle.normal.textColor = Color.white;
            }

            int workers = colony.ants.Count - 1;
            int eggs = 0, larvae = 0, pupae = 0;
            for (int i = 0; i < colony.broods.Count; i++)
            {
                switch (colony.broods[i].stage)
                {
                    case BroodStage.Egg: eggs++; break;
                    case BroodStage.Larva: larvae++; break;
                    default: pupae++; break;
                }
            }

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(8, 8, 300, showHelp ? 350 : 150), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var r = new Rect(18, 14, 290, 22);
            void Line(string s) { GUI.Label(r, s, hudStyle); r.y += 20; }

            Line("<b>AntTime</b>  วันที่ " + colony.day + "   ความเร็ว x" + Speeds[speedIndex]);
            Line("มดงาน: " + workers + " / " + Colony.MaxAnts + "     ราชินี: 1");
            Line("ไข่ " + eggs + "   ตัวอ่อน " + larvae + "   ดักแด้ " + pupae);
            Line("เสบียงในคลัง: " + world.TotalStoredFood() + "     อาหารบนดิน: " + colony.foods.Count);
            Line("งานขุดค้าง: " + colony.digJobs.Count + "     ดินที่ขนออก: " + colony.soilMoved);
            Line("<b>พู่กัน:</b> " + BrushName() + "   ขนาด " + brushSize);

            DrawToast();

            if (!showHelp) return;
            r.y += 8;
            Line("<b>วิธีเล่น</b>  (กด H ซ่อน/แสดง)");
            Line("ลากเมาส์ซ้าย = สั่งงานตามพู่กัน");
            Line("1 ขุดอุโมงค์   2 ห้องอนุบาล");
            Line("3 ห้องเสบียง   4 ห้องราชินี   5 ยกเลิก");
            Line("[ ] = ปรับขนาดพู่กัน");
            Line("ลากเมาส์ขวา / WASD = เลื่อนจอ, ล้อ = ซูม");
            Line("Space หยุด/เล่น   , . ปรับความเร็ว");
            Line("C กลับไปที่รัง   R สร้างโลกใหม่");
            Line("F5 บันทึก   F9 โหลด (บันทึกอัตโนมัติทุก 2 นาที)");
        }

        void DrawToast()
        {
            if (toastTimer <= 0f || string.IsNullOrEmpty(toast)) return;

            var size = hudStyle.CalcSize(new GUIContent(toast));
            var box = new Rect(Screen.width * 0.5f - size.x * 0.5f - 12f, Screen.height - 70f, size.x + 24f, 30f);

            GUI.color = new Color(0f, 0f, 0f, 0.6f * Mathf.Clamp01(toastTimer));
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(toastTimer));
            GUI.Label(new Rect(box.x + 12f, box.y + 5f, size.x + 4f, 22f), toast, hudStyle);
            GUI.color = Color.white;
        }

        string BrushName()
        {
            switch (brush)
            {
                case Mark.Dig: return "ขุดอุโมงค์";
                case Mark.Nursery: return "ห้องอนุบาล";
                case Mark.Pantry: return "ห้องเสบียง";
                case Mark.Throne: return "ห้องราชินี";
                default: return "ยกเลิกคำสั่ง";
            }
        }
    }
}
