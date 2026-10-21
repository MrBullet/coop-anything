/* hfw_probe.c — sampler injected into HorizonForbiddenWest.exe.
 *
 * Default (no mode byte): writes nothing to game memory. The only stores
 * are into this DLL's own log. DllMain only creates a thread. The thread
 * samples at 10 Hz and sleeps, so a 45 s run cannot spin or hook Present.
 *
 * --spawn-once (opt-in): the loader writes one byte, 1, to
 * hfw_probe.dll.mode next to this DLL. That file is not in the game
 * directory. The thread does NOT call FUN_14134c950. One live call
 * raised 0xC0000005: the function's first load is
 * MOV RAX,[0x1489581F0] and the next load, MOV RCX,[RAX+0xF8] at
 * RVA 0x134C9BA, is not behind the null test. That qword is BSS
 * (written once, by FUN_140d2fca0). The only caller, FUN_14134c590,
 * does not pass it in a register. This mode logs the block and exits.
 * It does not store the entity double3 and does not call FUN_14101bd70.
 *
 * --read-slot (opt-in, mode byte 2): also read-only. Waits until the
 * GameModule* at RVA 0x8983150 (VA 0x148983150) is non-null, then
 * samples for 20 s at 2 Hz. Each sample is one line:
 * slot module=0x.. plus50=0x.. entity=0x..
 * plus50 is [module+0x50]. entity is [[module+0x50]+0x20] when plus50
 * is non-null, else 0. No game function is called. Nothing is written.
 *
 * Anchors (image base of the exe, this exact build):
 * 0x8982C80 PlayerManagerGame*
 * 0x8982DD0 SceneManagerGame*
 * player = [[pm + 0x10]] first entry (count at +0x08)
 * entity = [player + 0x48] FUN_140916bf0
 *
 * World position is the double3 at entity+0xD8/+0xE0/+0xE8. The walk run
 * (local/hfw-probe-run2.log, 450 samples, entity 0x25700154D00, faults=0)
 * tracked it: still until sample 87, then 10.125 m first-to-last, path
 * length 110.690 m. All 450 finite. FUN_140962590 copies exactly these
 * three qwords under the lock at entity+0x298 (TryEnter, else Enter, then
 * Leave). FUN_140962640 copies them in the same locked read as the Mat34.
 * This probe does not take that lock and does not call either function.
 *
 * The Mat34 at entity+0xF0 is not the live world position. On that walk its
 * translation stayed at X=0, Z=5.73 m, and Y tore to ~1e38. It is logged
 * as mat= and never sets sane=.
 *
 * sane=1 only when the three doubles are finite and inside +/-1e7.
 *
 * [[scene + 0x248]] is a frame counter, not a world id.
 *
 * Build: cl /LD /O2 /GS- hfw_probe.c /link /OUT:hfw_probe.dll
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdint.h>
#include <math.h>

#define RVA_PLAYER_MANAGER 0x8982C80ull
#define RVA_SCENE_MANAGER 0x8982DD0ull
#define RVA_GAME_MODULE 0x8983150ull
#define OFF_MODULE_SLOT 0x50
#define OFF_SLOT_ENTITY 0x20
#define OFF_LIST_COUNT 0x08
#define OFF_LIST_STORAGE 0x10
#define OFF_PLAYER_ENTITY 0x48
#define OFF_ENTITY_POS 0xD8
#define OFF_ENTITY_MAT 0xF0
#define OFF_SCENE_WORD 0x248

#define SAMPLE_HZ 10
#define SAMPLE_MS (1000 / SAMPLE_HZ)
#define SAMPLE_COUNT 450 /* 45 s at 10 Hz */
#define WAIT_TICKS 600 /* 60 s at 10 Hz for a save to load */
#define SLOT_HZ 2
#define SLOT_MS (1000 / SLOT_HZ)
#define SLOT_SAMPLES 40 /* 20 s at 2 Hz */
#define SLOT_WAIT_TICKS 120 /* 60 s at 2 Hz for the GameModule global */

/* FUN_14134c950 is not called. First unchecked deref is
 * RVA 0x134C9BA: MOV RCX, [RAX+0xF8] where RAX came from
 * MOV RAX, [0x1489581F0] at 0x134C9B1. RVA of that global: */
#define RVA_SPAWN_CTX 0x89581F0ull

