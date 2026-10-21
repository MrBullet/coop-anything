/* hfw_probe.c — read-only sampler injected into HorizonForbiddenWest.exe.
 *
 * Writes nothing to game memory. The only stores are into this DLL's own log.
 * DllMain only creates a thread. The thread samples at 10 Hz and sleeps, so a
 * 45 s run cannot spin, hook Present, or call a game function.
 *
 * Anchors (image base of the exe, this exact build):
 *   0x8982C80 PlayerManagerGame*
 *   0x8982DD0 SceneManagerGame*
 *   player  = [[pm + 0x10]]   first entry (count at +0x08)
 *   entity  = [player + 0x48] FUN_140916bf0
 *
 * The 2026-10-21 live log showed that entity+0xD8 is NOT a Vec3 of floats.
 * FUN_140d4f750 copies three qwords from entity+0xD8/+0xE0/+0xE8, and
 * FUN_1401cba60 prints a WorldPosition as three doubles (param[0], param[1],
 * param[2]) via vcvtpd2ps. This build logs BOTH interpretations and does not
 * treat either as confirmed until a live sample has sane metre-scale values.
 * world = [[scene + 0x248]] was a live counter (0x2280 -> 0x32AC), not a
 * stable resource id, so it is logged as a uint32 with no name attached.
 *
 * Build: cl /LD /O2 /GS- hfw_probe.c /link /OUT:hfw_probe.dll
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdint.h>
#include <math.h>

#define RVA_PLAYER_MANAGER 0x8982C80ull
#define RVA_SCENE_MANAGER  0x8982DD0ull
#define OFF_LIST_STORAGE   0x10
#define OFF_PLAYER_ENTITY  0x48
#define OFF_ENTITY_POS     0xD8
#define OFF_SCENE_WORD     0x248

#define SAMPLE_HZ          10
#define SAMPLE_MS          (1000 / SAMPLE_HZ)
#define SAMPLE_COUNT       450   /* 45 s at 10 Hz */

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

    fprintf(fp, "probe start hz=%d samples=%d read-only no-hook\n", SAMPLE_HZ, SAMPLE_COUNT);
    fflush(fp);

    for (i = 0; i < SAMPLE_COUNT && !g_stop; i++) {
        unsigned long long base = exe_base();
        unsigned long long pm = rd64(base + RVA_PLAYER_MANAGER);
        unsigned long long scene = rd64(base + RVA_SCENE_MANAGER);
        unsigned int world = 0;
        unsigned long long entity = 0;
        unsigned long long raw[3] = {0, 0, 0};
        float fx = 0, fy = 0, fz = 0;
        double dx = 0, dy = 0, dz = 0;
        int got = 0;
        LONG faults_before = g_faults;

        if (scene) {
            unsigned long long slot = rd64(scene + OFF_SCENE_WORD);
            if (slot) world = rd32(slot);
        }
        if (pm) {
            unsigned long long storage = rd64(pm + OFF_LIST_STORAGE);
            unsigned long long player = storage ? rd64(storage) : 0;
            entity = player ? rd64(player + OFF_PLAYER_ENTITY) : 0;
            if (entity) {
                got = rd_bytes(entity + OFF_ENTITY_POS, raw, sizeof(raw));
                if (got) {
                    memcpy(&fx, &raw[0], 4);
                    memcpy(&fy, (const char *)&raw[0] + 4, 4);
                    memcpy(&fz, &raw[1], 4);
                    memcpy(&dx, &raw[0], 8);
                    memcpy(&dy, &raw[1], 8);
                    memcpy(&dz, &raw[2], 8);
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
            int sane = got && finite_d(dx) && finite_d(dy) && finite_d(dz);
            fprintf(fp,
                "sample=%d world=0x%08X entity=0x%llX "
                "f32=(%.3f,%.3f,%.3f) f64=(%.3f,%.3f,%.3f) sane=%d "
                "raw=%016llX %016llX %016llX\n",
                i, world, entity,
                fx, fy, fz, dx, dy, dz, sane,
                raw[0], raw[1], raw[2]);
        }
        fflush(fp);
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
