// hfw-probe — phase 5.
//
// Default: read-only injected sampler. The DLL samples at 10 Hz from its
// own thread. It never writes game memory, never hooks Present, and never
// calls a game function.
//
// --spawn-once: the DLL does not call FUN_14134c950. One live call
// raised 0xC0000005 on MOV RCX,[RAX+0xF8] where RAX was loaded from
// the null global at VA 0x1489581F0 (RVA 0x89581F0). The flag still
// injects, but the thread only logs "spawn-once blocked" and exits.
// It does not write the entity double3. The loader writes one byte
// next to the DLL (hfw_probe.dll.mode, not the game directory). The
// DLL reads that byte from its own module path. Default stays the
// 45 s sampler.
//
// --read-slot: opt-in. Mode byte 2. The DLL waits until the GameModule*
// at RVA 0x8983150 is non-null, then for 20 s at 2 Hz logs
// slot module=0x.. plus50=0x.. entity=0x..
// plus50 is [module+0x50]. entity is [[module+0x50]+0x20] only when
// plus50 is non-null. It calls no game function and writes nothing.
// A line with plus50 non-null is the slot being live. --spawn-once
// stays the blocked log.
//
// --add-player: opt-in. Mode byte 3. One shot, then the thread exits.
// Waits until GameModule*, [module+0x50], the entity at
// [[module+0x50]+0x20], [0x148982CB0] and the player count at
// [PlayerManager+8] are all non-null / >= 1, logs them, then calls
// FUN_1401e6100(0x210) once and stops. It does not call FUN_140d73270:
// that constructor stores the live entity at [block+0x30] and pushes
// the block onto the camera manager's list, so the next tick drives
// the owner's entity a second time. It does not call FUN_1402250c0:
// the list walkers (FUN_140916a80 at RVA 0x916AC8, FUN_140916c20 at
// RVA 0x916C43) load [PlayerGame+0xE8], which FUN_1413d8470 writes
// and this probe does not. The count stays 1. FUN_1413d8470,
// FUN_14134c950 and FUN_14101bd70 are not called, and entity+0xD8 is
// not written. No second character. The line to look for is
// "add-player stop count=1 seh=0x00000000 block=0x.. (ctor and insert not called".
// --spawn-once and --read-slot are unchanged.
//
// --read-cam: opt-in. Mode byte 4. Same gate as --read-slot (GameModule*
// at RVA 0x8983150 non-null), then for 10 s at 2 Hz logs
// cam ptr=0x.. lock20=0x..
// ptr is the qword at RVA 0x8982CB0 (VA 0x148982CB0). lock20 is the
// first qword at [ptr+0x20] only when ptr is non-null, inside SEH.
// It does not call AcquireSRWLockExclusive or any game function and
// writes nothing. A line with ptr non-null means the manager is alive.
// Default, --read-slot, --spawn-once and --add-player are unchanged.
//
// --alloc-entity: opt-in. Mode byte 5. One shot, then the thread exits.
// It does not call FUN_1401f82b0. That factory takes a live parent
// container in RCX (the first instruction that reads an argument is
// MOV RAX,[RCX+0x58] at RVA 0x1F82CC) and the only real caller,
// FUN_1401f7bd0 at RVA 0x1F8048, passes MOV RCX,RBX where RBX was set
// by LEA RBX,[RDI-0x20] at 0x1F8012. The type
// record is not a register of the factory; it sits on that parent's
// stack. The probe cannot name that parent, so the factory is not
// called and no vector is passed. Instead, once GameModule* at RVA
// 0x8983150 is non-null, the thread runs the four inner steps once,
// inside SEH, with the ControlledEntity record at RVA 0x21DA6C0:
// FUN_1402033d0(record), FUN_1402032d0(record), FUN_1401e6100(size),
// FUN_1411a0980(record, block). It does not call FUN_1401f96d0, so
// the block is not pushed onto any vector the game walks. It does
// not call FUN_140d73270, FUN_14134c950, FUN_1413d8470, FUN_14101bd70,
// FUN_1402250c0 or FUN_1400fe310, and it does not write entity+0xD8.
// The line to look for is
// "alloc-entity stop ptr=0x.. vtable=0x.. size=0x300".
// This does not spawn a visible character and does not touch the
// owner's player. The pointer in the log is the whole result.
//
// --link-puppet: opt-in. Mode byte 6. One shot, then the thread exits.
// Same four steps as --alloc-entity (FUN_1401f82b0 is not called),
// then FUN_1401e6100(0x210) and FUN_140d73270(playerBlock, newEntity)
// once. The entity argument is the new ControlledEntity, not
// [[GameModule+0x50]+0x20]. FUN_1402250c0 is not called. The camera
// walker FUN_140930e90 calls the published entry's vtable+8
// (FUN_140d794f0), which calls FUN_1409150e0. That function's first
// field load is the count at [PlayerGame+0xF0], which FUN_140913be0
// stores as 0 at RVA 0x913C9C before the publish at 0xD733E7, and the
// JS arm at 0x915113 returns without loading the pointer at +0xF8.
// The line to look for is
// "link-puppet stop player=0x.. entity=0x.. vtable=0x.. seh=0x00000000".
// No visible body is expected. The owner's character should not move.
// A hitch is possible while the constructor holds the camera lock;
// alt-f4 if the game wedges. Start a fresh session and load a save.
//
// --register-entity: opt-in. Mode byte 7. One shot, then the thread
// exits. Same GameModule* gate as --alloc-entity (RVA 0x8983150).
// Same four ControlledEntity steps (record RVA 0x21DA6C0, size
// 0x300). FUN_1401f82b0 is not called. The parent UUID is the 16
// bytes at [[PlayerGame+0x30]+0x8], and that PlayerGame is the first
// entry the sampler already walks ([[PlayerManager+0x10]],
// PlayerManager* at RVA 0x8982C80). A null PlayerManager* or a null
// live entity returns without the call. The key is MD5 of those 16
// bytes plus a slot dword (0, then 1, then 2), little endian, with
// byte 6 and byte 8 patched the way FUN_14011b2b0 does. The 16 bytes
// are stored at [entity+0x8]. FUN_140986bc0 is not called. A slot
// whose digest is already at [record+0] of [EntityManagerGame+0x1d8]
// (count at +0x1e4, stride 0x20, EntityManagerGame* at RVA
// 0x8982C40) is skipped. The first free slot in 0..63 is used. All
// 64 taken returns without the call. Then, inside the same SEH,
// FUN_140901310(RCX unused, RDX = entity, R8 = 1, R9 = 0, 5th = 0).
// R8 = 1 skips [entity+0x68]. entity+0xD8 is not written.
// FUN_1402250c0, FUN_14134c950, FUN_1413d8470 and FUN_14101bd70 are
// not called. The line to look for is
// "register-entity slot=.. key=.. entity=0x.. seh=0x00000000 count_before=.. count_after=..".
// --add-player stays alloc-only. --link-puppet is unchanged.
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
 var readSlot = HasFlag(args, "--read-slot");
 var addPlayer = HasFlag(args, "--add-player");
 var readCam = HasFlag(args, "--read-cam");
 var allocEntity = HasFlag(args, "--alloc-entity");
 var linkPuppet = HasFlag(args, "--link-puppet");
 var registerEntity = HasFlag(args, "--register-entity");
 Note(spawnOnce ? "spawn flag seen" : "spawn flag absent");
 Note(readSlot ? "read-slot flag seen" : "read-slot flag absent");
 Note(addPlayer ? "add-player flag seen" : "add-player flag absent");
 Note(readCam ? "read-cam flag seen" : "read-cam flag absent");
 Note(allocEntity ? "alloc-entity flag seen" : "alloc-entity flag absent");
 Note(linkPuppet ? "link-puppet flag seen" : "link-puppet flag absent");
 Note(registerEntity ? "register-entity flag seen" : "register-entity flag absent");
 var modes = (spawnOnce ? 1 : 0) + (readSlot ? 1 : 0) + (addPlayer ? 1 : 0)
 + (readCam ? 1 : 0) + (allocEntity ? 1 : 0) + (linkPuppet ? 1 : 0)
 + (registerEntity ? 1 : 0);
 if (modes > 1)
 {
 Note("REFUSED: --spawn-once, --read-slot, --add-player, --read-cam, --alloc-entity, --link-puppet and --register-entity are mutually exclusive.");
 return 2;
 }

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
 // A live game keeps the previous hfw_probe.dll mapped, so a rebuild
 // cannot overwrite it. The build drops the new DLL beside it under
 // this name; prefer it. Once the game is closed, the next build
 // replaces hfw_probe.dll and this file is gone.
 var newer = Path.Combine(AppContext.BaseDirectory, "hfw_probe_new.dll");
 if (Arg(args, "--dll") is null && File.Exists(newer)) dll = newer;
 if (!File.Exists(dll))
 {
 Note($"REFUSED: probe dll not found: {dll}");
 return 5;
 }

 // Strikers' shape: VirtualAllocEx + WriteProcessMemory + CreateRemoteThread,
 // here pointed at LoadLibraryW so hfw_probe.dll runs inside the game.
 // The default DLL only reads. --spawn-once injects a DLL that logs
 // the block and does not call FUN_14134c950. --read-slot injects a
 // DLL that only reads [GameModule+0x50]. The mode byte sits next
 // to the DLL, which the game never writes. 0 sampler, 1 spawn-once,
 // 2 read-slot, 3 add-player (one shot), 4 read-cam, 5 alloc-entity,
 // 6 link-puppet (one shot), 7 register-entity (one shot).
 var mode = spawnOnce ? 1 : (readSlot ? 2 : (addPlayer ? 3 : (readCam ? 4 : (allocEntity ? 5 : (linkPuppet ? 6 : (registerEntity ? 7 : 0))))));
 var injected = Inject(proc.Id, Path.GetFullPath(dll), mode, out var whyNot);
 if (!injected)
 {
 Note("inject fail " + whyNot);
 Note("REFUSED: inject failed: " + whyNot);
 return 4;
 }
 Note(mode == 1 ? "injected mode=spawn-once"
 : mode == 2 ? "injected mode=read-slot"
 : mode == 3 ? "injected mode=add-player"
 : mode == 4 ? "injected mode=read-cam"
 : mode == 5 ? "injected mode=alloc-entity"
 : mode == 6 ? "injected mode=link-puppet"
 : mode == 7 ? "injected mode=register-entity"
 : "injected mode=sample");

 // HFW pauses when it loses foreground. This process must not become it.
 // ASFW_ANY lets the game call SetForegroundWindow and take focus back.
 AllowSetForegroundWindow(AsfwAny);
 if (spawnOnce)
 {
 Note($"injected {Path.GetFullPath(dll)} into pid {proc.Id} mode=spawn-once. " +
 "FUN_14134c950 is not called. log is hfw-probe.log in the game's working directory. " +
 "expect a line starting with 'spawn-once blocked'. do not expect count=2.");
 return 0;
 }
 if (readSlot)
 {
 Note($"injected {Path.GetFullPath(dll)} into pid {proc.Id} mode=read-slot. " +
 "no game call, no write. log is hfw-probe.log in the game's working directory. " +
 "a live slot is a line 'slot module=0x.. plus50=0x.. entity=0x..' with plus50 not 0. " +
 "samples 20s at 2 Hz after GameModule@0x8983150 is non-null.");
 return 0;
 }
 if (addPlayer)
 {
 Note($"injected {Path.GetFullPath(dll)} into pid {proc.Id} mode=add-player. " +
 "one shot: FUN_1401e6100 only, then exit. FUN_140d73270 and " +
 "FUN_1402250c0 are not called. FUN_1413d8470 and FUN_14134c950 are not called. " +
 "entity+0xD8 is not written. the player list is not touched and the count stays. " +
 "log is hfw-probe.log in the game's working directory. " +
 "start a fresh session, load a save first. " +
 "the line to look for is 'add-player stop count=1 seh=0x00000000 block=0x' " +
 "followed by '(ctor and insert not called'. " +
 "no second character. this does not write the player's position.");
 return 0;
 }
 if (readCam)
 {
 Note($"injected {Path.GetFullPath(dll)} into pid {proc.Id} mode=read-cam. " +
 "no game call, no write, no AcquireSRWLockExclusive. " +
 "log is hfw-probe.log in the game's working directory. " +
 "a live manager is a line 'cam ptr=0x.. lock20=0x..' with ptr not 0. " +
 "samples 10s at 2 Hz after GameModule@0x8983150 is non-null.");
 return 0;
 }
 if (allocEntity)
 {
 Note($"injected {Path.GetFullPath(dll)} into pid {proc.Id} mode=alloc-entity. " +
 "one shot: the four inner steps, then exit. FUN_1401f82b0 is not called " +
 "(its RCX is a parent container this probe cannot name). " +
 "FUN_1401f96d0 is not called, so the block is not inserted anywhere. " +
 "FUN_140d73270, FUN_14134c950, FUN_1413d8470, FUN_14101bd70, FUN_1402250c0 " +
 "and FUN_1400fe310 are not called. entity+0xD8 is not written. " +
 "log is hfw-probe.log in the game's working directory. " +
 "start a fresh session, load a save first. " +
 "the line to look for is 'alloc-entity stop ptr=0x' with a non-zero pointer. " +
 "this does not spawn a visible character and does not touch the player's entity. " +
 "the pointer in the log is the whole result. a hitch is unlikely; alt-f4 if wedged.");
 return 0;
 }
 if (linkPuppet)
 {
 Note($"injected {Path.GetFullPath(dll)} into pid {proc.Id} mode=link-puppet. " +
 "one shot: the four inner steps, then FUN_1401e6100(0x210), then " +
 "FUN_140d73270(playerBlock, newEntity) once, then exit. " +
 "FUN_1401f82b0 is not called. the owner's entity is not passed. " +
 "FUN_1402250c0 is not called, so the player list is not touched. " +
 "log is hfw-probe.log in the game's working directory. " +
 "start a fresh session, load a save first. a hitch is possible " +
 "while the constructor holds the camera lock; alt-f4 if wedged. " +
 "the line to look for is 'link-puppet stop player=0x' with entity=0x " +
 "and seh=0x00000000. the owner's character should not move. " +
 "a visible body is not expected yet.");
 return 0;
 }
 if (registerEntity)
 {
 Note($"injected {Path.GetFullPath(dll)} into pid {proc.Id} mode=register-entity. " +
 "one shot: the four inner steps, then a free MD5 key stored at [entity+0x8], " +
 "then FUN_140901310(unused, entity, 1, 0, 0) once, then exit. " +
 "FUN_140986bc0 is not called. FUN_1402250c0, FUN_14134c950, FUN_1413d8470 " +
 "and FUN_14101bd70 are not called. entity+0xD8 is not written. " +
 "log is hfw-probe.log in the game's working directory. " +
 "start a fresh session, load a save first. " +
 "the line to look for is 'register-entity slot=' with seh=0x00000000. " +
 "occupied_after is [EntityManagerGame+0x1e0] and should be one above occupied_before. " +
 "count_* is the capacity at +0x1e4. " +
 "a hitch is possible; alt-f4 if wedged.");
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

 internal static bool Inject(int pid, string dllPath, int mode, out string why)
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
 // 0 sampler, 1 spawn-once (blocked), 2 read-slot, 3 add-player, 4 read-cam,
 // 5 alloc-entity (four inner steps, factory not called),
 // 6 link-puppet (four steps, then FUN_140d73270 with the new entity),
 // 7 register-entity (four steps, MD5 key, then FUN_140901310 once).
 File.WriteAllBytes(dllPath + ".mode", new[] { (byte)mode });
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
 // FUN_14134c950 prologue still matches, but --spawn-once does not call it.
 // The faulting reload is MOV RAX,[RIP+disp] at 0x134C9B1 (global
 // VA 0x1489581F0) then MOV RCX,[RAX+0xF8] at 0x134C9BA with no TEST.
 ("spawn FUN_14134c950", 0x134C950, "48 89 5C 24 10 48 89 74 24 18 48 89 7C 24 20 48"),
 ("spawn call site", 0x134C657, "E8 F4 02 00 00"),
 ("spawn ctx reload", 0x134C9B1, "48 8B 05 38 B8 60 07"),
 ("spawn ctx deref +0xF8", 0x134C9BA, "48 8B 88 F8 00 00 00"),
 // size dword the spawn function passes to FUN_1401e6100 (0x210).
 ("spawn alloc size", 0x21615A0, "10 02 00 00"),
 // FUN_1401e6100 at the NetPlayerGame site. RCX is the size
 // (MOVSXD RCX,EDX), not a type-record pointer. --add-player calls
 // this entry with RCX=0x210 and then stops. It does not call
 // FUN_140d73270 or FUN_1402250c0.
 ("alloc call site", 0x13D852B, "48 63 CA E8 CD DB E0 FE"),
 // FUN_140d73270(RDI, RSI) immediately after that alloc.
 ("ctor call site", 0x13D853A, "48 8B D6 48 8B CF E8 2B AD 99 FF"),
 // The camera global. --read-cam saw it non-null after a save load,
 // so the constructor's unchecked load of it is no longer a block.
 ("ctor scene load", 0xD733C2, "48 8B 3D E7 F8 C0 07"),
 // FUN_140d5e2c0 publishes CameraPredictionManager* into the BSS qword.
 ("cam qword store", 0xD5FA19, "48 89 1D 90 32 C2 07"),
 // FUN_1401e6100 prologue. The one-shot calls this entry and no other.
 // FUN_1402250c0 stays in the table so a rebuild still proves the
 // push the previous run used; this build does not call it.
 ("alloc FUN_1401e6100", 0x1E6100, "48 89 5C 24 08 57 48 83 EC 20 65 48 8B 04 25 58"),
 ("insert FUN_1402250c0", 0x2250C0, "48 89 5C 24 18 48 89 7C 24 20 41 56"),
 // The two loads the list walkers do on every PlayerGame. Both are
 // [reg+0xE8] and the next instruction is TEST. A record without the
 // NetPlayerGame back-pointer takes the null arm. The camera-list
 // publish inside FUN_140d73270 is the one that is not optional:
 // LEA RCX,[RDI+0x30]; MOV RDX,R14; CALL FUN_1400fe310.
 ("walker load +0xE8", 0x916AC8, "49 8B 80 E8 00 00 00"),
 ("cache load +0xE8", 0x916C43, "48 8B 88 E8 00 00 00"),
 ("ctor cam-list publish", 0xD733DC, "48 8D 4F 30 4C 0F 44 F5 49 8B D6 E8 24 AF 38 FF"),
 ("GetWorldTransform fn", 0x962640, "48 89 74 24 10 57 48 83 EC 60"),
 ("Mat34 load", 0x962697, "C5 FC 10 87 F0 00 00 00"),
 // FUN_140d63060 writes the published GameModule's +0x50. The load of
 // [0x148983150] is the instruction before MOV RBX,RDX.
 ("gamemodule +0x50 load", 0xD6306F, "48 8B 3D DA 00 C2 07"),
 ("gamemodule +0x50 store", 0xD63090, "48 89 5F 50"),
 // FUN_1401f82b0 prologue. --alloc-entity does not call this entry:
 // RCX is a parent container, and the first argument read is
 // MOV RAX,[RCX+0x58]. The only real caller passes MOV RCX,RBX
 // at 0x1F8048, and that RBX came from LEA RBX,[RDI-0x20] at 0x1F8012.
 ("factory FUN_1401f82b0", 0x1F82B0, "48 89 5C 24 10 55 56 57 41 54 41 55 41 56 41 57"),
 ("factory reads RCX+0x58", 0x1F82CC, "48 8B 41 58"),
 ("factory caller RCX", 0x1F8048, "48 8B D6 48 8B CB"),
 // The four steps, in the order the factory itself runs them.
 ("ctor-from-record FUN_1402033d0", 0x2033D0, "0F B6 41 04 83 F8 07 77 33"),
 ("size-from-record FUN_1402032d0", 0x2032D0, "0F B6 41 04 83 F8 07 77 37"),
 ("place FUN_1411a0980", 0x11A0980, "48 85 D2 74 35 53 48 83 EC 20"),
 // ControlledEntity record. File offset is 0x21D9AC0 (RVA 0x21DA6C0).
 // Tag byte +0x4 is 0x04. The size dword at +0x10 is 0x300. The
 // constructor qword at +0x18 is 0x1411A0980, FUN_1411a0980.
 ("record tag+size", 0x21DA6C0, "FF FF FF FF 04 00 01 00 00 00 FF 00 00 00 00 00 00 03 00 00"),
 ("record ctor slot", 0x21DA6D8, "80 09 1A 41 01 00 00 00"),
 // FUN_140930e90 walks the camera list and calls [entry vtable+8]
 // with no null test. That slot is FUN_140d794f0, which calls
 // FUN_1409150e0. The first field load there is the count at
 // [PlayerGame+0xF0], and the next instruction subtracts one and
 // jumps away when the result is negative. FUN_140913be0 stores 0
 // at that offset before FUN_140d73270 publishes the entry.
 ("walker call vtable+8", 0x931073, "FF 50 08"),
 ("walker count load +0xF0", 0x91510A, "8B 93 F0 00 00 00 83 EA 01 78"),
 ("ctor zeroes count +0xF0", 0x913C9C, "48 89 B7 F0 00 00 00"),
 // FUN_140d73270 prologue. --link-puppet calls this entry once,
 // with the new entity, and does not call FUN_1402250c0.
 ("link FUN_140d73270", 0xD73270, "48 89 5C 24 10 48 89 6C"),
 // The list the walker reads: count at +0x30, array at +0x38.
 ("walker list count +0x30", 0x930FF8, "49 63 45 30 4C 8B 7D"),
 ("walker list array +0x38", 0x930FEE, "49 8B 7D 38 41 B9 01"),
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
