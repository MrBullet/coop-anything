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
 * --add-player (opt-in, mode byte 3): one shot, then exit. Waits until
 * GameModule*, [module+0x50], [[module+0x50]+0x20], [0x148982CB0] and
 * the player count at [PlayerManager+8] are all non-null / >= 1.
 * Then, exactly once and inside SEH:
 * FUN_1401e6100(RCX=0x210) as at RVA 0x13D852E.
 * It does not call FUN_140d73270. That constructor stores the live
 * entity at [block+0x30] (RVA 0x913CB7) and then pushes the block's
 * +0x130 sub-object onto the camera manager's list at
 * [CameraPredictionManager+0x30] (LEA RCX,[RDI+0x30] / CALL
 * FUN_1400fe310 at RVA 0xD733DC / 0xD733E7). The next frame walks that
 * list and drives [PlayerGame+0x30] a second time, which is the
 * owner's entity. It also does not call FUN_1402250c0, so the player
 * list at pm+8 is not touched either: FUN_140916a80 at RVA 0x916AC8
 * and FUN_140916c20 at RVA 0x916C43 load [PlayerGame+0xE8], which
 * FUN_1413d8470 writes and this probe does not. The count stays 1.
 * FUN_1413d8470, FUN_14134c950 and FUN_14101bd70 are not called.
 * entity+0xD8 is not written. No second character is created.
 *
 * --read-cam (opt-in, mode byte 4): also read-only. Same gate as
 * --read-slot: waits until GameModule* at RVA 0x8983150 is non-null,
 * then samples the qword at RVA 0x8982CB0 (VA 0x148982CB0) for 10 s
 * at 2 Hz. Each sample is one line:
 * cam ptr=0x.. lock20=0x..
 * lock20 is the first qword at [ptr+0x20] only when ptr is non-null,
 * and that read is inside the same SEH as every other deref. It does
 * not call AcquireSRWLockExclusive, FUN_140d73270, FUN_14134c950, or
 * FUN_1413d8470. It writes nothing.
 *
 * --alloc-entity (opt-in, mode byte 5): one shot, then exit. Does NOT
 * call FUN_1401f82b0. That factory's first argument is a live parent
 * container (RCX at 0x1F82CC loads [RCX+0x58], and the only real caller
 * FUN_1401f7bd0 at 0x1F8048 passes MOV RCX,RBX (RBX was
 * LEA RBX,[RDI-0x20] at 0x1F8012). The type record
 * is not a register argument of the factory; it is popped off that
 * parent's stack. A parent we cannot name is not passed. The four
 * inner steps are called instead, once, inside SEH:
 *   ctor = FUN_1402033d0(record)        ; case 4 returns [record+0x18]
 *   size = FUN_1402032d0(record)        ; case 4 returns [record+0x10]
 *   block = FUN_1401e6100(size)         ; 0x300 for this record
 *   FUN_1411a0980(record, block)        ; RCX record, RDX block
 * The block is not pushed. FUN_1401f96d0 is not called, and neither is
 * the second call the factory makes at 0x1F85FC. The pointer is logged
 * and the thread returns. The game never walks the block.
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
#define CAM_SAMPLES 20 /* 10 s at 2 Hz */
#define OFF_CAM_LOCK 0x20

/* FUN_14134c950 is not called. First unchecked deref is
 * RVA 0x134C9BA: MOV RCX, [RAX+0xF8] where RAX came from
 * MOV RAX, [0x1489581F0] at 0x134C9B1. RVA of that global: */
#define RVA_SPAWN_CTX 0x89581F0ull

/* FUN_140d73270 at RVA 0xD733C2: MOV RDI, [0x148982CB0], then
 * LEA RBX,[RDI+0x20] / CALL [0x14174B1C8] with no TEST RDI.
 * --read-cam (2026-10-21, 20 samples, faults=0) saw this qword
 * non-null after a save load. lock20=0 is an unlocked SRWLOCK. */
#define RVA_CTOR_SCENE 0x8982CB0ull
#define RVA_ALLOC 0x1E6100ull
#define PLAYER_GAME_SIZE 0x210
#define ADD_WAIT_TICKS 120 /* 60 s at 2 Hz */

/* ControlledEntity type record. Tag byte at +0x4 is 0x04, so
 * FUN_1402032d0 returns the dword at +0x10 (0x300) and FUN_1402033d0
 * returns the qword at +0x18 (FUN_1411a0980). */
#define RVA_ENTITY_RECORD 0x21DA6C0ull
#define RVA_CTOR_FROM_RECORD 0x2033D0ull
#define RVA_SIZE_FROM_RECORD 0x2032D0ull
#define RVA_PLACE_CONTROLLED 0x11A0980ull

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

typedef unsigned long long (__fastcall *alloc_fn)(unsigned long long size);
typedef unsigned long long (__fastcall *record_fn)(unsigned long long record);
typedef void (__fastcall *place_fn)(unsigned long long record, unsigned long long block);

/* One shot. Four inner steps of FUN_1401f82b0, not the factory.
 * The factory is not called: its RCX is a parent container the probe
 * cannot name (see the comment at the top). The block is not inserted
 * anywhere. FUN_1401f96d0 is not called, so the game has no pointer to
 * the block and never walks it. */
static DWORD WINAPI alloc_entity(LPVOID unused)
{
 FILE *fp;
 int waited = 0;
 unsigned long long base;
 unsigned long long module = 0;
 unsigned long long record;
 unsigned long long ctor = 0;
 unsigned int size = 0;
 unsigned long long block = 0;
 unsigned long long vtable = 0;
 unsigned int seh = 0;
 (void)unused;

 SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_BELOW_NORMAL);
 fopen_s(&fp, "hfw-probe.log", "w");
 if (!fp) return 0;
 setvbuf(fp, NULL, _IONBF, 0);

 fprintf(fp,
 "alloc-entity start one-shot gate=GameModule@0x8983150 "
 "steps=ctor-from-record,size-from-record,alloc,place "
 "record=0x21DA6C0 factory-not-called\n");

 while (!g_stop && waited < SLOT_WAIT_TICKS) {
 module = rd64(exe_base() + RVA_GAME_MODULE);
 if (module) break;
 waited++;
 Sleep(SLOT_MS);
 }

 fprintf(fp,
 "alloc-entity gate module=0x%llX waited_s=%d faults=%ld\n",
 module, waited / SLOT_HZ, g_faults);

 if (!module) {
 fprintf(fp,
 "alloc-entity timeout. FUN_1402033d0, FUN_1402032d0, "
 "FUN_1401e6100 and FUN_1411a0980 not called.\n");
 fflush(fp);
 fclose(fp);
 return 0;
 }

 base = exe_base();
 record = base + RVA_ENTITY_RECORD;
 __try {
 record_fn ctor_from = (record_fn)(base + RVA_CTOR_FROM_RECORD);
 record_fn size_from = (record_fn)(base + RVA_SIZE_FROM_RECORD);
 alloc_fn alloc = (alloc_fn)(base + RVA_ALLOC);
 place_fn place = (place_fn)(base + RVA_PLACE_CONTROLLED);
 ctor = ctor_from(record);
 size = (unsigned int)size_from(record);
 block = alloc((unsigned long long)size);
 if (block) place(record, block);
 } __except (EXCEPTION_EXECUTE_HANDLER) {
 seh = (unsigned int)GetExceptionCode();
 }

 if (block && !seh) vtable = rd64(block);

 fprintf(fp,
 "alloc-entity stop ptr=0x%llX vtable=0x%llX size=0x%X "
 "ctor=0x%llX seh=0x%08X (factory not called, not inserted)\n",
 block, vtable, size, ctor, seh);
 fflush(fp);
 fclose(fp);
 return 0;
}

