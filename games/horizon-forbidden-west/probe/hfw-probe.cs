// hfw-probe — phase 5 milestone 1.
//
// Read-only injected module. The DLL samples at 10 Hz from its own thread.
// It never writes game memory, never hooks Present, and never calls a game
// function. The loader does not attach a console to the game process.
//
// Injection pattern (Strikers, MIT, github.com/everydayaussie/Strikers):
// an external .NET process OpenProcess's HorizonForbiddenWest.exe, checks the
// loaded image identity, VirtualAllocEx + WriteProcessMemory a tiny stub,
// and CreateRemoteThread's it. Strikers uses that stub only to call the
// game's allocator (live-probe/Program.GameAlloc.cs, Program.BlightLoad.cs).
// Their published memory map is Machine Strike only — no player transform —
// so the anchors below come from a targeted Ghidra pass on this exact exe
// (local/player_world_chain.txt, local/getlp_fn.txt). Build guard is copied
// from live-probe KnownBuilds ("667B1777-949F000") and strengthened to the
// full SHA-256 of the exe on disk.
//
// Pointer chain (image base 0x140000000, RVAs):
// gPlayerManagerGame RVA 0x8982C80 PlayerManagerGame*
// +0x10 first player-list storage (ptr[])
// +0x08 count of that list (int32)
// player = [storage]
// +0x48 Entity* (FUN_140916bf0)
// entity
// entity+0xD8 is three qwords (FUN_140d4f750 copies +0xD8/+0xE0/+0xE8).
// The live 2026-10-21 log showed those are not a position.
// The world transform is the Mat34 at entity+0xF0 (0x3C bytes, 3x4 floats,
// row-major). Entity_ExportedGetWorldTransform is bound by FUN_14095bfb0
// to FUN_140962640, which copies that matrix under the lock at entity+0x298.
// Translation is the last row (+0x114/+0x118/+0x11C). Forward is row 2.
// [[scene+0x248]] counted 0x2280 -> 0x32AC on that run: a counter, not a
// world id. No stable world id was found.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

internal static class Program
{
 private const string ExpectedSha256 =
 "9cec6626ab60059d186edbacca4ce667573e8b28c916fca1e07072002055429e";

 private const uint ExpectedTimeDateStamp = 0x667B1777;
 private const uint ExpectedSizeOfImage = 0x0949F000;

 private const ulong RvaPlayerManager = 0x8982C80;
 private const ulong RvaSceneManager = 0x8982DD0;
 private const int OffListCount = 0x08;
 private const int OffListStorage = 0x10;
 private const int OffPlayerEntity = 0x48;
 private const int OffEntityOld = 0xD8;
 private const int OffEntityMat = 0xF0;
 private const int OffSceneCounter = 0x248;

 private const uint ProcessVmRead = 0x0010;
 private const uint ProcessQueryInformation = 0x0400;

 public static int Main(string[] args)
 {
 if (args.Length > 0 && args[0] == "--selftest")
 {
 return SelfTest(args.Length > 1 ? args[1] : DefaultExe());
 }

 var exe = Arg(args, "--exe") ?? DefaultExe();
 var seconds = int.TryParse(Arg(args, "--seconds"), out var s) ? s : 45;
 var logPath = Arg(args, "--log") ?? Path.Combine(AppContext.BaseDirectory, "hfw-probe.log");

 if (!Guard(exe, out var why))
 {
 Console.Error.WriteLine("REFUSED: " + why);
 return 2;
 }

 var proc = Process.GetProcessesByName("HorizonForbiddenWest").FirstOrDefault();
 if (proc is null)
 {
 Console.Error.WriteLine("REFUSED: HorizonForbiddenWest.exe is not running.");
 return 3;
 }

 var dll = Arg(args, "--dll") ?? Path.Combine(AppContext.BaseDirectory, "hfw_probe.dll");
 if (!File.Exists(dll))
 {
 Console.Error.WriteLine($"REFUSED: probe dll not found: {dll}");
 return 5;
 }

 // Strikers' shape: VirtualAllocEx + WriteProcessMemory + CreateRemoteThread,
 // here pointed at LoadLibraryW so hfw_probe.dll runs inside the game.
 // The DLL only reads. The loader never writes game memory.
 var injected = Inject(proc.Id, Path.GetFullPath(dll), out var whyNot);
 if (!injected)
 {
 Console.Error.WriteLine("REFUSED: inject failed: " + whyNot);
 return 4;
 }

 Console.WriteLine($"injected {dll} into pid {proc.Id}; log is hfw-probe.log in the game's working directory.");
 Console.WriteLine($"waiting {seconds}s for frames, then the dll's thread stops on its own.");
 Thread.Sleep(TimeSpan.FromSeconds(seconds));
 return 0;
 }

