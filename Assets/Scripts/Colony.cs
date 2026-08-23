using System.Collections.Generic;
using UnityEngine;

namespace AntTime
{
    // หัวใจของ simulation: มด, งาน, ไข่/ตัวอ่อน, อาหาร, ราชินี
    public class Colony
    {
        public readonly World world;
        readonly Pathfinder pf;

        public readonly List<Ant> ants = new List<Ant>();
        public readonly List<Brood> broods = new List<Brood>();
        public readonly List<FoodItem> foods = new List<FoodItem>();

        readonly HashSet<int> claimedDig = new HashSet<int>();
        readonly Dictionary<int, float> blocked = new Dictionary<int, float>();
        readonly HashSet<int> broodTiles = new HashSet<int>();

        // cache ที่รีเฟรชเป็นระยะ ไม่ต้องคอยอัปเดตทีละช่อง
        public readonly List<int> digJobs = new List<int>();
        public readonly List<int> pantryTiles = new List<int>();
        public readonly List<int> nurseryTiles = new List<int>();
        public readonly List<int> throneTiles = new List<int>();
        float cacheTimer;

        public float simTime;
        public int day = 1;
        public int antsBorn;
        public int antsDied;
        public int foodGathered;
        public int soilMoved;
        public int pathFails;
        public int pathOk;
        int nextAntId = 1;
        int nextFoodId = 1;
        float foodSpawnTimer;
        float layTimer;

        // ---- ค่าปรับสมดุลเกม ----
        public const float MoveInterval = 0.055f;
        public const float QueenMoveInterval = 0.30f;
        const float HungerRate = 0.0035f;
        const float EatThreshold = 0.6f;
        const float EggTime = 22f;
        const float LarvaMinTime = 26f;
        const int LarvaMeals = 3;
        const float PupaTime = 26f;
        const float LayInterval = 9f;
        const int LayFoodCost = 2;
        const int LayFoodReserve = 10;   // ราชินีจะวางไข่ต่อเมื่อคลังเหลือมากพอ
        const float FoodSpawnInterval = 3.5f;
        const int MaxFoodItems = 30;
        const float DayLength = 120f;
        public const int MaxAnts = 220;

        public Ant Queen;

        public Colony(World w)
        {
            world = w;
            pf = new Pathfinder(w);
            world.CarveStarterNest();
            RefreshCaches();

            SpawnQueen();
            for (int i = 0; i < 10; i++) SpawnAnt(world.Idx(world.NestX, world.NestY - 3));

            // เสบียงตั้งต้น
            for (int i = 0; i < pantryTiles.Count; i++)
            {
                world.stored[pantryTiles[i]] = World.PantryTileCap;
                if (world.TotalStoredFood() >= 24) break;
            }
        }

        // ---------- entity spawn ----------

        void SpawnQueen()
        {
            Queen = new Ant
            {
                id = 0,
                role = Role.Queen,
                tile = throneTiles.Count > 0 ? throneTiles[throneTiles.Count / 2] : world.Idx(world.NestX, world.NestY),
            };
            ants.Add(Queen);
        }

        public void SpawnAnt(int tile)
        {
            if (ants.Count >= MaxAnts) return;
            var a = new Ant
            {
                id = nextAntId++,
                tile = world.Walkable(tile) ? tile : Queen.tile,
                hunger = Random.Range(0f, 0.2f),
            };
            a.role = (Role)(a.id % 3); // Digger / Forager / Nurse สลับกันไป
            ants.Add(a);
            antsBorn++;
        }

        // ---------- main tick ----------

        public void Tick(float dt)
        {
            simTime += dt;
            day = 1 + (int)(simTime / DayLength);

            cacheTimer -= dt;
            if (cacheTimer <= 0f) { RefreshCaches(); cacheTimer = 0.5f; }

            TickFoodSpawn(dt);
            TickBrood(dt);
            TickQueen(dt);

            for (int i = ants.Count - 1; i >= 0; i--)
            {
                var a = ants[i];
                if (a.role == Role.Queen) continue;
                TickAnt(a, dt);
                if (!a.alive) { ants.RemoveAt(i); antsDied++; }
            }
        }