/* One shot. Alloc only. The constructor publishes the block onto the
 * camera manager with the owner's entity at +0x30, and the list insert
 * publishes it onto pm+8 with [PlayerGame+0xE8] still zero. Neither
 * call is made. The block is a leak the game does not walk. */
static DWORD WINAPI add_player(LPVOID unused)
{
 FILE *fp;
 int waited = 0;
 unsigned long long base;
 unsigned long long module = 0;
 unsigned long long plus50 = 0;
 unsigned long long entity = 0;
 unsigned long long cam = 0;
 unsigned long long pm = 0;
 int count = 0;
 unsigned long long block = 0;
 unsigned int seh = 0;
 (void)unused;

 SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_BELOW_NORMAL);
 fopen_s(&fp, "hfw-probe.log", "w");
 if (!fp) return 0;
 setvbuf(fp, NULL, _IONBF, 0);

 fprintf(fp,
 "add-player start one-shot gate=GameModule@0x8983150 "
 "plus50 entity cam count>=1\n");

 while (!g_stop && waited < ADD_WAIT_TICKS) {
 base = exe_base();
 module = rd64(base + RVA_GAME_MODULE);
 plus50 = module ? rd64(module + OFF_MODULE_SLOT) : 0;
 entity = plus50 ? rd64(plus50 + OFF_SLOT_ENTITY) : 0;
 cam = rd64(base + RVA_CTOR_SCENE);
 pm = rd64(base + RVA_PLAYER_MANAGER);
 count = pm ? (int)rd32(pm + OFF_LIST_COUNT) : 0;
 if (module && plus50 && entity && cam && pm && count >= 1) break;
 waited++;
 Sleep(SLOT_MS);
 }

 fprintf(fp,
 "add-player gate module=0x%llX plus50=0x%llX entity=0x%llX "
 "cam=0x%llX pm=0x%llX count=%d waited_s=%d faults=%ld\n",
 module, plus50, entity, cam, pm, count, waited / SLOT_HZ, g_faults);

 if (!module || !plus50 || !entity || !cam || !pm || count < 1) {
 fprintf(fp,
 "add-player timeout. FUN_1401e6100, FUN_140d73270 and "
 "FUN_1402250c0 not called. count stays %d.\n",
 count);
 fflush(fp);
 fclose(fp);
 return 0;
 }

 base = exe_base();
 __try {
 alloc_fn alloc = (alloc_fn)(base + RVA_ALLOC);
 block = alloc(PLAYER_GAME_SIZE);
 } __except (EXCEPTION_EXECUTE_HANDLER) {
 seh = (unsigned int)GetExceptionCode();
 }

 fprintf(fp, "add-player alloc block=0x%llX seh=0x%08X\n", block, seh);
 if (!block || seh) {
 fprintf(fp, "add-player stop count=%d seh=0x%08X (ctor and insert not called)\n",
 count, seh);
 fflush(fp);
 fclose(fp);
 return 0;
 }

 /* Do not call FUN_140d73270. It stores the live entity at
 * [block+0x30] and pushes block+0x130 onto the camera manager
 * list, so the next tick drives the owner's entity twice.
 * Do not call FUN_1402250c0. The list walkers load
 * [PlayerGame+0xE8], which stays zero without FUN_1413d8470.
 * The block is not inserted anywhere. Count stays. */
 fprintf(fp,
 "add-player stop count=%d seh=0x%08X block=0x%llX "
 "(ctor and insert not called; list needs NetPlayerGame at "
 "PlayerGame+0xE8, and the ctor shares entity+0x30)\n",
 count, seh, block);
 fflush(fp);
 fclose(fp);
 return 0;
}

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