 private const uint ProcessCreateThread = 0x0002;
 private const uint ProcessVmOperation = 0x0008;
 private const uint ProcessVmWrite = 0x0020;
 private const uint MemCommitReserve = 0x3000;
 private const uint PageReadWrite = 0x04;

 internal static bool Inject(int pid, string dllPath, out string why)
 {
 why = "";
 var access = ProcessCreateThread | ProcessQueryInformation | ProcessVmOperation
 | ProcessVmWrite | ProcessVmRead;
 var handle = OpenProcess(access, false, pid);
 if (handle == 0)
 {
 why = $"OpenProcess failed ({Marshal.GetLastWin32Error()})";
 return false;
 }

 try
 {
 var wide = Encoding.Unicode.GetBytes(dllPath + "\0");
 var remote = VirtualAllocEx(handle, 0, (nint)wide.Length, MemCommitReserve, PageReadWrite);
 if (remote == 0)
 {
 why = $"VirtualAllocEx failed ({Marshal.GetLastWin32Error()})";
 return false;
 }

 if (!WriteProcessMemory(handle, remote, wide, wide.Length, out var wrote) || wrote != wide.Length)
 {
 why = $"WriteProcessMemory failed ({Marshal.GetLastWin32Error()})";
 return false;
 }

 var loadLibrary = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
 if (loadLibrary == 0)
 {
 why = "LoadLibraryW not found";
 return false;
 }

 var thread = CreateRemoteThread(handle, 0, 0, loadLibrary, remote, 0, out _);
 if (thread == 0)
 {
 why = $"CreateRemoteThread failed ({Marshal.GetLastWin32Error()})";
 return false;
 }

 WaitForSingleObject(thread, 10000);
 CloseHandle(thread);
 return true;
 }
 finally
 {
 CloseHandle(handle);
 }
 }

 private static string DeadReadPath(nint handle, Process proc, int seconds, string logPath)
 {
 return "";
 }

 // One frame. Reads only. A null along the chain is logged, not skipped silently.
 internal static string ReadFrame(nint handle, ulong moduleBase)
 {
 var pm = ReadU64(handle, moduleBase + RvaPlayerManager);
 var scene = ReadU64(handle, moduleBase + RvaSceneManager);
 var world = WorldId(handle, scene);

 if (pm == 0)
 return $"frame counter={world} player=null";

 var count = ReadI32(handle, pm + OffListCount);
 var storage = ReadU64(handle, pm + OffListStorage);
 if (count <= 0 || storage == 0)
 return $"frame counter={world} pm=0x{pm:X} players={count}";

 var player = ReadU64(handle, storage);
 if (player == 0)
 return $"frame counter={world} player=empty";

 var entity = ReadU64(handle, player + OffPlayerEntity);
 if (entity == 0)
 return $"frame counter={world} player=0x{player:X} entity=null";

 var raw = ReadBytes(handle, entity + OffEntityOld, 24);
 var mat = ReadBytes(handle, entity + OffEntityMat, 60);
 var tx = BitConverter.ToSingle(mat, 0x24);
 var ty = BitConverter.ToSingle(mat, 0x28);
 var tz = BitConverter.ToSingle(mat, 0x2C);
 var hx = BitConverter.ToSingle(mat, 0x18);
 var hy = BitConverter.ToSingle(mat, 0x1C);
 var hz = BitConverter.ToSingle(mat, 0x20);
 return $"frame counter={world} entity=0x{entity:X} pos=({tx:F3},{ty:F3},{tz:F3}) fwd=({hx:F3},{hy:F3},{hz:F3}) old={BitConverter.ToString(raw)}";
 }