        void RefreshCaches()
        {
            digJobs.Clear(); pantryTiles.Clear(); nurseryTiles.Clear(); throneTiles.Clear();
            var tiles = world.tiles;
            var marks = world.marks;
            for (int i = 0; i < tiles.Length; i++)
            {
                Tile t = tiles[i];
                if (t == Tile.Pantry) pantryTiles.Add(i);
                else if (t == Tile.Nursery) nurseryTiles.Add(i);
                else if (t == Tile.Throne) throneTiles.Add(i);

                if (marks[i] != Mark.None && Tiles.IsDiggable(t)) digJobs.Add(i);
            }

            broodTiles.Clear();
            for (int i = 0; i < broods.Count; i++) broodTiles.Add(broods[i].tile);

            // อาหารที่โดนกองดินทับจนเข้าไม่ถึงแล้ว ต้องเอาออก
            // ไม่งั้นมันจะกินโควตาอาหารบนดินไว้เฉย ๆ จนไม่มีอาหารใหม่เกิด แล้วรังจะอดตาย
            for (int i = foods.Count - 1; i >= 0; i--)
                if (!world.Walkable(foods[i].tile)) foods.RemoveAt(i);

            RebuildClaims();

            if (blocked.Count > 0)
            {
                var expired = new List<int>();
                foreach (var kv in blocked) if (kv.Value < simTime) expired.Add(kv.Key);
                for (int i = 0; i < expired.Count; i++) blocked.Remove(expired[i]);
            }
        }

        // สร้าง "การจอง" ใหม่จากมดที่ยังมีชีวิตอยู่เท่านั้น
        // กันไม่ให้ของถูกจองค้างโดยมดที่ตายไปแล้ว (ไม่งั้นอาหารบนดินจะไม่มีใครไปเก็บอีกเลย)
        void RebuildClaims()
        {
            claimedDig.Clear();
            for (int i = 0; i < foods.Count; i++) foods[i].claimed = false;
            for (int i = 0; i < broods.Count; i++) broods[i].claimed = false;

            for (int i = 0; i < ants.Count; i++)
            {
                var a = ants[i];
                if (!a.alive) continue;

                if (a.jobTile >= 0 && (a.state == AntState.MoveToDig || a.state == AntState.Digging))
                    claimedDig.Add(a.jobTile);

                if (a.foodItemId >= 0)
                {
                    var f = FoodById(a.foodItemId);
                    if (f != null) f.claimed = true;
                    else a.foodItemId = -1;
                }

                if (a.broodTarget >= 0)
                {
                    bool found = false;
                    for (int k = 0; k < broods.Count; k++)
                        if (broods[k].tile == a.broodTarget) { broods[k].claimed = true; found = true; break; }
                    if (!found) a.broodTarget = -1;
                }
            }
        }

        // ---------- อาหารบนผิวดิน ----------

        void TickFoodSpawn(float dt)
        {
            foodSpawnTimer -= dt;
            if (foodSpawnTimer > 0f || foods.Count >= MaxFoodItems) return;
            foodSpawnTimer = FoodSpawnInterval;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                int x = Mathf.Clamp(world.NestX + Random.Range(-70, 71), 4, world.W - 5);
                int y = world.surfaceY[x] - 1;
                if (y < 1) continue;
                if (world.Get(x, y) != Tile.Sky) continue;
                int t = world.Idx(x, y);
                if (HasFoodAt(t)) continue;
                foods.Add(new FoodItem { id = nextFoodId++, tile = t, amount = Random.Range(4, 9) });
                return;
            }
        }

        bool HasFoodAt(int tile)
        {
            for (int i = 0; i < foods.Count; i++) if (foods[i].tile == tile) return true;
            return false;
        }

        public FoodItem FoodById(int id)
        {
            for (int i = 0; i < foods.Count; i++) if (foods[i].id == id) return foods[i];
            return null;
        }

        // ---------- ไข่ / ตัวอ่อน / ดักแด้ ----------

