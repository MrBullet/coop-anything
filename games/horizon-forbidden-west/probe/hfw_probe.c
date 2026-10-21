/* hfw_probe.c — sampler injected into HorizonForbiddenWest.exe.
 *
 * Default (no mode byte): writes nothing to game memory. The only stores
 * are into this DLL's own log. DllMain only creates a thread. The thread
 * samples at 10 Hz and sleeps, so a 45 s run cannot spin or hook Present.
 *
 * --spawn-once (opt-in): the loader writes one byte, 1, to
 * hfw_probe.dll.mode next to this DLL. That file is not in the game
 * directory. The thread waits until the player-list count at [pm+0x08]
 * is at least 1, logs it, calls FUN_14134c950 exactly once inside SEH,
 * logs the count and [pm+0x10] again, then exits. That function allocates
 * a PlayerGame and the add slot pushes it. This mode does not store the
 * entity double3 and does not call FUN_14101bd70.
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
#define RVA_SPAWN 0x134C950ull
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

/* void FUN_14134c950(undefined8). The only caller is CALL rel32 at RVA
 * 0x134C657, with no register setup, and the MOV [RSP+8],RCX in the
 * prologue is never read. Allocation size is the dword at RVA 0x21615A0. */
typedef void (__fastcall *spawn_fn)(unsigned long long unused);

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

static int call_spawn(unsigned long long fn, unsigned int *code)
{
 __try {
 ((spawn_fn)fn)(0);
 return 1;
 } __except (EXCEPTION_EXECUTE_HANDLER) {
 if (code) *code = GetExceptionCode();
 InterlockedIncrement(&g_faults);
 return 0;
 }
}

/* Wait for a loaded save, call the live spawn path once, log the count. */
static DWORD WINAPI spawn_once(LPVOID unused)
{
 FILE *fp;
 int i;
 unsigned int before = 0;
 unsigned int after = 0;
 unsigned long long storage_before = 0;
 unsigned long long storage_after = 0;
 unsigned int code = 0;
 int called = 0;
 int ready = 0;
 (void)unused;

 SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_BELOW_NORMAL);
 fopen_s(&fp, "hfw-probe.log", "w");
 if (!fp) return 0;
 setvbuf(fp, NULL, _IOFBF, 8192);
 fprintf(fp, "spawn-once start call=FUN_14134c950 no-pose-write\n");
 fflush(fp);

 /* 60 s at 10 Hz. A save that never loads is a timeout, not a call. */
 for (i = 0; i < 600 && !g_stop; i++) {
 unsigned long long pm = rd64(exe_base() + RVA_PLAYER_MANAGER);
 if (pm) {
 before = rd32(pm + OFF_LIST_COUNT);
 storage_before = rd64(pm + OFF_LIST_STORAGE);
 if (before >= 1) {
 ready = 1;
 break;
 }
 }
 if ((i % SAMPLE_HZ) == (SAMPLE_HZ - 1)) fflush(fp);
 Sleep(SAMPLE_MS);
 }

 if (!ready) {
 fprintf(fp, "spawn-once timeout count=%u storage=0x%llX faults=%ld\n",
 before, storage_before, g_faults);
 fflush(fp);
 fclose(fp);
 return 0;
 }

 fprintf(fp, "spawn-once before count=%u storage=0x%llX\n",
 before, storage_before);
 fflush(fp);

 called = call_spawn(exe_base() + RVA_SPAWN, &code);

 {
 unsigned long long pm = rd64(exe_base() + RVA_PLAYER_MANAGER);
 if (pm) {
 after = rd32(pm + OFF_LIST_COUNT);
 storage_after = rd64(pm + OFF_LIST_STORAGE);
 }
 }
 fprintf(fp,
 "spawn-once after called=%d code=0x%08X count=%u storage=0x%llX "
 "delta=%d faults=%ld\n",
 called, code, after, storage_after, (int)after - (int)before, g_faults);
 fflush(fp);
 fclose(fp);
 return 0;
}

static int spawn_wanted(HINSTANCE inst)
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
 return got == 1 && b == 1;
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
 DisableThreadLibraryCalls(inst);
 thread = CreateThread(NULL, 0,
 spawn_wanted(inst) ? spawn_once : logger, NULL, 0, NULL);
 if (thread) CloseHandle(thread);
 } else if (reason == DLL_PROCESS_DETACH) {
 InterlockedExchange(&g_stop, 1);
 }
 return TRUE;
}