static volatile LONG g_stop = 0;
static volatile LONG g_faults = 0;

static unsigned long long exe_base(void)
{
 return (unsigned long long)GetModuleHandleW(NULL);
}

static int rd_bytes(unsigned long long a, void *dst, size_t n)
{
 __try {
 memcpy(dst, (const void *)a, n);
 return 1;
 } __except (EXCEPTION_EXECUTE_HANDLER) {
 InterlockedIncrement(&g_faults);
 return 0;
 }
}

static unsigned long long rd64(unsigned long long a)
{
 unsigned long long v = 0;
 if (!rd_bytes(a, &v, 8)) return 0;
 return v;
}

static unsigned int rd32(unsigned long long a)
{
 unsigned int v = 0;
 if (!rd_bytes(a, &v, 4)) return 0;
 return v;
}

static int finite_d(double v)
{
 return isfinite(v) && v < 1.0e7 && v > -1.0e7;
}

/* Do not call FUN_14134c950. Log why and return. */
static DWORD WINAPI spawn_once(LPVOID unused)
{
 FILE *fp;
 unsigned long long ctx = 0;
 (void)unused;

 SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_BELOW_NORMAL);
 fopen_s(&fp, "hfw-probe.log", "w");
 if (!fp) return 0;
 setvbuf(fp, NULL, _IOFBF, 8192);
 ctx = rd64(exe_base() + RVA_SPAWN_CTX);
 fprintf(fp,
 "spawn-once blocked: FUN_14134c950 derefs [0x1489581F0]+0xF8 "
 "(RAX from MOV RAX,[0x1489581F0] at RVA 0x134C9B1, then "
 "MOV RCX,[RAX+0xF8] at 0x134C9BA with no null test). "
 "live qword=0x%llX. caller FUN_14134c590 does not pass it. "
 "not called. count stays.\n",
 ctx);
 fflush(fp);
 fclose(fp);
 return 0;
}

/* 0 = default sampler, 1 = spawn-once (blocked), 2 = read-slot. */
static int mode_byte(HINSTANCE inst)
{
 wchar_t path[MAX_PATH];
 HANDLE f;
 char b = 0;
 DWORD got = 0;
 if (!GetModuleFileNameW(inst, path, MAX_PATH)) return 0;
 if (wcslen(path) + 6 >= MAX_PATH) return 0;
 wcscat_s(path, MAX_PATH, L".mode");
 f = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
 NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
 if (f == INVALID_HANDLE_VALUE) return 0;
 ReadFile(f, &b, 1, &got, NULL);
 CloseHandle(f);
 if (got != 1) return 0;
 return (unsigned char)b;
}

/* Wait until [0x148983150] is non-null, then log module / +0x50 /
 * [[+0x50]+0x20] for 20 s. Reads only. No game call. */
static DWORD WINAPI read_slot(LPVOID unused)
{
 FILE *fp;
 int i;
 int waited = 0;
 unsigned long long module = 0;
 (void)unused;

 SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_BELOW_NORMAL);
 fopen_s(&fp, "hfw-probe.log", "w");
 if (!fp) return 0;
 setvbuf(fp, NULL, _IOFBF, 8192);

 fprintf(fp,
 "read-slot start hz=%d samples=%d gate=GameModule@0x8983150 "
 "read-only no-call\n",
 SLOT_HZ, SLOT_SAMPLES);

 while (!g_stop && waited < SLOT_WAIT_TICKS) {
 module = rd64(exe_base() + RVA_GAME_MODULE);
 if (module) break;
 waited++;
 Sleep(SLOT_MS);
 }
 if (!module) {
 fprintf(fp,
 "read-slot timeout gate=GameModule@0x8983150 waited_s=%d "
 "module=0x0 plus50=0x0 entity=0x0 faults=%ld\n",
 waited / SLOT_HZ, g_faults);
 fflush(fp);
 fclose(fp);
 return 0;
 }

 for (i = 0; i < SLOT_SAMPLES && !g_stop; i++) {
 unsigned long long base = exe_base();
 unsigned long long plus50 = 0;
 unsigned long long entity = 0;
 LONG faults_before = g_faults;
 module = rd64(base + RVA_GAME_MODULE);
 if (module)
 plus50 = rd64(module + OFF_MODULE_SLOT);
 if (plus50)
 entity = rd64(plus50 + OFF_SLOT_ENTITY);
 if (g_faults != faults_before) {
 fprintf(fp,
 "slot fault sample=%d module=0x%llX plus50=0x%llX entity=0x%llX\n",
 i, module, plus50, entity);
 } else {
 fprintf(fp,
 "slot module=0x%llX plus50=0x%llX entity=0x%llX\n",
 module, plus50, entity);
 }
 fflush(fp);
 Sleep(SLOT_MS);
 }

 fprintf(fp, "read-slot stop samples=%d faults=%ld\n", i, g_faults);
 fflush(fp);
 fclose(fp);
 return 0;
}

