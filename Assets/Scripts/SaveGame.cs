using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace AntTime
{
    // เซฟทั้งโลกและอาณานิคมลงไฟล์เดียว บีบอัดด้วย gzip
    // (ตาราง tile ดิบ ๆ ประมาณ 200 KB บีบแล้วเหลือหลักสิบ KB)
    public static class SaveGame
    {
        const uint Magic = 0x544E4141;   // "AANT"
        const int Version = 1;

        public const string ManualSlot = "save1.dat";
        public const string AutoSlot = "autosave.dat";

        public static string PathFor(string slot)
        {
            return Path.Combine(Application.persistentDataPath, slot);
        }

        public static bool Exists(string slot) => File.Exists(PathFor(slot));

        public static bool Save(string slot, World world, Colony colony)
        {
            string path = PathFor(slot);
            string temp = path + ".tmp";
            try
            {
                using (var file = File.Create(temp))
                using (var gzip = new GZipStream(file, CompressionMode.Compress))
                using (var w = new BinaryWriter(gzip))
                {
                    w.Write(Magic);
                    w.Write(Version);
                    world.Write(w);
                    colony.Write(w);
                }

                // เขียนลงไฟล์ชั่วคราวก่อนแล้วค่อยสลับ ถ้าเกมดับกลางคัน เซฟเดิมจะไม่พัง
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveGame] เซฟไม่สำเร็จ: " + e.Message);
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                return false;
            }
        }

        // คืน false ถ้าโหลดไม่ได้ โดยไม่แตะ world/colony ที่กำลังเล่นอยู่
        public static bool Load(string slot, out World world, out Colony colony)
        {
            world = null;
            colony = null;
            string path = PathFor(slot);
            if (!File.Exists(path)) return false;

            try
            {
                using (var file = File.OpenRead(path))
                using (var gzip = new GZipStream(file, CompressionMode.Decompress))
                using (var r = new BinaryReader(gzip))
                {
                    if (r.ReadUInt32() != Magic) throw new InvalidDataException("ไม่ใช่ไฟล์เซฟของ AntTime");
                    int version = r.ReadInt32();
                    if (version != Version) throw new InvalidDataException("เซฟเวอร์ชัน " + version + " ใช้กับเกมเวอร์ชันนี้ไม่ได้");

                    var w = World.Read(r);
                    var c = new Colony(w, false);
                    c.ReadState(r);

                    world = w;
                    colony = c;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveGame] โหลดไม่สำเร็จ: " + e.Message);
                world = null;
                colony = null;
                return false;
            }
        }
    }
}