/* Same GameModule gate as read_slot, then sample [0x148982CB0] for
 * 10 s. lock20 is [ptr+0x20] only when ptr is non-null. Reads only.
 * Does not call AcquireSRWLockExclusive or any game function. */
static DWORD WINAPI read_cam(LPVOID unused)
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
 "read-cam start hz=%d samples=%d gate=GameModule@0x8983150 "
 "qword=0x8982CB0 read-only no-call\n",
 SLOT_HZ, CAM_SAMPLES);

 while (!g_stop && waited < SLOT_WAIT_TICKS) {
 module = rd64(exe_base() + RVA_GAME_MODULE);
 if (module) break;
 waited++;
 Sleep(SLOT_MS);
 }
 if (!module) {
 fprintf(fp,
 "read-cam timeout gate=GameModule@0x8983150 waited_s=%d "
 "cam ptr=0x0 lock20=0x0 faults=%ld\n",
 waited / SLOT_HZ, g_faults);
 fflush(fp);
 fclose(fp);
 return 0;
 }

 for (i = 0; i < CAM_SAMPLES && !g_stop; i++) {
 unsigned long long base = exe_base();
 unsigned long long ptr = 0;
 unsigned long long lock20 = 0;
 LONG faults_before = g_faults;
 ptr = rd64(base + RVA_CTOR_SCENE);
 if (ptr)
 lock20 = rd64(ptr + OFF_CAM_LOCK);
 if (g_faults != faults_before) {
 fprintf(fp,
 "cam fault sample=%d ptr=0x%llX lock20=0x%llX\n",
 i, ptr, lock20);
 } else {
 fprintf(fp, "cam ptr=0x%llX lock20=0x%llX\n", ptr, lock20);
 }
 fflush(fp);
 Sleep(SLOT_MS);
 }

 fprintf(fp, "read-cam stop samples=%d faults=%ld\n", i, g_faults);
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
 else if (mode == 3) entry = add_player;
 else if (mode == 4) entry = read_cam;
 else if (mode == 5) entry = alloc_entity;
 thread = CreateThread(NULL, 0, entry, NULL, 0, NULL);
 if (thread) CloseHandle(thread);
 } else if (reason == DLL_PROCESS_DETACH) {
 InterlockedExchange(&g_stop, 1);
 }
 return TRUE;
}
