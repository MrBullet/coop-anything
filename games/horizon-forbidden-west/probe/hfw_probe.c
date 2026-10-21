/* hfw_probe.c — read-only sampler injected into HorizonForbiddenWest.exe.
 *
 * Writes nothing to game memory. The only stores are into this DLL's own log.
 * DllMain only creates a thread. The thread samples at 10 Hz and sleeps, so a
 * 45 s run cannot spin, hook Present, or call a game function.
 *
 * Anchors (image base of the exe, this exact build):
 * 0x8982C80 PlayerManagerGame*
 * 0x8982DD0 SceneManagerGame*
 * player = [[pm + 0x10]] first entry (count at +0x08)
 * entity = [player + 0x48] FUN_140916bf0
 *
 * The 2026-10-21 live log showed that entity+0xD8 is NOT a Vec3 of floats.
 * FUN_140d4f750 copies three qwords from entity+0xD8/+0xE0/+0xE8. Those stay
 * in the log as the old= side column.
 *
 * The world transform is the Mat34 that Entity_ExportedGetWorldTransform
 * returns. Registrar FUN_14095bfb0 binds that name to FUN_140962640, which
 * locks entity+0x298 and copies:
 *   +0xD8/+0xE0/+0xE8   three qwords (the rejected read)
 *   +0xF0 .. +0x128     0x3C-byte Mat34 (3x4 floats, row-major)
 *   +0x110              one float (the last column's w)
 * FUN_140962440 reads the same Mat34 at +0xF0 and transposes it.
 * Translation is the last row: floats at +0x114, +0x118, +0x11C.
 * Heading is the forward vector, row 2: floats at +0x108, +0x10C, +0x110.
 *
 * sane=1 only when those three translation floats are finite and inside
 * +/-1e7. The old qwords never set it.
 *
 * The 2026-10-21 second live run wrote all 450 samples, sane=1 on every
 * line, counter climbing the whole time, faults=0. pos= never changed.
 * That freeze was the loader's console taking foreground (HFW pauses on
 * focus loss), not a stalled worker. This build does not AllocConsole,
 * does not AttachConsole, and fflush's at most once a second.
 *
 * [[scene + 0x248]] counted 0x2280 -> 0x32AC on that run, so it is logged
 * as a uint32 counter, not a world id. No stable world id was found.
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
#define OFF_LIST_STORAGE 0x10
#define OFF_PLAYER_ENTITY 0x48
#define OFF_ENTITY_OLD 0xD8
#define OFF_ENTITY_MAT 0xF0
#define OFF_SCENE_WORD 0x248

#define SAMPLE_HZ 10
#define SAMPLE_MS (1000 / SAMPLE_HZ)
#define SAMPLE_COUNT 450 /* 45 s at 10 Hz */

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

static int finite_f(float v)
{
 return isfinite(v) && v < 1.0e7f && v > -1.0e7f;
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

 fprintf(fp, "probe start hz=%d samples=%d read-only no-hook mat34\n",
 SAMPLE_HZ, SAMPLE_COUNT);

 for (i = 0; i < SAMPLE_COUNT && !g_stop; i++) {
 unsigned long long base = exe_base();
 unsigned long long pm = rd64(base + RVA_PLAYER_MANAGER);
 unsigned long long scene = rd64(base + RVA_SCENE_MANAGER);
 unsigned int counter = 0;
 unsigned long long entity = 0;
 unsigned long long raw[3] = {0, 0, 0};
 float mat[15];
 float tx = 0, ty = 0, tz = 0;
 float hx = 0, hy = 0, hz = 0;
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
 got = rd_bytes(entity + OFF_ENTITY_OLD, raw, sizeof(raw));
 got_mat = rd_bytes(entity + OFF_ENTITY_MAT, mat, sizeof(mat));
 if (got_mat) {
 hx = mat[6]; hy = mat[7]; hz = mat[8];
 tx = mat[9]; ty = mat[10]; tz = mat[11];
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
 int sane = got_mat && finite_f(tx) && finite_f(ty) && finite_f(tz);
 fprintf(fp,
 "sample=%d counter=0x%08X entity=0x%llX "
 "pos=(%.3f,%.3f,%.3f) fwd=(%.3f,%.3f,%.3f) sane=%d "
 "old=%016llX %016llX %016llX\n",
 i, counter, entity,
 tx, ty, tz, hx, hy, hz, sane,
 raw[0], raw[1], raw[2]);
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
 thread = CreateThread(NULL, 0, logger, NULL, 0, NULL);
 if (thread) CloseHandle(thread);
 } else if (reason == DLL_PROCESS_DETACH) {
 InterlockedExchange(&g_stop, 1);
 }
 return TRUE;
}