        void TickBrood(float dt)
        {
            for (int i = broods.Count - 1; i >= 0; i--)
            {
                var b = broods[i];
                b.timer += dt;

                // ถ้าห้องถูกถมกลับ ตัวอ่อนก็ไม่รอด
                if (!Tiles.IsOpen(world.tiles[b.tile])) { broods.RemoveAt(i); continue; }

                switch (b.stage)
                {
                    case BroodStage.Egg:
                        if (b.timer >= EggTime) { b.stage = BroodStage.Larva; b.timer = 0f; }
                        break;
                    case BroodStage.Larva:
                        if (b.fed >= LarvaMeals && b.timer >= LarvaMinTime) { b.stage = BroodStage.Pupa; b.timer = 0f; }
                        break;
                    case BroodStage.Pupa:
                        if (b.timer >= PupaTime)
                        {
                            SpawnAnt(b.tile);
                            broods.RemoveAt(i);
                        }
                        break;
                }
            }
        }

        public bool LarvaNeedsFood(Brood b) => b.stage == BroodStage.Larva && b.fed < LarvaMeals && !b.claimed;

        void TickQueen(float dt)
        {
            var q = Queen;
            q.hunger += HungerRate * dt * 0.5f;

            // ราชินีเดินกลับห้องบัลลังก์ถ้าไม่ได้อยู่ในห้อง
            if (world.tiles[q.tile] != Tile.Throne && throneTiles.Count > 0)
            {
                if (q.path == null || q.pathStep >= q.path.Count)
                {
                    int dest = NearestInList(throneTiles, q.tile);
                    if (dest >= 0) SetPath(q, dest);
                }
                StepAlongPath(q, dt, QueenMoveInterval);
                return;
            }

            layTimer += dt;
            if (layTimer < LayInterval) return;
            layTimer = 0f;

            if (world.TotalStoredFood() < LayFoodReserve) return;
            if (ants.Count + broods.Count >= MaxAnts) return;

            int nest = FreeNurseryTile();
            if (nest < 0) return;

            ConsumeStoredFood(LayFoodCost);
            broods.Add(new Brood { tile = nest, stage = BroodStage.Egg });
            broodTiles.Add(nest);
        }

