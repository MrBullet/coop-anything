/* hfw_probe.c — read-only per-frame logger injected into HorizonForbiddenWest.exe.
 *
 * Writes nothing to game memory. The only stores are into this DLL's own log.
 *
 * Anchors (image base of the exe, this exact build):
 *   0x8982C80  PlayerManagerGame*
 *   0x8982DD0  SceneManagerGame*
 *   player = [[pm + 0x10]]         first entry of the list (count at +0x08)
 *   entity = [player + 0x48]       FUN_140916bf0
 *   pos    =  entity + 0xD8        3 floats, vmovups at RVA 0xD4F8D4
 *   head   =  entity + 0xE8        float,    vmovsd  at RVA 0xD4F8DC
 *   world  = [[scene + 0x248]]     uint32 id of the loaded world resource
 *
 * Build: cl /LD /O2 hfw_probe.c /link /OUT:hfw_probe.dll
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>

#define RVA_PLAYER_MANAGER 0x8982C80ull
#define RVA_SCENE_MANAGER  0x8982DD0ull
#define OFF_LIST_STORAGE   0x10
#define OFF_PLAYER_ENTITY  0x48
#define OFF_ENTITY_POS     0xD8
#define OFF_ENTITY_HEADING 0xE8
#define OFF_SCENE_WORLD    0x248

static unsigned long long exe_base(void)
{
    return (unsigned long long)GetModuleHandleA(NULL);
}

static unsigned long long rd64(unsigned long long a)
{
    __try { return *(unsigned long long *)a; }
    __except (EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

static unsigned int rd32(unsigned long long a)
{
    __try { return *(unsigned int *)a; }
    __except (EXCEPTION_EXECUTE_HANDLER) { return 0; }
}

static float rdf(unsigned long long a)
{
    __try { return *(float *)a; }
    __except (EXCEPTION_EXECUTE_HANDLER) { return 0.0f; }
}

static DWORD WINAPI logger(LPVOID unused)
{
    FILE *fp;
    int i;
    (void)unused;

    fopen_s(&fp, "hfw-probe.log", "w");
    if (!fp) return 0;

    for (i = 0; i < 1500; i++) {
        unsigned long long base = exe_base();
        unsigned long long pm = rd64(base + RVA_PLAYER_MANAGER);
        unsigned long long scene = rd64(base + RVA_SCENE_MANAGER);
        unsigned int world = 0;
        unsigned long long entity = 0;
        float x = 0, y = 0, z = 0, h = 0;

        if (scene) {
            unsigned long long res = rd64(scene + OFF_SCENE_WORLD);
            if (res) world = rd32(res);
        }
        if (pm) {
            unsigned long long storage = rd64(pm + OFF_LIST_STORAGE);
            unsigned long long player = storage ? rd64(storage) : 0;
            entity = player ? rd64(player + OFF_PLAYER_ENTITY) : 0;
            if (entity) {
                x = rdf(entity + OFF_ENTITY_POS);
                y = rdf(entity + OFF_ENTITY_POS + 4);
                z = rdf(entity + OFF_ENTITY_POS + 8);
                h = rdf(entity + OFF_ENTITY_HEADING);
            }
        }

        fprintf(fp, "frame world=0x%08X entity=0x%llX pos=(%.3f,%.3f,%.3f) heading=%.3f\n",
                world, entity, x, y, z, h);
        fflush(fp);
        Sleep(33);
    }
    fclose(fp);
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    (void)inst; (void)reserved;
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(inst);
        CreateThread(NULL, 0, logger, NULL, 0, NULL);
    }
    return TRUE;
}