 private static string WorldId(nint handle, ulong scene)
 {
 if (scene == 0) return "none";
 var resource = ReadU64(handle, scene + OffSceneCounter);
 if (resource == 0) return "0";
 var id = ReadU32(handle, resource);
 return $"0x{id:X8}";
 }

 internal static bool Guard(string exePath, out string why)
 {
 why = "";
 if (!File.Exists(exePath))
 {
 why = $"exe not found: {exePath}";
 return false;
 }

 string hash;
 using (var sha = SHA256.Create())
 using (var fs = File.OpenRead(exePath))
 hash = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();

 if (hash != ExpectedSha256)
 {
 why = $"sha256 {hash} != {ExpectedSha256}";
 return false;
 }

 // Strikers' runtime guard: PE TimeDateStamp + SizeOfImage, read from the file header.
 using var fs2 = File.OpenRead(exePath);
 using var br = new BinaryReader(fs2);
 fs2.Seek(0x3C, SeekOrigin.Begin);
 var peOff = br.ReadInt32();
 fs2.Seek(peOff + 8, SeekOrigin.Begin); // TimeDateStamp
 var stamp = br.ReadUInt32();
 fs2.Seek(peOff + 24 + 56, SeekOrigin.Begin); // OptionalHeader.SizeOfImage
 var size = br.ReadUInt32();
 if (stamp != ExpectedTimeDateStamp || size != ExpectedSizeOfImage)
 {
 why = $"pe {stamp:X8}-{size:X} != {ExpectedTimeDateStamp:X8}-{ExpectedSizeOfImage:X}";
 return false;
 }
 return true;
 }

 private static int SelfTest(string exe)
 {
 var fail = 0;
 void Check(string name, bool ok, string detail)
 {
 Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name} {detail}");
 if (!ok) fail++;
 }

 Check("sha256+pe guard", Guard(exe, out var why), why);

 // Byte anchors. Strikers rows (already verified) plus the chain this probe walks.
 var anchors = new (string name, ulong rva, string hex)[]
 {
 ("strikers patch-site", 0xE39A41, "80 79 28 00 74 52"),
 ("strikers coin-flip", 0xE54F95, "41 81 E0 01 00 00 80 7D 0A 41 FF C8 41 83 C8 FE 41 FF C0"),
 ("strikers vtable slot", 0x190D718, "B0 F3 E3 40 01 00 00 00"),
 ("GetLocalPlayer name", 0x18A9E08, "47 65 74 4C 6F 63 61 6C 50 6C 61 79 65 72 00"),
 ("local-player getter", 0x916BF0, "48 83 EC 28 48 8B 0D 85 C0 06 08 E8 80 FE FF FF"),
 ("GetLocalPlayer fn", 0x928430, "4C 8B DC 49 89 73 18 57 48 83 EC 30"),
 ("PlayerManagerGame vftable", 0x18FBE60, "30 CA D5 40 01 00 00 00"),
 ("SceneManagerGame vftable", 0x18FB458, "70 92 AA 40 01 00 00 00"),
 ("entity qword read (old)", 0xD4F8D4, "C5 F8 10 B5 D8 00 00 00"),
 ("entity qword read 2 (old)", 0xD4F8DC, "C5 FB 10 BD E8 00 00 00"),
 ("GetWorldTransform fn", 0x962640, "48 89 74 24 10 57 48 83 EC 60"),
 ("Mat34 load", 0x962697, "C5 FC 10 87 F0 00 00 00"),
 };