        int NearestInList(List<int> list, int from)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                float d = Dist2(from, list[i]);
                if (d < bestD) { bestD = d; best = list[i]; }
            }
            return best;
        }

        int FreeNurseryTile()
        {
            for (int i = 0; i < nurseryTiles.Count; i++)
                if (!broodTiles.Contains(nurseryTiles[i])) return nurseryTiles[i];
            return -1;
        }

        void ConsumeStoredFood(int amount)
        {
            for (int i = 0; i < pantryTiles.Count && amount > 0; i++)
            {
                int t = pantryTiles[i];
                int take = Mathf.Min(amount, world.stored[t]);
                world.stored[t] -= (byte)take;
                amount -= take;
            }
        }

        // ---------- AI ของมดงาน ----------

        void TickAnt(Ant a, float dt)
        {
            a.age += dt;
            a.hunger += HungerRate * dt;
            if (a.hunger >= 1f) { a.alive = false; ReleaseAll(a); return; }

            switch (a.state)
            {
                case AntState.Idle:
                    ChooseJob(a);
                    break;

                case AntState.MoveToDig:
                    if (StepAlongPath(a, dt, MoveInterval))
                    {
                        if (!Tiles.IsDiggable(world.tiles[a.jobTile]) || !IsNeighbor(a.tile, a.jobTile))
                        {
                            ReleaseJob(a);
                            break;
                        }
                        a.state = AntState.Digging;
                        a.workTimer = Tiles.DigTime(world.tiles[a.jobTile]);
                    }
                    break;

                case AntState.Digging:
                    a.workTimer -= dt;
                    if (a.workTimer <= 0f) FinishDig(a);
                    break;

                case AntState.MoveToDump:
                    if (StepAlongPath(a, dt, MoveInterval)) DropSoil(a);
                    break;

                case AntState.MoveToFood:
                    if (StepAlongPath(a, dt, MoveInterval)) PickUpFood(a);
                    break;

                case AntState.MoveToPantry:
                    if (StepAlongPath(a, dt, MoveInterval)) DepositFood(a);
                    break;

                case AntState.MoveToLarva:
                    if (StepAlongPath(a, dt, MoveInterval)) FeedLarva(a);
                    break;

                case AntState.MoveToEat:
                    if (StepAlongPath(a, dt, MoveInterval)) EatAtPantry(a);
                    break;

                case AntState.Wander:
                    if (StepAlongPath(a, dt, MoveInterval)) a.state = AntState.Idle;
                    break;
            }
        }

        void ChooseJob(Ant a)
        {
            // 1) ถืออะไรอยู่ ต้องเอาไปส่งให้เสร็จก่อน
            if (a.carry == Carry.Soil) { GoDump(a); return; }
            if (a.carry == Carry.Food)
            {
                if (a.carryForFeeding && TryGoFeedLarva(a)) return;
                if (TryGoDeposit(a)) return;
                a.carry = Carry.None;      // ไม่มีที่เก็บ วางทิ้งไว้
                a.carryForFeeding = false;
                a.broodTarget = -1;
                return;
            }

            // 2) หิวมาก ไปกินก่อน
            if (a.hunger > EatThreshold && TryGoEat(a)) return;

            // 3) เสบียงใกล้หมด ทั้งรังทิ้งงานขุดไปหาอาหารก่อน
            //    (ไม่งั้นถ้าสั่งขุดเยอะ ๆ มดจะมัวแต่ขนดินจนอดตายยกรัง)
            if (FoodShortage() && TryGoForage(a)) return;

            // 4) ตามอาชีพ แล้วค่อยตกไปงานอื่น
            switch (a.role)
            {
                case Role.Nurse:
                    if (TryTakeFoodForLarva(a)) return;
                    if (TryGoForage(a)) return;
                    if (TryGoDig(a)) return;
                    break;
                case Role.Forager:
                    if (TryGoForage(a)) return;
                    if (TryGoDig(a)) return;
                    if (TryTakeFoodForLarva(a)) return;
                    break;
                default:
                    if (TryGoDig(a)) return;
                    if (TryGoForage(a)) return;
                    if (TryTakeFoodForLarva(a)) return;
                    break;
            }

            Wander(a);
        }

        // เสบียงในคลังต่ำกว่าที่รังต้องใช้หรือยัง
        public bool FoodShortage()
        {
            return world.TotalStoredFood() < Mathf.Max(8, ants.Count / 2);
        }

        bool TryGoDig(Ant a)
        {
            if (digJobs.Count == 0) return false;

            // งานเยอะมากก็สุ่มดูแค่บางส่วน ไม่ต้องไล่ทั้งลิสต์ทุกครั้ง
            const int MaxScan = 300;
            int count = digJobs.Count;
            int scan = Mathf.Min(count, MaxScan);
            int offset = count > MaxScan ? Random.Range(0, count) : 0;

            int best = -1; float bestD = float.MaxValue;
            for (int k = 0; k < scan; k++)
            {
                int t = digJobs[(offset + k) % count];
                if (claimedDig.Contains(t) || IsBlocked(t)) continue;
                float d = Dist2(a.tile, t);
                if (d < bestD) { bestD = d; best = t; }
            }
            if (best < 0) return false;

            int spot = AccessSpot(best, a.tile);
            if (spot < 0) { Block(best); return false; }
            if (!SetPath(a, spot)) { Block(best); return false; }

            claimedDig.Add(best);
            a.jobTile = best;
            a.state = AntState.MoveToDig;
            return true;
        }

        void FinishDig(Ant a)
        {
            int t = a.jobTile;
            if (t >= 0 && Tiles.IsDiggable(world.tiles[t]))
            {
                Mark m = world.marks[t];
                world.marks[t] = Mark.None;
                world.Set(t % world.W, t / world.W, Tiles.MarkResult(m));
                a.carry = Carry.Soil;
                soilMoved++;
                digJobs.Remove(t);
            }
            claimedDig.Remove(t);
            a.jobTile = -1;
            a.state = AntState.Idle;
        }

        // คืนของที่มดตัวนี้จองไว้ทั้งหมด (ใช้ตอนมดตาย)
        void ReleaseAll(Ant a)
        {
            ReleaseJob(a);
            if (a.foodItemId >= 0)
            {
                var f = FoodById(a.foodItemId);
                if (f != null) f.claimed = false;
                a.foodItemId = -1;
            }
            if (a.broodTarget >= 0)
            {
                for (int i = 0; i < broods.Count; i++)
                    if (broods[i].tile == a.broodTarget) { broods[i].claimed = false; break; }
                a.broodTarget = -1;
            }
        }

        void ReleaseJob(Ant a)
        {
            if (a.jobTile >= 0) claimedDig.Remove(a.jobTile);
            a.jobTile = -1;
            a.state = AntState.Idle;
        }

        void GoDump(Ant a)
        {
            int dump = FindDumpTile();
            if (dump < 0 || !SetPath(a, dump))
            {
                // ออกไปข้างนอกไม่ได้ ก็ปล่อยดินทิ้งในรัง
                a.carry = Carry.None;
                a.state = AntState.Idle;
                return;
            }
            a.jobTile = dump;
            a.state = AntState.MoveToDump;
        }

        // หาช่องอากาศเหนือผิวดินใกล้ปากรัง เอาไว้กองดินเป็นเนิน
        int FindDumpTile()
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int x = Mathf.Clamp(world.NestX + Random.Range(-22, 23), 2, world.W - 3);
                int y = world.surfaceY[x] - 1;
                if (y < 3) continue;
                int t = world.Idx(x, y);
                if (world.tiles[t] != Tile.Sky) continue;
                if (!world.Walkable(t) || IsBlocked(t)) continue;
                if (HasFoodAt(t)) continue;   // อย่ากองดินทับอาหาร
                return t;
            }
            return -1;
        }

        void DropSoil(Ant a)
        {
            int x = a.tile % world.W, y = a.tile / world.W;

            // ทิ้งดินได้เฉพาะจุดที่จองไว้จริง ๆ เท่านั้น
            // ถ้ามดหลุดมาทิ้งกลางทาง มันอาจถมปากปล่องจนรังขาดจากผิวดิน แล้วทั้งรังจะอดตาย
            bool atDumpSpot =
                a.tile == a.jobTile &&
                y > 1 &&
                world.Get(x, y) == Tile.Sky &&
                world.Get(x, y - 1) == Tile.Sky &&
                Tiles.IsSolid(world.Get(x, y + 1));

            if (atDumpSpot)
            {
                world.Set(x, y, Tile.TopSoil);   // ดินที่ทิ้งกลายเป็นเนินดินปากรัง
                a.tile = world.Idx(x, y - 1);    // มดปีนขึ้นไปยืนบนกองดิน
                a.carry = Carry.None;
                a.dumpFails = 0;
            }
            else if (++a.dumpFails >= 3)
            {
                a.carry = Carry.None;            // หาที่ทิ้งไม่ได้จริง ๆ ก็ปล่อยไว้ตรงนั้น
                a.dumpFails = 0;
            }

            a.jobTile = -1;
            a.state = AntState.Idle;
        }

        bool TryGoForage(Ant a)
        {
            FoodItem best = null; float bestD = float.MaxValue;
            for (int i = 0; i < foods.Count; i++)
            {
                var f = foods[i];
                if (f.claimed || IsBlocked(f.tile)) continue;
                float d = Dist2(a.tile, f.tile);
                if (d < bestD) { bestD = d; best = f; }
            }
            if (best == null) return false;
            if (!SetPath(a, best.tile)) { Block(best.tile); return false; }

            best.claimed = true;
            a.foodItemId = best.id;
            a.state = AntState.MoveToFood;
            return true;
        }

        void PickUpFood(Ant a)
        {
            var f = FoodById(a.foodItemId);
            a.foodItemId = -1;
            if (f == null) { a.state = AntState.Idle; return; }

            f.amount--;
            f.claimed = false;
            if (f.amount <= 0) foods.Remove(f);

            a.carry = Carry.Food;
            a.carryForFeeding = false;
            a.state = AntState.Idle;
        }

        bool TryGoDeposit(Ant a)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < pantryTiles.Count; i++)
            {
                int t = pantryTiles[i];
                if (world.stored[t] >= World.PantryTileCap || IsBlocked(t)) continue;
                float d = Dist2(a.tile, t);
                if (d < bestD) { bestD = d; best = t; }
            }
            if (best < 0) return false;
            if (!SetPath(a, best)) { Block(best); return false; }
            a.jobTile = best;
            a.state = AntState.MoveToPantry;
            return true;
        }

        void DepositFood(Ant a)
        {
            int t = a.jobTile;
            if (t >= 0 && world.tiles[t] == Tile.Pantry && world.stored[t] < World.PantryTileCap)
            {
                world.stored[t]++;
                foodGathered++;
                a.carry = Carry.None;
            }
            a.jobTile = -1;
            a.state = AntState.Idle;
        }

        // พี่เลี้ยงไปหยิบอาหารจากคลังก่อน แล้วค่อยเอาไปป้อนตัวอ่อน
        bool TryTakeFoodForLarva(Ant a)
        {
            Brood target = null; float bestD = float.MaxValue;
            for (int i = 0; i < broods.Count; i++)
            {
                var b = broods[i];
                if (!LarvaNeedsFood(b)) continue;
                float d = Dist2(a.tile, b.tile);
                if (d < bestD) { bestD = d; target = b; }
            }
            if (target == null) return false;

            int src = -1; float srcD = float.MaxValue;
            for (int i = 0; i < pantryTiles.Count; i++)
            {
                int t = pantryTiles[i];
                if (world.stored[t] <= 0 || IsBlocked(t)) continue;
                float d = Dist2(a.tile, t);
                if (d < srcD) { srcD = d; src = t; }
            }
            if (src < 0) return false;
            if (!SetPath(a, src)) { Block(src); return false; }

            target.claimed = true;
            a.broodTarget = target.tile;
            a.jobTile = src;
            a.carryForFeeding = true;
            a.state = AntState.MoveToEat;   // ใช้ state เดียวกัน carryForFeeding บอกว่าไปหยิบ ไม่ใช่ไปกิน
            return true;
        }

        bool TryGoFeedLarva(Ant a)
        {
            Brood target = null; float bestD = float.MaxValue;
            for (int i = 0; i < broods.Count; i++)
            {
                var b = broods[i];
                if (b.stage != BroodStage.Larva || b.fed >= LarvaMeals) continue;
                if (b.tile == a.broodTarget) { target = b; break; }   // ตัวที่จองไว้ก่อน
                float d = Dist2(a.tile, b.tile);
                if (d < bestD) { bestD = d; target = b; }
            }
            if (target == null) return false;
            if (!SetPath(a, target.tile)) { Block(target.tile); return false; }
            target.claimed = true;
            a.broodTarget = target.tile;
            a.jobTile = target.tile;
            a.state = AntState.MoveToLarva;
            return true;
        }

        void FeedLarva(Ant a)
        {
            for (int i = 0; i < broods.Count; i++)
            {
                var b = broods[i];
                if (b.tile != a.jobTile) continue;
                b.fed++;
                b.claimed = false;
                a.carry = Carry.None;
                a.carryForFeeding = false;
                a.broodTarget = -1;
                break;
            }
            a.jobTile = -1;
            a.state = AntState.Idle;
        }

        bool TryGoEat(Ant a)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < pantryTiles.Count; i++)
            {
                int t = pantryTiles[i];
                if (world.stored[t] <= 0 || IsBlocked(t)) continue;
                float d = Dist2(a.tile, t);
                if (d < bestD) { bestD = d; best = t; }
            }
            if (best < 0) return false;
            if (!SetPath(a, best)) { Block(best); return false; }
            a.jobTile = best;
            a.carryForFeeding = false;
            a.state = AntState.MoveToEat;
            return true;
        }

        void EatAtPantry(Ant a)
        {
            int t = a.jobTile;
            if (t >= 0 && world.stored[t] > 0)
            {
                world.stored[t]--;
                if (a.carryForFeeding) a.carry = Carry.Food;   // หยิบไปป้อนตัวอ่อน
                else a.hunger = 0f;                            // กินเอง
            }
            else
            {
                a.carryForFeeding = false;
                a.broodTarget = -1;
            }
            a.jobTile = -1;
            a.state = AntState.Idle;
        }

        void Wander(Ant a)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int x = a.tile % world.W + Random.Range(-10, 11);
                int y = a.tile / world.W + Random.Range(-8, 9);
                if (!world.Walkable(x, y)) continue;
                if (SetPath(a, world.Idx(x, y))) { a.state = AntState.Wander; return; }
            }
            a.state = AntState.Idle;
        }

        // ---------- การเดิน ----------

        bool SetPath(Ant a, int dest)
        {
            if (!world.Walkable(dest)) return false;
            var p = pf.Find(a.tile, dest);
            if (p == null) { pathFails++; return false; }
            pathOk++;
            a.path = p;
            a.pathStep = 0;
            a.targetTile = dest;
            return true;
        }

        // คืน true เมื่อถึงปลายทาง (หรือไปต่อไม่ได้แล้ว)
        bool StepAlongPath(Ant a, float dt, float interval)
        {
            if (a.path == null || a.pathStep >= a.path.Count) return true;

            a.moveTimer -= dt;
            if (a.moveTimer > 0f) return false;
            a.moveTimer = interval;

            int next = a.path[a.pathStep];
            if (!world.Walkable(next))
            {
                if (!SetPath(a, a.targetTile)) { a.path = null; return true; }
                return false;
            }

            a.tile = next;
            a.pathStep++;
            return a.pathStep >= a.path.Count;
        }

        // ---------- utility ----------

        static readonly int[] NX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] NY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public bool IsNeighbor(int a, int b)
        {
            int ax = a % world.W, ay = a / world.W;
            int bx = b % world.W, by = b / world.W;
            return Mathf.Abs(ax - bx) <= 1 && Mathf.Abs(ay - by) <= 1;
        }

        int AccessSpot(int target, int from)
        {
            int tx = target % world.W, ty = target / world.W;
            int best = -1; float bestD = float.MaxValue;
            for (int d = 0; d < 8; d++)
            {
                int nx = tx + NX[d], ny = ty + NY[d];
                if (!world.Walkable(nx, ny)) continue;
                int ni = world.Idx(nx, ny);
                float dist = Dist2(from, ni);
                if (dist < bestD) { bestD = dist; best = ni; }
            }
            return best;
        }

        float Dist2(int a, int b)
        {
            int ax = a % world.W, ay = a / world.W;
            int bx = b % world.W, by = b / world.W;
            float dx = ax - bx, dy = ay - by;
            return dx * dx + dy * dy;
        }

        void Block(int tile) { blocked[tile] = simTime + 4f; }

        bool IsBlocked(int tile)
        {
            float until;
            return blocked.TryGetValue(tile, out until) && until > simTime;
        }

        // ---------- คำสั่งจากผู้เล่น ----------

        public void ApplyMark(int x, int y, Mark m)
        {
            if (!world.InBounds(x, y)) return;
            int i = world.Idx(x, y);
            Tile t = world.tiles[i];

            if (t == Tile.Stone || t == Tile.Sky) return;

            if (m == Mark.None)
            {
                world.marks[i] = Mark.None;
                digJobs.Remove(i);
                claimedDig.Remove(i);
                if (Tiles.IsChamber(t)) world.Set(x, y, Tile.Tunnel);
                return;
            }

            if (Tiles.IsOpen(t))
            {
                // ขุดไว้แล้ว แค่เปลี่ยนหน้าที่ของห้อง
                world.marks[i] = Mark.None;
                world.Set(x, y, Tiles.MarkResult(m));
                return;
            }

            if (Tiles.IsDiggable(t) && world.marks[i] != m)
            {
                world.marks[i] = m;
                if (!digJobs.Contains(i)) digJobs.Add(i);
            }
        }

        public int LarvaCount()
        {
            int n = 0;
            for (int i = 0; i < broods.Count; i++) if (broods[i].stage == BroodStage.Larva) n++;
            return n;
        }
    }
}
