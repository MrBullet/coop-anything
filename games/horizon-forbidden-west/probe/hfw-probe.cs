// hfw-probe — phase 5.
//
// Default: read-only injected sampler. The DLL samples at 10 Hz from its
// own thread. It never writes game memory, never hooks Present, and never
// calls a game function.
//
// --spawn-once: the DLL waits until the player-list count is at least 1,
// calls FUN_14134c950 once, logs the count again, and exits. It does not
// write the entity double3. The loader writes one byte next to the DLL
// (hfw_probe.dll.mode, not the game directory). The DLL reads that byte
// from its own module path. Default stays the 45 s sampler.
//
// The second live run (450/450 samples, sane=1, counter climbing, pos
// frozen) froze because this exe is a console process: opening it steals
// foreground, and HFW pauses on focus loss. It is a WinExe (WINDOWS
// subsystem) so it has no console to activate. It also calls
// AllowSetForegroundWindow(ASFW_ANY) before the wait so the game can
// take the foreground back. The DLL never AllocConsole / AttachConsole.
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
// World position is the double3 at entity+0xD8/+0xE0/+0xE8. The walk run
// (local/hfw-probe-run2.log) tracked it; the Mat34 translation at
// entity+0xF0 did not. FUN_140962590 copies those three qwords under the
// lock at entity+0x298. The probe reads them and does not take the lock.
// The Mat34 stays a side column. [[scene+0x248]] is a counter, not a world id.
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
 private const int OffEntityPos = 0xD8;
 private const int OffEntityMat = 0xF0;
 private const int OffSceneCounter = 0x248;

 private const uint ProcessVmRead = 0x0010;
 private const uint ProcessQueryInformation = 0x0400;

 public static int Main(string[] args)
 {
 try
 {
 return Run(args);
 }
 catch (Exception ex)
 {
 Note("EXCEPTION: " + ex.GetType().Name + ": " + ex.Message);
 return 1;
 }
 }

 private static int Run(string[] args)
 {
 Note("args=[" + string.Join(" | ", args) + "]");
 if (HasFlag(args, "--selftest"))
 {
 var selfExe = Arg(args, "--selftest");
 if (string.IsNullOrEmpty(selfExe))
 {
 var idx = Array.IndexOf(args, "--selftest");
 selfExe = (idx >= 0 && idx + 1 < args.Length) ? JoinPath(args, idx + 1) : DefaultExe();
 }
 if (string.IsNullOrEmpty(selfExe)) selfExe = DefaultExe();
 Note("selftest exe=" + selfExe);
 return SelfTest(selfExe);
 }

 var spawnOnce = HasFlag(args, "--spawn-once");
 Note(spawnOnce ? "spawn flag seen" : "spawn flag absent");

 var exe = Arg(args, "--exe") ?? DefaultExe();
 var seconds = int.TryParse(Arg(args, "--seconds"), out var s) ? s : 45;
 Note("exe=" + exe);

 if (!Guard(exe, out var why))
 {
 Note("hash fail " + why);
 Note("REFUSED: " + why);
 return 2;
 }
 Note("hash ok " + ExpectedSha256 + " stamp=667B1777-949F000");

 var proc = Process.GetProcessesByName("HorizonForbiddenWest").FirstOrDefault();
 if (proc is null)
 {
 Note("process not found");
 Note("REFUSED: HorizonForbiddenWest.exe is not running.");
 return 3;
 }
 Note($"process found pid={proc.Id}");

 var dll = Arg(args, "--dll") ?? Path.Combine(AppContext.BaseDirectory, "hfw_probe.dll");
 if (!File.Exists(dll))
 {
 Note($"REFUSED: probe dll not found: {dll}");
 return 5;
 }

 // Strikers' shape: VirtualAllocEx + WriteProcessMemory + CreateRemoteThread,
 // here pointed at LoadLibraryW so hfw_probe.dll runs inside the game.
 // The default DLL only reads. --spawn-once calls FUN_14134c950 once.
 // The mode byte sits next to the DLL, which the game never writes.
 var injected = Inject(proc.Id, Path.GetFullPath(dll), spawnOnce, out var whyNot);
 if (!injected)
 {
 Note("inject fail " + whyNot);
 Note("REFUSED: inject failed: " + whyNot);
 return 4;
 }
 Note(spawnOnce ? "injected mode=spawn-once" : "injected mode=sample");

 // HFW pauses when it loses foreground. This process must not become it.
 // ASFW_ANY lets the game call SetForegroundWindow and take focus back.
 AllowSetForegroundWindow(AsfwAny);
 if (spawnOnce)
 {
 Note($"injected {Path.GetFullPath(dll)} into pid {proc.Id} mode=spawn-once. " +
 "one call of FUN_14134c950. log is hfw-probe.log in the game's working directory. " +
 "load a save first. alt-f4 if the game stays wedged.");
 return 0;
 }
 Note($"injected {Path.GetFullPath(dll)} into pid {proc.Id}. " +
 "log is hfw-probe.log in the game's working directory. " +
 $"waiting {seconds}s; the dll thread stops on its own.");
 Thread.Sleep(TimeSpan.FromSeconds(seconds));
 return 0;
 }

 private const int AsfwAny = -1;

 // WinExe has no console. Selftest still prints; a live run writes a file
 // next to the exe so a refusal is visible without a window.
 private static void Note(string line)
 {
 try
 {
 Console.Error.WriteLine(line);
 }
 catch (IOException)
 {
 }
 var path = Path.Combine(AppContext.BaseDirectory, "hfw-probe-run.txt");
 var bytes = Encoding.UTF8.GetBytes(line + "\r\n");
 for (var attempt = 0; attempt < 8; attempt++)
 {
 try
 {
 using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
 fs.Write(bytes, 0, bytes.Length);
 return;
 }
 catch (IOException)
 {
 Thread.Sleep(30);
 }
 catch (UnauthorizedAccessException)
 {
 return;
 }
 }
 }

 private const uint ProcessCreateThread = 0x0002;
 private const uint ProcessVmOperation = 0x0008;
 private const uint ProcessVmWrite = 0x0020;
 private const uint MemCommitReserve = 0x3000;
 private const uint PageReadWrite = 0x04;

 internal static bool Inject(int pid, string dllPath, bool spawnOnce, out string why)
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
 // One byte in front of the wide path. The DLL reads it back from its
 // own file (the loader's copy, not the game directory) to decide
 // which thread to start. The remote thread is still LoadLibraryW.
 File.WriteAllBytes(dllPath + ".mode", new[] { spawnOnce ? (byte)1 : (byte)0 });
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

 var raw = ReadBytes(handle, entity + OffEntityPos, 24);
 var mat = ReadBytes(handle, entity + OffEntityMat, 60);
 var x = BitConverter.ToDouble(raw, 0);
 var y = BitConverter.ToDouble(raw, 8);
 var z = BitConverter.ToDouble(raw, 16);
 var mx = BitConverter.ToSingle(mat, 0x24);
 var my = BitConverter.ToSingle(mat, 0x28);
 var mz = BitConverter.ToSingle(mat, 0x2C);
 return $"frame counter={world} entity=0x{entity:X} pos=({x:F3},{y:F3},{z:F3}) mat=({mx:F3},{my:F3},{mz:F3})";
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
 ("double3 load +0xD8", 0x962681, "C5 F8 10 87 D8 00 00 00"),
 ("double3 load +0xE8", 0x962689, "C5 FB 10 8F E8 00 00 00"),
 ("ctor zeroes +0xD8", 0x948AEE, "48 89 A9 D8 00 00 00"),
 ("writer store +0xD8", 0x101C83F, "C4 C1 7B 11 B6 D8 00 00"),
 // FUN_140d8c2d0 is PlayerManagerGame::vftable+8. This is the arm
 // that pushes into pm+8: LEA RCX,[RSI+8]; MOV [RSP+0x78],RBX;
 // LOCK INC [RBX+0x18]; LEA RDX,[RSP+0x78]; CALL FUN_1402250c0.
 ("player-list insert pm+8", 0xD8C381, "48 8D 4E 08 48 89 5C 24 78 F0 FF 43 18 48 8D 54 24 78 E8 28 8D 49 FF"),
 // FUN_14134c950 prologue: MOV [RSP+0x10],RBX / MOV [RSP+0x18],RSI /
 // MOV [RSP+0x20],RDI / MOV [RSP+8],RCX. The call site in
 // FUN_14134c590 is a bare E8 with no register setup.
 ("spawn FUN_14134c950", 0x134C950, "48 89 5C 24 10 48 89 74 24 18 48 89 7C 24 20 48"),
 ("spawn call site", 0x134C657, "E8 F4 02 00 00"),
 // size dword the spawn function passes to FUN_1401e6100 (0x210).
 ("spawn alloc size", 0x21615A0, "10 02 00 00"),
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

 // Start-Process -ArgumentList '--exe','M:\Horizon Forbidden West\...'
 // splits the path on spaces before the exe sees it. Rejoin until the next flag.
 private static bool IsFlag(string a) => a.StartsWith("--", StringComparison.Ordinal);

 private static string? Arg(string[] args, string name)
 {
 for (var i = 0; i < args.Length - 1; i++)
 {
 if (args[i] != name) continue;
 return JoinPath(args, i + 1);
 }
 return null;
 }

 private static string JoinPath(string[] args, int start)
 {
 var parts = new List<string>();
 for (var j = start; j < args.Length; j++)
 {
 if (IsFlag(args[j])) break;
 parts.Add(args[j]);
 }
 return parts.Count == 0 ? "" : string.Join(" ", parts);
 }

 private static bool HasFlag(string[] args, string name)
 {
 foreach (var a in args)
 if (a == name) return true;
 return false;
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

 [DllImport("user32")]
 private static extern bool AllowSetForegroundWindow(int processId);
}