static DWORD WINAPI logger(LPVOID unused)
{
 FILE *fp;
 int i;
 int faults_logged = 0;
 (void)unused;

 /* Below the game's worker threads. Sleep yields; this is not a frame hook. */
 SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_BELOW_NORMAL);

 fopen_s(&fp, "hfw-probe.log", "w");
 if (!fp) return 0;
 /* One flush a second. fflush every sample is disk IO inside the game. */
 setvbuf(fp, NULL, _IOFBF, 8192);

 fprintf(fp, "probe start hz=%d samples=%d read-only no-hook double3\n",
 SAMPLE_HZ, SAMPLE_COUNT);

 for (i = 0; i < SAMPLE_COUNT && !g_stop; i++) {
 unsigned long long base = exe_base();
 unsigned long long pm = rd64(base + RVA_PLAYER_MANAGER);
 unsigned long long scene = rd64(base + RVA_SCENE_MANAGER);
 unsigned int counter = 0;
 unsigned long long entity = 0;
 double pos[3] = {0, 0, 0};
 float mat[15];
 float mx = 0, my = 0, mz = 0;
 int k;
 int got = 0;
 int got_mat = 0;
 LONG faults_before = g_faults;

 for (k = 0; k < 15; k++) mat[k] = 0;

 if (scene) {
 unsigned long long slot = rd64(scene + OFF_SCENE_WORD);
 if (slot) counter = rd32(slot);
 }
 if (pm) {
 unsigned long long storage = rd64(pm + OFF_LIST_STORAGE);
 unsigned long long player = storage ? rd64(storage) : 0;
 entity = player ? rd64(player + OFF_PLAYER_ENTITY) : 0;
 if (entity) {
 got = rd_bytes(entity + OFF_ENTITY_POS, pos, sizeof(pos));
 got_mat = rd_bytes(entity + OFF_ENTITY_MAT, mat, sizeof(mat));
 if (got_mat) {
 mx = mat[9]; my = mat[10]; mz = mat[11];
 }
 }
 }

 if (g_faults != faults_before) {
 if (faults_logged < 8) {
 fprintf(fp, "fault sample=%d entity=0x%llX (read failed, continuing)\n",
 i, entity);
 faults_logged++;
 }
 } else {
 int sane = got && finite_d(pos[0]) && finite_d(pos[1]) && finite_d(pos[2]);
 fprintf(fp,
 "sample=%d counter=0x%08X entity=0x%llX "
 "pos=(%.3f,%.3f,%.3f) sane=%d "
 "mat=(%.3f,%.3f,%.3f)\n",
 i, counter, entity,
 pos[0], pos[1], pos[2], sane,
 mx, my, mz);
 }
 if ((i % SAMPLE_HZ) == (SAMPLE_HZ - 1)) fflush(fp);
 Sleep(SAMPLE_MS);
 }

 fprintf(fp, "probe stop samples=%d faults=%ld\n", i, g_faults);
 fflush(fp);
 fclose(fp);
 return 0;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
 HANDLE thread;
 (void)reserved;
 if (reason == DLL_PROCESS_ATTACH) {
 int mode = mode_byte(inst);
 LPTHREAD_START_ROUTINE entry = logger;
 DisableThreadLibraryCalls(inst);
 if (mode == 1) entry = spawn_once;
 else if (mode == 2) entry = read_slot;
 thread = CreateThread(NULL, 0, entry, NULL, 0, NULL);
 if (thread) CloseHandle(thread);
 } else if (reason == DLL_PROCESS_DETACH) {
 InterlockedExchange(&g_stop, 1);
 }
 return TRUE;
}