 var pe = File.ReadAllBytes(exe);
 foreach (var (name, rva, hex) in anchors)
 {
 var expect = Convert.FromHexString(hex.Replace(" ", ""));
 var off = RvaToOffset(pe, rva);
 var got = new byte[expect.Length];
 Buffer.BlockCopy(pe, off, got, 0, got.Length);
 Check(name, got.AsSpan().SequenceEqual(expect), $"RVA 0x{rva:X} @file 0x{off:X}");
 }

 Console.WriteLine(fail == 0 ? "SELFTEST OK" : $"SELFTEST {fail} FAILED");
 return fail == 0 ? 0 : 1;
 }

 // Minimal PE: RVA -> file offset. Good enough for this exe (no packing).
 private static int RvaToOffset(byte[] pe, ulong rva)
 {
 var peOff = BitConverter.ToInt32(pe, 0x3C);
 var sectionCount = BitConverter.ToInt16(pe, peOff + 6);
 var optSize = BitConverter.ToInt16(pe, peOff + 20);
 var sec = peOff + 24 + optSize;
 for (var i = 0; i < sectionCount; i++)
 {
 var s = sec + i * 40;
 var virt = BitConverter.ToUInt32(pe, s + 12);
 var rawSize = BitConverter.ToUInt32(pe, s + 16);
 var raw = BitConverter.ToUInt32(pe, s + 20);
 if (rva >= virt && rva < virt + Math.Max(rawSize, 1))
 return (int)(raw + (rva - virt));
 }
 throw new InvalidOperationException($"RVA 0x{rva:X} not in any section");
 }

 private static ulong ModuleBase(Process proc)
 {
 foreach (ProcessModule m in proc.Modules)
 {
 if (m.ModuleName.Equals("HorizonForbiddenWest.exe", StringComparison.OrdinalIgnoreCase))
 return (ulong)m.BaseAddress;
 }
 throw new InvalidOperationException("module not found in process");
 }

 private static string DefaultExe() =>
 Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
 "local", "workspace", "hfw-binaries", "HorizonForbiddenWest.exe");

 private static string? Arg(string[] args, string name)
 {
 for (var i = 0; i < args.Length - 1; i++)
 if (args[i] == name) return args[i + 1];
 return null;
 }

 private static ulong ReadU64(nint h, ulong addr)
 {
 var b = ReadBytes(h, addr, 8);
 return BitConverter.ToUInt64(b, 0);
 }

 private static uint ReadU32(nint h, ulong addr) => BitConverter.ToUInt32(ReadBytes(h, addr, 4), 0);
 private static int ReadI32(nint h, ulong addr) => BitConverter.ToInt32(ReadBytes(h, addr, 4), 0);

 private static byte[] ReadBytes(nint h, ulong addr, int n)
 {
 var buf = new byte[n];
 if (!ReadProcessMemory(h, (nint)addr, buf, n, out var got) || got != n)
 return new byte[n];
 return buf;
 }

 [DllImport("kernel32", SetLastError = true)]
 private static extern nint OpenProcess(uint access, bool inherit, int pid);

 [DllImport("kernel32", SetLastError = true)]
 private static extern bool ReadProcessMemory(nint h, nint addr, byte[] buf, int size, out int read);

 [DllImport("kernel32", SetLastError = true)]
 private static extern bool WriteProcessMemory(nint h, nint addr, byte[] buf, int size, out int written);

 [DllImport("kernel32", SetLastError = true)]
 private static extern nint VirtualAllocEx(nint h, nint addr, nint size, uint type, uint protect);

 [DllImport("kernel32", SetLastError = true)]
 private static extern nint CreateRemoteThread(nint h, nint attrs, nint stack, nint start, nint param, uint flags, out uint tid);

 [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
 private static extern nint GetModuleHandle(string name);

 [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
 private static extern nint GetProcAddress(nint module, string name);

 [DllImport("kernel32")]
 private static extern uint WaitForSingleObject(nint h, uint ms);

 [DllImport("kernel32")]
 private static extern bool CloseHandle(nint h);
}
