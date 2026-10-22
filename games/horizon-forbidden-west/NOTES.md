# Horizon Forbidden West (2024 PC, Decima / Nixxes port) — co-op notes

Status: **phase 5, ENTITY ALLOC ONE-SHOT BUILT, NOT RUN** (2026-10-21) —
`--alloc-entity` calls the four inner steps of `FUN_1401f82b0` once
and stops. It does not call the factory itself: `RCX` is a parent
container the probe cannot name. It does not call `FUN_1401f96d0`,
so the new block is not pushed onto any vector the game walks.
`--add-player` is unchanged and still stops after `FUN_1401e6100(0x210)`.
It does not call `FUN_140d73270` and it does not call `FUN_1402250c0`,
so it does not insert into the player list and it does not publish the
block onto the camera manager. The count stays 1. It does not call
`FUN_1413d8470`, `FUN_14134c950` or `FUN_14101bd70`, and it does not
write `entity+0xD8`. No second character. A separate allocator,
`FUN_1401f82b0`, constructs a `ControlledEntity` (size `0x300`, type
record RVA `0x21DA6C0`) without taking the owner's entity. It was not
called. `--read-cam` and `--read-slot` stay read-only. `--spawn-once`
still does not call `FUN_14134c950`. Default is still the 45 s sampler.
Claim issue: cranebeaker/coop-anything#83. See [DECISION.md](DECISION.md).

The BSS qword at `0x1489581F0` has one writer, `MOV [0x1489581F0], RDI` at
RVA `0xD304B9` in `FUN_140d2fca0`, confirmed from the exe bytes. It
stores a `NetErrorHandlerGame*` built at startup, and `+0xF8` on that
object is a `StateInGame*` whose `+0x108` is zeroed in the same
constructor. The other pointer the spawn path loads,
`[[0x148983150]+0x50]`, is written by `MOV [RDI+0x50], RBX` at RVA
`0xD63090` in `FUN_140d63060` while `GameModuleStateLoading` runs.
`--spawn-once` still does not call `FUN_14134c950`. Claim issue:
cranebeaker/coop-anything#83. See [DECISION.md](DECISION.md).

## Game identity (observed, verified this session)

- Steam AppId 2420110 (Complete Edition), build id 13801562 (recorded from `flt.ini` in
  the install), `HorizonForbiddenWest.exe` file version 1.5.80.0, 45,978,232 bytes,
  SHA-256 `9cec6626ab60059d186edbacca4ce667573e8b28c916fca1e07072002055429e`
  (re-hashed from the workspace copy this session — match).
- PE header identity: TimeDateStamp `0x667B1777`, SizeOfImage `0x949F000`, x64,
  entry `0x85D30`, Rich header present. No `.bind` section, `.text` entropy 6.58
  → no SteamStub/Denuvo packing (script `local/pe_scan.py`, log `local/pe_scan_out.txt`).
- Engine: Decima (Guerrilla), PC port by Nixxes. Dev tree paths embedded in the binary
  are `D:\Dev\HRZ2-PC\Code\...` (internal project HRZ2-PC).
- Install layout: 191 files, **124.9 GB** total; repack-style (`flt.ini`, `CRS`,
  bundled `steam_api64.dll` + `steamclient64.dll`). No anti-cheat DLLs.

## Phase 1 research: every existing attempt found (with sources)

### 1. Official Guerrilla co-op history — the dormant code is real
- Guerrilla built a co-op mode for HFW; early **in-dev footage (2020 build) leaked on
  r/horizon Jan 2023**, thread since removed by mods (treated as authenticity signal):
  https://www.ggrecon.com/articles/leaked-horizon-forbidden-west-multiplayer-shows-co-op-mode
- The initiative survives as a separate announced title **"Horizon Hunters Gathering"**:
  announced Feb 2026 (Gematsu), rebooted from live-service into "smaller scope, more
  traditional co-op multiplayer" after negative playtests (Bloomberg/Schreier, Aug 2026):
  https://www.gematsu.com/2026/08/bloomberg-horizon-hunters-gathering-being-rebooted-following-negative-player-feedback
- Consequence for us: Guerrilla's multiplayer work was **cancelled/pivoted, not removed
  from the shipped PC exe** — see binary orientation below. The retail build carries the
  full net layer.
- Fan demand threads (r/horizon "Imagine if Forbidden West had an online co-op mode",
  Feb 2022): https://www.reddit.com/r/horizon/comments/syqhr2/ — no dev response, no
  official MP.

### 2. everydayaussie/Strikers — a working, networked HFW mod (MIT)
- https://github.com/everydayaussie/Strikers — "Online multiplayer mod for Machine Strike
  inside Horizon Forbidden West, by replacing the AI's move with another player's inputs".
  License MIT. Created 2026-09-15, last push 2026-10-18. Also on Nexus:
  https://www.nexusmods.com/horizonforbiddenwest/mods/209 . Bug reports: its GitHub issues.
- What it **really** syncs: only the turn-based board game Machine Strike (seat inputs
  relayed between players) — not world/quests/story. You can also play vs AI or watch.
- Stack: .NET 10 x64 (Avalonia UI), builds with `build.ps1` → `Strikers.zip` (8 files).
  Memory reading works externally (`OpenProcess`/`ReadProcessMemory`) with 3 spots
  needing in-process code (patches/hooks), so **in-process injection into HFW is proven
  by a shipped, MIT-licensed mod**.
- **RE shortcut:** `docs/memory-map.md` + `docs/memory-tables.md` publish a deep memory
  map of the running game (challenge manager chain, vtables, RVAs, streaming manager,
  RTTI checks, how to find them again after updates). It documents its build as
  `667B1777-949F000` (PE timestamp + SizeOfImage) — **identical to our exe's identity**
  (`0x667B1777` / `0x949F000`), so its RVAs almost certainly apply to our binary; it also
  states Steam build `14835813` vs our recorded `13801562` — **verified this session**:
  18/19 documented RVAs byte-exact on our exe, so the exe content matches and the build-id
  difference is a depot recording difference (see "Strikers RVA verification" below).
- Status: **Path 1 candidate is dead for campaign co-op** (Strikers is board-game only),
  but it is the reference for injection + memory + two-instance networking on this game.

### 3. JustAddMP — the framework path (watch, don't wait)
- https://github.com/JustAddMP/HorizonForbiddenWest — created 2026-10-17, **README only
  ("planned")**, single commit "Planned: Horizon Forbidden West co-op". No code.
- https://github.com/JustAddMP/core — active: created 2026-10-14, last push 2026-10-20,
  releases v0.6.0→v0.13.0 (Oct 16–20, 2026), **no license file** (`license: null`), 0 stars.
  What core really provides (README + release notes, read 2026-10-20): launcher with DLL
  injection, optional version.dll proxy, MinHook, D3D11 Present hook + overlay, Steam P2P
  **and** UDP transports, host-authoritative sessions, Steam lobbies/invites, co-op
  panels, remote-motion controllers (LocomotionController/VehicleController), Scaleform
  (SWF) patcher, trace/log tooling, fuzz + loopback test suites. Ships with
  https://github.com/JustAddMP/MafiaDE (game at v0.13.0, "first real two-PC test" done).
  Built by one author (AEastwood). Requires VS2022 x64 C++ toolset.
- Unknown: license (none stated → cannot vendor it into a PR per hub ground rule 7
  without asking the author; treat as an external dependency / coordination candidate).

### 4. Decima modding tooling (same-engine base)
- https://github.com/cauldronloader/cauldron — **GPL-3.0 Rust mod loader** for HZD/HFW/DS:
  crates `cauldron` (loader API), `libdecima` (types + addresses for supported games),
  `pulse` (**Decima RTTI/symbol dumper as a mod**), `winhttp` proxy DLL for loading.
  Last updated 2026-04-29. Credits: ShadelessFox (Decima Workshop), Nukem9 (GGRTTI),
  Fexty12573 (DecimaLoader).
- https://github.com/Jayveer/Decima-Explorer — unpack/pack Decima archives (C++,
  updated Feb 2024). https://github.com/ShadelessFox/decima — "Decima Workshop".
- Nexus "Native Loader": loads native DLL mods from a `nativemods` folder at game start,
  no game-file replacement, also loads DecimaLoader plugins
  (https://www.nexusmods.com/horizonforbiddenwest/mods/toprecent).
- `data/directory.yaml` gap list already names Decima (Horizon, Death Stranding) as a
  family with **no co-op base** — this session's finds (cauldron + Strikers) are the seed
  of an `engine-families.md` contribution.

### 5. Sibling games / dead ends / noise
- Death Stranding (Decima): only async "Strand" sharing; co-op mod proposals on
  r/DeathStranding (2020, 2021) never shipped — no DS co-op found. HZD: no co-op mod
  found either (directory gap, status none).
- Nexus Mods HFW inventory: 167 mods / 930 files, all filed under "Miscellaneous"
  (search snippet 2026-10-20) — ReShade/HDR/cosmetics/native-loader class, **no campaign
  multiplayer mod**. Strikers (mod 209) is the only networked one.
- Unreliable source flagged: tecnobits.com "How to play multiplayer mode in HFW" claims
  MP exists — contradicted by gamepressure.com and the game itself (accessibility
  "Co-Pilot mode" is local controller sharing, not online MP). Text treated as data.
- Steam community discussions (app 2420110) concern mod-ban fears, nothing technical.

## Phase 3 orientation: binary findings (observed)

Scan: `local/pe_scan.py` (Python + `pefile` 2024.8.26, already on PATH) against
`<game dir>\HorizonForbiddenWest.exe`, log in `local/pe_scan_out.txt`.

- **Imports:** full BSD socket API from `WS2_32.dll` (socket/bind/listen/accept/connect/
  select/WSASend/WSASendTo/WSARecvFrom/recv/...), `WINHTTP.dll` (full HTTP client),
  `steam_api64.dll` (context init, callbacks, `SteamAPI_RestartAppIfNecessary`),
  dbghelp/faultrep crash reporting. **No anti-cheat, no Denuvo.**
- **RTTI:** 8,868 MSVC RTTI type names harvested. The dormant multiplayer layer includes:
  - Sessions/party: `NetSessionManager`, `NetSessionManagerNoOp`, `NetPartyManager`,
    `NetPartyManagerPC`, `NetPartyMember(Game)`, `NetPartyListener`,
    `NetJoinSessionEventListener`, `CryptoSession(Colicy)`.
  - Matchmaking: `MatchmakingService(Listener)`, `PartyMatchmakingService`,
    `StateMatchmaking(Config)`, `MatchmakingFaction/Team(Group)` + script exports
    (`MatchmakingGroup_sExportedMatchmakingEnter/Cancel`, `SetGameStarted/Ended`, ...).
  - Replication: `NetReplicatedObject(Manager)`, `NetReplicatedFactManager`,
    `NetReplicatedStringManager`, `NetMsgRequest/ReplyReplicationFlag`,
    `EntityActionReplication` parameter sets, `ESceneReplicationMode`.
  - **~80 `Net*State` component replication classes** (the whole gameplay sync surface):
    `NetEntityState`, `NetPlayerState`, `NetControlledEntityState`, `NetQuestSystemState`,
    `NetQuestSectionState`, `NetSceneManagerState`, `NetHumanoidState`, weapon/mount/loot/
    rope/hack/inventory/hit-reaction states, etc.
  - Script-visible MP API: `MultiplayerGroup_sExportedIsSessionMaster`,
    `MultiplayerGroup_sExportedGetNetworkTime`,
    `MultiplayerGameLibGroup_sExportedIsDedicatedServer`.
  - Co-op strings: `CoopShared`, `CoopPlayerControllerDisconnected`,
    `MaxCoopPlayerCount`, `E_GM_COOP_LEFT`.
- **Strings:** `SessionID = %s`, `JOIN_SESSION`, `Currently connected to %d remote
  sessions`, `host unreachable`, `IN_PRELOBBY` / `IN_POSTLOBBY`,
  `DefaultPreGameLobbyWaitTime` / `DefaultPostGameLobbyWaitTime`,
  `NETGAME 'DUMMY_ONLINE' GAMENAME` (dev leftover), `SteamNetworkingSockets012`
  (Steam Networking Sockets interface), `NxSteamImpl@nx` (engine net-library namespace
  wrapping Steam callbacks), `x-session-id`, `SessionID`/`session_id` telemetry fields.
- Co-op-relevant source files survive as debug strings (`.../Code/ThirdParty/...` paths).

## Phase 3 (complete): Ghidra headless reachability (observed, 2026-10-20)

Tools installed this session (all inside git-ignored `local/`, owner pre-approved):
Ghidra 12.1.4 official release zip (SHA-256 verified against the release notes:
`ddac49f9…87d2d4db`) in `local/ghidra/`, Temurin JDK 21 in `local/jdk21/`
(Ghidra 12 needs JDK 21; system Java is 17). Headless project `local/ghidra_proj`
(`analyzeHeadless.bat`, ~20 min full analysis of the 46 MB exe). Post-analysis
scripts: `local/ghidra_scripts/NetReachability{,2,3}.java`; reports
`local/ghidra_net_reachability*.txt`; logs `local/ghidra_headless*.log`.

Full findings + decompile excerpts: [DECISION.md](DECISION.md). Summary:

- **Startup chain**: the exe exports `MainProg` and `GameMainProg`; call graph
  `MainProg` → `GameMainProg` → `FUN_140d2fca0` → `FUN_140d3d960` (~920-line
  net-subsystem ctor, inside `MainProg`'s forward closure of 2,584 functions).
- **NoOp is wired, unconditionally**: the subsystem ctor writes
  `NetSessionManagerNoOp::vftable` (RVA `0x184AEE0`) into the session-manager
  slot; NoOp object is a 0x50-byte stub (name-getter slot returns
  `"NetSessionManagerNoOp"`). Sibling NoOp stubs wired the same way:
  `NetNpAvailabilityCheckNoOp`, `NetFriendManagerNoOp`,
  `NetSignInDialogServiceNoOp`, `CommerceManagerNoOp`,
  `UnifiedEntitlementManagerNoOp`.
- **Real `NetSessionManager` is link-stripped**: only its MSVC TypeDescriptor
  survives (RVA `0x224E948`, referenced exactly once in the whole file — as
  NoOp's base-class descriptor at RVA `0x19F4658`). No Complete-Object Locator,
  no vtable, no name string, no constructor anywhere in the image (raw byte scan
  all sections). Nothing to flip on.
- **Party/matchmaking layer is real and constructed at every startup** from the
  same ctor: `NetPartyManager` ctor `FUN_140426d30`, `NetPartyManagerPC` vtables
  written inline, `MatchmakingService` ctor `FUN_140b116b0`,
  `StateMatchmaking` ctor `FUN_1413a75c0` — destructors also have real callers.
- **Transport**: `"SteamNetworkingSockets012"` (RVA `0x178D3B4`) has **zero
  references** in the image (code or data) — the game's Steam Networking Sockets
  acquisition is compiled out. The plain Steam bootstrap is live
  (`FUN_1400ba630`: `SteamAPI_RestartAppIfNecessary`, `SteamInternal_SteamAPI_Init`).
- **`JOIN_SESSION`** (RVA `0x19897B0`) is built by `FUN_1413a8ae0`, a **virtual
  in `StateMatchmaking::vftable`** (RVA `0x19886C8`), as a `TelemetryLaunchAction`
  tagged `"invitation"`. `IN_PRELOBBY`/`RUNNING`/`IN_POSTLOBBY` are formatted by
  `FUN_140d6faf0` ("Round state { State: %s }").
- **Co-op script keys are wired via data tables** (raw pointer scan; Ghidra's
  initial "no xref" was a data-ref artifact): `MaxCoopPlayerCount` →
  `.data 0x1EC1AC8`, `CoopShared` → `.data 0x1F48C90`,
  `CoopPlayerControllerDisconnected` → `.data 0x1FC8728`,
  `host unreachable` → `.rdata 0x176C648`.
- **`NetGameDummy`** ("NETGAME 'DUMMY_ONLINE' GAMENAME"): factory `FUN_140d96060`
  builds 0x3CA8-byte session objects; no direct CALL xrefs (indirect-only),
  only referrer of all four `NetGameDummy` vtables.
- **Renderer = D3D12** (static proof): exports `D3D12SDKPath`/`D3D12SDKVersion`/
  `NVSDK_NGX_D3D12_*`; install root ships DX12 FSR/DLSS + DirectStorage. Matters
  for JustAddMP (D3D11-only Present hook).
- Reachability verdict: **NoOp is the wired path; the real session manager and
  the game's P2P transport init do not exist as code**; matchmaking/party/state
  objects exist and are constructed, sitting above the NoOp. Written up as the
  core evidence in [DECISION.md](DECISION.md).

## Strikers RVA verification (observed, 2026-10-20)

Script `local/verify_strikers_rva.py` (pefile) — **18/19 documented checks pass
byte-exact**; details table in [DECISION.md](DECISION.md). Highlights:

- Patch-site byte rows from `docs/memory-tables.md` all match exactly
  (`0xE39A41`→`80 79 28 00 74 52`, `0xE54F95` 19-byte coin-flip run,
  `0xE45F8E`, `0xE025A5`, `0xE04FCD`, `0xE3B8B9`, `0xE01E4F`, `0xE050D9`,
  `0xE3F680`, `0xE404F0`).
- Vtable slot `0x190D718` holds exactly `0x140E3F3B0` as documented.
- RTTI behind vtables resolves to the documented class names
  (`.?AVBoardGameChallengeInstance@@`, `.?AVAIBoardGamePlayer@@`,
  `.?AVBoardGameUnitCollectionComponentResource@@`, `.?AVRTTIObject@@`).
- The one "FAIL" was my checker's assumption, not a mismatch: the doc's
  "type description at RVA `0x21769D0`" is a Decima class-info record
  (present at exactly that RVA), not an MSVC TypeDescriptor.
- Steam build id discrepancy resolved as far as bytes allow: same exe content
  at every sampled location + identical PE identity → Strikers' `14835813` vs
  our recorded `13801562` is a depot/manifest recording difference, not a
  different binary. Strikers refuses writes unless the runtime build matches —
  copy that guard.
- Strikers has **0 GitHub issues** and no docs besides `memory-map.md` /
  `memory-tables.md` (checked 2026-10-20); README covers release-hash checking
  only.

## Workspace (phase 2, partial — cheapest correct option)

- Isolated workspace: `local/workspace/hfw-binaries/` — a **binaries-only copy**
  (`HorizonForbiddenWest.exe` 45.9 MB, `steam_api64.dll`, `flt.ini`), hash-verified
  identical to the install. This is all RE orientation needs; `<game dir>` was only
  read from, never written.
- **Full install is 124.9 GB** (191 files), so a full copy was deliberately NOT made.
  Candidate drives have room for it (details in the agent's local state). Plan for the first
  *runtime* phase (two instances): full `robocopy` copy to another drive, then
  hardlinked instance pairs (methods.md §2) so each instance has its own config/saves —
  decision deferred to when a runtime test is actually scheduled.
- Tools present (checked 2026-10-20): Python 3.14 + `pefile`, Java 17, Rust/cargo,
  .NET SDK, git, gh CLI (authenticated). **Installed later this session** (see
  "Phase 3 (complete)" above): Ghidra 12.1.4 + Temurin JDK 21 inside `local/`.
  Still missing: `strings`, DIE, 7z, MSVC cl.

## Derived (labelled guesses)

- **Route chosen: (c) own transport + injected module feeding the `Net*State`
  structures puppet-style** (Strikers pattern), with route (a) reduced to an
  *intel* source (the NoOp vtable is the engine's session interface; constructed
  matchmaking objects are a later accelerant) and route (b) parked pending
  JustAddMP license + a D3D12 hook. Reasoning in [DECISION.md](DECISION.md).
- The dormant layer's real/NoOp split is a **link-time decision**, not a runtime
  config: party/matchmaking/state survived in the image, the session manager and
  Steam-sockets acquisition did not.
- `NetGameDummy` (0x3CA8-byte object) is *assumed* to be the local/dummy session
  object the dormant online mode would run — factory is indirect-only, unconfirmed.

## Unknown

- Whether anything pumps `Net*State` replication during a normal single-player
  run (if not, treat those classes as data we serialize ourselves).
- Whether the game's WS2_32 usage includes a live UDP transport or only telemetry/
  telemetry-over-TCP + WinHTTP (socket call sites not yet mapped).
- Which renderer the game creates at runtime by default (static evidence: D3D12 —
  exports + DX12 upscalers; confirm before choosing overlay/hook layer).
- What invokes `FUN_140d96060` (NetGameDummy factory) and `FUN_1400ba630`
  (Steam bootstrap) — both indirect-only today.
- What backend the `StateMatchmaking`/telemetry paths expect (`host unreachable`
  message table) — gone services vs local.
- JustAddMP/core license (none stated) — matters before building on it (rule 7).
- Whether Decima's async/strand-like structures exist in HFW (Death Stranding does).

## Phase 5 milestone 1: read-only probe (2026-10-21)

### Injection approach — why Strikers, not Cauldron

Strikers (MIT, cloned under `local/`) is the pattern, because its RVAs are
byte-exact on this exe and Cauldron's are not. What Strikers actually does
(`src/live-probe/`): an external .NET process `OpenProcess`es the game, reads
the PE header, and **refuses every write unless the build is `667B1777-949F000`**
(`Program.cs` `KnownBuilds`). The three things that need code inside the game
(allocator calls, board-size patches) go in as a stub: `VirtualAllocEx` a page,
`WriteProcessMemory` the bytes, `CreateRemoteThread` it
(`Program.GameAlloc.cs`, `Program.BlightLoad.cs`). Everything else is read with
`ReadProcessMemory` from outside.

Cauldron (GPL-3.0, also under `local/`) instead drops a `winhttp.dll` proxy
beside the exe and loads mods from there (`crates/winhttp/src/lib.rs`). That
means writing a DLL into the install — which we will not do — and its address
table targets HZD, not this HFW build. So: external process, Strikers' guard
shape, our own DLL for the per-frame read.

### Address anchors (this exe, image base `0x140000000`)

Strikers' memory map covers Machine Strike only, so the player chain is from a
targeted Ghidra pass on the existing project (`-noanalysis`, no re-analysis).
Scripts in `local/ghidra_scripts/`, decompiles in `local/`.

| What | RVA | Evidence |
|---|---|---|
| `PlayerManagerGame*` global | `0x8982C80` | ctor `FUN_140d5e2c0` writes it right after setting `PlayerManagerGame::vftable` |
| `SceneManagerGame*` global | `0x8982DD0` | same ctor, `FUN_140d7f620` sets the vftable, 0x250-byte object |
| player list | `pm+0x08` count, `pm+0x10` ptr[] | `FUN_140d34130` walks exactly this |
| local player | first entry, `[pm+0x10]` | `GetLocalPlayer` (`FUN_140928430`) copies that list out |
| entity | `player+0x48` | `FUN_140916bf0`, the only caller of the local-player lookup |
| position | `entity+0xD8`, three doubles | walk run tracked them; `FUN_140962590` copies `+0xD8/+0xE0/+0xE8` under the lock at `entity+0x298` |
| not position | `entity+0xF0`, Mat34, 0x3C bytes | `FUN_140962640` also copies this; the walk run's translation did not move |
| ctor of both | `FUN_140948a10` | zeroes the three qwords (`MOV [RCX+0xD8], RBP` at RVA `0x948AEE`, RBP is 0) and writes the identity Mat34 at `+0xF0` from `0x141fdffd0` |
| scene counter | `[[scene+0x248]]`, uint32 | `FUN_140aa0a00` reads it; it counted every frame |

`GetLocalPlayer` is the script export `Player_sExportedGetLocalPlayer`
(string RVA `0x18A9E08`); its function pointer is loaded at RVA `0x91357F`.

### What was built

`games/horizon-forbidden-west/probe/`:

- `hfw_probe.c` — injected DLL. `DllMain` only creates a thread. The thread
 samples at 10 Hz (`Sleep(100)`, 450 samples, below-normal priority), reads
 the chain, and writes `hfw-probe.log`. It never writes game memory, never
 hooks `Present`, and never calls a game function. Every pointer deref is
 one `memcpy` inside `__try`. A bad read logs one `fault` line and the loop
 continues. Each sample logs the Mat34 translation and forward vector,
 and keeps the old `entity+0xD8` qwords as an `old=` column. `sane=1` is
 set only from the translation floats. The log is fully buffered and
 flushed once a second, not on every sample. The DLL does not call
 `AllocConsole` or `AttachConsole`.
- `hfw-probe.cs` — loader. Refuses to run unless the exe's SHA-256 is
 `9cec6626…02055429e` **and** the PE stamp is `667B1777-949F000` (Strikers'
 guard plus the hash). `--selftest` checks the anchor bytes above, including
 three Strikers rows, against the exe on disk. The project is a `WinExe`
 (PE subsystem 2), so launching it creates no console and does not take
 foreground. Before it waits it calls `AllowSetForegroundWindow(ASFW_ANY)`
 so the game can take focus back. Status goes to `hfw-probe-run.txt` beside
 the exe, not to a window.

Built with the VS 2026 Build Tools already on the machine (`cl 14.51`) and the
.NET 10 SDK. Nothing new installed. `hfw_probe.dll` is 147,456 bytes.

### Offline test (ran)

```
dotnet run --project probe/hfw-probe.csproj -c Release -- --selftest <exe>
```

11/11 PASS against the binaries-only workspace copy of the exe: SHA-256 + PE
guard, the three Strikers rows, and the anchors above, including the
`GetWorldTransform` prologue at RVA `0x962640` and the Mat34 load at
`0x962697`.

### Runtime test (2026-10-21, owner launched the game)

The owner injected the first build (`hfw-probe.exe --seconds 45`, pid 25980).
The DLL wrote 1,500 lines and then stopped. No access-violation line. The
game froze: alt-tab showed the pause menu, and Continue did not return
control. The process was already gone by the time this session looked; it
was not killed from here, and saves were not touched.

Quoted from `<game dir>\hfw-probe.log` (first line and last line; all 1,500
match this shape):

```
frame world=0x00002280 entity=0x18EF75A0380 pos=(-268433330386265072951800722111395266560.000,5.732,-39718163054592.000) heading=-426513958130057668028530688.000
frame world=0x000032AC entity=0x18EF75A0380 pos=(-268433330386265072951800722111395266560.000,5.732,-39718163054592.000) heading=-426513958130057668028530688.000
```

What that is, and what it is not:

- One stable entity pointer, `0x18EF75A0380`, for the whole run. The walk
 `PlayerManagerGame* -> [pm+0x10] -> [player+0x48]` did reach a live object.
- `world` is not a world id. It moved `0x2280 -> 0x32AC` (+4140 over 1499
 samples, about +2 or +4 per 33 ms). That is a counter at
 `[[scene+0x248]]`, not a loaded-world resource id.
- The position is not a transform. Y as a float is `5.732`
 (`0x40B76C8B`). X and Z are `0xFF49F261` and `0xD6107E6C`, which are not
 finite game coordinates. Read as a double, the first qword is
 `5996.547` and the second is an overflow. `FUN_140d4f750` copies three
 qwords from `+0xD8/+0xE0/+0xE8` (the `vmovups`/`vmovsd` at RVA `0xD4F8D4`
 is that copy, and `RBP` there is reassigned before the load). A
 `WorldPosition` (`FUN_1401cba60`) is three doubles. Neither layout is
 confirmed by this log.

Why it froze: not a loader lock, not a hooked `Present` (the DLL never
hooked one), not a suspended thread, and not a write. `DllMain` only
called `CreateThread`. The worker then `fprintf` + `fflush` of a ~200 byte
line, 30 times a second, into the game's own working directory, for all
1,500 samples. That is synchronous disk IO on a thread inside the game
process, and the logged `world` counter kept moving, so the process was
not deadlocked — it was stalled hard enough that focus changes dropped it
into the pause menu and Continue never got back to the sim. The sampler
is now 10 Hz, one flush per sample, 450 samples, `THREAD_PRIORITY_BELOW_NORMAL`.

A targeted `-noanalysis` pass on the existing Ghidra project
(`local/ghidra_scripts/FixPlayerChain*.java`, reports `local/fix_player_chain*.txt`)
confirmed the entity getter and rejected the float Vec3. The follow-up
(`FindWorldXform.java`, `DecompileXform.java`) found the real reader.

`Entity_ExportedGetWorldTransform` (string RVA `0x18AB5E0`) is registered by
`FUN_14095bfb0`. That registrar is called from `FUN_1409476e0` at RVA
`0x94870A`, and the function pointer passed in the stack argument is
`FUN_140962640` (the `LEA` at RVA `0x9486FE` targets `0x962640`, which is
code, not a pointer slot). `FUN_140962640` takes the entity, locks
`entity+0x298`, and copies:

- three qwords at `+0xD8`, `+0xE0`, `+0xE8` — the rejected read,
- a 0x3C-byte `Mat34` at `+0xF0` (`vmovups ymm0, [rdi+0xF0]` at RVA
 `0x962697`, then the rest through `+0x128`),
- one float at `+0x110`.

`FUN_140962440`, the export bound just before it, reads the same `+0xF0`
matrix and transposes it, which is what a 3x4 row-major matrix looks like
when a caller wants columns. `WorldPosition` (`FUN_1401cba60`) is a separate
three-double type used by the fact system; nothing in that printer reads the
player entity.

So the world position is the last row of that matrix: floats at
`entity+0x114`, `+0x118`, `+0x11C`. The forward vector is row 2, at
`+0x108`, `+0x10C`, `+0x110`. `sane=1` is computed from the translation
only. The old three qwords stay in the log as `old=` and never set the flag.

No stable world id was found. `[[scene+0x248]]` is the counter the live log
already measured (`0x2280` to `0x32AC`). The scene-manager script exports
(`GetWorldTransform`, `GetPosition`, `GetWorldNode`) are registrars of the
same shape, not a resource id. The probe logs that dword as `counter=`.

The next live log has to show `sane=1` on `pos=` before this is confirmed
in a running game. The matrix layout itself is what the export copies.

### Runtime test 2 (2026-10-21, owner, Mat34 build)

The owner injected the Mat34 build (`hfw-probe.exe --seconds 45`). The
DLL wrote the full 45 s and then stopped. No `fault` line. The game froze
again the moment the loader opened: they could not move, alt-tab showed
the pause menu, Continue did not resume. The process was not killed from
here, and saves were not touched. The log was copied to `local/` and not
left only in the game directory.

452 lines: a start line, 450 samples, a stop line. Quoted first, middle,
and last sample (every sample has this shape; `pos=` and `fwd=` are
identical on all 450):

```
sample=0 counter=0x00002697 entity=0x1C41B0C4280 pos=(0.000,-26340.094,5.732) fwd=(0.000,0.000,1.000) sane=1 old=40B76A4DC6FB7B68 C0A6CF8F42F55099 4074F4826B83DE20
sample=224 counter=0x00003075 entity=0x1C41B0C4280 pos=(0.000,-26340.094,5.732) fwd=(0.000,0.000,1.000) sane=1 old=40B76A4DC6FB7B68 C0A6CF8F42F55099 4074F4826B83DE20
sample=449 counter=0x0000365F entity=0x1C41B0C4280 pos=(0.000,-26340.094,5.732) fwd=(0.000,0.000,1.000) sane=1 old=40B76A4DC6FB7B68 C0A6CF8F42F55099 4074F4826B83DE20
probe stop samples=450 faults=0
```

What that is:

- `sane=1` on all 450 samples, `sane=0` on none. The three floats at
 `entity+0x114/+0x118/+0x11C` are finite and inside ±1e7. That is a real
 Mat34 translation, not the garbage the `+0xD8` float read produced.
 `pos=(0.000, -26340.094, 5.732)`: X is 0, Z is the same `5.732`
 (`0x40B76C8B`) the first run misread as Y, Y is about -26.3 km. `fwd=`
 is the unit +Z vector on every line. The entity pointer is stable,
 `0x1C41B0C4280` (a different allocation from the first run's
 `0x18EF75A0380`).
- `pos=` did not change. They did not walk. The freeze started when the
 loader's window opened, which is before sample 0, so the transform was
 sampled from a paused player for the whole file.
- `counter=` climbed `0x2697 -> 0x365F` (+4040 over 449 steps, no
 decreases) and the stop line is present. A few plateaus of 4–11 samples
 sit inside an otherwise steady climb; the counter did not stop early.
 The process stayed alive for the full 45 s. `faults=0`. This is not a
 DLL deadlock and not an access violation.
- `old=` decoded as doubles is `(5994.304, -2919.780, 335.282)`. That is
 a sane metre-scale triple and it also never moved. `FUN_140962590`
 copies those three qwords under the same `entity+0x298` lock, and
 `FUN_140962640` copies them beside the Mat34. The export still treats
 the Mat34 as the world transform. Both reads agree the player was still.

Why it froze: the loader was a console exe. `dotnet` `OutputType=Exe` is
PE subsystem 3 (`IMAGE_SUBSYSTEM_WINDOWS_CUI`). Starting it allocates a
console and that console becomes the foreground window. HFW pauses on
focus loss, which is the pause menu they saw, and Continue cannot resume
while the console still owns the foreground. The DLL never called
`AllocConsole` or `AttachConsole`. The 10 Hz `fflush` was still one
flush per sample into the game directory, but the counter moving through
sample 449 rules out a stall: a stuck worker would have stopped the log
early or frozen the counter.

`FUN_140962640` and `FUN_140962440` were re-read from the existing
decompile (`local/xform_fns.txt`, no re-analysis). The copy is still
`+0xD8/+0xE0/+0xE8` then the 0x3C matrix at `+0xF0`, with the trailing
float at `+0x110`. The transpose reader loads `ymm` from `param_2+0xF0`
and pulls `+0x110` out as its own dword. The offsets did not move. No
new offset was guessed.

The loader is now a `WinExe` (subsystem 2, confirmed on the built exe).
It creates no console. Before the wait it calls
`AllowSetForegroundWindow(ASFW_ANY)` so the game can take the foreground
back. Status is appended to `hfw-probe-run.txt` beside the exe. The DLL
flushes the log once a second instead of once a sample. Offline selftest
after the rebuild: 13/13 PASS, `SELFTEST OK`.

### Runtime test (blocked — manual steps, superseded)

Steam is logged in, but `steamapps/libraryfolders.vdf` lists only `C:`, `E:`,
`K:`, `D:`. The install is on a drive that is not one of them, and
`steam.exe -applaunch 2420110` logged `ExecCommandLine` and started nothing
(no game process after 20 s). The owner later launched the game themselves;
see the runtime test above. Launching the exe directly would trip
`SteamAPI_RestartAppIfNecessary` and restart through Steam, which fails the
same way. `<game dir>` was not written.

The save under `%USERPROFILE%\Documents\Horizon Forbidden West Complete Edition\`
(one `profile.dat`, 197,012 bytes) was copied into `local/` before the launch
attempt.

To run it:

1. Add the install's drive as a Steam library folder (Steam → Settings →
 Storage → Add Drive), so app 2420110 resolves.
2. Start the game and load a save so a player entity exists.
3. From `probe/bin/Release/net10.0`, start it with `Start-Process` so the
 shell does not attach a console:
 `Start-Process .\hfw-probe.exe -ArgumentList '--exe','<game dir>\HorizonForbiddenWest.exe','--seconds','45'`
 The loader is a windowless `WinExe`. It checks the hash, injects
 `hfw_probe.dll`, and waits. It must not open a console: a console steals
 foreground and HFW pauses on focus loss. Status is `hfw-probe-run.txt`
 next to the exe. The sample log is `hfw-probe.log` in the game's working
 directory. A good sample has `sane=1` and a `pos=(x,y,z)` in metres, with
 `fwd=(x,y,z)` a unit-ish direction. `old=` is the `+0xD8` qwords read as
 doubles (metres), kept so the two reads can be compared. `sane=0` means
 the Mat34 row is still wrong — do not keep the game running to retry.
 `counter=` is `[[scene+0x248]]` and is expected to keep climbing. Walk a
 few metres during the 45 s. The second live run's `pos=` did not move,
 because the game was paused for the whole sample.
4. Close the game. Do not leave it running.

### Runtime test 3 (2026-10-21, owner walking, borderless)

The owner confirmed the earlier freeze was a pre-existing fullscreen
focus bug, switched the game to borderless windowed, and ran
`hfw-probe.exe --exe <game dir>\HorizonForbiddenWest.exe --seconds 45`
from `probe\bin\Release\net10.0`. The loader is a `WinExe`, so it printed
nothing and returned immediately; that is not a failed inject. About 45 s
later `<game dir>\hfw-probe.log` was written. The log was copied to
`local/hfw-probe-run2.log` and parsed there. The game directory was not
written. The game was not launched from here.

452 lines: start, 450 samples, stop. `faults=0` (the stop line is the
only line containing the word `fault`). One entity for all 450 samples,
`0x25700154D00`. `counter=` climbed `0xAEA0 -> 0xC5FC` (+5980, no
decreases, per-sample delta 4..20). `fwd=(0.000,0.000,1.000)` on every
sample, sane or not.

`pos=` did **not** track the walk:

- `sane=1` on 280 samples, `sane=0` on 170. `sane=0` is the Y float
 outside ±1e7 (up to ~1e38). X is `0.000` on all 450. Z only wobbles
 `5.731..5.734` (3 mm). Forward never changes.
- The 280 sane samples are not a path. The longest sane run is samples
 0..87, `pos` identical at `(0.000,-0.000,5.732)`. Later sane Y values
 include `-8158409` and `2730540.5` between samples that are ~0, which
 is a torn float, not metres walked. First sane to last sane is 2.6 cm,
 and that centimetre is the same Z wobble.
- Quoted first, a mid sample, and last. The mid line is the first sane
 sample whose Y differs; it is not a position along a path:

```
sample=0 counter=0x0000AEA0 entity=0x25700154D00 pos=(0.000,-0.000,5.732) fwd=(0.000,0.000,1.000) sane=1 old=40B76E8999883B80 C0A68DFDB9AE304C 4074F50991243420
sample=99 counter=0x0000B190 entity=0x25700154D00 pos=(0.000,-1.028,5.732) fwd=(0.000,0.000,1.000) sane=1 old=40B77050DFAFD6D7 C0A682E3085CE41C 4074F1EC654373BC
sample=449 counter=0x0000C5FC entity=0x25700154D00 pos=(0.000,-0.026,5.731) fwd=(0.000,0.000,1.000) sane=1 old=40B7665FAB0947C3 C0A68205CBF3B27F 4074F1833DCB5309
```

What did move is `old=`, the three qwords at `entity+0xD8/+0xE0/+0xE8`
read as little-endian doubles. All 450 are finite and inside ±1e7.
They sit still through sample 87 (same place as the paused run's
`(5994.304,-2919.780,335.282)`), then move:

- min/max X `5990.370 .. 6009.283` (range 18.914)
- min/max Y `-2886.996 .. -2853.557` (range 33.439)
- min/max Z `334.798 .. 336.771` (range 1.973)
- straight-line first→last `10.125` m; farthest point from the start
 `33.934` m at sample 219; path length `110.690` m; max step `1.188` m
 per 100 ms sample. 184 steps under 1 cm, 237 steps of at least 5 cm.
 19 / 18 / 34 sign changes on X / Y / Z. That is a walk, not a counter.

`FUN_140962640` copies those three qwords in the same locked read as the
Mat34. The paused run could not tell them apart because both were still.
This run separates them: the doubles move, the Mat34 translation and
forward do not. The double3 is the world position. The Mat34 is not.

### Setter search (2026-10-21, Ghidra `-noanalysis`, no re-analysis)

The question was who stores the three qwords the walk moved. The getter
pair was re-read first, then a byte scan of `.text` for stores of
`+0xD8` followed by `+0xE0` and `+0xE8`, then a decompile of every hit
that was a scalar double store or that sat next to the `+0x298` lock.

Functions read, and what each one actually writes:

- `FUN_140962590` (RVA `0x962590`, 161 bytes) and `FUN_140962640`
 (RVA `0x962640`, 235 bytes) only **load** the three qwords. Both take
 the critical section at `entity+0x298`: `LEA RBX, [reg+0x298]`,
 `TryEnterCriticalSection`, `EnterCriticalSection` if that fails, copy,
 `LeaveCriticalSection`. Neither stores the field. Their only callers
 are the script-export registrar `FUN_1409476e0` (the `LEA`s at
 `0x9486E6` and `0x9486FE`) and data pointers. They are not the writer.
- `FUN_140948a10` (RVA `0x948a10`, 833 bytes) is the entity constructor.
 At RVA `0x948AEE` / `0x948AF5` / `0x948AFC` it does
 `MOV [RCX+0xD8], RBP`, `MOV [RCX+0xE0], RBP`, `MOV [RCX+0xE8], RBP`
 with RBP = 0, then `vmovups [RCX+0xF0], ymm0` from the identity matrix
 at `0x141fdffd0` and `MOV [RCX+0x110], EAX`. It does not take the
 `+0x298` lock. This is the zeroing, not the per-frame update.
- `FUN_141274ae0` (RVA `0x1274ae0`, 538 bytes), called from
 `FUN_141274640`, copies one record onto another: `vmovsd xmm0,
 [RDI+0xD8]` then `vmovsd [RBX+0xD8], xmm0` at RVA `0x1274B90`, then
 `[RDI+0xE0]` as a dword and `vmovups ymm` from `[RDI+0xE8]`. No
 `+0x298` lock. It is a clone of the record, not the sim that advances
 the player.
- `FUN_1410b9a00` (RVA `0x10b9a00`) `vmovsd [RDI+0xD8], xmm0` at
 `0x10B9A92` is inside a 0x100-byte element copy loop. The `+0xE0` store
 is a dword and `+0xE8` is a pointer with a refcount increment. Not the
 player double3.
- `FUN_14119c470` (RVA `0x119c470`, 4649 bytes) and `FUN_1412c3c30`
 (RVA `0x12c3c30`, 2138 bytes) `vmovsd` a triple at `[RBP+0xD8]`. RBP
 is the frame pointer; the disassembly around the site is other
 `RBP+` spills (`-0x60`, `+0x100`, `+0x108`). Stack, not the entity.
- `FUN_140f53f40` (RVA `0xf53f40`, 3695 bytes) was the only site whose
 `+0xD8` store sat within 0x80 bytes of a `+0x298` displacement and was
 followed by a `+0xF0` store. The `+0x298` use is `vminsd xmm6, xmm11,
 [RBP+0x298]`, a stack slot, and the `vmovsd [RBP+0xD8], xmm0` at
 `0xF549E7` writes a zeroed xmm. Not the entity lock.

### Per-frame writer (2026-10-21, Ghidra `-noanalysis`)

The literal `+0xD8` store grep hits unrelated objects. Starting from
`FUN_140948a10` instead: it installs `Entity::vftable` at `+0x00`
(VA `0x1418A9F40`), `+0x20` (`0x1418ACAA0`) and `+0x30` (`0x1418AD860`).
None of those slots store the double3. The ten callers of the
constructor are derived ctors (`CameraEntity`, `Sequence`,
`GravityPocket`, `ControlledEntity`, `Humanoid`, `Explosion`,
`BulletSystem`, `Throwable`, `InventoryWeapon`) and none of them
reference the `PlayerManagerGame*` global.

The integrator is not on the entity vtable. It is a component method
that holds the entity at `this+0x48` and writes it under the entity's
own lock:

- `FUN_14101bd70` (RVA `0x101BD70`, 4709 bytes). `MOV R14, [RSI+0x48]`
 at `0x101BE01` is the entity. `VMOVUPS XMM8, [R14+0xD8]` at `0x101BF50`
 and `VMOVSD XMM0, [R14+0xE8]` at `0x101BF62` load the current double3.
 A float3 at `[RAX+0x580]` is scaled by the constant xmmword at
 `0x1419C38C0`, converted with `VCVTSS2SD`, and added
 (`VADDSD XMM6, XMM0, XMM8` at `0x101C7F8`, then Y and Z). Under the
 lock `LEA RBX, [R14+0x298]` / `TryEnter` / `Enter` at `0x101C822`, it
 stores `VMOVSD [R14+0xD8], XMM6` at `0x101C83F`, `[R14+0xE0]` at
 `0x101C848`, `[R14+0xE8]` at `0x101C851`, then `OR [R14+0x98], 1` and
 `Leave`. The delta is that scaled float3, not a velocity field read
 in this function. Sole caller: `FUN_141017470` at `0x101853B`, itself
 called from `FUN_141016b00` at `0x1016CD6`.
- `FUN_14103f2d0` (RVA `0x103F2D0`, 5469 bytes) is the same shape on a
 sibling component. `MOV R15, [RDI+0x48]` at `0x103F34E`. The addend is
 `[RDI+0x650]` scaled by the same constant, added at `0x103FFAE`, stored
 under `LEA RBX, [R15+0x298]` at `0x103FFEA` / `+0xE0` / `+0xE8`. Sole
 caller: `FUN_14103c000` at `0x103C696`, called from `FUN_14103b010`.

`FUN_14101bd70` is the one whose object is this entity: same `+0x48`
indirection the player chain uses, same `+0x298` critical section, and
the Mat34 floats at `entity+0xFC/+0x100` are read in the same function.
Both stores are conditional on a float compare against a threshold at
`+0x5A0` / `+0x670`, so a frame where that compare fails does not write.

### Spawn path

No. None of the ten callers of `FUN_140948a10` reference
`PlayerManagerGame*` (`0x8982C80`), and `FUN_141274ae0` is only called
from `FUN_141274640` as a record clone. The functions that do reference
the global (`FUN_140928430`, `FUN_140916bf0`, `FUN_140d34130`, and the
other readers) only read the existing list. There is no call site that
constructs a second object with this constructor and inserts it into
the player list. A puppet that writes the one live entity moves the
owner.

### Player slots

`GetLocalPlayer` (`FUN_140928430`) copies out the whole list at
`pm+0x08` (count) / `pm+0x10` (pointer array). `FUN_140916a80`, which
`FUN_140916bf0` calls to get the entity, indexes a second array at
`param+0x10 + index*0x10` with a count at `param+0x08 + index*0x10`,
and returns `player+0x48` only for the matching entry. So `pm+0x08` is
a real count, not a hard-coded 1. Single-player never constructs a
second one (see spawn path above). A puppet that reuses the one live
entity would move the owner's player.

### Probe retarget (2026-10-21)

`hfw_probe.c` now logs the double3 as `pos=(x,y,z)` in metres.
`sane=1` only when all three are finite and inside ±1e7. The Mat34
translation is a `mat=` column and does not set `sane=`. `counter=`
and the hash guard are unchanged. Still read-only, 10 Hz, one `memcpy`
per field inside SEH, flush at most once a second.

`hfw_loopback.c` exchanges one 48-byte datagram on `127.0.0.1` only
(both sockets bound to `INADDR_LOOPBACK`, ephemeral port, never
`INADDR_ANY`). Layout: three doubles of position, then three doubles
of forward. No game process.

Offline, this session:

```
hfw_loopback.exe
LOOPBACK OK 127.0.0.1:<ephemeral> 48 bytes pos=(5991.074,-2886.996,334.957) fwd=(0.000,0.000,1.000)

dotnet run --project probe/hfw-probe.csproj -c Release -- --selftest <exe>
14/14 PASS, SELFTEST OK
```

The writer-store anchor added with the per-frame writer
(`C4 C1 7B 11 B6 D8 00 00` at RVA `0x101C83F`) was checked the same way:

```
dotnet run --project probe/hfw-probe.csproj -c Release -- --selftest <exe>
15/15 PASS, SELFTEST OK
```

The new anchors are the double3 loads inside `FUN_140962640`
(`C5 F8 10 87 D8 00 00 00` at RVA `0x962681`, `C5 FB 10 8F E8 00 00 00`
at `0x962689`) and the constructor's zeroing store at `0x948AEE`.

### Who inserts into the player list (2026-10-21, Ghidra `-noanalysis`)

The count at `pm+0x08` is not written by a function that names the
global. It is written by a shared vector push, and the only function
that calls that push with `pm+8` is a `PlayerManagerGame` virtual.

The store. `FUN_1402250c0` (RVA `0x2250C0`, 168 bytes) is the push.
`MOV EDI, [RCX]` / `INC EDI` at `0x2250D0`, then when the count exceeds
the capacity dword at `+0x04` it reallocates the pointer at `+0x08`
through `FUN_1401e6260` and stores it back (`MOV [RBX+8], R8` at
`0x225131`). The element store is `MOV [R8+RCX*8], RAX` at `0x22514A`.
The count store is `MOV [RBX], ECX` at `0x22515A` (`89 0B`). Layout of
the argument is count / capacity / pointer, so a caller that passes
`pm+8` grows exactly the list `GetLocalPlayer` copies.

The caller that passes `pm+8`. `PlayerManagerGame::vftable` is at RVA
`0x18FBE60`. Slot `+0x08` (RVA `0x18FBE68`) is `FUN_140d8c2d0`
(`0xD8C2D0`, 554 bytes); slot `+0x10` is the matching remove
`FUN_140d8c500`. `FUN_140d8c2d0(pm, player)` refcounts the record and
pushes it:

- always into `pm+0x38` (`LEA RCX, [RSI+0x38]` / `CALL 0x1402250c0`
 at `0xD8C2FF`) — the "all players" vector,
- into `pm+0x28` if `player->vfunc+0x68` is true (`0xD8C320`),
- into `pm+0x18` if `player->vfunc+0x60` is true (`0xD8C34A`),
- into `pm+0x08` otherwise (`LEA RCX, [RSI+8]` / `CALL 0x1402250c0`
 at `0xD8C38A`).

`pm+0x08` is the list `FUN_140928430` copies and `FUN_140916a80`
indexes, so the third arm is the one that adds a body the local-player
lookup can see. After the push it calls `FUN_140916c20`, which walks
`pm+0x10` and caches the first live entry at `pm+0x48`. The remove
slot does not call the push; it calls `FUN_140916820`, which scans
each of the four vectors and decrements the count.

What gets inserted. Not the entity from `FUN_140948a10`, and not the
clone `FUN_141274ae0`. The argument is a `PlayerGame` record. Its
constructor is `FUN_140d73270` (`0xD73270`, 416 bytes): it calls
`FUN_140913be0` (which writes `Player::vftable` at `+0x00` and `+0x20`,
VA `0x1418AA868` / `0x1418AAEE8`) and then writes `PlayerGame::vftable`.
`param_1[6]` of that record is the entity argument, which is where
`player+0x48` comes from — the same offset `FUN_14101bd70` reads. The
entity itself is still built by `FUN_140948a10` elsewhere; this insert
only takes the pointer.

Call chain, single-player. `FUN_14134c590` (`0x134C590`) calls
`FUN_14134c950` (`0x134C950`) and then walks `[pm+0x10]` / `[pm+8]`,
so it runs against the live manager. `FUN_14134c950` allocates with
`FUN_1401e6100` at `0x134CA82` and constructs with `FUN_140d73270` at
`0x134CA90` (and again at `0x134CAA8` on the zero-size path). At the
bottom, `0x134CF23` loads the global (`MOV RCX, [0x148982C80]`) and
`0x134CF30` does `CALL [RAX+8]` with the new `PlayerGame` in `RSI` —
that is `FUN_140d8c2d0`. Both functions are only referenced from
vtables (`0x1980FF8` for `FUN_14134c590`), not from
`NetSessionManagerNoOp` and not from the stripped `NetSessionManager`.
The `MsgRemotePlayerJoined` post inside `FUN_140d8c2d0` is a side
effect after the push, gated on `[[0x148983150]+0x178]`; it is not the
insert. A second path, `FUN_1413d8630`, calls the same virtual but its
only references are data (`0x1980B40`), so it is not the live one.

So the path is reachable from a live system, not only from the dead
net layer. It is a virtual on a vtable, which is why it has no direct
`CALL` xref — the call site is the indirect `CALL [RAX+8]` above.
Calling it from an injected thread is the next step, and it was not
called here.

### Spawn call convention (2026-10-21, Ghidra `-noanalysis`)

`FUN_14134c950` was re-read before anything called it. Prologue at
RVA `0x134C950`:

```
48 89 5C 24 10 MOV [RSP+0x10], RBX
48 89 74 24 18 MOV [RSP+0x18], RSI
48 89 7C 24 20 MOV [RSP+0x20], RDI
48 89 4C 24 08 MOV [RSP+8], RCX
```

The saved RCX is the home slot at `[RBP+0x67]`. At `0x134CB4A` that
slot is zeroed (`MOV [RBP+0x67], RDI`, RDI is 0) and then passed to
`FUN_140108010` as the destination of the `"player"` name. The incoming
argument is discarded. The only caller confirms it: `FUN_14134c590` at
`0x134C657` is `E8 F4 02 00 00` with no `MOV RCX` between it and the
previous call. So the call is `void FUN_14134c950(void)`.

It does not return a value the caller uses. `FUN_14134c590` ignores
RAX and goes on to walk `[pm+0x10]`.

Allocation size is not an argument. Byte at RVA `0x2161594` is the
type tag `0x04`, and the size switch (`0x134CA52`, table at
`0x134CF84`, case 4 falls through) uses the qword loaded at
`0x134C9EB` from RVA `0x21615A0`. Those bytes are `10 02 00 00`, so
`FUN_1401e6100` is called with `0x210`. That record is static data in
the image, read by twenty other functions; it is not something the
probe builds.

Thread context. `FUN_14134c950` itself has no `GS:` operand (408
instructions, zero hits). Three callees do, all the same shape:

- `FUN_1401e6100` at `0x1E610A`: `MOV RAX, GS:[0x58]`, then
 `[tls+0x1858]`. If that byte is 0 it calls `FUN_14165833c`
 (`__dyn_tls_on_demand_init`, the same call `FUN_14134b340` makes).
 Then `[tls+0x1A10]`; if that slot is 0 it stores the process-wide
 pointer at `[0x14897A388]` and uses it.
- `FUN_140108010` at `0x108094` and `FUN_140107920` at `0x107994`:
 same `GS:[0x58]`, same `+0x1A10` slot, same fallback, and a
 `CMOVZ` onto the static allocator at `0x141FE1F00` when the slot
 already holds that address.

An injected thread has an empty TEB slot, so those three sites take
the fallback instead of a game-thread context. No lock is taken on
entry. The critical section at `0x134CED1` is `TryEnter` on
`[0x148982D88]+0x30` and `Enter` if that fails; it is the same lock
the game thread takes, and this call runs while the sim is live, so
the game can hitch for the call.

What the call does after the alloc, which is why it is one shot and
SEH-wrapped: `CoCreateGuid`, writes the GUID at `PlayerGame+8`, builds
the name, then calls `PlayerGame` virtuals `+0x58`, `+0x20`, `+0x38`
before the add slot at `0x134CF30`. Those virtuals run on the injected
thread. SEH catches an access violation; it does not catch a stall
inside one of them.

### Probe one-shot (2026-10-21)

`--spawn-once` is opt-in. The default is still the 45 s read-only
sampler. The loader writes one byte to `hfw_probe.dll.mode` beside the
DLL (not in the game directory). The DLL reads that byte from its own
module path and, only when it is 1, starts `spawn_once` instead of the
sampler. That thread waits until `[pm+0x08] >= 1` (60 s timeout), logs
`spawn-once before count=N storage=...`, calls `FUN_14134c950` once
inside `__try` with a zeroed unused argument, then logs
`spawn-once after called=1 code=0x00000000 count=N storage=... delta=...`.
`storage` is `[pm+0x10]`. It does not store the double3 and does not
call `FUN_14101bd70`. A count of 1 then 2 is the second body.

A second body is the milestone after a log that shows `count=1` then
`count=2`. Driving that body's double3 from a received pose is the
step after that, and it is still not the owner's entity.

### Runtime test 4 (2026-10-21, owner, --spawn-once, no log)

The owner deleted `<game dir>\hfw-probe.log`, then from
`probe\bin\Release\net10.0`:

```
Start-Process .\hfw-probe.exe -ArgumentList '--exe','M:\Horizon Forbidden West\HorizonForbiddenWest.exe','--spawn-once'
```

The shell returned immediately. The game stayed playable. The log was
not recreated. That is not a silent inject.

The binary they launched is the spawn-once build, not an older exe.
`hfw-probe.exe` LastWriteTime 2026-10-21 15:59:39, `hfw_probe.dll` in
that folder 15:57:56, source `hfw_probe.c` 15:58:10 and `hfw-probe.cs`
15:56:42. The native DLL contains `spawn-once after`. The managed
`hfw-probe.dll` contains `--spawn-once` as UTF-16 (the apphost exe does
not; .NET strings live in the managed DLL). Subsystem is still 2.

Evidence is `hfw-probe-run.txt` beside that exe (written 16:03:41):

```
REFUSED: exe not found: M:\Horizon
```

`Start-Process -ArgumentList` with commas is a string array. Windows
PowerShell 5.1 splits each element on spaces before the child sees it,
so the loader received `--exe`, `M:\Horizon`, `Forbidden`, `West\HorizonForbiddenWest.exe`,
`--spawn-once`. `Arg` took only the next token. `Guard` failed on a
path that does not exist, `Note` wrote the refusal, and `Main` returned
2. No inject, no `hfw_probe.dll.mode`, no game log. The game kept
running because nothing was loaded into it. The two older lines in the
same sidecar are earlier runs (one 45 s inject, one more split path).

The loader now rejoins tokens after `--exe` / `--dll` / `--selftest`
until the next `--` flag, so the comma form and a quoted path both
work. Every exit writes to `hfw-probe-run.txt` next to the exe:
`spawn flag seen` or `spawn flag absent`, `process found pid=N` or
`process not found`, `hash ok` or `hash fail`, `injected` or
`inject fail`, and `EXCEPTION:` if anything throws before that.
The hash guard is unchanged. `--spawn-once` still waits for
`[pm+0x08] >= 1`, calls `FUN_14134c950` once inside SEH with a zero
argument, and does not store the double3.

The rebuilt loader was then started once, by mistake, while the game
was already open, to check the rejoin. It did inject. The game log
(copied here, not left as the only copy) is three lines:

```
spawn-once start call=FUN_14134c950 no-pose-write
spawn-once before count=1 storage=0x1FF952101E0
spawn-once after called=0 code=0xC0000005 count=1 storage=0x1FF952101E0 delta=0 faults=1
```

`count` stayed 1. `0xC0000005` is the SEH access violation inside
`FUN_14134c950`. The call returned. That is not `count=2`. The same
session then decompiled the caller and the first loads (below). Do not
rerun the call. Alt-f4 this session first: the access violation ran
inside the game, so the process is dirty even if it still looks
playable.

### Why the call faulted (2026-10-21, Ghidra `-noanalysis`)

Copied the game log to `local/hfw-probe-spawn-av.log` (read-only). Same
three lines as above. No second inject. The game process was left
running; the old `hfw_probe.dll` stayed mapped, so the rebuilt DLL was
written after renaming that mapped file aside.

Caller of the bare `E8` at `0x134C657` is the whole of `FUN_14134c590`
(`0x134C590`, 361 bytes, `void(void)`). It does not put a live object
in RCX. The spawn call is reached by fall-through when
`[0x148983278]` is null, and after a few other calls when it is not.
None of those calls leave RCX set up for `FUN_14134c950`. The saved
RCX in the spawn prologue is still discarded at `0x134CB4A`.

First pointer the spawn function dereferences:

```
0x134C979 MOV RAX, [0x1489581F0] ; tested, JZ skips the first virtual
0x134C9B1 MOV RAX, [0x1489581F0] ; reloaded
0x134C9BA MOV RCX, [RAX+0xF8] ; no TEST RAX. this is the AV
0x134C9C1 MOV RAX, [RCX+0x108] ; next, also unchecked
```

`0x134C9B1` is `48 8B 05 38 B8 60 07`. RIP+disp lands on VA
`0x1489581F0` (RVA `0x89581F0`). That qword is past the file-backed
`.data` (virtual size `0x6EECA90`, raw size `0x626800`), so the loader
zeroes it. The write was then checked against the exe bytes, not
against Ghidra's xref (next section). The later load at `0x134C9D5`
of `[0x148983150]` is the same class (a BSS pointer the caller does
not pass) and is also unchecked at `0x134C9FA`
(`MOV RCX, [RAX+0x50]`).

Decision: **removed the call.** It is not one pointer the probe can
read offline and pass. `--spawn-once` still injects, but the thread
only writes

```
spawn-once blocked: FUN_14134c950 derefs [0x1489581F0]+0xF8 ...
```

and returns. It does not call `FUN_14134c950`. Do not expect
`count=2` from this build.

Selftest after the rebuild (offline exe, no inject, no launch):
20/20, `SELFTEST OK`. New anchors: the reload at `0x134C9B1`
(`48 8B 05 38 B8 60 07`) and `MOV RCX,[RAX+0xF8]` at `0x134C9BA`.

### Who writes `0x1489581F0` (2026-10-21, exe bytes, no inject)

Checked the offline exe
(`local/workspace/hfw-binaries/HorizonForbiddenWest.exe`, SHA-256
unchanged) with capstone 5.0.7. No Ghidra re-analysis. No inject, no
launch, nothing written under `M:\`. `--spawn-once` is still the
blocked path from `05ab3b2`; the call was not put back.

The store is a real write. File bytes at RVA `0xD304B9` are
`48 89 3D 30 7D C2 07`. That is `MOV [RIP+0x7C27D30], RDI`. The next
instruction begins at `0xD304C0`, and
`0xD304C0 + 0x7C27D30 = 0x89581F0`, which is VA `0x1489581F0`. A scan
of every executable section for `REX.W` + `MOV r/m64, r64` (`89 /r`),
`MOV r/m64, imm32` (`C7 /0`), and `LEA r64, [RIP]` whose disp32 lands
on that RVA found **one write and zero LEAs**:

```
RVA 0xD304B9  48 89 3D 30 7D C2 07   MOV [0x1489581F0], RDI
```

194 other `REX.W MOV r64, [RIP]` sites read it (`8B /r`), including
the two loads inside `FUN_14134c950` (`0x134C979`, `0x134C9B1`). No
32-bit form and no locked RMW hits the qword. Nothing else stores it,
so nothing fills it later when a save loads.

What gets stored. Inside `FUN_140d2fca0` (`0xD2FCA0`), `R12` is
zeroed at `0xD2FF78` (`XOR R12D, R12D`) and is not written again
before the store. The size comes from a Decima type record; if it is
0, `RDI` is set from `R12` at `0xD304AB` (`MOV RDI, R12`). Otherwise
`FUN_1401e6100` returns the block into `RDI` (`0xD304A6`). Either way
the global receives that pointer, then `FUN_140d3d960(RDI)` runs at
`0xD304C0`. The only direct caller of `FUN_140d2fca0` is `0x429D4`,
and the only caller of that is the startup chain (`MainProg` →
`GameMainProg`). The incoming `RCX` is saved at `[RSP+8]` and never
reloaded. `FUN_140231060` returns `AL=1` at `0x231544` on the success
path and `AL=0` at `0x23154D`; `FUN_140d2fca0` returns immediately
when `AL` is 0 (`0xD2FCC0`), so the store only happens when startup
gets past that gate.

The object is not `NetSessionManagerNoOp`. `FUN_140d3d960` writes
`NetErrorHandlerGame::vftable` (RVA `0x18FB208`, COL at `0x1A83B18`,
type descriptor `.?AVNetErrorHandlerGame@@`) at `[this]` and a second
`NetErrorHandlerGame` vtable (`0x18FC2A0`) at `[this+8]`. It then
fills slots on that object, including `+0xF8`. The `+0xF8` store is
`MOV [RDI+0xF8], RSI` at `0xD3E8D7`, after `FUN_140d966f0(RSI, ...)`
at `0xD3E8CF`. That constructor writes `StateInGame::vftable` (RVA
`0x18FFD88`, `.?AVStateInGame@@`) at `[this]` and zeroes
`[this+0x108]` at `0xD967CD` (`MOV [RCX+0x108], RSI` with `ESI==0`).
So once startup has returned, `[0x1489581F0]` is a
`NetErrorHandlerGame*`, `[that+0xF8]` is a `StateInGame*`, and
`[StateInGame+0x108]` is still 0. `NetSessionManagerNoOp::vftable`
(`0x184AEE0`) is not written onto either object.

The null the probe saw is not a second address. `0x134C9B1` is
`48 8B 05 38 B8 60 07`; `0x134C9B1+7+0x0760B838 = 0x1489581F0`, the
same RVA the startup store writes. Image base `0x140000000` makes the
VA identical. A loaded save does not have another writer. If the live
qword was 0 while the session was in game, startup did not publish
it (the `AL==0` early return, or the size-0 path that stores `R12`).
That was not re-read from the process this session.

`FUN_14134c950` is still not callable from the probe thread. The
reload at `0x134C9B1` has no null test, and the next load is a
different global: `MOV RAX, [0x148983150]` at `0x134C9D5`, then
`MOV RCX, [RAX+0x50]` at `0x134C9FA` with no test. `R15` (the pointer
passed to `FUN_140d73270` as `RDX` at `0x134CA87`) is
`[[0x148983150]+0x50]+0x20`, taken at `0x134C9FE`. Three `REX.W`
stores hit `0x148983150`:

- `0xD5E0C6` `MOV [0x148983150], RBP` with `EBP==0` (clears it),
- `0xD5E768` `MOV [0x148983150], RDI` (publishes it), in a function
 that writes `PlayerManagerGame::vftable` (`0x18FBE60`,
 `.?AVPlayerManagerGame@@`) at `0xD5E646` and `GameModuleState`
 (`0x18FB998`) at `0xD5E6B0`,
- `0xD61673` `MOV [0x148983150], R12` (clears it again).

The entity pointer is therefore a field of whatever `0xD5E768`
published, not an argument of the spawn call. Building a
`PlayerGame` without `FUN_14134c950` does not remove the dependency.
`FUN_140d73270(this, entity)` forwards `RDX` into `FUN_140913be0`,
which stores it at `[this+0x30]` (`MOV [RDI+0x30], RBX` at
`0x913CB7`). Direct calls of `FUN_140d73270` are only `0x134CA90`,
`0x134CAA8` (both inside `FUN_14134c950`, `RDX = R15`) and
`0x13D8540`. The third one is the same shape: `MOV RAX, [0x148983150]`
at `0x13D847F`, then `MOV RDX, RSI` / `CALL 0x140d73270`. There is no
direct `CALL` of `FUN_140d8c2d0`; the insert stays the virtual at
`[PlayerManagerGame::vftable+8]`.

Conclusion: **(c)**. Neither path is callable from an injected thread.
The missing pointer is `[[0x148983150]+0x50]+0x20`, the entity passed
as `RDX` to `FUN_140d73270`. `0x148983150` itself is only published at
`0xD5E768` and is cleared at `0xD5E0C6` and `0xD61673`. `+0x50` on
that object was not found as a store in the publish function, so the
entity pointer is not named beyond that load. Do not re-enable
`FUN_14134c950`. Do not emit `FUN_140d8c2d0`.

### Who writes `+0x50` on the object at `0x148983150` (2026-10-21)

Offline only. Capstone 5.0.7 on the workspace exe (SHA-256 unchanged)
plus one Ghidra `-noanalysis -process` pass (`local/ghidra_scripts/Plus50.java`,
report `local/plus50.txt`) for function bounds and xrefs. No re-analysis,
no inject, no launch, nothing written under `M:\`. `--spawn-once` was
not rebuilt into a call. The probe selftest gained two byte anchors
(`48 8B 3D DA 00 C2 07` at `0xD6306F`, `48 89 5F 50` at `0xD63090`)
and was run offline against the workspace exe: 23/23, `SELFTEST OK`.
The game was not launched.

The publish function is `FUN_140d5e2c0` (`0xD5E2C0`, 6903 bytes). Its
only code reference is a slot, not a call: `GameModule::vftable+0x20`
(vtable RVA `0x18FB8B0`, `.?AVGameModule@@`, COL `0x1A838A0`). `RCX`
is copied to `RDI` at `0xD5E2E5` and `RDI` is not assigned again before
the publish, so the stored pointer is that `GameModule*`. The publish
itself is `48 89 3D E1 49 C2 07` at `0xD5E768`, `MOV [RIP+0x7C249E1], RDI`,
and `0xD5E76F + 0x7C249E1 = 0x8983150`.

Stores this function makes into that object (base `RCX` before the copy,
base `RDI` after it):

| RVA | bytes | store |
|---|---|---|
| `0xD5E2E8` | `44 89 B1 74 06 00 00` | `[this+0x674] = 0` |
| `0xD5E301` | `48 89 91 90 04 00 00` | `[this+0x490] = [[0x1489581C8]+0x30]` |
| `0xD5E529` | `C5 F8 11 87 80 04 00 00` | `[this+0x480] = xmm0` |
| `0xD5E728` | `48 89 8F 78 04 00 00` | `[this+0x478] = RCX` (the `GameModuleState` built at `0xD5E6B0`, vtable `0x18FB998`) |
| `0xD5E741` | `C7 87 74 06 00 00 03 00 00 00` | `[this+0x674] = 3` |
| `0xD5EB0C` | `48 89 9F 70 02 00 00` | `[this+0x270] = RBX` |
| `0xD5EFA1` | `48 89 9F 80 01 00 00` | `[this+0x180] = RBX` |
| `0xD5F00F` | `48 89 87 58 05 00 00` | `[this+0x558] = RAX` |
| `0xD5F11A` | `48 89 87 88 01 00 00` | `[this+0x188] = RAX` |
| `0xD5F6AA` | `48 89 5F 60` | `[this+0x60] = RBX` |
| `0xD5F815` | `48 89 87 60 02 00 00` | `[this+0x260] = RAX` |
| `0xD5F8D4` | `48 89 87 98 01 00 00` | `[this+0x198] = RAX` |
| `0xD5F92D` | `48 89 9F A0 01 00 00` | `[this+0x1A0] = RBX` |
| `0xD5F94C` | `48 89 87 E8 01 00 00` | `[this+0x1E8] = RAX` |
| `0xD5F96A` | `48 89 87 D0 01 00 00` | `[this+0x1D0] = RAX` |
| `0xD5FBC7` | `48 89 9F 68 02 00 00` | `[this+0x268] = RBX` |
| `0xD5FD1C` | `48 89 9F 78 02 00 00` | `[this+0x278] = RBX` |
| `0xD5FD23` | `C6 87 70 06 00 00 00` | `[this+0x670] = 0` |
| `0xD5FD2A` | `C7 87 74 06 00 00 01 00 00 00` | `[this+0x674] = 1` |

`+0x50` is not in that list. The `MOV [RAX+0x50], R14` at `0xD5E671`
(`4C 89 70 50`) writes the `PlayerManagerGame` allocated at `0xD5E628`
(vtable `0x18FBE60` stored at `0xD5E67D`), which is published to the
other global `0x148982C80` at `0xD5E68B`. That is a different object.

Writers of `[reg+0x50]` were then required to load `0x148983150` in the
same function and to keep that register (or a `MOV` copy of it) as the
store base. 994 `REX.W MOV r64, [0x148983150]` sites, one hit:

```
FUN_140d63060
RVA 0xD6306F  48 8B 3D DA 00 C2 07  MOV RDI, [0x148983150]
RVA 0xD63076  48 8B DA              MOV RBX, RDX
RVA 0xD63090  48 89 5F 50           MOV [RDI+0x50], RBX
```

`RDI` is not written between the load and the store. The previous value
is released first (`MOV RCX, [RDI+0x50]` at `0xD63082`, `CALL 0x1402084c0`
at `0xD6308B` when it is non-null). `RDX` is refcounted at `[RDX+0x18]`
(`F0 FF 42 18` at `0xD6307E`) before it is stored. One direct caller,
`FUN_14134bf40` at `0x134BFEC`. That function's `RDX` is the unwrapped
object from `[[[0x148983150]+0x490]+0x88]`, taken at `0x134BF90` as
`[entity+0x20]` when the handle tag at bit 60 is set, otherwise from
`[that+0xE8]` / `[that+0xE0]` by the same unwrap (`0x134BFE3`). No RTTI
name was recovered for that object: no constructor both writes a vtable
and writes the `float` at `+0x198` which `FUN_140d63060` reads back at
`0xD630A8`. It is a refcounted Decima object reached through the
`GameModule+0x490` handle, not a typed player record.

When it runs. `FUN_14134bf40` has no direct `CALL`. Its one code xref is
the tail `JMP` at `0x134BCD6` inside `FUN_14134bbc0`, whose one code xref
is the `JMP` at `0x134BB29` inside `FUN_14134bac0`. `FUN_14134bac0` has no
`CALL` either. Its slot is `GameModuleStateLoading::vftable+0x30` (vtable
RVA `0x18FB5A0`, `.?AVGameModuleStateLoading@@`). That vtable is written
once, at `0xD5F98C`, inside the same `FUN_140d5e2c0`, onto the state
object then stored at `[GameModule+0x1D0]`. So the `+0x50` store runs when
that loading-state virtual runs, which is after startup has published the
`GameModule`, not per frame and not inside the publish function. The
`float`/`dword` it copies (`+0x198`, `+0x19c`, `+0x1A0`) are only
available once that object exists; a freshly published `GameModule` does
not have `+0x50` filled by `FUN_140d5e2c0`.

`+0x50` holds that refcounted object. The entity pointer the spawn path
wants is still one field further, `[that+0x20]`, which `FUN_14134c950`
loads at `0x134C9FE`. This session did not read it from a live process.

### `--read-slot` runtime (2026-10-21, owner, save loaded)

The owner loaded a save, then from `probe\bin\Release\net10.0`:

```
Start-Process .\hfw-probe.exe -ArgumentList '--exe','<game dir>\HorizonForbiddenWest.exe','--read-slot'
```

No inject and no launch from this session. The game directory was
read, not written. The log was copied to `local/hfw-probe-slot.log`
(git-ignored). The sidecar beside the exe says `read-slot flag seen`,
`hash ok`, `process found pid=22384`, `injected mode=read-slot`.
`--spawn-once` was not this run.

42 lines: one start line, 40 `slot` lines, one stop line. No `fault`
line. `faults=0`. First, middle (line 21 of 40), and last are the
same three pointers:

```
slot module=0x2357D4C2A00 plus50=0x23109E3E4D0 entity=0x23111517280
slot module=0x2357D4C2A00 plus50=0x23109E3E4D0 entity=0x23111517280
slot module=0x2357D4C2A00 plus50=0x23109E3E4D0 entity=0x23111517280
read-slot stop samples=40 faults=0
```

40/40 have `plus50 != 0`. 40/40 have `entity != 0`. All 40 lines are
byte-identical: `module` `0x2357D4C2A00`, `plus50` `0x23109E3E4D0`,
`entity` `0x23111517280`. Nothing changed across the 20 s at 2 Hz.
The reader did not fault, and the module pointer did not stay 0.

**SLOT LIVE.** `GameModule+0x50` is the object
`MOV [RDI+0x50], RBX` at RVA `0xD63090` stores, and it stayed
non-null for the whole sample. `entity` is also non-null: that is
`[[module+0x50]+0x20]`, the pointer `FUN_140d73270` stores at
`Player+0x30`. It was recorded, not passed to anything.
`--spawn-once` stays blocked. `FUN_14134c950` was not called.

### Next milestone (documented, not implemented)

A second `PlayerGame` can be constructed with
`FUN_140d73270(this, entity)` using that entity pointer
(`0x23111517280` this run), then inserted via the virtual
`FUN_140d8c2d0` (`PlayerManagerGame::vftable+8`). That call was not
written.

The missing piece is who allocates the `PlayerGame` and the calling
convention of the constructor. The only allocator the existing
decompile names for this record is `FUN_1401e6100` inside
`FUN_14134c950` (`0x134CA82`), size `0x210` from the static type
record at RVA `0x21615A0`, and that function is the one that faults
on null `[0x1489581F0]`. `FUN_140d73270` itself is
`PlayerGame*(RCX=this, RDX=entity)`: it forwards `RDX` into
`FUN_140913be0`, which stores it at `[this+0x30]` (`MOV [RDI+0x30],
RBX` at `0x913CB7`). Microsoft x64, two register args, return in
`RAX`. Do not call it until the allocation is separated from
`FUN_14134c950`. Do not re-enable that function. Do not emit
`FUN_140d8c2d0`.

### Second allocator: `FUN_1413d8470` (2026-10-21, Ghidra `-noanalysis`)

Offline only. One Ghidra `-noanalysis -readOnly -process` pass per
script (`local/ghidra_scripts/PlayerGameAlloc.java` and the numbered
follow-ups, reports `local/playergame_alloc*.txt`). No symbol-table
scan, no re-analysis, no inject, no launch, nothing written under
`M:\`. `--spawn-once` was not rebuilt into a call. The function was
not called.

The call at `0x13D8540` is inside `FUN_1413d8470` (`0x13D8470`, 365
bytes). It is a `NetPlayerGame` constructor, not a stack temporary:

```
undefined8 * FUN_1413d8470(undefined8 *param_1)
```

`RCX` at entry is the `NetPlayerGame` being built. It is saved to
`RBX` at `0x13D8495` and returned in `RAX` at `0x13D85CF`. The
`PlayerGame` is a fresh heap block, not a field of that object and
not a pool slot.

The size is the same static record the blocked function uses, read
rather than written as an immediate. Bytes at RVA `0x2161590`:

```
FF FF FF FF 04 00 01 00 00 00 FF 00 01 00 00 00
10 02 00 00 ...
```

Tag byte `0x2161594` is `0x04`. Case 4 of the size switch falls
through, so `EDX` is the dword at `0x21615A0`, which is `10 02 00 00`
(`0x210`). Then:

```
RVA 0x13D852B MOVSXD RCX, EDX
RVA 0x13D852E CALL 0x1401e6100
RVA 0x13D8533 MOV RDI, RAX
RVA 0x13D853A MOV RDX, RSI
RVA 0x13D853D MOV RCX, RDI
RVA 0x13D8540 CALL 0x140d73270
```

`RSI` is `[[0x148983150]+0x50]+0x20`, loaded at `0x13D847F` /
`0x13D84A0` / `0x13D84A4` with no null test. If the size is 0 the
function zeroes `EDI` at `0x13D8538` and still calls the constructor,
passing a null `this`. On the live path the size is not 0.

After the constructor it writes three vtables onto `param_1`, in
order, each confirmed from the COL before the vtable:

| RVA | store | RTTI |
|---|---|---|
| `0x13D8554` | `[RBX] = 0x141849A60` | `.?AVNetReplicatedObject@@` |
| `0x13D8573` | `[RBX] = 0x1418C48F8` | `.?AVNetPlayer@@` |
| `0x13D85CC` | `[RBX] = 0x141980B30` | `.?AVNetPlayerGame@@` |

It then stores the new `PlayerGame` at `[NetPlayerGame+0x40]`
(`0x13D8576`) and the `NetPlayerGame` back at `[PlayerGame+0xE8]`
(`0x13D85BE`).

Globals this function dereferences: `0x148983150` (the `GameModule*`,
one load, no store), and the static type record
`0x2161594` / `0x2161596` / `0x2161598` / `0x21615A0` / `0x21615A8` /
`0x21615D8`, plus the two bytes copied onto the object at
`0x2012B70` / `0x2012B71`. It does **not** dereference
`0x1489581F0`. 87 instructions, 17 data targets, one watch hit, and
that hit is the module global.

References to `FUN_140d73270`, all five of them:

| from | what it is |
|---|---|
| `0x134CA90` | `CALL` inside `FUN_14134c950` |
| `0x134CAA8` | `CALL` inside `FUN_14134c950`, null-`this` arm |
| `0x13D8540` | `CALL` inside `FUN_1413d8470` |
| `0xD724ED` | `JMP` thunk at `0xD724E0`: `MOV RCX, RDX; XOR EDX, EDX; JMP FUN_140d73270`. Constructs whatever pointer it was given. Does not allocate. |
| `0x8C36984` | data, no function |

`FUN_1401e6100` inside `FUN_14134c950` is called twice (`0x134CA82`
for the `PlayerGame`, `0x134CE47` for a second record). The second
one reads a different type record, RVA `0x220F054`, whose size dword
at `0x220F060` is `48 00 00 00` (`0x48`), and the pointer it loads at
`0x134CDDD` is the thunk `0x13D8450`, not `FUN_140d73270`. So that
allocation is not a `PlayerGame`.

Who calls `FUN_1413d8470`. No direct `CALL` from another function.
Two references: the data slot at RVA `0x8C8EC20` (a packed dword
`01 3D 84 70` inside `.pdata`, the only such dword outside `.text`),
and the thunk at `0x13D8450`:

```
RVA 0x13D8450 TEST RDX, RDX
RVA 0x13D8453 JZ 0x1413d845d
RVA 0x13D8455 MOV RCX, RDX
RVA 0x13D8458 JMP 0x1413d8470
```

That thunk's only reference is a data read at `0x134CDDD`, which is
inside `FUN_14134c950`. So the only thing that invokes this allocator
today is the function that is blocked.

Conclusion: a second allocator exists and it does not touch the null
global. `FUN_1413d8470` at RVA `0x13D8470` calls `FUN_1401e6100` at
`0x13D852E` with `0x210` from the type record at `0x21615A0`, then
`FUN_140d73270(block, [[GameModule+0x50]+0x20])`. It runs only as the
constructor of a `NetPlayerGame`, and the only caller of that
constructor is `FUN_14134c950` itself, via the thunk at `0x13D8450`.
Nothing else allocates a `0x210` `PlayerGame`. The thunk at
`0xD724E0` constructs an already-allocated pointer with a null entity
and does not allocate. Not called. `--spawn-once` stays blocked.

## Not tried (next session)
- Socket activity observation, single-instance mutex survey for two copies.
- Map WS2_32 call sites; map the tables that invoke the NetGameDummy factory.
- Ask JustAddMP/core author about a license (owner approval for contact).
- Milestone 2, revised again and then read live: `[0x148982CB0]` is a
 `CameraPredictionManager*` published by `FUN_140d5e2c0` at
 `0xD5FA19` and cleared by `FUN_140d60010` at `0xD60547`. The owner
 ran `--read-cam` after a save load and the pointer was
 `0x235749C3E80` for all 20 samples (`lock20=0x0`, unlocked,
 `faults=0`). `--add-player` is the one-shot below. Do not call
 `FUN_14134c950`. Do not call `FUN_1413d8470`. Do not call
 `FUN_14101bd70`. Do not write the double3. Do not store to
 `entity+0xD8`. Do not emit `FUN_140d8c2d0`.

### `--add-player` blocked (2026-10-21, Ghidra `-noanalysis`)

Offline only. One Ghidra `-noanalysis -readOnly -process` pass
(`local/ghidra_scripts/AddPlayerFrames.java`,
`local/ghidra_scripts/AddPlayerHazard.java`, reports
`local/addplayer_frames.txt` and `local/addplayer_hazard.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`.

The call frames, written down before the probe was touched:

Alloc, `FUN_1401e6100` at RVA `0x13D852E`, inside `FUN_1413d8470`:

```
RVA 0x13D8527 TEST EDX, EDX
RVA 0x13D8529 JZ 0x1413d8538
RVA 0x13D852B MOVSXD RCX, EDX
RVA 0x13D852E CALL 0x1401e6100
RVA 0x13D8533 MOV RDI, RAX
```

`RCX` is the size, sign-extended from `EDX`. `EDX` is the dword at
RVA `0x21615A0` (`10 02 00 00`, `0x210`) on the case-4 path. There is
no second argument and no flags argument. `RDX` is dead at the call.
The body (`0x1E6100`, 22 instructions) is `MOV RAX, GS:[0x58]`, then
`[tls+0x1858]` with a fallback call to `__dyn_tls_on_demand_init`
(`0x165833C`), then `[tls+0x1A10]` with a fallback store of
`[0x14897A388]`, then `JMP [allocator+0x88](allocator, size)`. It does
not read `0x1489581F0` and it takes no lock. On its own it would be
callable from the probe thread.

Constructor, `FUN_140d73270(RCX=block, RDX=entity)`, not called. It
fills the `0x210` block, then:

```
RVA 0xD7339C MOV RCX, [0x148982D88]
RVA 0xD733A3 TEST RCX, RCX
RVA 0xD733A6 JZ 0x140d733c2
RVA 0xD733B3 CALL [RAX+0x10]
RVA 0xD733C2 MOV RDI, [0x148982CB0]
RVA 0xD733C9 LEA RBX, [RDI+0x20]
RVA 0xD733D3 CALL [0x14174B1C8]
RVA 0xD733DC LEA RCX, [RDI+0x30]
RVA 0xD733E7 CALL 0x1400fe310
```

`0xD7339C` has a null test. `0xD733C2` does not. Both calls after it
use `RDI+0x20` and `RDI+0x30`. A null there is `0xC0000005` inside the
constructor, the same class of fault `--spawn-once` already hit. The
image bytes at `0x148982D88` are eight zeroes; the live qword is
whatever the running game stored, and the probe cannot know it before
the call. The "scene pointer" reading of `RDI` is wrong; the writer
pass below names it.

Insert, the `pm+8` arm of `FUN_140d8c2d0`. The `PlayerGame` is in
`RBX`, which was `RDX` at entry (`MOV RBX, RDX` at `0xD8C2E4`):

```
RVA 0xD8C381 LEA RCX, [RSI+8]
RVA 0xD8C385 MOV [RSP+0x78], RBX
RVA 0xD8C38A LOCK INC [RBX+0x18]
RVA 0xD8C38E LEA RDX, [RSP+0x78]
RVA 0xD8C393 CALL 0x1402250c0
```

`FUN_1402250c0(RCX=pm+8, RDX=&slot)` copies `[RDX]` into the list and
stores the new count with `MOV [RBX], ECX` at `0x22515A`. Reaching
that arm is not free: `0xD8C313` and `0xD8C33D` call
`[PlayerGame+0x68]` and `[PlayerGame+0x60]` first, and the false/false
path is the only one that takes `pm+8`.

Decision: the constructor is not safe to call from the probe thread,
so none of the three calls is emitted. Calling the allocator alone
would leak a `0x210` block the constructor never takes, and the insert
needs a constructed `PlayerGame`. `--add-player` injects and logs:

```
add-player blocked: FUN_140d73270 derefs [0x148982CB0] ... not called. count stays.
```

Do not expect `count=2`. `--spawn-once` and `--read-slot` are
unchanged. Selftest is 26/26 on the offline exe, including the alloc
call site (`48 63 CA E8 CD DB E0 FE` at `0x13D852B`), the constructor
call site (`48 8B D6 48 8B CF E8 2B AD 99 FF` at `0x13D853A`) and the
blocking load (`48 8B 3D E7 F8 C0 07` at `0xD733C2`).

The running game had the previous `hfw_probe.dll` mapped, so the new
DLL is `hfw_probe_new.dll` beside it and the loader prefers that name
when the file exists.

### Who writes `[0x148982CB0]` (2026-10-21, Ghidra `-noanalysis`)

Offline only. No inject, no launch, nothing written under `M:\`, no
re-analysis, no symbol-table scan. A Python scan of `.text` in the
workspace exe (SHA-256 unchanged, image base `0x140000000`) found every
RIP-relative `disp32` whose target is VA `0x148982CB0`, then checked
that no absolute `imm64` of that VA exists anywhere in the file (0
hits). Ghidra was then opened `-noanalysis -readOnly -process` three
times (`local/ghidra_scripts/SceneQword.java` and the two follow-ups,
reports `local/scene_qword.txt`, `local/scene_qword2.txt`,
`local/scene_qword3.txt`) and asked only about the sites the scan
named.

The load the constructor uses is the one already quoted, and Ghidra
prints the same instruction:

```
RVA 0xD733C2  48 8B 3D E7 F8 C0 07  MOV RDI, qword ptr [0x148982CB0]
```

`0xD733C2 + 7 + 0x07C0F8E7 = 0x8982CB0`. The next two calls are not
virtuals of `RDI`. `CALL [0x14174B1C8]` at `0xD733D3` is the IAT slot
`PTR_AcquireSRWLockExclusive`, and `CALL [0x14174B1C0]` at `0xD733F4`
is `PTR_ReleaseSRWLockExclusive`. `RCX` for the acquire is
`LEA RBX, [RDI+0x20]` from `0xD733C9`. `FUN_1400fe310` at `0xD733E7`
takes `LEA RCX, [RDI+0x30]` and `RDX = R14`, and its first read is
`MOVSXD RBP, dword ptr [RCX]`.

The qword is its own BSS slot, not a field of the `PlayerManager`.
`PlayerManagerGame*` is the neighbouring global at RVA `0x8982C80`,
`0x30` bytes earlier. The same `.text` scan against that slot returns
303 sites, and the only store among them is `MOV [0x148982C80], RAX`
at `0xD5E68B` (`48 89 05 EE 45 C2 07`). Nothing in that set is a store
through a base register plus `0x30`. Both addresses sit past the raw
size of `.data` (VA `0x1CA1000`, raw `0x626800`), so both are
zero-filled BSS, not a field of a file-backed object.

Of the 18 RIP-relative sites that name `0x148982CB0`, two store and
sixteen only load or compare. The stores:

```
FUN_140d5e2c0  RVA 0xD5FA19  48 89 1D 90 32 C2 07  MOV [0x148982CB0], RBX
FUN_140d60010  RVA 0xD60547  4C 89 2D 62 27 C2 07  MOV [0x148982CB0], R13
```

`FUN_140d5e2c0` (`0xD5E2C0`, 6903 bytes) is `GameModule::vftable+0x20`
(vtable RVA `0x18FB8B0`, `.?AVGameModule@@`, COL `0x1A838A0`). It is
the same function that publishes `PlayerManagerGame*` at `0xD5E68B`
and `GameModule*` at `0xD5E768`. `RBX` at the store is the block
allocated eight instructions earlier:

```
RVA 0xD5F9F1  48 63 C8              MOVSXD RCX, EAX
RVA 0xD5F9F4  E8 07 67 48 FF        CALL 0x1401e6100
RVA 0xD5F9F9  48 8B D8              MOV RBX, RAX
RVA 0xD5F9FC  33 D2                 XOR EDX, EDX
RVA 0xD5FA04  48 8B CB              MOV RCX, RBX
RVA 0xD5FA07  E8 B4 F7 9C 00        CALL 0x14172f1c0        ; memset 0, size 0x250
RVA 0xD5FA0C  48 8B CB              MOV RCX, RBX
RVA 0xD5FA0F  E8 AC 87 BF FF        CALL 0x1409581c0
RVA 0xD5FA14  B9 18 00 00 00        MOV ECX, 0x18
RVA 0xD5FA19  48 89 1D 90 32 C2 07  MOV [0x148982CB0], RBX
```

`RBX` is not written between `MOV RBX, RAX` and the store. The size
comes from the static record at RVA `0x20FAC30`: the file dword there
is `0xFFFFFFFF` and the next byte is `0x04`, which is the
`byte == 4` test at `0xD5F9C9`, and `FUN_1402032d0` returns the size
that `MOVSXD` puts in `RCX`. The memset length is the immediate
`0x250` at `0xD5FA01`. `FUN_1409581c0` (186 bytes) then writes the
vtable:

```
RVA 0x9581CC  LEA RAX, [0x1418AC588]
RVA 0x9581E2  MOV [RCX], RAX
RVA 0x9581E5  ADD RCX, 0x20
RVA 0x9581E9  CALL [0x14174B5F8]     ; InitializeSRWLock(this+0x20)
RVA 0x9581EF  LEA RCX, [RBX+0x28]
RVA 0x9581F3  CALL [0x14174B5F8]     ; InitializeSRWLock(this+0x28)
RVA 0x9581F9  MOV [RBX+0x30], RDI    ; RDI is 0
```

The COL before `0x1418AC588` is VA `0x141A259B8` (signature `1`), and
its type descriptor is `.?AVCameraPredictionManager@@`. The second
pointer, stored at `+0x70` from `LEA RAX, [0x141833F88]`, is
`.?AVRenderDataCollector@@` (COL `0x1419E4DF8`). So `+0x20` is an
`SRWLOCK` that this constructor initializes, not a function pointer,
and `+0x30` is a count-and-pointer record that `FUN_1409581c0` zeroes
and `FUN_1400fe310` appends to.

`FUN_140d60010` (`0xD60010`, 5873 bytes) is the next slot,
`GameModule::vftable+0x28`. It loads the qword, zeroes it, then calls
the virtual destructor:

```
RVA 0xD60538  48 8B 0D 71 27 C2 07  MOV RCX, [0x148982CB0]
RVA 0xD6053F  45 33 ED              XOR R13D, R13D
RVA 0xD60542  48 85 C9              TEST RCX, RCX
RVA 0xD60545  74 11                 JZ 0x140d60558
RVA 0xD60547  4C 89 2D 62 27 C2 07  MOV [0x148982CB0], R13
RVA 0xD60552  48 8B 01              MOV RAX, [RCX]
RVA 0xD60555  FF 50 08              CALL [RAX+0x8]
```

`R13` is zero at the store. The same function then clears
`0x148982FD8` and, further down, reads `SceneManagerGame*` at
`0x148982DD0`. Both functions have only data references (vtable slots
`0x18FB8D0` and `0x18FB8D8`), no direct `CALL`.

Decision: **(a)**. The only non-null store is `0xD5FA19`, and it runs
inside `GameModule::vftable+0x20`, the same startup function that
publishes `GameModule*` and `PlayerManagerGame*`. The only clear is
`0xD60547`, inside `GameModule::vftable+0x28`. Nothing else writes the
qword, so a loaded game still holds the pointer from that startup
store unless that second virtual has already run. Whether the pointer
is non-null after a save load was not read from a process this
session; the static evidence says the startup store fills it and only
the shutdown virtual clears it.

The constructor is still not called from `--read-cam`. That flag is
the read that was missing, and the owner ran it. `--add-player` is
unblocked below. `--spawn-once` and `--read-slot` are unchanged.

### `--read-cam` live (2026-10-21, owner's log)

After a save load, 20 samples at 2 Hz, `faults=0`. The pointer did
not move:

```
read-cam start hz=2 samples=20 gate=GameModule@0x8983150 qword=0x8982CB0 read-only no-call
cam ptr=0x235749C3E80 lock20=0x0
read-cam stop samples=20 faults=0
```

`lock20=0x0` is not a missing store. `FUN_1409581c0` initializes the
`SRWLOCK` at `this+0x20` with `InitializeSRWLock`, and an
`InitializeSRWLock` lock stays 0 until a thread acquires it. Zero
means unlocked. The `CameraPredictionManager` at `0x235749C3E80` is
alive. The startup store at `0xD5FA19` survived the save load.

### `--add-player` unblocked (2026-10-21, Ghidra `-noanalysis`)

Offline only, after the live read above. One Ghidra
`-noanalysis -readOnly -process` pass
(`local/ghidra_scripts/CtorGlobals.java`, `CtorFull.java`,
`CtorRest.java`; reports `local/ctor_globals.txt`,
`local/ctor_full.txt`, `local/ctor_rest.txt`). No re-analysis, no
symbol scan, no inject, no launch, nothing written under `M:\`.

`FUN_140d73270` from the lock acquire to the return loads one global:

```
RVA 0xD733C2 MOV RDI, [0x148982CB0]
RVA 0xD733C9 LEA RBX, [RDI+0x20]
RVA 0xD733D3 CALL [0x14174B1C8] ; PTR_AcquireSRWLockExclusive
RVA 0xD733D9 TEST RSI, RSI
RVA 0xD733E0 CMOVZ R14, RBP
RVA 0xD733E7 CALL 0x1400fe310
RVA 0xD733F4 CALL [0x14174B1C0] ; PTR_ReleaseSRWLockExclusive
RVA 0xD7340F RET
```

`RSI` there is the `PlayerGame` block (`MOV RSI, RCX` at `0xD73282`),
which the probe allocated. `RBP` is zero (`XOR EBP, EBP` at
`0xD7328A`). `R14` is `LEA R14, [RSI+0x130]` from `0xD7329A`, the
sub-object appended into the camera manager's list at `+0x30`. The
`TEST RSI` / `CMOVZ` swaps in the zero only if the block itself is
null. `FUN_1400fe310` then reads `[RCX]` and `[RCX+8]`, and `RCX` is
`RDI+0x30` on the manager the live log just proved non-null. It
grows that list through `FUN_1401e6260`, which is the same TLS
allocator family as `FUN_1401e6100` and has null tests on both
arguments.

The rest of the constructor, before the lock, was re-read in the same
pass. Its globals:

| Site | What it loads | Null test |
|---|---|---|
| `0xD732F7` | `YMM` constant at `0x141FDFFD0` (`.rdata`) | none needed, static |
| `0xD73307` | dword at `0x141FDFFF0` (`.rdata`) | none needed, static |
| `0xD7339C` | `[0x148982D88]` | `TEST RCX` / `JZ 0xD733C2` |
| `0xD733C2` | `[0x148982CB0]` | none in the function; live and non-null |

`FUN_140913be0`, called at `0xD73285` with the block and the entity,
loads one global: `[0x148982FE8]` at `0x913CCA`, behind
`TEST RAX` / `JZ 0x913CE5`. The only thing it stores from the entity
argument is `[this+0x30]` at `0x913CB7`. Its other call is
`InitializeSRWLock` through the IAT.

So the only unchecked pointer load in the constructor was
`[0x148982CB0]`, and the owner's log shows it live. The one-shot
still waits until that qword, the entity and the player count are
non-null before it calls anything, because a second launch can be
back at the menu.

The insert is not `FUN_140d8c2d0`. That function calls
`[PlayerGame+0x68]` at `0xD8C313` and `[PlayerGame+0x60]` at
`0xD8C33D` before the push, and those slots are not proven. The push
itself, re-read from `FUN_1402250c0`:

```
RVA 0x2250D0 MOV EDI, [RCX] ; count
RVA 0x2250D2 MOV R14, RDX ; &slot
RVA 0x2250D5 MOV R8, [RCX+0x8] ; storage
RVA 0x225142 MOV RAX, [R14]
RVA 0x22514A MOV [R8+RCX*8], RAX
RVA 0x22515A MOV [RBX], ECX ; new count
```

`RCX` is the list header (`pm+8`) and `RDX` is the address of a
local holding the `PlayerGame*`. The game's site does the same with
`MOV [RSP+0x78], RBX` at `0xD8C385` and `LEA RDX, [RSP+0x78]` at
`0xD8C38E`. The probe does that itself and calls `FUN_1402250c0`
directly. It does not call `FUN_140d8c2d0`, so the `LOCK INC
[RBX+0x18]` at `0xD8C38A` is not emitted either.

`AcquireSRWLockExclusive` inside the constructor blocks while the
game holds the lock at `[cam+0x20]`. That is a hitch, not an access
violation. The probe does not replace the game's call. One shot only.

After a save load, from `probe\bin\Release\net10.0` (the shell
returns immediately; the log is `M:\Horizon Forbidden West\hfw-probe.log`;
if that file is missing, read `hfw-probe-run.txt` beside the exe):

```
Start-Process .\hfw-probe.exe -ArgumentList '--exe','<game dir>\HorizonForbiddenWest.exe','--add-player'
```

Load a save first. A hitch is possible while the constructor holds
the camera lock; alt-f4 if the game wedges. This does not write the
player's position. A success line is `add-player stop count=2 seh=0x00000000`.

Selftest is 29/29 on the offline exe. The two new anchors are the
prologues the one-shot calls: `FUN_1401e6100` at `0x1E6100`
(`48 89 5C 24 08 57 48 83 EC 20 65 48 8B 04 25 58`) and
`FUN_1402250c0` at `0x2250C0`
(`48 89 5C 24 18 48 89 7C 24 20 41 56`).

### `--add-player` crashed after the insert (2026-10-21, owner's log)

The owner ran the unblocked one-shot, tabbed back to the game, and
the game crashed. SEH on the probe thread saw nothing: alloc, ctor
and the list push all returned, and the count store landed. Copied
read-only from the game directory to `local/hfw-probe-addplayer.log`:

```
add-player start one-shot gate=GameModule@0x8983150 plus50 entity cam count>=1
add-player gate module=0x21C7CE32A00 plus50=0x2180BBB94D0 entity=0x21811717280 cam=0x21CD7933700 pm=0x21CF0884520 count=1 waited_s=0 faults=0
add-player alloc block=0x21D42380000 seh=0x00000000
add-player ctor built=0x21D42380000 seh=0x00000000
add-player stop count=2 seh=0x00000000 block=0x21D42380000
```

`probe\bin\Release\net10.0\hfw-probe-run.txt` was not present. The
inserted record is the `PlayerGame` at `0x21D42380000`. `FUN_140913be0`
stores the entity argument at `[this+0x30]` (`MOV [RDI+0x30], RBX` at
RVA `0x913CB7`), and the argument was the live entity
`0x21811717280`, so two `PlayerGame` records point at the owner's
entity. `[PlayerGame+0xE8]` is still zero: `FUN_1413d8470` is what
writes the `NetPlayerGame` there, and it was not called.

Offline only after that log. One Ghidra `-noanalysis -readOnly
-process` pass (`local/ghidra_scripts/ListWalk.java`,
`CamInsert.java`, `PgVtable.java`, `PgSlots.java`; reports
`local/list_walk.txt`, `local/cam_insert.txt`, `local/pg_vtable.txt`,
`local/pg_slots.txt`). No re-analysis, no symbol scan, no inject, no
launch, nothing written under `M:\`.

Who walks the list the push grew. `FUN_1402250c0` itself only copies
the pointer (`MOV [R8+RCX*8], RAX` at `0x22514A`) and stores the count
(`MOV [RBX], ECX` at `0x22515A`). It does not read the record. The
readers of `pm+8` / `pm+0x10`:

`FUN_140916a80` (RVA `0x916A80`, the lookup `FUN_140916bf0` calls) at
RVA `0x916AC8`:

```
MOV RAX, [R8+0xE8]
TEST RAX, RAX
JZ 0x140916ade
```

`FUN_140916c20` (RVA `0x916C20`, called at the bottom of the real
insert `FUN_140d8c2d0`) at RVA `0x916C43`:

```
MOV RCX, [RAX+0xE8]
TEST RCX, RCX
JZ 0x140916c55
```

Both loads of `[PlayerGame+0xE8]` are behind a null test, and the
null arm only reads the byte at `[PlayerGame+0x58]`, which
`FUN_140913be0` zeroes at `0x913C42`. So a missing `NetPlayerGame`
does not fault these two. The same `TEST` / `JZ` shape is what the
`PlayerGame` virtuals do: `vftable+0x38` is `FUN_140d75980`, and
`MOV RAX, [RSI+0xE8]` at `0xD75A05` is followed by `TEST RAX, RAX` /
`JZ` at `0xD75A0C`. `+0x40` (`FUN_140d75c40`, `0xD75C8F`) and `+0x50`
(`FUN_140d753e0`, `0xD75418`) are the same. No field already in hand
fills `+0xE8`; the only writer is `FUN_1413d8470`, which is still not
called.

The step that does not have a null arm is the constructor. Re-read
from `FUN_140d73270`:

```
RVA 0xD73285 CALL 0x140913be0 ; stores RDX at [this+0x30]
RVA 0xD7329A LEA R14, [RSI+0x130]
RVA 0xD733C2 MOV RDI, [0x148982CB0]
RVA 0xD733DC LEA RCX, [RDI+0x30]
RVA 0xD733E0 CMOVZ R14, RBP ; RBP is 0, only if the block is null
RVA 0xD733E4 MOV RDX, R14
RVA 0xD733E7 CALL 0x1400fe310
```

`FUN_1400fe310` is the same vector push family as `FUN_1402250c0`.
`RCX` is the camera manager's list at `+0x30` and `RDX` is the new
`PlayerGame+0x130`. The `CMOVZ` only substitutes null when the block
itself is null, which it was not (`built=0x21D42380000`). So the ctor
returned with the owner's entity at `[PlayerGame+0x30]` and a second
entry on the camera manager's list. That list is walked on the game's
own thread, which is why the probe's SEH stayed at `0x00000000` and
the crash came after the log line, when the owner tabbed back.

Decision: **(b)**, and stricter than the list insert. The shared
entity is published by the constructor, not by `FUN_1402250c0`, so
stopping before the insert would leave the camera-list entry in
place. `--add-player` now calls `FUN_1401e6100(0x210)` once, logs the
block, and returns. It does not call `FUN_140d73270` and it does not
call `FUN_1402250c0`. The block is a leak the game does not know
about, so neither walk can see it. The count stays 1. No second
character is expected. The list insert stays off until the
`NetPlayerGame` link at `[PlayerGame+0xE8]` exists, and the
constructor stays off until the entity at `[PlayerGame+0x30]` is not
the owner's.

Start a fresh game session first. The one that crashed is dead. Load
a save, then from `probe\bin\Release\net10.0`:

```
Start-Process .\hfw-probe.exe -ArgumentList '--exe','<game dir>\HorizonForbiddenWest.exe','--add-player'
```

The line to look for is `add-player stop count=1 seh=0x00000000 block=0x`
followed by `(ctor and insert not called`. Do not expect `count=2`.

Selftest is 32/32 on the offline exe, including the two walker loads
(`49 8B 80 E8 00 00 00` at `0x916AC8`, `48 8B 88 E8 00 00 00` at
`0x916C43`) and the constructor's camera-list publish
(`48 8D 4F 30 4C 0F 44 F5 49 8B D6 E8 24 AF 38 FF` at `0xD733DC`).

### A second entity, not the owner's (2026-10-21, Ghidra `-noanalysis`)

Offline only. HEAD `9c3c94c` verified. Probe code was not rebuilt and
was not changed. `--add-player` still stops after `FUN_1401e6100(0x210)`.
No inject, no launch, nothing written under `M:\`. One
`-noanalysis -readOnly -process` pass per script
(`local/ghidra_scripts/EntityAlloc.java` through `EntityAlloc13.java`,
reports `local/entity_alloc.txt` through `local/entity_alloc13.txt`).
No re-analysis, no symbol-table scan. The `.rdata`/`.data` qword search
in `EntityAlloc8.java` looked for three known ctor addresses only.

**(a).** There is an allocator that constructs an entity of the same
kind as `[[GameModule+0x50]+0x20]` and does not receive the owner's
entity. It was not called.

What the camera publish actually is. Re-read, not re-argued.
`FUN_1400fe310` (`0xFE310`, 133 bytes) is a pointer-vector push:
`[RCX]` is the count, `[RCX+4]` the capacity, `[RCX+8]` the array, and
the element store is `MOV [RCX+RBP*8], R14` at `0xFE385`. The call at
`0xD733E7` passes `RCX = [0x148982CB0]+0x30` and `RDX = PlayerGame+0x130`
(`LEA R14, [RSI+0x130]` at `0xD7329A`). The element is the `PlayerGame`
sub-object, not an entity. 397 call sites; the other bases are other
objects' vectors (`FUN_14043b260` eight times, `FUN_140434550`,
`FUN_140d5e2c0` at `0xD5EA7D` pushing onto `[0x148982C40]+0xB8`). None
of them allocate an entity.

The owner's entity is not allocated on the path that hands it over.
`FUN_14134bf40` (`0x134BF40`) unwraps `[[[0x148983150]+0x490]+0x88]` and
passes the result to `FUN_140d63060`, which stores it at
`[GameModule+0x50]` (`MOV [RDI+0x50], RBX` at `0xD63090`). The entity is
one field further, `[that+0x20]`, loaded by `FUN_14134c950` at
`0x134C9FE` and by `FUN_1413d8470` at `0x13D84A4`. Both of those stay
uncalled. `FUN_140d63060` does not allocate that object.

The entity constructor is `FUN_140948a10` (`0x948A10`, 833 bytes). It
takes the object in `RCX`; it does not allocate. Its ten direct callers
are derived constructors, and each one also takes the object in `RCX`
and then overwrites the vtables `FUN_140948a10` installed:

| ctor | RTTI |
|---|---|
| `FUN_140998180` | `CameraEntity` |
| `FUN_140b28e60` / `FUN_140b2f400` | `SequenceEntity` / `Sequence` |
| `FUN_140ec6170` | `GravityPocket` |
| `FUN_1411a0980` | `ControlledEntity` |
| `FUN_1411a7df0` | `Humanoid` |
| `FUN_1412691f0` | `Explosion` |
| `FUN_1412730f0` | `BulletSystem` |
| `FUN_141284ae0` | `Throwable` |
| `FUN_14128caa0` | `InventoryWeapon` |

`ControlledEntity` is the one the spawn path's entity matches: the
secondary vtable `FUN_140948a10` writes at `+0x20` is replaced by
`FUN_1411a0980` with `0x1419588C0` (`.?AVControlledEntity@@`). A
`CameraEntity` or a `Sequence` is not this object. The placement thunk
at `0x9476A0` (`TEST RDX,RDX / MOV RCX,RDX / JMP FUN_140948a10`) has
data references only, no direct `CALL`.

The allocator. `ControlledEntity`'s Decima type record is at RVA
`0x21DA6C0`. Bytes there:

```
FF FF FF FF 04 00 01 00 00 00 FF 00 00 00 00 00
00 03 00 00 10 00 00 00 80 09 1A 41
```

Tag byte at `+0x4` is `0x04`. `FUN_1402032d0` case 4 returns the dword
at `+0x10`, which is `00 03 00 00` (`0x300`). The qword at `+0x18` is
`0x1411A0980`, `FUN_1411a0980`. The name at the record's string slot is
`ControlledEntity`.

`FUN_1401f82b0` (`0x1F82B0`, 1140 bytes) is the generic factory that
consumes a record of that shape. `R8` is the type record:

```
RVA 0x1F85D0 CALL 0x1402033d0   ; RAX = [record+0x18], the ctor
RVA 0x1F85D5 MOV RBX, RAX
RVA 0x1F85D8 CALL 0x1402032d0   ; EAX = size from the record
RVA 0x1F85DD MOVSXD RCX, EAX
RVA 0x1F85E0 CALL 0x1401e6100   ; the block
RVA 0x1F85E5 MOV RCX, [RBP-0x70]
RVA 0x1F85E9 MOV RDX, RAX
RVA 0x1F85EC MOV RSI, RAX
RVA 0x1F85EF CALL RBX           ; placement ctor
```

For this record that is `FUN_1401e6100(0x300)` followed by
`FUN_1411a0980(record, block)`, which calls `FUN_140948a10(block)`.
`RSI` is the new object. It is then wrapped and pushed by
`FUN_1401f96d0` (`0x1F96D0`, 201 bytes) onto the caller's vector at
`[container+0x50]`, 0x20-byte elements. That vector is the container
the caller passed in; it is not the camera manager and not
`[GameModule+0x50]`.

`FUN_1401f8730` (`0x1F8730`, 985 bytes) is the same sequence at
`0x1F8A3F`. Both are called from `FUN_1401f7bd0`, `FUN_1401f8d40` and
`FUN_1401f90a0`, which pass a type record taken from a property array,
not a fixed address.

What calling it would touch. Scanned the bodies of `FUN_1401f82b0`,
`FUN_1401f8730`, `FUN_1411a0980` and `FUN_140948a10`: no instruction
references `0x148983150`, `0x1489581F0`, `0x148982CB0`, `0x148982C80`
or `0x148983128`, and none calls `FUN_140d73270`, `FUN_14134c950`,
`FUN_1413d8470`, `FUN_14101bd70`, `FUN_1402250c0` or `FUN_1400fe310`.
The new block is not the owner's entity. The owner's entity stays
where it is, and nothing in this chain writes `entity+0xD8`.

Not this. `FUN_140d5e2c0` does allocate `0x490` bytes at `0xD5E308` and
zeroes `[RAX+0x20]`, but that object gets `StreamingStrategyManagerGame::vftable`
(`0x18FC188`) and is published to `0x148982A58`. It is not the entity.
The `+0x88` stores inside that function zero or copy a handle; they do
not construct one.

Next milestone, not built. A one-shot that calls
`FUN_1401f82b0` with the `ControlledEntity` record at RVA `0x21DA6C0`
and a container that is not the camera manager and not the player
list, then returns the new block. It stops before `FUN_140d73270`.
The record is data, not a guessed address: tag `0x04`, size `0x300`,
ctor `FUN_1411a0980`. Calling the factory was not done this session.

### `--alloc-entity` (2026-10-21, Ghidra `-noanalysis`)

Offline only. HEAD `00d2243` verified. No inject, no launch, nothing
written under `M:\`. Two `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/FactoryCall.java`,
`local/ghidra_scripts/FactoryInner.java`, reports
`local/factory_call.txt` and `local/factory_inner.txt`). No
re-analysis, no symbol-table scan.

The factory is not callable from the probe. `FUN_1401f82b0` takes two
arguments, and neither is the type record. The prologue reads the
parent first:

```
RVA 0x1F82CC MOV RAX, [RCX+0x58]
RVA 0x1F82D0 MOV R12, RDX
RVA 0x1F82D3 MOVSXD R8, dword ptr [RCX+0x50]
RVA 0x1F82E2 MOV R13, RCX
```

`RCX` is a container. The record the factory constructs comes off that
container's own stack, `[[RCX+0x58] + [RCX+0x50]*0x20 - 0x18]`, and
`RDX` is a second value from the same stack. The only real caller,
`FUN_1401f7bd0`, sets it up like this:

```
RVA 0x1F8012 LEA RBX, [RDI-0x20]
RVA 0x1F8048 MOV RDX, RSI
RVA 0x1F804B MOV RCX, RBX
RVA 0x1F804E CALL 0x1401f82b0
```

The other two sites (`FUN_1401f8d40` at `0x1F8E29`, `FUN_1401f90a0` at
`0x1F92FB`) do the same shape: `RCX` is the caller's own frame plus
`-0x20`, `RDX` is a value that frame already holds. A parent we cannot
name is not passed, so the factory is not called. There is no
`alloc-entity blocked` line, because the four inner steps below are
fully known and those are what runs.

What happens to the block if you return before `FUN_1401f96d0`. The
push is not optional inside the factory: after the placement call the
factory always calls `FUN_1401f96d0(param_1+0x50, &local_128)`, and
that function's first argument is the caller's vector
(`[RCX]` count, `[RCX+4]` capacity, `[RCX+8]` storage, 0x20-byte
elements). There is no path that returns the pointer without pushing
it. So the factory is not called with a vector of ours either.

The four inner steps, done by hand instead. Each takes the record in
`RCX` and nothing else:

```
FUN_1402033d0 tag 4 -> [record+0x18] the constructor
FUN_1402032d0 tag 4 -> [record+0x10] the size, 0x300
FUN_1401e6100(size) -> the block
FUN_1411a0980(RCX=record, RDX=block)
```

`FUN_1411a0980` ignores its first argument, moves `RDX` into `RCX` and
calls `FUN_140948a10`, then writes `ControlledEntity::vftable` at `+0`,
`+0x20` and `+0x30`. The factory's own fifth call, at `0x1F85FC`, is
not one of these four: it calls a function pointer `FUN_1401feb90`
returned into `[RBP-0x58]`, with `RCX = [RBP-0x68] + R14`, and `R14`
is the previous stack entry of the same parent. That one is not
called. `FUN_1401f96d0` is not called. The block is not inserted
anywhere, so the game has no pointer to it and never walks it.

`--alloc-entity` waits until `GameModule*` at `0x148983150` is
non-null, runs those four calls once inside SEH, logs the pointer and
its first qword, and returns. It does not pass the owner's entity. It
does not call `FUN_140d73270`, `FUN_14134c950`, `FUN_1413d8470`,
`FUN_14101bd70`, `FUN_1402250c0` or `FUN_1400fe310`, and it does not
write `entity+0xD8`. `--add-player`, `--read-slot`, `--read-cam` and
`--spawn-once` are unchanged.

Start a fresh session and load a save first. This does not spawn a
visible character and does not touch the owner's player. A hitch is
unlikely, but alt-f4 if the game wedges. From
`probe\bin\Release\net10.0`:

```
Start-Process .\hfw-probe.exe -ArgumentList '--exe','<game dir>\HorizonForbiddenWest.exe','--alloc-entity'
```

The line to look for is `alloc-entity stop ptr=0x` with a non-zero
pointer, then `vtable=0x` and `size=0x300`. The pointer is the whole
result. Do not expect a character on screen.

Selftest is 40/40 on the offline exe, including the factory prologue
(`48 89 5C 24 10 55 56 57 41 54 41 55 41 56 41 57` at `0x1F82B0`), the
record's size dword (`FF FF FF FF 04 .. 00 03 00 00` at `0x21DA6C0`,
file offset `0x21D9AC0`) and the constructor slot
(`80 09 1A 41 01 00 00 00` at `0x21DA6D8`).

### `--alloc-entity` live, vtable confirmed (2026-10-21)

The owner ran it. The game did not crash. Copied from the owner's log
(the game was still running):

```
alloc-entity start one-shot gate=GameModule@0x8983150 steps=ctor-from-record,size-from-record,alloc,place record=0x21DA6C0 factory-not-called
alloc-entity gate module=0x1D480132300 waited_s=0 faults=0
alloc-entity stop ptr=0x1CEAD250600 vtable=0x7FF62AFF8958 size=0x300 ctor=0x7FF62A840980 seh=0x00000000 (factory not called, not inserted)
```

`ctor=0x7FF62A840980` is `FUN_1411a0980` (RVA `0x11A0980`). The
preferred image base is `0x140000000`, but this process is rebased.
The runtime image base is `0x7FF62A840980 - 0x11A0980 =
0x7FF6296A0000`. The vtable the constructor wrote is
`0x7FF62AFF8958 - 0x7FF6296A0000 = 0x1958958`.

That matches. One `-noanalysis -readOnly -process` pass
(`local/ghidra_scripts/CamListWalk.java`, report
`local/cam_list_walk.txt`) quotes the symbol Ghidra already has:

```
VA 0x141958958 RVA 0x1958958 primary +0 written by FUN_1411a0980 symbol=ControlledEntity::vftable
```

`FUN_1411a0980` writes it with `LEA RAX, [0x141958958]` at RVA
`0x11A0995` and `MOV [RBX], RAX` at `0x11A099C`
(`local/factory_inner.txt`). The two sub-vtables at `+0x20` and
`+0x30` are the same class (`0x1419588C0`, `0x141958B08`). The live
object is a `ControlledEntity`. `size=0x300` and `seh=0x00000000`.
The four steps return a live object and do not crash the game.

### The camera-list walker (`FUN_140930e90`)

The previous crash published the owner's entity. The question this
time is a fresh `ControlledEntity`. Two more read-only passes
(`CamWalkBody.java`, `CamSlot.java`, `CamSlot2.java`; reports
`local/cam_walk_body.txt`, `local/cam_slot.txt`, `local/cam_slot2.txt`).
No re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`.

`FUN_1400fe310` at `0xD733E7` stores `PlayerGame+0x130` into the array
at `[CameraPredictionManager+0x38]` and the count at `+0x30`. The
function that walks that array on the next tick is `FUN_140930e90`
(`0x930E90`):

```
RVA 0x930FEE MOV RDI, [R13+0x38] ; the array
RVA 0x930FF8 MOVSXD RAX, dword ptr [R13+0x30] ; the count
RVA 0x931026 MOV RDX, [RDI] ; one entry, PlayerGame+0x130
RVA 0x931056 MOV RAX, [RDX] ; its vtable
RVA 0x931073 CALL [RAX+8] ; no null test
```

`FUN_140d73270` writes that entry's vtable itself, before the publish:
`LEA RAX, [0x1418FBEF0]` / `MOV [R14], RAX` at `0xD732BD` / `0xD732C4`.
Slot `+8` of `PlayerGame::vftable` is `FUN_140d794f0`. Its first act
is `LEA RBX, [RCX-0x130]` (back to the `PlayerGame`) and
`CALL FUN_1409150e0`. That function's first field load is the count:

```
RVA 0x91510A MOV EDX, [RBX+0xF0]
RVA 0x915110 SUB EDX, 1
RVA 0x915113 JS 0x140915152 ; count was 0: return 0
```

The pointer at `[PlayerGame+0xF8]` is only loaded at `0x915115`, which
this jump skips. `FUN_140d794f0` then tests the result
(`TEST RAX, RAX` / `JZ` at `0xD7950F`) and returns. `FUN_140913be0`,
which `FUN_140d73270` calls at `0xD73285` before the publish, writes
both of those: `MOV [RDI+0xF0], RSI` at `0x913C9C` and
`MOV [RDI+0xF8], RSI` at `0x913CA3`, with `RSI` zeroed at `0x913BEF`.
The same function `InitializeSRWLock`s `[PlayerGame+0x100]` at
`0x913CAA`, and that is the lock `FUN_1409150e0` acquires first
(`LEA RDI, [RCX+0x100]` at `0x9150EA`).

So the next tick reads the vtable the constructor wrote, the count
the constructor zeroed, and the lock the constructor initialized. It
does not follow `[PlayerGame+0xF8]`. It also does not read the
`ControlledEntity`: `FUN_140d794f0` returns before
`FUN_140914f80`'s result is used as a pointer, and `FUN_140914f80`
returns 0 for the same zero count. The entity argument only lands at
`[PlayerGame+0x30]` (`0x913CB7`), which this walk does not load.

`--link-puppet` is the one-shot that does this. It waits until
`GameModule*` and `[0x148982CB0]` are both non-null, runs the four
steps, allocates `0x210`, calls `FUN_140d73270(playerBlock, newEntity)`
once, logs both pointers and the SEH code, and returns. It does not
call the factory, does not pass the owner's entity, does not call
`FUN_1402250c0`, and does not write `entity+0xD8`. `--add-player`
stays alloc-only.

Start a fresh session and load a save first. A hitch is possible
while the constructor holds the camera lock; alt-f4 if the game
wedges. The owner's character should not move. A visible body is not
expected yet. From `probe\bin\Release\net10.0`:

```
Start-Process .\hfw-probe.exe -ArgumentList '--exe','<game dir>\HorizonForbiddenWest.exe','--link-puppet'
```

The line to look for is `link-puppet stop player=0x` with a non-zero
`entity=0x`, `size=0x300` and `seh=0x00000000`.

### `--link-puppet` live, no crash (2026-10-21)

The owner ran it. The game stayed playable. Copied from the owner's
log:

```
link-puppet start one-shot gate=GameModule@0x8983150 cam=0x8982CB0 steps=ctor-from-record,size-from-record,alloc,place,alloc-player,link record=0x21DA6C0 factory-not-called owner-not-passed
link-puppet gate module=0x1E1FC772300 cam=0x1E6D6973200 waited_s=0 faults=0
link-puppet stop player=0x1E7CBF33200 entity=0x1E7BB2E1800 vtable=0x7FF62AFF8958 size=0x300 ctor=0x7FF62A840980 seh=0x00000000 (owner not passed, FUN_1402250c0 not called)
```

`ctor=0x7FF62A840980` is `FUN_1411a0980` (RVA `0x11A0980`), so this
process's image base is `0x7FF62A840980 - 0x11A0980 = 0x7FF6296A0000`,
the same rebase as the `--alloc-entity` run. The vtable
`0x7FF62AFF8958 - 0x7FF6296A0000 = 0x1958958` is
`ControlledEntity::vftable`. `player=0x1E7CBF33200` is the new
`PlayerGame`; `entity=0x1E7BB2E1800` is the new `ControlledEntity`,
not the owner's. `seh=0x00000000`. Publishing this fresh entity onto
the camera list (the `LEA`/`CALL` at `0xD733E7` inside
`FUN_140d73270`) did not crash the game. The earlier crash was the
owner's entity specifically.

`FUN_1402250c0` was not called, so the `PlayerManager` list did not
grow. No visible body was expected and none appeared. The owner's
character staying put, with no second body on screen, is the result
this build was supposed to produce. Nothing to run.

### The registration step, and the parent it still needs

Offline only. Two `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/RegFifth.java`,
`local/ghidra_scripts/RegField.java`, `BaseRow.java`; reports
`local/reg_fifth.txt`, `local/reg_field.txt`, `local/base_row.txt`).
No re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. No new probe mode: the call below is not complete.

The fifth call is not a fixed function. At RVA `0x1F85FC`:

```
RVA 0x1F85F1 MOVSX RCX, word ptr [RBP-0x68]
RVA 0x1F85F6 MOV RDX, RSI ; the new ControlledEntity
RVA 0x1F85F9 ADD RCX, R14 ; R14 = previous stack entry of the parent
RVA 0x1F85FC CALL qword ptr [RBP-0x58]
```

`[RBP-0x58]` is written by the second `FUN_1401feb90` call, at
`0x1F858E`. That function walks the type record's property rows
(`[record+0x60]`, count at `[record+7]`, 0x38 bytes each) and, on a
name match, copies the row into the out-struct. The qword it copies
to offset `+0x20` of that struct is the function pointer, and the
word at `+0x8` is the field offset added to `R14`.

`ControlledEntity`'s own record (RVA `0x21DA6C0`) has no rows:
`[record+7]` is 0 and `[record+0x60]` is null. Its one base, at
`[record+0x58]` (VA `0x1421D9410`, type VA `0x1420FA8C0`, offset 0),
has twelve. The row whose function pointer is non-null and whose
name is the registration is `Parent`:

```
row +0x10 "Parent"
row +0x8 0x70000 ; field offset 0
row +0x18 0x140956290
row +0x20 0x140956240 ; the pointer copied to [RBP-0x58]
```

`0x140956240` is eight bytes and not a function of its own:

```
RVA 0x956240 MOV RDX, [RDX]
RVA 0x956243 XOR R8D, R8D
RVA 0x956246 JMP 0x14094afe0
```

So the call the factory makes is `FUN_14094afe0(RCX = R14 + 0,
RDX = [newEntity], R8 = 0)`. `R14` is the stack entry the factory
loaded at entry from the parent:

```
RVA 0x1F82EA MOV R14, [[RCX+0x58] + [RCX+0x50]*0x20 - 0x20]
```

The missing field is that one: the qword at
`[[parent+0x58] + [parent+0x50]*0x20 - 0x20]`, i.e. parent field
`+0x58` indexed by the count at parent `+0x50`. The parent itself is
still unnamed. `FUN_1401f7bd0` builds it as `LEA RBX, [RDI-0x20]`
before the call at `0x1F804E`, and `RDI` there is the caller's own
frame. No probe mode until that container is a named live object.

`FUN_1401f96d0` (`0x1F96D0`) is not this step. It appends one 0x20-byte
record — object pointer, type pointer, a refcounted string, a flag —
to the vector at `[parent+0x50]` (count), `[parent+0x54]` (capacity),
`[parent+0x58]` (storage). That is the factory's bookkeeping, the
same vector the fifth call reads back. It does not point the scene
at the entity.

What else has to point at the entity before a model appears: not the
vtable. Slot 0 of `ControlledEntity::vftable` is `FUN_141186f10`
(`0x1186F10`, 8 bytes), and it only returns `&DAT_1421da6c0`, the type
record. Its three references are the vtable itself, a second vtable,
and one call inside `FUN_1411a692c`. It loads nothing and is not
reached from the fifth call. The `Model` row of the same base record
(row 7, field offset `0xC8`) has both function pointers zero, so the
factory's fifth call never invokes a model spawn either. The model
pointer is data at `entity+0xC8`; nothing in this chain writes it.

No mode ships. `FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and
`FUN_14101bd70` stay uncalled, and `entity+0xD8` stays unwritten.
`--add-player` stays alloc-only. The next run happens only after the
parent container is named.

### The parent container is an `RTTIMarshallerJSON`, and it is not a global (2026-10-21)

Offline only. Six `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/ParentContainer.java`, `ParentSlot.java`,
`ParentVtable.java`, `ParentCallers.java`, `ParentSites.java`,
`ParentDispatch.java`, `ParentParse.java`; reports
`local/parent_container.txt`, `local/parent_slot.txt`,
`local/parent_vtable.txt`, `local/parent_callers.txt`,
`local/parent_sites.txt`, `local/parent_dispatch.txt`,
`local/parent_parse.txt`). No re-analysis, no symbol scan, no inject,
no launch, nothing written under `M:\`. No new probe mode.

`FUN_1401f7bd0` has no direct caller. Its two references are data:
the `.pdata` entry at RVA `0x8BA6AB0`, and one vtable slot. That
vtable is `JSONParsingHandler::vftable` at RVA `0x17B3738`, and
`FUN_1401f7bd0` is slot `+0x20` of it (the fourth function pointer,
at RVA `0x17B3758`). The Complete Object Locator one qword before the
vtable, at RVA `0x19D3D58`, says offset 32, type descriptor RVA
`0x2243688`, name `.?AVRTTIMarshallerJSON@@`. The hierarchy under it
is `RTTIMarshallerJSON` at offset 0, `RTTIMarshaller` at offset 0,
and `JSONParsingHandler` at offset 32.

So the container the factory is handed is the `RTTIMarshallerJSON`
object itself. `FUN_1401f7bd0` copies its argument with
`MOV RDI, RCX` at RVA `0x1F7C04`, and `RDI` there is the
`JSONParsingHandler` subobject at `marshaller+0x20`. The
`LEA RBX, [RDI-0x20]` at `0x1F802A` (and again at `0x1F7FB4`) walks
back to the marshaller, and `MOV RCX, RBX` at `0x1F804B` is what
`FUN_1401f82b0` receives. It is a JSON loader, not a scene, a world,
or a spawn list: the same class is what `FUN_1401f5840` builds when
the content type is `application/json` or `text/json`.

The record `FUN_14094afe0` needs is pushed by `FUN_1401f96d0`
(`0x1F96D0`). Called with the vector at `handler+0x30`
(`LEA RBX, [RDI+0x30]` at `0x1F7D31`, `MOV RCX, RBX` at `0x1F7F8F`),
which is `marshaller+0x50`. The store, with `RBX` that vector, `R8`
its storage and `RBP` the record:

```
RVA 0x1F975F C4 A1 78 11 04 01 VMOVUPS xmmword ptr [RCX + R8*1], XMM0
RVA 0x1F9769 4A 89 44 01 10 MOV qword ptr [RCX + R8*1 + 0x10], RAX
RVA 0x1F978C 89 0B MOV dword ptr [RBX], ECX
```

`XMM0` was loaded from `[RBP]` at `0x1F9756`, so the first qword of
the new record is the first qword of the record passed in `RDX`.
That is the qword the factory later reads back as `R14`. The call
inside `FUN_1401f7bd0` that pushes it before the factory runs is the
second one, at RVA `0x1F7F9C` (`E8 2F 17 00 00`).

The miss. Nothing keeps this object. `RTTIMarshallerJSON::vftable`
(RVA `0x17B3710`) is written by exactly two functions,
`FUN_1401f5b50` (`MOV [RCX], RAX` at `0x1F5B88`) and `FUN_1401f5c20`
(the destructor). Every call site builds the object in a local:

```
FUN_140434550 at 0x434CA8 LEA RCX, [RBP-0x80]
FUN_140436f20 at 0x436F96 LEA RCX, [RBP-0x80]
FUN_14021ae80 at 0x21AEAA LEA RCX, [RSP+0x60]
FUN_140436bd0 at 0x436C70 LEA RCX, [RBP-0x60]
FUN_140435790 at 0x435816 LEA RCX, [RSP+0x30]
```

Each of those calls `FUN_1401f5c20` on the same local before it
returns. The one heap allocation, `FUN_1401f5840` at `0x1F5914`,
allocates `0x98` bytes through the TLS allocator at
`GS:[0x58]+0x1A10` (fallback global `0x14897A388`) and returns the
pointer to its caller; it does not store it to a global either. There
is no container global to read after a save load, so that probe is
not the next step.

`FUN_1401f7bd0` is reached through the handler's own vtable, not by a
direct call. `FUN_1401f62a0` (`0x1F62A0`, 6395 bytes) is the parser
every one of those sites hands the marshaller to, and it calls
`[RBP+0x28]` at `0x1F6FB5` and `0x1F70BF` with the same shape as the
factory's fifth call. The next milestone, described only, is to name
that stack slot: which instruction in `FUN_1401f62a0` writes
`[RBP+0x28]`, and whether it is slot `+0x20` of
`JSONParsingHandler::vftable`. No call is written. `FUN_1402250c0`,
`FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70` stay uncalled,
`entity+0xD8` stays unwritten, `--add-player` stays alloc-only and
`--link-puppet` stays as shipped.

### `FUN_14094afe0` parents an entity; it does not register one (2026-10-21)

Offline only. Five `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/RegAfe0.java`, `SpawnHunt.java`,
`SpawnFrame.java`, `SpawnName.java`, `GlobalWriter.java`,
`WorldName.java`, `EntityInsert.java`; reports `local/reg_afe0.txt`,
`local/spawn_hunt.txt`, `local/spawn_frame.txt`,
`local/spawn_name.txt`, `local/global_writer.txt`,
`local/world_name.txt`, `local/entity_insert.txt`). No re-analysis,
no symbol scan, no inject, no launch, nothing written under `M:\`.
No new probe mode. The call below is not complete, so nothing was
emitted.

The function is 1185 bytes. Its three arguments are the child
entity, the new parent entity, and a flag, not a scene record:

```
RVA 0x94AFEA MOV R15, [RCX + 0x80] ; old parent, saved
RVA 0x94AFF1 MOV RBX, RCX ; child
RVA 0x94AFFB MOV EBP, R8D ; flag
RVA 0x94AFFE MOV RDI, RDX ; new parent
```

It takes the SRW lock at `0x148954BA8` (`0x94AFF4`). If
`[child+0x80]` already equals the new parent it returns 0 at
`0x94B039`. If the new parent is the child itself it prints
`"You shouldn't parent entity to itself!"` (`LEA RCX, [0x1418AC010]`
at `0x94B05F`) and writes `0xDEADCA7` to address 0 (`0x94B077`).

When the child already has a parent, it unlinks the child from that
parent's sibling list and clears the field:

```
RVA 0x94B0CC MOV [RCX + 0x90], RAX ; prev sibling skips the child
RVA 0x94B0D5 MOV [RDX + 0x88], RAX ; or the parent head does
RVA 0x94B0EF LEA RCX, [RDX + 0x220]
RVA 0x94B100 CALL 0x140ad5f40 ; remove from the old parent's +0x220
RVA 0x94B137 MOV [RBX + 0x80], RSI ; child+0x80 = 0
RVA 0x94B13E MOV [RBX + 0x90], RSI ; child+0x90 = 0
```

`RSI` is 0 (`XOR ESI, ESI` at `0x94B098`). When the new parent is
non-null it links the other way. The flag only changes the third
argument of the insert and one bit at `child+0x98`; the factory's
`0` takes the first arm:

```
RVA 0x94B169 TEST EBP, EBP
RVA 0x94B16B JZ 0x14094b195
RVA 0x94B195 LEA RCX, [RDI + 0x220] ; parent+0x220
RVA 0x94B19C XOR R9D, R9D
RVA 0x94B19F MOV R8, RBX ; the child
RVA 0x94B1A5 CALL 0x140ad5480
RVA 0x94B1B0 CALL 0x14092fce0
RVA 0x94B1B5 MOV [RBX + 0x80], RDI ; child+0x80 = parent
RVA 0x94B1C3 MOV [RBX + 0x90], RAX ; child+0x90 = old head
RVA 0x94B1CA MOV [RDI + 0x88], RBX ; parent+0x88 = child
```

`entity+0xC8` is only tested (`CMP [RBX+0xC8], RSI` at `0x94B145`
and `0x94B1D1`). It is never stored, so the model pointer is not
written here. After the lock is released, a non-zero old parent is
handed to `FUN_140ad7130` through `[EntityManagerGame*+0x1F00]`
(`MOV RCX, [0x148982C40]` at `0x94B252`, `MOV RCX, [RCX+0x1F00]` at
`0x94B25C`). Bit 27 of `[child+0x98]` posts `MsgChildEntityRemoved`
on the old parent and `MsgChildEntityAdded` on the new one. The
return is 1.

So the factory's fifth call sets the new entity's parent pointer and
splices it into that parent's child list. It does not insert the
entity into a world list and it does not load a model. Calling it
with a stack record as the parent parents the fresh entity to a
temporary. That is not a body.

Callers of `FUN_14094afe0` itself, every code ref, not only the
first 15:

| ref | RVA | what RCX is |
|---|---|---|
| `.pdata` | `0x8BFA7A4` | not code |
| data | `0x1BD00E8` | not a vtable; the qword is `0x94B0900094AFE0`, two packed RVAs |
| data | `0x1BD011C` | same packed-RVA word |
| call | `0x94B4AA` | `FUN_14094b490`, `MOV RBX, RCX` at `0x94B4A7`: the child entity, and `RDX` is its current `[child+0x80]` |
| thunk | `0x956246` | the eight-byte `Parent` row stub: `MOV RDX, [RDX]` / `XOR R8D, R8D` / `JMP 0x14094afe0`. Its one reference is the type-record row at `0x1E61C28` |

None of those is inside `FUN_1401f82b0` or `FUN_1401f7bd0`. The JSON
path reaches this function only through the `Parent` row thunk, which
has no code caller of its own. The one real call, `FUN_14094b490`,
re-parents the child onto the parent it already has and then posts
`MsgParentChanged` (`LEA RAX, [0x1418B1AA8]` at `0x94B4DE`). It does
not take a scene. Its own callers (`FUN_140902670` at `0x902978`,
`FUN_140948e40` at `0x948E7C` and `0x948E98`, `FUN_140955660` at
`0x9556BF`, `FUN_140961930` at `0x961949`, `FUN_140988ad0` at
`0x9894A5`, `FUN_140993c50` at `0x993C5C`) pass the child entity in
`RCX` and its current parent in `RDX`. `FUN_140948e40` is a
destructor: it zeroes `RDX` and `R8` before the call at `0x948E78`,
so that one unparents.

The global `FUN_14094afe0` reads, `0x148982C40` (RVA `0x8982C40`), is
`EntityManagerGame*`. The store is `MOV [0x148982C40], RBX` at
`0xD5E79A` inside `FUN_140d5e2c0`, and the vtable written onto that
object one instruction earlier is `LEA RAX, [0x1418FB560]` at
`0xD5E78B`. The Complete Object Locator at that vtable minus 8
(`0x141A85628`) names `.?AVEntityManagerGame@@`. A second store,
`MOV [0x148982C40], R13` at `0xD607BE` in `FUN_140d60010`, is the
matching clear. The first six vtable slots (`FUN_140900b10`,
`FUN_14004fae0`, `FUN_140d5b880`, `FUN_140d5b960`, `FUN_140d5b8f0`,
`FUN_140d5c110`) do not call `FUN_14094afe0`, `FUN_14094b490` or
`FUN_140902670`. The object is live, and this function only uses it
to notify a parent change.

Spawn strings, one `findBytes` pass, not a symbol walk. `SpawnEntity`,
`PlaceEntity`: zero hits. `CreateEntity` (RVA `0x18B01C0`) is
referenced from `FUN_140990b90`, `CreateEntityForSequenceVariable`
(`0x18E0DA0`) from `FUN_140b9fe80`, `CreateEntityFromSpawnSetup`
(`0x19A2DAB`) from `FUN_1414d3520`. None of those three functions
loads `GameModule` (`0x8983150`), `CameraPredictionManager`
(`0x8982CB0`), `PlayerManager` (`0x8982C80`), `SceneManagerGame`
(`0x8982DD0`) or `EntityManagerGame` (`0x8982C40`). They are script
export registrars. `EntitySpawn`, `EntitySpawnProgram`,
`EntitySpawnOverrideProgram` and `EntitySpawnSetup` have no code ref.
The `ControlledEntity` type record at `0x21DA6C0` is compared as a
word (`SUB AX, word ptr [0x1421DA6C0]`) by type-check functions; the
first twenty such refs are all that shape, and the pointer value
`0x1421DA6C0` itself is stored at exactly two data sites (`0x1EC45D0`,
`0x21DB6E0`), neither inside a function. No function takes that type
record and an `EntityManagerGame*` together.

Miss. There is no runtime register call whose arguments are a global
we already hold and the fresh entity. `FUN_140902220` is the nearest
shape (`MOV RDX, RSI` / `MOV RCX, RDI` / `CALL 0x140902670` at
`0x902556`), and `FUN_140902670` does load `EntityManagerGame*`
(`CMP [0x148982C40], 0` at `0x90297D`), but the call unparents every
child of the entity in `RDX`. It is a teardown, not an insert.
Decompiled or disassembled this pass: `FUN_14094afe0`,
`FUN_14094b490`, `FUN_140902670` (the window at `0x902920`),
`FUN_140902220` (entry and the call at `0x90255C`), `FUN_140900380`
(the `EntityManagerGame` constructor, first 40 instructions),
`FUN_140948e40`, and the six vtable slots above. The single next
decompile is `FUN_140ad5480`, the insert into `[parent+0x220]` at
`0x94B1A5`: name that list, and whether it is the scene's entity
array or only the parent's children. No call until that is named.

### `[parent+0x220]` is a per-parent node array (2026-10-21)

Offline only. Three `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/ListInsert.java`, `EmSlots.java`,
`EmSlots2.java`; reports `local/list_insert.txt`,
`local/em_slots.txt`, `local/em_slots2.txt`). No re-analysis, no
symbol scan, no inject, no launch, nothing written under `M:\`. No
new probe mode. No call frame is complete, so nothing was emitted.

`FUN_140ad5480` is 1711 bytes. The call at `0x94B1A5` fills four
arguments, and the factory's `0` flag takes the first arm:

```
RVA 0x94B195 LEA RCX, [RDI + 0x220] ; the container
RVA 0x94B19C XOR R9D, R9D ; flag, 0
RVA 0x94B19F MOV R8, RBX ; the child entity
RVA 0x94B1A2 MOV RDX, RDI ; the parent entity
RVA 0x94B1A5 CALL 0x140ad5480
```

Inside, `RCX` is saved at `0xAD548C`, `R9D` at `0xAD548F`, `R8` at
`0xAD5499`, `RDX` at `0xAD549C`. It takes the SRW lock at
`0x148956828` (`0xAD549F`) and returns immediately when the parent
and the child are the same pointer (`CMP R15, R14` / `JZ 0x140ad5b15`
at `0xAD54B6`).

The container is not the sibling list. The sibling list is two
qwords, `parent+0x88` the head and `child+0x90` the next child, both
written by `FUN_14094afe0` itself. `[parent+0x220]` is a counted
array of `0x38`-byte nodes, and the child entity is not one of those
nodes. The array header sits `0x18` bytes before the pointer the
caller passes:

```
RVA 0xAD54ED MOV RBX, [R13 + 0x20] ; data, so parent+0x240
RVA 0xAD54F1 LEA RBP, [R13 + 0x18] ; header, so parent+0x238
RVA 0xAD54F5 MOVSXD RAX, dword ptr [RBP] ; count
RVA 0xAD54FB IMUL RDX, RAX, 0x38 ; stride
```

Each node stores the entity as a pointer plus `0x20`, not the
entity. The scan compares `[node+0] - 0x20` with the child
(`LEA RAX, [RCX-0x20]` / `CMOVZ` / `CMP RAX, R14` at `0xAD5516`),
and a new node is built the same way (`LEA RAX, [R14+0x20]` at
`0xAD556E`, stored at `[RSP+0x20]`). The push copies that stack
record into the array:

```
RVA 0xAD5A2D LEA RDX, [RSP + 0x20] ; the stack node
RVA 0xAD5A32 MOV RCX, RBP ; parent+0x238
RVA 0xAD5A35 CALL 0x140add0b0
```

`FUN_140add0b0` (318 bytes) grows the array by `0x38` through
`FUN_1401e6100` (`IMUL RCX, RAX, 0x38` / `CALL 0x1401e6100` at
`0xADD10B`) and copies one record with `FUN_140ad45f0`. That copy
writes seven fields and nothing else (`0xAD4604` through
`0xAD4637`): `[node+0x00]` the entity-plus-`0x20` pointer, `[+0x08]`
and `[+0x10]` zero, `[+0x18]` the node's own address, `[+0x20]` and
`[+0x28]` qwords, `[+0x30]` and `[+0x34]` dwords. The matching
remove, `FUN_140ad5f40` at `0x94B100`, walks the same
`count * 0x38` array (`0xAD5F84`) and the same `pointer - 0x20`
test (`0xAD5FA6`). So `[parent+0x220]` is this parent's node array.
It is not a scene-wide entity list: the only entity it records is
the child, and only as a field of a wrapper allocated for this
parent.

`EntityManagerGame::vftable` is the qword at RVA `0x18FB560`. The
Complete Object Locator at vtable minus 8 is `0x141A85628`, the same
one already named `.?AVEntityManagerGame@@`. Slots 0 through 15,
read as qwords, not only the first six:

| slot | offset | RVA | what it is |
|---|---|---|---|
| 0 | `+0x00` | `0x900B10` | `FUN_140900b10`, 78 bytes, 3 calls |
| 1 | `+0x08` | `0x4FAE0` | `FUN_14004fae0`, 3 bytes |
| 2 | `+0x10` | `0xD5B880` | `FUN_140d5b880`, 106 bytes |
| 3 | `+0x18` | `0xD5B960` | `FUN_140d5b960`, 1504 bytes |
| 4 | `+0x20` | `0xD5B8F0` | `FUN_140d5b8f0`, 102 bytes |
| 5 | `+0x28` | `0xD5C110` | `FUN_140d5c110`, 16 bytes |
| 6 | `+0x30` | `0xD5BF50` | `FUN_140d5bf50`, 437 bytes |
| 7 | `+0x38` | `0x1A865F8` | not a function; the bytes there are not ASCII |
| 8 | `+0x40` | `0xD5C9F0` | `FUN_140d5c9f0`, 43 bytes |
| 9 | `+0x48` | `0x61890` | `FUN_140061890`, 6 bytes |
| 10 | `+0x50` | `0x503C0` | `FUN_1400503c0`, 3 bytes |
| 11 | `+0x58` | `0x503C0` | same 3-byte function |
| 12 | `+0x60` | `0x134B830` | `FUN_14134b830`, 550 bytes |
| 13 | `+0x68` | `0x134BAA0` | 19-byte thunk to `FUN_1413ce4e0` |
| 14 | `+0x70` | `0x134BAC0` | `FUN_14134bac0`, 245 bytes |
| 15 | `+0x78` | `0x42700` | `_guard_check_icall`, 3 bytes |

None of them inserts an entity. Slot `+0x18` (`FUN_140d5b960`)
returns at `0xD5B963` when `RDX` is null, then reads `[R8+0x20]` at
`0xD5B9B6` and calls `FUN_1408f9890` at `0xD5B9D4`. The `0x90` at
`0xD5B9E0` and the `LEA RCX, [R13+0xC8]` at `0xD5BEC0` are offsets on
a pointer that argument produced, not a store of an entity into a
list. Slot `+0x20` (`FUN_140d5b8f0`) forwards `RDX` and `R8` to
`FUN_140d70aa0` at `0xD5B946`; its `+0x88` and `+0x90` loads are
`[RCX+0x88]` at `0xD5B913` and `[RAX+0x90]` at `0xD5B92A` on other
objects. Slot `+0x30` (`FUN_140d5bf50`) walks the player list already
mapped, `[PlayerManager+0x10]` for `[PlayerManager+0x8]` entries
(`0xD5BFAD`, `0xD5BFB5`), and reads `[player+0xC8]` and `[player+0xE8]`
(`0xD5BFDD`, `0xD5BFF7`). It does not push. Slot `+0x60`
(`FUN_14134b830`) does call the allocator, but the size comes from a
script value (`CALL 0x1401e6100` at `0x134B934`) and the pointer is
stored to the global `0x148982AF8` (`0x134B952`), then
`FUN_140d61c00` is called on `GameModule*` (`0x134B9A4`). The fresh
entity is not an argument. Slot `+0x70` (`FUN_14134bac0`) is the
function already traced to `GameModuleStateLoading`: it calls
`FUN_14134c4e0` (`0x134BAC9`) and tails into `FUN_14134bbc0` at
`0x134BB29`. The `+0xD8` hit is a load, `MOV RCX, [RCX+0xD8]` at
`0x134BB11`, on the object at `[[0x1489581F0]+0xF8]`.

Miss. No slot of this vtable takes `EntityManagerGame*` and an entity
pointer and inserts that entity. The node array at `[parent+0x220]`
needs the parent entity in `RDX`, which we do not have without the
owner's entity, so that call is not fillable either. The single next
decompile is `FUN_140d5b960` (vtable slot `+0x18`): name `RDX` and
`R8` at `0xD5B960`, and say whether the pointer it builds at
`0xD5B9EB` is an entity it registers or a field of an argument it was
given. No call until those two registers are named.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

Selftest is 46/46 on the offline exe. The six new anchors are the
walker's call (`FF 50 08` at `0x931073`), the count load and its
`JS` (`8B 93 F0 00 00 00 83 EA 01 78` at `0x91510A`), the constructor's
zero store (`48 89 B7 F0 00 00 00` at `0x913C9C`), the link prologue
(`48 89 5C 24 10 48 89 6C` at `0xD73270`), and the two list loads
(`49 63 45 30` at `0x930FF8`, `49 8B 7D 38` at `0x930FEE`). No inject,
no launch.

### Slot `+0x18` is the damage dispatcher (2026-10-21)

Offline only. Three `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot18.java`, `Slot18Names.java`,
`Slot18Col.java`; reports `local/slot18.txt`,
`local/slot18_names.txt`, `local/slot18_col.txt`). No re-analysis, no
symbol scan, no inject, no launch, nothing written under `M:\`. No
new probe mode. No call frame is complete, so nothing was emitted.

`FUN_140d5b960` is 1504 bytes and takes three arguments. `RCX` is the
`EntityManagerGame*` (saved to `R15` at `0xD5B987`, then used as
`this+0x28` for the critical section). The other two are named by the
entry and by the RTTI of the object the function builds from them:

```
RVA 0xD5B960 TEST RDX,RDX
RVA 0xD5B963 JZ 0x140d5be41 ; return when the entity is null
RVA 0xD5B96C MOV qword ptr [RAX + 0x18],R8 ; spill the damage params
RVA 0xD5B999 MOV RDI,R8
RVA 0xD5B9A0 MOV RSI,RDX
```

`RDX` is the `Entity*` being damaged. `R8` is a `DamageParams const&`.
Both names come from the function itself, not from a caller. At
`0xD5BAE8` it stores the vtable whose Complete Object Locator
(`0x141A85C68`) names
`ParameterSet<EventTypeMarshal, Entity*, DamageParams const&, EntityManagerGame*, void (EntityManagerGame::*)(Entity*, DamageParams const&)>`,
and it fills that record with `RDX+0x20` (`LEA RCX, [RSI+0x20]` at
`0xD5BAEB`) and a copy of `R8`. Later it builds `MsgModifyDamage`
(`0xD5BCD4`, COL `0x141A85150`), `MsgModifyDamageDealt`
(`0xD5BBB6`), `MsgModifyDamageFlags` (`0xD5BC4B`) and, on the network
arm, `NetMsgDamage` (`0xD5BEED`, COL `0x141A46A58`).

`R8` is the record `FUN_1408f9890` copies. The call at `0xD5B9D4` is
`FUN_1408f9890(RBP-0x80, R8)`, and the callee (237 bytes) only copies
fields: two `VMOVUPS` at `0x8F989A` and `0x8F98A2`, then one field at
a time from `[RDX+0x48]` through `[RDX+0xB1]` into the destination.
It returns the destination. So `R8` is a `0xB2`-byte damage record,
not an entity.

The pointer at `0xD5B9EB` is a field of `R8`, and it is a load:

```
RVA 0xD5B9B6 MOV RAX,qword ptr [RDI + 0x20] ; [DamageParams+0x20]
RVA 0xD5B9C2 LEA RCX,[RAX + 0x18]
RVA 0xD5B9C6 CMOVZ RCX,RDX ; 0x38 when that field is null
RVA 0xD5B9CD MOV R13,qword ptr [RCX] ; the entity pointer inside it
RVA 0xD5B9DC LEA RAX,[R13 + 0x70]
RVA 0xD5B9E0 MOV ECX,0x90
RVA 0xD5B9E5 CMOVZ RAX,RCX
RVA 0xD5B9EB MOV RDI,qword ptr [RAX + 0x20]
```

Nothing is written there. When the loaded qword is non-null the next
instruction subtracts `0x20` (`ADD RDI, -0x20` at `0xD5B9F4`), the
same entity-from-node adjustment already named for `[parent+0x220]`.
The result is the other entity in the damage, and it is only read:
passed to `FUN_1409aa0a0(entity, other)` at `0xD5BA6D`, and used as
the base of `MsgModifyDamageDealt` at `[RDI+0x70]` (`0xD5BBB2`). The
`LEA RCX, [R13+0xC8]` at `0xD5BEC0` is the same kind of load, a byte
compared against `2` at `0xD5BED0`. No list grows.

There is no direct call. The reference manager lists five sites and
all five are data. One is the vtable slot itself, qword RVA `0xD5B960`
at `0x18FB578`, which is `EntityManagerGame::vftable + 0x18`. The
other four (`0x8C35DE0`, `0x1C0F0F4`, `0x1C0F150`, `0x1C0F170`) are
unwind rows: each qword is `0xD5B960` in the low half and a frame
offset in the high half, followed by the spill sites `0xD5B983` and
`0xD5B98A`. No `CALL` instruction targets the entry, so no caller
shows the argument types. The RTTI above is what names them.

Miss. Neither register is an entity this function inserts. `RDX` is
an entity the caller already has, and the function only dispatches
damage onto it. The single next decompile is `FUN_1409aa0a0`, called
at `0xD5BA6D` as `(RDX, the other entity)`: say whether that function
links the two entities or only compares them. No call until that is
named.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_1409aa0a0` only compares two entities (2026-10-21)

Offline only. Three `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/LinkOrCmp.java`, `Ad6140.java`,
`SceneXrefs.java`; reports `local/linkorcmp.txt`, `local/ad6140.txt`,
`local/scene_xrefs.txt`). No re-analysis, no symbol scan, no inject,
no launch, nothing written under `M:\`. No new probe mode. No call
frame is complete, so nothing was emitted.

`FUN_1409aa0a0` is 291 bytes and returns a byte. The call at
`0xD5BA6D` is `(RCX = RSI, RDX = RDI)`, the entity being damaged and
the other entity from the previous section. Inside, those two
registers are saved and then only read:

```
RVA 0x9AA0AA MOV RDI,RCX ; the first entity
RVA 0x9AA0AD MOV RBX,RDX ; the second entity
```

The first calls are the shared lock at `0x148956828`
(`TryAcquireSRWLockShared` at `0x9AA0B7`, `AcquireSRWLockShared` at
`0x9AA0C8`, `ReleaseSRWLockShared` at `0x9AA1AF`). The first field
use of either argument is the node array already named on a parent:

```
RVA 0x9AA0CE MOV RAX,qword ptr [0x148982c40] ; EntityManagerGame*
RVA 0x9AA0D5 CMP byte ptr [RAX + 0xb1],0x0
RVA 0x9AA0DC JNZ 0x1409aa17c
RVA 0x9AA0E2 CMP byte ptr [RAX + 0xb2],0x0
RVA 0x9AA0E9 JNZ 0x1409aa17c
RVA 0x9AA0EF MOV RCX,qword ptr [RDI + 0x240] ; data
RVA 0x9AA0F9 MOVSXD RAX,dword ptr [RDI + 0x238] ; count
RVA 0x9AA100 IMUL R8,RAX,0x38
```

`RCX` is the first entity. `RDX` is the second entity. The global is
only those two flag bytes; `GameModule*`, `SceneManagerGame*` and
`PlayerManager*` are not loaded.

It does not link them. No instruction writes `child+0x80`,
`parent+0x88`, or a new node. The walk compares each existing node
with the other entity and steps by `0x38`:

```
RVA 0x9AA110 MOV RDX,qword ptr [RCX] ; node+0, entity+0x20
RVA 0x9AA113 TEST RDX,RDX
RVA 0x9AA116 LEA RAX,[RDX + -0x20]
RVA 0x9AA11A CMOVZ RAX,R9
RVA 0x9AA11E CMP RAX,RBX ; the second entity
RVA 0x9AA121 JNZ 0x1409aa12e
RVA 0x9AA128 CMP dword ptr [RCX + 0x34],R9D
RVA 0x9AA12C JZ 0x1409aa1a6 ; hit: return 1
RVA 0x9AA12E ADD RCX,0x38
```

A miss walks the second entity's array the same way (`CMP RAX, RDI`
at `0x9AA15F`) and returns 0 (`XOR BL, BL` at `0x9AA178`). The other
arm, taken when `[EntityManagerGame+0xB1]` or `+0xB2` is set, calls
`FUN_140ad6140` (90 bytes) on `[entity+0x220]` and does not store
either:

```
RVA 0x9AA17C LEA RCX,[RDI + 0x220]
RVA 0x9AA183 MOV RDX,RBX
RVA 0x9AA186 CALL 0x140ad6140
```

`FUN_140ad6140` is the same compare through the header the caller
passes (`[container+0x20]` data, `[container+0x18]` count, stride
`0x38`). Its only write is the return byte: `XOR AL, AL` at
`0xAD6194`, or `MOV AL, 0x1` at `0xAD6197` when `CMP RDX, R10` at
`0xAD6174` hits and the node's dword at `+0x34` is 0 or the sentinel
at `0x141E89268`. `FUN_1409aa0a0` returns that byte with
`MOVZX EAX, BL` at `0x9AA1B5`. The damage callback tests it and
branches (`TEST AL, AL` / `JNZ 0x140d5bba6` at `0xD5BA72`); it does
not receive a pointer.

Thirty call sites in twenty-seven functions, plus one data xref at
`0x8BFF928`. Every call passes two pointers and tests `AL`. Besides
the damage site, the ones that show the relation pass the parent
already stored at `+0x80`, which this function only reads:

| call | function | what it passes |
|---|---|---|
| `0xA7C875` | `FUN_140a7c840` | `(entity, [entity+0x80])` |
| `0xAD8C3A`, `0xAD8C52` | `FUN_140ad8b90` | `(RDI, RBX)`, then `([RDI+0x80], RBX)` |
| `0xAD8CF1`, `0xAD8D09` | `FUN_140ad8c90` | `(RDI, [player+0x48])`, then `([RDI+0x80], same)` |
| `0x161D125`, `0x161D13D` | `FUN_14161d0a0` | the same parent walk |

The other twenty-three pass a pointer and a second pointer adjusted by
`-0x20` (the node-to-entity step), or the pair saved at
`[this+0x48]` and `[this+0x58]`. None of them is given a manager
global.

Miss. The compare never puts an entity anywhere. The single next
decompile is `FUN_140a8b1d0` (86 bytes). It loads `SceneManagerGame*`
from `0x148982DD0` and passes its second argument to the append
already named, `FUN_1400fe310`:

```
RVA 0xA8B1DF MOV RDI,qword ptr [0x148982dd0]
RVA 0xA8B1E6 MOV RSI,RDX
RVA 0xA8B1E9 LEA RBX,[RDI + 0x1c0]
RVA 0xA8B1F3 CALL qword ptr [0x14174b1c8] ; AcquireSRWLockExclusive
RVA 0xA8B1F9 LEA RCX,[RDI + 0x1b0]
RVA 0xA8B200 MOV RDX,RSI
RVA 0xA8B203 CALL 0x1400fe310
```

The question is whether `RDX` is an `Entity*`, and whether the counted
array at `SceneManagerGame+0x1b0` is the scene's entity list. No call
until that register is named.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_140a8b1d0` appends a job object, not an entity (2026-10-21)

Offline only. Three `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/SceneAppend.java`, `SceneAppendCallers.java`,
`SceneAppendWho.java`, plus `SceneDrain.java` for the consumer;
reports `local/scene_append.txt`, `local/scene_append_callers.txt`,
`local/scene_append_who.txt`, `local/scene_drain.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. No new probe mode. No call frame is complete, so nothing
was emitted.

`FUN_140a8b1d0` is 86 bytes and returns void. It does not read `RCX`
or `R8`. The only argument it keeps is `RDX`:

```
RVA 0xA8B1DF MOV RDI,qword ptr [0x148982dd0] ; SceneManagerGame*
RVA 0xA8B1E6 MOV RSI,RDX ; the only argument
RVA 0xA8B1E9 LEA RBX,[RDI + 0x1c0] ; exclusive SRWLOCK
RVA 0xA8B1F9 LEA RCX,[RDI + 0x1b0]
RVA 0xA8B200 MOV RDX,RSI
RVA 0xA8B203 CALL 0x1400fe310
```

`RCX` of the inner call is the counted array. `RDX` is the pointer
the caller passed. `R8` is not an argument.

`FUN_1400fe310` (133 bytes) appends that pointer. It is not a search.
It increments the count, grows the buffer by 8-byte slots when the
capacity at `+0x4` is exceeded, and writes the pointer at the old
index:

```
RVA 0xFE325 MOVSXD RBP,dword ptr [RCX] ; count
RVA 0xFE32B MOV R14,RDX ; the pointer
RVA 0xFE32E LEA ECX,[RBP + 0x1]
RVA 0xFE331 MOV dword ptr [RSI],ECX ; count = count + 1
RVA 0xFE333 CMP ECX,dword ptr [RSI + 0x4] ; capacity
RVA 0xFE35A LEA EAX,[RBX*0x8] ; grow, 8 bytes per slot
RVA 0xFE364 CALL 0x1401e6260
RVA 0xFE385 MOV qword ptr [RCX + RBP*0x8],R14 ; append
```

The array at `SceneManagerGame+0x1b0` is therefore a counted pointer
vector: count at `+0x1b0`, capacity at `+0x1b4`, data at `+0x1b8`,
stride 8, element a pointer. The lock sits at `+0x1c0`. The element
is not an `Entity*` and not a `0x38` node.

Both code callers pass their own `this`, saved from `RCX` into `RBP`
at the entry, and that object already owns a different counted array
of `0x18`-byte records at `+0x1d8`:

| call | function | what RDX is |
|---|---|---|
| `0xA9B596` | `FUN_140a9b480` (421 bytes) | `MOV RBP, RCX` at `0xA9B4B8`, then `MOV RDX, RBP` |
| `0xA9B46F` | `FUN_140a9b340` (314 bytes) | `MOV RBP, RCX` at `0xA9B34C`, then `MOV RDX, RBP` and a tail `JMP` |

`FUN_140a9b340` is the enqueue: it grows `[this+0x1e0]` by `0x18`
(`LEA RDX, [RAX+RAX*2]; SHL RDX, 3` at `0xA9B3D6`) and stores a
pointer plus 16 bytes of payload. `FUN_140a9b480` drains that queue
and only then calls `FUN_140a8b1d0`, and only after 25 passes
(`CMP R14D, 0x19` at `0xA9B57A`). The data xref at `0x8C0CE94` is a
packed pair of RVAs (`0xA8B1D0` in the low dword), not a call.

The consumer of the scene array is `FUN_140d6bce0` (397 bytes). It
takes the exclusive lock, steals the vector, and calls
`FUN_140a9b480` on each pointer:

```
RVA 0xD6BD86 MOV RDI,qword ptr [0x148982dd0]
RVA 0xD6BD9D MOVSXD R14,dword ptr [RDI + 0x1b0] ; count
RVA 0xD6BDA4 MOV RSI,qword ptr [RDI + 0x1b8] ; data
RVA 0xD6BDAB MOV qword ptr [RDI + 0x1b0],R13 ; clear
RVA 0xD6BDB2 MOV qword ptr [RDI + 0x1b8],R13
RVA 0xD6BDD3 MOV RCX,qword ptr [RBX]
RVA 0xD6BDD6 CALL 0x140a9b480
RVA 0xD6BDDB ADD RBX,0x8
```

An allocated `ControlledEntity` (size `0x300`) does not have that
queue. Passing it as `RDX` would make the drain read `entity+0x1d8`
and `entity+0x1e0`. Miss. The globals we already hold
(`SceneManagerGame*`, `EntityManagerGame*`, `GameModule*`,
`PlayerManager*`) plus the allocated entity do not fill this call.
No call frame.

The single next decompile is `FUN_140a9b780` (207 bytes), called from
the drain at `0xA9B54A` as `(RCX = the job, RDX = [record+0],
R8 = record+8)`. Its first use of `RDX` is `CALL qword ptr [RDX]`
at `0xA9B7AC`, then a word at `[RAX]` and a byte at `[RAX+0x5]`. The
question is whether that `RDX` is the `Entity*` the job was queued
with. The enqueue sites (`FUN_140a9f910` at `0xA9F9EC` and the same
shape in `FUN_140a9fc10`, `FUN_140cb5ff0`, `FUN_140d502d0`) recover a
pointer with `LEA RBP, [RAX-0x20]` at `0xA9F938` from `[arg+0x38]`
before they build the `0x18` record. Do not decompile those until
`FUN_140a9b780` names `RDX`.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_140a9b780` dispatches a `MsgScene`, not an entity (2026-10-21)

Offline only. Four `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/JobRdx.java`, `JobRdx2.java`, `JobRdx3.java`,
`JobRdx4.java`; reports `local/job_rdx.txt`, `local/job_rdx2.txt`,
`local/job_rdx3.txt`, `local/job_rdx4.txt`). No re-analysis, no
symbol scan, no inject, no launch, nothing written under `M:\`. No
new probe mode. `RDX` is not an `Entity*`, so no call frame was
emitted.

`FUN_140a9b780` is 207 bytes and returns void. The three arguments
are saved at the entry and never written back:

```
RVA 0xA9B78F MOV RSI,R8
RVA 0xA9B792 MOV RBX,RDX
RVA 0xA9B795 MOV RDI,RCX
```

`RCX` is the job object (the drain passes `RBP`, its own `this`).
`RDX` is the first qword of the `0x18`-byte record. `R8` is
`record+8`, the other 16 bytes, and it is only forwarded.

The virtual call at `0xA9B7AC` is slot 0 of `RDX`, and its return is
a type record, not the object:

```
RVA 0xA9B7A6 MOV RAX,qword ptr [RBX]
RVA 0xA9B7A9 MOV RCX,RBX
RVA 0xA9B7AC CALL qword ptr [RAX]
RVA 0xA9B7AE MOV R9,RAX
RVA 0xA9B7D4 LEA RCX,[0x142121e40]
RVA 0xA9B7DB CMP R9,RCX
```

`RAX` is compared with the record at RVA `0x2121E40`, including a
walk of `[R9+0x58]` when the tags match. That record has the same
header shape as `ControlledEntity` at `0x21DA6C0` (tag `+4` is
`0x04`, one base at `+0x58`, no property rows). Its name qword, at
the same `+0x40` slot that reads `ControlledEntity` on the entity
record, reads `MsgScene`. The base at `+0x58` names `MsgBase`. So
`RDX` is a `MsgScene*`. It is not an `Entity*`, not a type record
(the call *returns* the type record), and not the job (that is
`RCX`).

The only other call is at `0xA9B83D`, and only when the type check
passes, `[RCX+0x2e0]` is non-null, and `[RDX+0x10]` is non-zero:

```
RVA 0xA9B81F MOV RCX,qword ptr [RDI + 0x2e0]
RVA 0xA9B82B CMP byte ptr [RBX + 0x10],0x0
RVA 0xA9B837 MOV R8,RSI
RVA 0xA9B83A MOV RDX,RBX
RVA 0xA9B83D CALL qword ptr [RAX]
```

That is a virtual call on the pointer already stored in the job.
Nothing in the 207 bytes writes a pointer into a counted array, a
parent slot, or a model slot. The earlier `CALL 0x140a9f0d0` at
`0xA9B798` only walks two lists the job already owns (`+0x1f0` /
`+0x1f8` and `+0x1e8`) and forwards `RDX`.

Who queues it. `FUN_140a9b480` reads the record at `0xA9B540`
(`MOV RDX, [RDI]`) and calls this function at `0xA9B54A`. The store
is in `FUN_140a9b340`, one record of `0x18` bytes:

```
RVA 0xA9B34C MOV RBP,RCX ; the job
RVA 0xA9B353 MOV RSI,R8 ; the 16-byte payload
RVA 0xA9B35A MOV RDI,RDX ; the first qword
RVA 0xA9B439 VMOVUPS xmmword ptr [RDX + RCX*0x8],XMM0
RVA 0xA9B43E VMOVHPD qword ptr [RDX + RCX*0x8 + 0x10],XMM1
```

`XMM0` was built from `RDI` then the 16 bytes of `R8`, so the first
qword of the record is the `RDX` the caller of `FUN_140a9b340`
passed. One such caller is enough. `FUN_140a9f910` at `0xA9F9EC`:

```
RVA 0xA9F935 MOV R14,RDX ; the caller's message
RVA 0xA9F9C1 MOV RAX,qword ptr [R14]
RVA 0xA9F9C4 MOV RCX,R14
RVA 0xA9F9C7 CALL qword ptr [RAX] ; same slot 0, the type record
RVA 0xA9F9CC CALL 0x1402034a0 ; allocate from that record
RVA 0xA9F9D1 MOV RDX,R14
RVA 0xA9F9D7 MOV RBX,RAX
RVA 0xA9F9DD CALL qword ptr [R8 + 0x10] ; copy the message
RVA 0xA9F9E6 MOV RDX,RBX
RVA 0xA9F9EC CALL 0x140a9b340
```

`FUN_1402034a0` (180 bytes) reads the record the same way the entity
factory does: constructor from `FUN_1402033d0`, size from
`FUN_1402032d0`, block from `FUN_1401e6100`, then the placement
constructor. For `MsgScene` the size dword at `+0x10` is `0x18`. The
first qword of the job record is that fresh `MsgScene*`, a copy of
the message the caller already had. It is not the entity recovered
by `LEA RBP, [RAX-0x20]` at `0xA9F938`.

Miss. No instruction in `FUN_140a9b780` registers an entity. The
`MsgScene` record has no constructor pointer at `+0x18` (that qword
is 0), and the four functions that touch this message
(`FUN_140a9b780`, `FUN_140a9f910`, `FUN_140aa4180`, `FUN_14095fcb0`)
do not reference `EntityManagerGame*` (`0x8982C40`), `GameModule*`
(`0x8983150`), `SceneManagerGame*` (`0x8982DD0`) or `PlayerManager*`
(`0x8982C80`). An allocated `ControlledEntity` does not fill `RDX`.

The single next decompile is `FUN_140aa4180` (292 bytes), called at
`0xA9F954` as `(RCX = the return of FUN_14095fcb0(entity), RDX = the
job)`. The question is whether that call inserts the entity it was
given into a manager we already hold, or only forwards the
`MsgScene`. No call until that is named.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_140aa4180` forwards a job onto a `SceneComponent`, it does not insert an entity (2026-10-22)

Offline only. Four `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Aa4180.java`, `Aa4180b.java`, `Aa4180c.java`,
`Aa4180d.java`; reports `local/aa4180.txt`, `local/aa4180b.txt`,
`local/aa4180c.txt`, `local/aa4180d.txt`). No re-analysis, no
symbol scan, no inject, no launch, nothing written under `M:\`. No
new probe mode. The function does not insert, so no call frame was
emitted.

`FUN_140aa4180` is 292 bytes and returns void. It takes two
arguments. There is no `R8`. Both are saved at the entry, and only
`RDX` is written back, into a field of `RCX`:

```
RVA 0xAA418F MOV RBX,qword ptr [RCX + 0x48]
RVA 0xAA4193 MOV RSI,RDX
RVA 0xAA4196 MOV qword ptr [RCX + 0x50],RDX
RVA 0xAA419A MOV RDI,RCX
```

`RCX` is the return of `FUN_14095fcb0`. That function is 101 bytes.
It does not return the entity and it does not return a manager. It
looks a component up in the entity's component map and returns that
component:

```
RVA 0x95FCB6 MOV RDI,RCX
RVA 0x95FCB9 LEA RDX,[0x142121a20]
RVA 0x95FCC0 ADD RCX,0xa0
RVA 0x95FCC7 CALL 0x1408fa230
```

`FUN_1408fa230` binary-searches the map at `entity+0xa0` for the
type id and returns the matching pointer. The id at RVA `0x2121A20`
has the same header shape as `ControlledEntity` (`tag +4` is
`0x04`, size dword at `+0x10` is `0x70`). Its name qword at `+0x40`
reads `SceneComponent`. So `RCX` of `FUN_140aa4180` is a
`SceneComponent*` already attached to the entity. If the lookup
misses, `FUN_14095fcb0` builds one (`FUN_1408f9ff0` on the same
record) and `FUN_140950d10` attaches it to the entity, then returns
that new component. Either way the return is the component, not the
entity and not one of the managers we hold.

`RDX` is the job `FUN_140a9f910` already had in `RDI` (`MOV RDX,RDI`
at `0xA9F94E`). The only pointer store in the 292 bytes is the one
above: the job is written to `SceneComponent+0x50`. The other stores
are the saved registers and a stack message. Nothing writes
`child+0x80`, `parent+0x88`, a counted world or scene array, or a
model slot.

What it does instead. It walks children the component's entity
already has, and forwards the same job:

```
RVA 0xAA419D MOV RBX,qword ptr [RBX + 0x88] ; entity+0x88, the child list
RVA 0xAA41E6 MOV RBX,qword ptr [RBX + 0x90] ; next sibling
RVA 0xAA41BD LEA RCX,[RBX + 0xa0]
RVA 0xAA41C4 LEA RDX,[0x142121a20] ; SceneComponent again
RVA 0xAA41CB CALL 0x1408fa230
RVA 0xAA41E1 CALL 0x140aa4180 ; recurse, RDX still the job
```

Then it dispatches messages the component already owns.
`FUN_140aa4e20` builds a `MsgSceneRestrictionChanged` (type record
RVA `0x2121CE0`, name at `+0x40`) whose payload qword is the job,
and `FUN_14097fbe0` walks the listeners already registered on
`entity+0x2c0`. When `SceneComponent+0x38` is non-zero,
`FUN_140aa4790` filters the job's message list for `MsgScene`
(record RVA `0x2121E40`, the same record `FUN_140a9b780` compared
against) and `FUN_14097fec0` only bumps a listener dword. The stack
object built at `0xAA4214` is a `MsgAddedToScene` (vftable RVA
`0x18C6AA0` points at type record `0x2122940`, name `MsgAddedToScene`,
size `0x18`), and it is passed to the same listener walk
(`FUN_14097fbe0` at `0xAA428F`). `FUN_14097fbe0` calls registered
handlers. It does not append an entity.

The four globals we hold are not referenced. `FUN_140aa4180`,
`FUN_14095fcb0`, `FUN_140aa4e20` and `FUN_140aa4790` contain no load
of `EntityManagerGame*` (`0x8982C40`), `GameModule*` (`0x8983150`),
`SceneManagerGame*` (`0x8982DD0`) or `PlayerManager*` (`0x8982C80`).
An allocated `ControlledEntity` has no `SceneComponent` at `+0xa0`
and no listener table at `+0x2c0`, so it does not fill `RCX` either.

Miss. `FUN_140aa4180` does not insert the entity into a manager. It
forwards the job to a `SceneComponent` the entity already carries,
and that component posts `MsgAddedToScene` to listeners the entity
already has. No call frame.

The call is also gated. `FUN_140a9f910` only reaches it when
`[message+0x60] == 1` (`CMP dword ptr [RDX+0x60], 0x1` /
`JNZ 0xA9F959` at `0xA9F940`). After it returns, the same function
walks a pointer array the job already owns and virtual-calls slot 0
with the recovered entity:

```
RVA 0xA9F959 MOV RBX,qword ptr [RDI + 0x2f0]
RVA 0xA9F960 MOVSXD RAX,dword ptr [RDI + 0x2e8]
RVA 0xA9F967 LEA RSI,[RBX + RAX*0x8]
RVA 0xA9F970 MOV RCX,qword ptr [RBX]
RVA 0xA9F973 MOV RDX,RBP
RVA 0xA9F97E MOV RAX,qword ptr [RCX]
RVA 0xA9F981 CALL qword ptr [RAX]
```

`RCX` is an element of `job+0x2f0` (count at `job+0x2e8`, stride 8).
`RDX` is the entity recovered by `LEA RBP,[RAX-0x20]` at `0xA9F938`.
`R8B` is 1 when `[message+0x60]` is 0. That is the first call in
this chain whose arguments are the entity and a live object the job
already points at.

The single next decompile is whatever function the vtable at
`[job+0x2f0]` slot 0 actually is. The question is whether that
virtual call inserts the entity in `RDX` into a manager we already
hold. Read one live element of `job+0x2f0` (the job object is the
`RCX` of `FUN_140a9f910`, which is the `RBP` the drain at `0xA9B54A`
passes) and decompile only that slot-0 target. No call until that
target is named.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### The slot-0 call at `0xA9F981` has no named target (2026-10-22)

Offline only. Read-only `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0.java` through `Slot2f0t.java`;
reports `local/slot2f0.txt` through `local/slot2f0t.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. No new probe mode. The element type was not named, so
no call frame was emitted.

The call site matches the earlier dump. `FUN_140a9f910` is 752
bytes. After the `SceneComponent` forward it walks a pointer array:

```
RVA 0xA9F959 MOV RBX,qword ptr [RDI + 0x2f0]
RVA 0xA9F960 MOVSXD RAX,dword ptr [RDI + 0x2e8]
RVA 0xA9F967 LEA RSI,[RBX + RAX*0x8]
RVA 0xA9F970 MOV RCX,qword ptr [RBX]
RVA 0xA9F973 MOV RDX,RBP
RVA 0xA9F97A SETZ R8B
RVA 0xA9F97E MOV RAX,qword ptr [RCX]
RVA 0xA9F981 CALL qword ptr [RAX]
```

`RCX` is one element. `RDX` is the entity from `LEA RBP,[RAX-0x20]`
at `0xA9F938`. Slot 0 of that element is still not a named function.
The drain at `0xA9B54A` does not reach it: that instruction is
`CALL 0x140a9b780`.

Who writes the two fields. `FUN_140a99f10` (1033 bytes) is the
`Scene` constructor. RTTI on the vtable it installs at `0xA99F60`
(`0x18C6CF8`) reads `.?AVScene@@`. Its entry is `XOR R8D,R8D`, and
the two stores are that zero:

```
RVA 0xA9A1FE MOV qword ptr [RCX + 0x2e8],R8
RVA 0xA9A205 MOV qword ptr [RCX + 0x2f0],R8
```

It publishes an empty array. It does not push a listener.

The append primitive for this shape is `FUN_1400fe310` (133 bytes).
It increments the dword count and stores its second argument at
`data[oldCount]`. Two callers pass a field at `+0x2e8`:

```
RVA 0x1405F11 ADD RCX,0x2e8
RVA 0x1405F18 CALL 0x1400fe310
```

`FUN_141405ec0` builds the object it appends with vtable RVA
`0x1990B10`, RTTI `.?AVQuestLocationMarkerWrapper@@`. Slot 0 of
that vtable is `FUN_141406690` (357 bytes). The other caller,
`FUN_141408870`, uses vtable `0x1990F20`, RTTI
`.?AVQuestSequenceEntityMarkerWrapper@@`, slot 0 `FUN_1414091b0`
(64 bytes). Both append `this` onto the array of the pointer they
were given. That owner is not the `Scene` job: `FUN_141405880`
grows a separate array at `[RSI+0x90]` / `[RSI+0x98]`. Those two
slot-0 functions are not the call at `0xA9F981`.

`FUN_14094bd80` (112 bytes) and `FUN_14094be00` (104 bytes) are a
different use of the same offsets. `FUN_14094bd80` links a node
(`MOV qword ptr [RDI+0x2e8],RSI` when the tail is null, else
`MOV qword ptr [RAX+0x8],RSI`) and `FUN_14094be00` walks
`[node+0x8]` calling the pointer stored at `node+0x10`. The node
built at `0x94B918` writes `0x961110`, which is not an RTTI
vtable. The second pointer in that node, `0x18AD850`, names
`ParameterSet`, and its slot 0 is `FUN_1409734a0` (87 bytes), a
destructor. That list is not the array walk at `0xA9F981`.

`FUN_140a9f910` is not a `Scene` vtable slot. The seven `Scene`
vtables (`0x18C6CF8` and the six siblings the constructor
installs) have 8-to-12-byte slot-0 thunks. None of the first
twelve slots mentions `+0x2e8` or `+0x2f0`. The two collection
slot-0 functions are `FUN_140a9a400`
(`NonStreamingSceneObjectCollection`) and `FUN_140a9a470`
(`StreamingSceneObjectCollection`). The references at `0x1BE4614`,
`0x1BE4670` and `0x8C0E28C` are unwind bytes (`10 f9 a9 00`), not
calls. A byte scan of executable memory found an `E9` at
`0xAAA4C0` whose displacement lands on `0xA9F910`, and the qword
`0x140AAA4C0` is stored once, at RVA `0x2120E68`, beside the
pointer to type record RVA `0x2107110`. That record's name at
`+0x40` reads `MsgEntityAddedToScene` (size dword `0x68`). The
listing has no instruction at `0xAAA4C0`, so that store is not a
proven call. It does name the message this function handles. It
does not name the object in the array.

No instruction found in these passes stores a listener pointer
into the array the `Scene` constructor zeroes and `FUN_140a9f910`
walks. The four globals we hold are not loaded by the constructor
or by `FUN_1400fe310`. An allocated `ControlledEntity` does not
fill `RCX` of the virtual call.

Miss. The slot-0 target is not named, and nothing in the named
functions inserts the entity.

The single next decompile is the caller of `FUN_1400fe310` whose
vector is the `Scene` `FUN_140a99f10` constructs (the `RCX` of
`FUN_140a9f910`), not the quest-marker owners above. The question
is the vtable of the pointer it passes in `RDX`. That vtable's
slot 0 is the call at `0xA9F981`. No call until that vtable is
read from the bytes.

### The JMP at `0xAAA4C0` is a message handler, not a vtable slot (2026-10-22)

Offline only. One `-noanalysis -readOnly -process` pass
(`local/ghidra_scripts/Slot2f0u.java`, report `local/slot2f0u.txt`)
disassembled the window that the earlier byte scan had left as
raw bytes, then a second pass (`Slot2f0v.java`,
`local/slot2f0v.txt`) listed every call of `FUN_1400fe310` whose
`RCX` is an object `+0x2e8`. No re-analysis, no symbol scan, no
inject, no launch, nothing written under `M:\`. No new probe mode.
No call frame was emitted.

The window is a run of five-byte thunks, each padded with `INT3`:

```
RVA 0xAAA4A0 JMP 0xA9FE00
RVA 0xAAA4B0 JMP 0xA9FC10
RVA 0xAAA4C0 JMP 0xA9F910
RVA 0xAAA4D0 JMP 0xA9F680
```

`0xAAA4C0` is the thunk. It is not a vtable slot. The qword
`0x140AAA4C0` sits at RVA `0x2120E68` in a handler table, paired
with the type-record pointer at `0x2120E60` (`0x142107110`). The
eight bytes before that qword are the type record, not a Complete
Object Locator, so the walk-back finds no RTTI and no slot index.
The same shape repeats for the neighbours:

```
RVA 0x2120E60 record 0x2107110 MsgEntityAddedToScene handler 0xAAA4C0
RVA 0x2120E70 record 0x2105640 MsgEntitySpawnCanceled handler 0xAAA4B0
RVA 0x2120E80 record 0x2101A70 MsgEntityRemovedFromScene handler 0xAAA4A0
```

So `FUN_140a9f910` is the handler for `MsgEntityAddedToScene`. That
names the message. It does not name the object stored in the array
at `+0x2f0`.

Who writes that array. Of 1,383 references to `FUN_1400fe310`, two
set `RCX` to an object `+0x2e8`, and both were already excluded:

```
RVA 0x1405F11 ADD RCX,0x2e8 ; in FUN_141405ec0
RVA 0x1405F18 CALL 0x1400fe310
```

`FUN_141405ec0` builds a `QuestLocationMarkerWrapper` (vtable
`0x1990B10`) and `FUN_141408870` builds a
`QuestSequenceEntityMarkerWrapper` (vtable `0x1990F20`). Each takes
the owner from `MOV RCX,qword ptr [RDX]`, which is the caller's
`R15`/`RDI`, and the caller then stores the wrapper into its own
array at `+0x90` (`MOV EBX,dword ptr [RSI+0x90]` at `0x1405C57`).
That is not the `Scene` object `FUN_140a9f910` takes as `RCX`.

The only code caller of the `Scene` constructor is
`FUN_1414c9580` (177 bytes). It is the `MovableScene` constructor:
after `CALL 0x140a99f10` it writes vtable RVA `0x19A08C0`, whose
COL names `.?AVMovableScene@@`, and it zeroes fields from `+0x410`
up. It never mentions `+0x2e8` or `+0x2f0`. Its only code caller,
`FUN_1414c9640` (188 bytes), overwrites those vtables with RVA
`0x19A03C0`, whose COL names `.?AVScheduledScene@@`. The 24 slots
of that vtable inherit `FUN_1402085f0`, and the `FUN_1400fe310`
inside it (`0x2086BE`) appends onto the vector passed in `RDX`,
not onto `this+0x2e8`. The one override that stores a pointer,
`FUN_1414c5680` (225 bytes, slot `+0x40`), writes it to
`ScheduledScene+0x470` after calling `FUN_140a9a7f0`. Nothing in
the `Scene`, `MovableScene` or `ScheduledScene` constructors or
vtables pushes an element into the array `FUN_140a9f910` walks.

Inside the handler the decompiler reads the walk as a forward:

```
(*(code *)**(undefined8 **)*puVar8)(*puVar8, plVar9, *(int *)(param_2 + 0xc) == 0);
```

`plVar9` is the entity recovered from `param_2[7] - 0x20`. The
same function later appends an entity position into the scene's
own array at `+0x338` (`FUN_1400e7e60` at `0xA9FB..`), which is a
different field, and it calls `FUN_140aba360` on an object looked
up in the array at `+0x1f8`. Neither store is the virtual call,
and neither one loads `EntityManagerGame*` (`0x8982C40`),
`GameModule*` (`0x8983150`), `SceneManagerGame*` (`0x8982DD0`) or
`PlayerManager*` (`0x8982C80`).

Miss. The slot-0 target is not named, so it does not insert the
entity into a manager we hold. No call frame.

The single next decompile is `FUN_141406800`, which
`FUN_141406690` calls once the quest wrapper has matched the
entity. The question is whether the 0xA0-byte object it builds
there is stored into a `Scene`'s `+0x2f0`, and what vtable that
object gets. No call until that store is shown.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### Slot 0 of the object at `job+0x2f0` is `FUN_141406690`, and it does not insert the entity (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0u.java` through `Slot2f0zl.java`;
reports `local/slot2f0u.txt` through `local/slot2f0zl.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. No new probe mode. The slot-0 function does not insert,
so no call frame was emitted.

The thunk window is not a vtable. `0xAAA4C0` is `JMP 0xA9F910`, the
`MsgEntityAddedToScene` handler already named above. The object the
handler calls is the element of the array, and that element is
written by the wrapper constructors.

`FUN_141405880` (1315 bytes) is the only caller of `FUN_141405ec0`.
It does not grow an array at `+0x90`. That dword is a counter the
function bumps after the call. The owner it passes is a pointer it
loaded one loop earlier:

```
RVA 0x14059C0 MOV R15,qword ptr [R14]
RVA 0x1405C3C MOV RDX,R15
RVA 0x1405C42 CALL 0x141405ec0
```

`R14` is one entry of the pointer array `FUN_1413ffac0` filled.
That function reads the global at RVA `0x8983278`, hashes the caller's
key in the map at `+0x128`, and appends the value with `FUN_1400fe310`.
`FUN_141405ec0` then does what the earlier note already quoted:

```
RVA 0x1405F0B MOV RCX,qword ptr [RDX]
RVA 0x1405F11 ADD RCX,0x2e8
RVA 0x1405F18 CALL 0x1400fe310
```

`[RDX]` is the first qword of that entry, and `FUN_140a9d520` is
called on it at `0x14059DD` before the constructor runs. It walks
`[obj+0x178]` and returns the first object whose type record matches
RVA `0x2124270` (name `ScenePrefabInstance`) and whose parent matches
RVA `0x2122100` (name `Scene`, size `0x410`). So the `RCX` of the
append is a live `Scene`. The pointer it appends is the wrapper
itself, vtable RVA `0x1990B10`, RTTI `.?AVQuestLocationMarkerWrapper@@`.

The destructor confirms the same array. `FUN_141406490` frees the
wrapper and then removes it:

```
RVA 0x1406521 MOV RCX,qword ptr [RBX + 0x10]
RVA 0x1406525 MOV RCX,qword ptr [RCX]
RVA 0x1406528 CALL 0x1413fa710
```

`FUN_1413fa710` (125 bytes) searches `data` at `+0x2f0` for the
wrapper and shrinks the dword count at `+0x2e8`. That is the array
`FUN_140a9f910` walks. The sibling
`QuestSequenceEntityMarkerWrapper` (vtable `0x1990F20`) is removed
the same way by `FUN_141408ac0`.

A byte scan of every `ADD r64, 0x2e8` in executable memory found
eight sites. Two are `ADD RSP`. The other four that are not the two
wrapper constructors are `FUN_1404392d0` (a `ServerEventManager`
destructor, calls `FUN_140100dd0`), `FUN_140709e50` (calls
`FUN_1407178d0`, a chunked-list clear) and `FUN_14099bad0` (calls
`FUN_14021b390`, then zeroes `+0x2f0`). None of them pushes a new
element. No other writer of this array was found.

Slot 0 of the wrapper is `FUN_141406690` (357 bytes). `RDX` is the
entity. The function compares it and forwards it. It never stores it:

```
RVA 0x14066AB MOV RCX,qword ptr [RCX + 0x10]
RVA 0x14066B2 CALL 0x140ab8dd0
RVA 0x14066C3 MOV RCX,qword ptr [RBP + 0x280]
RVA 0x14066CE MOV RCX,qword ptr [RBP + 0x278]
RVA 0x1406797 CALL 0x140abc560
RVA 0x14067A7 CALL 0x141406800
```

`FUN_140ab8dd0` returns a quest record. The two loads compare
`entity+0x278` and `entity+0x280` against that record's identity.
`FUN_140abc560` returns an entity already stored on the quest, and
the call to `FUN_141406800` runs only when `RDX` is that entity.
`FUN_141406800` (264 bytes) allocates `0xA0` bytes and copies two
qwords from the entity (`+0x8` and `+0x10`) into it. The only
pointer store of that block is into the quest, through
`FUN_141407df0`. Nothing writes `child+0x80`, `parent+0x88`, a model
slot, or a counted world list. The four globals we hold are not
loaded. `FUN_1414091b0` (64 bytes), slot 0 of the sequence wrapper,
forwards the same way and does not store `RDX` either.

Miss. The virtual call notifies a quest marker that an entity it
already tracks was added to a `Scene`. It does not insert that
entity into `EntityManagerGame`, `GameModule`, `SceneManagerGame` or
`PlayerManager`.

The single next decompile is `FUN_141407df0` (called at `0x14068C2`
with `RCX` = the quest and `RDX` = the `0xA0`-byte block). The
question is whether that function, which is the only one here that
receives both a live quest object and a pointer derived from the
entity, writes the entity into a manager we already hold.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_141407df0` appends a quest node, not the entity (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0zu.java`, `Slot2f0zv.java`; reports
`local/slot2f0zu.txt`, `local/slot2f0zv.txt`). No re-analysis, no
symbol scan, no inject, no launch, nothing written under `M:\`. No
new probe mode. The function does not insert the entity, so no call
frame was emitted.

The call at `0x14068C2` is inside `FUN_141406800` (264 bytes). The
two registers are set immediately before it, and both of them were
filled at the top of that function, not by the block that builds
the `0xA0` object:

```
RVA 0x1406823 MOV RSI,RDX
RVA 0x140682B MOV R14,qword ptr [RCX + 0x18]
RVA 0x14068BC MOV RDX,RDI
RVA 0x14068BF MOV RCX,R14
RVA 0x14068C2 CALL 0x141407df0
```

`RSI` is the entity, the `RDX` `FUN_141406690` passed in. `R14` is
`[wrapper+0x18]`, the quest the wrapper was constructed with
(`FUN_141405ec0` stores `R8` there at `0x1405EE8`). `RDI` is the
`0xA0`-byte block allocated at `0x140686C` (`MOV EDX,0xa0`, then
`CALL qword ptr [RAX+0x88]`), or zero when that allocation fails.
So `RCX` of `FUN_141407df0` is the quest and `RDX` is the `0xA0`
block. The entity pointer itself is not an argument. It only
contributes the two qwords copied out of it:

```
RVA 0x14068AD VMOVUPS XMM0,xmmword ptr [RSI + 0x8]
RVA 0x14068B2 VMOVUPS xmmword ptr [RDI + 0x4],XMM0
```

`FUN_141407df0` is 431 bytes. It allocates `0x450` bytes from the
same allocator (`MOV EDX,0x450` at `0x1407E6E`, virtual call
`[RAX+0x88]` at `0x1407E73`), stores the `0xA0` block at the new
object's `+0x8`, and appends that new object onto an array that
belongs to the quest:

```
RVA 0x1407E1B MOV RDI,RDX
RVA 0x1407E8B MOV qword ptr [RSI + 0x8],RDI
RVA 0x1407F4B MOV qword ptr [RCX + RAX*0x8],RSI
```

The array pointer is `quest+0x78` and the count is the dword at
`quest+0x70` (`LEA RDI,[RBP+0x70]` at `0x1407ECA`, the store above
is the push, then the dword is incremented). `RCX` in that store is
the grown buffer, and `RSI` is the `0x450` node, not the entity.
The two later byte stores, `MOV byte ptr [RAX+0x9a],1` at
`0x1407F88` and `MOV byte ptr [RAX+0xb0],1` at `0x1407F93`, write
flags through `[quest+0x18]`, not a pointer. Nothing in the
function writes `child+0x80`, `parent+0x88`, or a model slot, and
it never loads `EntityManagerGame*` (`0x8982C40`), `GameModule*`
(`0x8983150`), `SceneManagerGame*` (`0x8982DD0`), `PlayerManager*`
(`0x8982C80`) or the map global at `0x8983278`.

Miss. The quest receives a refcounted copy of two qwords from the
entity and keeps it on its own list. It does not write the entity
into a manager we hold.

The single next decompile is `FUN_1413efb50` (called at `0x1407EBA`
with `RCX` = the `0x450` node, which holds the `0xA0` block at
`+0x8`). The question is whether that function writes the entity,
or the `0xA0` block, into one of the four managers we hold.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_1413efb50` initialises a quest marker, not a manager (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0zw.java`, `Slot2f0zx.java`,
`Slot2f0zy.java`, `Slot2f0zz.java`, `Slot2f0zza.java`; reports
`local/slot2f0zw.txt` through `local/slot2f0zza.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. No new probe mode. The function does not insert the
entity, so no call frame was emitted.

The call at `0x1407EBA` is inside `FUN_141407df0` (431 bytes), and
it takes one argument. `RCX` is the `0x450` node just allocated;
`RDX` and `R8` are not set for this call:

```
RVA 0x1407E79 MOV RSI,RAX
RVA 0x1407E8B MOV qword ptr [RSI + 0x8],RDI
RVA 0x1407E97 MOV RCX,RSI
RVA 0x1407E9A MOV qword ptr [RSI + 0x10],RBP
RVA 0x1407E9E MOV qword ptr [RSI + 0x18],RAX
RVA 0x1407EBA CALL 0x1413efb50
```

`RDI` is the `0xA0` block (`MOV RDI,RDX` at `0x1407E1B`), stored at
`node+0x8` before the call. `RBP` is the quest. `RAX` at `0x1407E9E`
is `[quest+0x30]`, stored at `node+0x18`. The entity pointer is not
in any of those registers.

`FUN_1413efb50` is 580 bytes. It reads `node+0x10` (the quest) and
looks that quest up in the map at `0x148983278` (`0x13EFB94`,
`0x13EFCD2`). When the marker is not yet enabled it writes
`QuestMarkerInstance::EnabledState::vftable` (`LEA RAX,[0x14198d168]`
at `0x13EFC37`) onto the node itself and calls `FUN_14137aee0` at
`0x13EFD51`. That call's `RDX` is `[node+0x8]`, the `0xA0` block
(`MOV RDX,qword ptr [RDI+0x8]` at `0x13EFD21`), and its `RCX` is
`node+0x3f0`. `FUN_14137aee0` (946 bytes) loads none of the four
manager globals. Nothing in `FUN_1413efb50` writes `child+0x80`,
`parent+0x88`, a model slot, or a counted list, and it never loads
`EntityManagerGame*` (`0x8982C40`), `GameModule*` (`0x8983150`),
`SceneManagerGame*` (`0x8982DD0`) or `PlayerManager*` (`0x8982C80`).

The one later function that does load a manager is not this call.
`FUN_1413efe60` (3218 bytes), reached from the already-enabled arm
at `0x13EFD65`, loads `GameModule*` at `0x13F0749` and pushes onto
`[[GameModule+0x558]+0x40]`. The element is a `0x240`-byte
`CustomMarker` it allocates itself (`MOV EDX,0x240` at `0x13F07EF`,
`CustomMarkerManager::CustomMarker::vftable` at `0x13F0812`), and
what it stores at that object's `+0x1e0` is `node+0x10`
(`LEA R15,[RSI+0x10]` at `0x13F0750`, `MOV [RDI+0x1e0],R15` at
`0x13F086C`). The push is `FUN_1402250c0` at `0x13F08A0` with
`RCX = [[GameModule+0x558]+0x40]` and `RDX = &local`, and the local
holds the new `CustomMarker*`. Neither the entity nor the `0xA0`
block is the element. `FUN_1413efe60` also reads `[node+0x3c8]` at
entry and returns immediately when that dword is 0, which is the
value `FUN_141407df0` stored at `0x1407EB0`.

Miss. `FUN_1413efb50` only fills fields of the quest's own `0x450`
node. The quest-marker chain (wrapper, slot 0, the `0xA0` copy, the
`quest+0x78` node, and now this initialiser) does not register an
entity. Its callers that also load `GameModule*` (`FUN_14140dd20`,
`FUN_141407660`, `FUN_14140a460`) pass the same node, not the entity.

The single next decompile is `FUN_140d73270` (`0xD73270`, 416
bytes). It is the constructor that takes the entity in `RDX` and
publishes a `PlayerGame` into the camera manager we already hold
(`MOV RDI,[0x148982CB0]` at `0xD733C2`, then `FUN_1400fe310` at
`0xD733E7`). The question is which of its stores, other than
`[PlayerGame+0x30] = entity` at `0x913CB7`, writes that entity into
`EntityManagerGame`, `SceneManagerGame` or `PlayerManager`, or
whether the only world-facing store is the camera-list push.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_140d73270` stores the entity only at `[PlayerGame+0x30]` (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0zzb.java`, `Slot2f0zzc.java`,
`Slot2f0zzd.java`, `Slot2f0zzf.java`, `Slot2f0zzg.java`,
`Slot2f0zzh.java`, `Slot2f0zzi.java`, `Slot2f0zzj.java`; reports
`local/slot2f0zzb.txt` through `local/slot2f0zzj.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. No new probe mode. No store writes the entity into a
manager, so no call frame was emitted.

`FUN_140d73270` is 416 bytes. The entry saves `RCX` and calls with
`RDX` still the incoming argument; `R8` is not an argument and is
never read:

```
RVA 0xD73270 MOV qword ptr [RSP + 0x10],RBX
RVA 0xD73282 MOV RSI,RCX
RVA 0xD73285 CALL 0x140913be0
```

The object loaded at `0xD733C2` is a `CameraPredictionManager*`. The
load is `48 8B 3D E7 F8 C0 07`, and
`0xD733C2 + 7 + 0x07C0F8E7 = 0x8982CB0`. The publisher of that slot,
`FUN_140d5e2c0` at `0xD5FA19`, stores the block it just constructed
with `FUN_1409581c0`, and that constructor writes
`LEA RAX,[0x1418AC588]` at `0x9581CC`. The Complete Object Locator
before that vtable is VA `0x141A259B8`, whose type descriptor is
`.?AVCameraPredictionManager@@`.

The entity pointer is saved once, inside the callee. `FUN_140913be0`
(280 bytes) does `MOV RBX,RDX` at `0x913C0D` and the only store of
that register is:

```
RVA 0x913CB7 MOV qword ptr [RDI + 0x30],RBX
```

`RDI` is `RCX` of `FUN_140913be0`, which is the `PlayerGame` being
built, so this is `[PlayerGame+0x30] = entity`. `RBX` is reloaded
from the stack at `0x913CE5` and the entity is not in a register
again. Nothing else in `FUN_140d73270` or in `FUN_140913be0` stores
it. The other stores write vtables (`PlayerGame::vftable` at
`[RSI]`, `[RSI+0x20]`, `[RSI+0x128]` and `[RSI+0x130]`, RVAs
`0x18FBD80`, `0x18FB308`, `0x18FBF48`, `0x18FBEF0`) or zeroes. None
is a parent pointer, a model slot, or a counted list.

The one list this function does push is not the entity. After the
lock on `[CameraPredictionManager+0x20]`:

```
RVA 0xD733C2 MOV RDI,qword ptr [0x148982cb0]
RVA 0xD733D9 TEST RSI,RSI
RVA 0xD733DC LEA RCX,[RDI + 0x30]
RVA 0xD733E0 CMOVZ R14,RBP
RVA 0xD733E4 MOV RDX,R14
RVA 0xD733E7 CALL 0x1400fe310
```

`R14` was set at `0xD7329A` with `LEA R14,[RSI+0x130]`, and `RBP` is
0, so `RDX` is `PlayerGame+0x130` (null only if `this` is null).
`FUN_1400fe310` writes that pointer at `[RCX + count*8]`
(`MOV qword ptr [RCX + RBP*0x8],R14` at `0xFE385`). The element is
the `PlayerGame` subobject, not the entity.

Callers, five references, four of them code:

| from | what RDX is |
|---|---|
| `0x134CA90` | `FUN_14134c950`, `MOV RDX,R15` at `0x134CA87`. `R15` is `[[GameModule+0x50]+0x20]`, the live entity. |
| `0x134CAA8` | the same function, null-`this` arm, `MOV RDX,R15` at `0x134CAA0`. |
| `0x13D8540` | `FUN_1413d8470`, `MOV RDX,RSI` at `0x13D853A`. `RSI` is the same `[[GameModule+0x50]+0x20]`. |
| `0xD724ED` | thunk at `0xD724E0`: `MOV RAX,RDX` / `XOR EDX,EDX` / `MOV RCX,RAX` / `JMP FUN_140d73270`. It constructs whatever pointer it was given, and it zeroes the entity argument. |

`FUN_14134c950` and `FUN_1413d8470` are the two that hold both the
entity we can name and `GameModule*`, and both are already blocked.
The thunk passes no entity at all. None of the four passes the
entity together with `EntityManagerGame*`, `SceneManagerGame*` or
`PlayerManager*`.

The `PlayerGame` vtable at `0x18FBD80` has five slots that load one
of the four manager globals, and none of them is a new insert of the
entity:

| slot | function | what it loads | what it does with an entity |
|---|---|---|---|
| `+0x28` | `FUN_140d73c90` (3939 bytes) | `GameModule*` at `0xD748B1` | reads `[PlayerGame+0x30]` only by handing the `PlayerGame` to `FUN_140d72230` |
| `+0x78` | `FUN_140d77660` (57 bytes) | `GameModule*` at `0xD77660` | writes `[GameModule+0x4bc]`, no entity |
| `+0xE8` | `FUN_140d8c2d0` (554 bytes) | `GameModule*` at `0xD8C3AF` | `RDX` is a `PlayerGame`, already excluded |
| `+0xF0` | `FUN_140d8c500` (282 bytes) | `GameModule*` at `0xD8C512` | the matching remove, `RDX` a `PlayerGame` |
| `+0x138` | `FUN_14111f3c0` (26 bytes) | `EntityManagerGame*` at `0x111F3C0` | reads `[EntityManagerGame+0xA0]`, no entity argument |

`FUN_140d73c90` is the only one of those that both loads a manager
and reaches the entity. Its entry saves `RDX` to `RSI`
(`MOV RSI,RDX` at `0xD73CB2`) and `RCX` to `R13`, and the later
`[R15+0x30]` at `0xD74AB1` is not the entity: `R15` was loaded from
`[RAX+0x80]` at `0xD74A84`. The entity path is the call at the
bottom of the `GameModule` load:

```
RVA 0xD748B1 MOV RAX,qword ptr [0x148983150]
RVA 0xD748C8 MOV R10,qword ptr [RAX + 0x50]
RVA 0xD748F2 MOV qword ptr [RSP + 0x28],R10
RVA 0xD74900 MOV RCX,R13
RVA 0xD7490E CALL 0x140d72230
```

`FUN_140d72230` (458 bytes) takes the `PlayerGame` in `RCX` and
`[GameModule+0x50]` as a stack argument. It loads neither of the
four manager globals itself, and its one `+0x30` is
`MOV RBP,qword ptr [R11+0x30]` at `0xD723E9` on a pointer of its
own. So the slot reaches the entity only through the `PlayerGame`
it was called on.

The single next decompile is `FUN_140d73c90` (`0xD73C90`, 3939
bytes, `PlayerGame::vftable+0x28`). The question is what `RDX` is at
`0xD73CB2`, and whether the call at `0xD7490E` writes
`[PlayerGame+0x30]` into `EntityManagerGame`, `SceneManagerGame` or
`PlayerManager`, or only reads it. No call until that register and
that store are named.

### `FUN_140d73c90` reads the entity and never stores it (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0zzk.java` through `Slot2f0zzu.java`;
reports `local/slot2f0zzk.txt` through `local/slot2f0zzu.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. No new probe mode. No store writes the entity into a
manager, so no call frame was emitted.

`FUN_140d73c90` is 3939 bytes. `RCX` is the `PlayerGame`. It is saved
to `R13` at entry and stays there until `0xD7438C`, which is after
every use of it as `this`:

```
RVA 0xD73C90 MOV qword ptr [RSP + 0x8],RCX
RVA 0xD73CB2 MOV RSI,RDX
RVA 0xD73CC4 MOV RDI,R8
RVA 0xD73CD7 MOV R13,RCX
```

`RDX` is not the entity. `RSI` is only ever read as a vector
(`VSUBSD XMM1,XMM0,qword ptr [RSI+0x30]` at `0xD741F3`,
`VMOVSD XMM7,qword ptr [RSI+0x30]` at `0xD74275`). The entity is
loaded from the `PlayerGame` later, inside the one callee that
reads `[PlayerGame+0x30]`.

The `GameModule` load at `0xD748B1` only reads that pointer. The
instruction is `48 8B 05 98 E8 C0 07`, and
`0xD748B1 + 7 + 0x07C0E898 = 0x8983150`. What follows takes
`[GameModule+0x50]` as a stack argument and the `PlayerGame` as
`this`:

```
RVA 0xD74823 LEA RDI,[R13 + 0x1e8]
RVA 0xD748B1 MOV RAX,qword ptr [0x148983150]
RVA 0xD748B8 MOV RDX,qword ptr [RDI]
RVA 0xD748BB MOV R9,qword ptr [R13 + 0x60]
RVA 0xD748C8 MOV R10,qword ptr [RAX + 0x50]
RVA 0xD748F2 MOV qword ptr [RSP + 0x28],R10
RVA 0xD74900 MOV RCX,R13
RVA 0xD7490E CALL 0x140d72230
```

`RDI` is `PlayerGame+0x1e8`, so `RDX` of the call is
`[PlayerGame+0x1e8]`, not the entity. `FUN_140d72230` (458 bytes)
saves that `RCX` to `R14` (`MOV R14,RCX` at `0xD72251`) and stores
the `PlayerGame` itself:

```
RVA 0xD7233C MOV qword ptr [RAX + 0xe8],R14
```

`RAX` there is a block the function just allocated (`LEA ECX,[R15+0x10]`
at `0xD72268`, `CALL 0x1401e6100` at `0xD7226C`). The one `+0x30` in
this callee is the epilogue, `LEA R11,[RSP+0x50]` at `0xD723DD` then
`MOV RBP,qword ptr [R11+0x30]` at `0xD723E9`, which reloads the saved
`RBP`. It loads none of `EntityManagerGame*` (`0x8982C40`),
`SceneManagerGame*` (`0x8982DD0`) or `PlayerManager*` (`0x8982C80`).

The entity is read once, in `FUN_140d73960` (742 bytes), which
`FUN_140d73c90` calls with the `PlayerGame` in `RCX`:

```
RVA 0xD73D66 MOV RCX,R13
RVA 0xD73D71 CALL 0x140d73960
```

```
RVA 0xD73B13 MOV RCX,qword ptr [RDI + 0x30]
RVA 0xD73B17 MOV RBP,qword ptr [RCX + 0x48]
RVA 0xD73B24 MOV RCX,qword ptr [0x1489581d0]
```

`RDI` is the `PlayerGame` (`MOV RDI,RCX` at `0xD7396D`). The next
instruction overwrites `RCX`, so the entity pointer is not in a
register again and is never stored. `RBP` is `[entity+0x48]`, and
that is what the function writes, into a block it allocates itself:

```
RVA 0xD73BDA LEA RAX,[0x141948960]
RVA 0xD73BE5 MOV qword ptr [RBX],RAX
RVA 0xD73BF8 MOV qword ptr [RBX + 0x30],RBP
RVA 0xD73C10 MOV qword ptr [RSI],RBX
RVA 0xD73C34 MOV qword ptr [RDI + 0x208],RSI
```

The Complete Object Locator before vtable RVA `0x1948960` is VA
`0x141AD7980` (signature `1`, type-descriptor offset `0x22A5E00`),
and the descriptor reads `.?AVHunterAbilityDeckManager@@`. The slot
written at `0x89581D0` is zero in the image, so that load is a
runtime object, not a second manager. The new object is then stored
at `[PlayerGame+0x208]`. The first 24 slots of its vtable reference
none of the four manager globals.

No other instruction in `FUN_140d73c90` loads `[PlayerGame+0x30]`.
The six `+0x30` operands in the function itself are the vector reads
on `RSI` and `RDI` (`0xD741F3`, `0xD741F8`, `0xD74275`),
`[RAX+0x30]` at `0xD74671`, `[R15+0x30]` at `0xD74AB1` (and `R15`
came from `[RAX+0x80]` at `0xD74A84`, not from the `PlayerGame`),
and the virtual call `CALL qword ptr [RAX+0x30]` at `0xD74B61`.
That slot is `FUN_140d74c00` (39 bytes), which zeroes
`[PlayerGame+0x1e8]` and releases it. It does not touch `+0x30`.

Miss. `FUN_140d73c90` reads `GameModule*` and reads the entity. It
does not write the entity into `EntityManagerGame`,
`SceneManagerGame`, `PlayerManager`, or a counted world list.

The single next decompile is `FUN_1410ce420` (`0x10CE420`, 918
bytes), called at `0xD73C16` with `RCX` = the new
`HunterAbilityDeckManager` and `RDX` = the object loaded from
`[0x1489581D0]`. It reads `[RCX+0x30]` at `0x10CE5C4`, which is
`[entity+0x48]` as stored at `0xD73BF8`, and it calls
`FUN_1410cd880` (`0x10CE5AB`) and `FUN_1410cdbb0` (`0x10CE77D`).
The question is whether any of those three stores the entity, or
`[entity+0x48]`, into `EntityManagerGame`, `SceneManagerGame` or
`PlayerManager`. No call until that store is shown.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_1410ce420` never holds the entity (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0zzw.java`; report
`local/slot2f0zzw.txt`). No re-analysis, no symbol scan, no inject,
no launch, nothing written under `M:\`. No new probe mode. No store
writes the entity into a manager, so no call frame was emitted.

`FUN_1410ce420` is 918 bytes. `RCX` is the
`HunterAbilityDeckManager` built at `0xD73BE5`. It is saved to `R12`
at entry and stays there:

```
RVA 0x10CE434 MOV R12,RCX
RVA 0x10CE437 MOV RBX,RDX
```

The entity is not an argument and is not loaded. `RDX` is the object
from `[0x1489581D0]`, and the first thing done with it is a walk of
its own array, `[RDX+0x30]` at `0x10CE458`. The read named in the
question is the deck's field:

```
RVA 0x10CE5C4 MOV RAX,qword ptr [R12 + 0x30]
```

`R12` is the deck, and `[deck+0x30]` is `[entity+0x48]` as stored at
`0xD73BF8`. The next two instructions index that object
(`MOV R14, [RAX+0x60]` at `0x10CE5C9`, `MOVSXD R15, [RAX+0x58]` at
`0x10CE5CD`). `RAX` is then overwritten. Nothing in the function
stores `R12`, `RAX` from that load, or any pointer back to the
`0x300` entity. The qword stores write fields of a block this
function allocates (`[RAX+0x20] = R12` at `0x10CE526` and again at
`0x10CE6CE`), which is the deck, not the entity.

Neither callee named in the question was opened. Both were checked
for a reference to a manager global and neither has one:
`FUN_1410cd880` (465 bytes, call at `0x10CE5AB`) and `FUN_1410cdbb0`
(291 bytes, call at `0x10CE77D`). The other eight calls
(`FUN_140407da0`, `FUN_1401e6100` twice, `FUN_1410de3b0` twice,
`FUN_1402084c0` twice, `FUN_140348c40`) also reference none of
`EntityManagerGame*` (`0x8982C40`), `SceneManagerGame*`
(`0x8982DD0`), `PlayerManager*` (`0x8982C80`) or `GameModule*`
(`0x8983150`). At `0x10CE5AB` the argument is a node of the array
walked from `RDX`. At `0x10CE77D` `RCX` is that same node
(`MOV RBP, [RDX+RCX+0x10]` at `0x10CE73D`) and `RDX` is
`[RBX]`, an element of a second array.

Miss. This chain reads `[entity+0x48]` and writes the deck's own
fields. It does not write the entity into `EntityManagerGame`,
`SceneManagerGame`, `PlayerManager`, or a counted world list.

`FUN_140d73960` is the only function that loads
`[PlayerGame+0x30]`. It has two calls. `0xD73D71` is inside
`FUN_140d73c90`, already walked. The other is `0xD737C1` in
`FUN_140d737a0` (353 bytes), which is `PlayerGame::vftable+0x20`
(the pointer at `0x18FBDA0`). It saves `RCX` to `R15` and reads the
entity itself:

```
RVA 0xD737B6 MOV R15,RCX
RVA 0xD737C6 MOV R14,qword ptr [R15 + 0x30]
```

`R14` is live at the return and is passed to no call. The function
loads none of the three manager globals. Its first call,
`FUN_140914010` at `0xD737B9`, loads `RepresentationManagerGame*`
(`0x8982DA0`, published by `MOV [0x148982DA0], RAX` at `0xD5EAC4`,
type `.?AVRepresentationManagerGame@@`) and then reads
`[[PlayerGame+0x30]+0x40]` at `0x914177`. That is a field of the
entity, and the pointer stored afterwards is the block it just
allocated (`MOV [RBX+0x50], R10` at `0x9141E0`). The virtual it
calls, `FUN_14142bed0` (`RepresentationManagerGame::vftable+0xC0`),
reads `[[PlayerGame+0x30]+0x30]` at `0x142BEF2` and passes that, not
the entity, to `FUN_141422500`. No function that references
`PlayerManager*` then reads `[[PlayerManager+0x10][i]+0x30]` (271
functions, no hit).

The single next decompile is `FUN_140d737a0` (`0xD737A0`, 353
bytes, `PlayerGame::vftable+0x20`). The question is what `R14`
(`[PlayerGame+0x30]`) is used for after `0xD737C6`, and whether any
callee it reaches with that pointer stores the entity into
`EntityManagerGame`, `SceneManagerGame` or `PlayerManager`. No call
until that use is named.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_140d737a0` reads the entity and never stores it (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0pga.java` through `Slot2f0pgh.java`;
reports `local/pgslot.txt`, `local/pgslotb.txt` through
`local/pgslotg.txt`, `local/pgsloth.txt`). No re-analysis, no symbol
scan, no inject, no launch, nothing written under `M:\`. No new probe
mode. No store writes the entity into a manager, so no call frame was
emitted.

`FUN_140d737a0` is 353 bytes. `RCX` is the `PlayerGame`. It is saved
to `R15` at entry and stays there. The slot pointer at
`PlayerGame::vftable+0x20` (`0x18FBDA0`) is this function.

```
RVA 0xD737B6 MOV R15,RCX
RVA 0xD737C6 MOV R14,qword ptr [R15 + 0x30]
```

`R14` is the entity. After that load it is used three times, all as a
base, and then popped. It is not copied, not stored, not passed, and
not returned:

```
RVA 0xD737DC MOVSXD RAX,dword ptr [R14 + 0x20]
RVA 0xD737E0 MOV RBX,qword ptr [R14 + 0x28]
RVA 0xD7391F MOV RBX,qword ptr [R14 + 0x28]
RVA 0xD7394C POP R14
RVA 0xD73952 RET
```

`[entity+0x20]` is a count and `[entity+0x28]` is the array it
indexes. Each element is `0x18` bytes. The five calls after the load
take an element, the `PlayerGame`, or a stack slot. None takes `R14`:

| at | callee | arguments |
|---|---|---|
| `0xD7383F` | `FUN_14033a370` (916 bytes) | `RCX` from `[0x14897A5C8]`, `RDX` = `[element+0x10]` |
| `0xD738EF` | `FUN_140108550` (116 bytes) | `RCX` = the element |
| `0xD73909` | `memcmp` | `RCX` = `[element]`, `RDX` = the type at `0x1CAABF0` |
| `0xD7392A` | `FUN_140d77560` (154 bytes) | `RCX` = the `PlayerGame`, `RDX` = `element+8` |
| `0xD73934` | `FUN_140107850` (189 bytes) | `RCX` = a stack slot |

None of those five references `EntityManagerGame*` (`0x8982C40`),
`SceneManagerGame*` (`0x8982DD0`) or `PlayerManager*` (`0x8982C80`),
so none was opened. `RAX` at the `RET` is whatever `FUN_140107850`
left. The `POP R14` restores the caller's register.

The two calls before the load are already walked and do not receive
`R14`. `FUN_140914010` at `0xD737B9` gets the `PlayerGame`.
`FUN_140d73960` at `0xD737C1` gets the `PlayerGame` and reads
`[PlayerGame+0x30]` itself; that is the deck chain, already closed.

Miss. This function reads two fields of the entity and writes
neither the entity, a parent pointer, nor a model slot.

`PlayerGame::vftable` (`0x18FBD80`) has 18 code slots, `+0` through
`+0x88`. The qword at `+0x90` is VA `0x141A865D0` and is not
executable, so the table ends there. The only slot that loads
`[PlayerGame+0x30]` is `+0x20`, this function. The other three
vtables `FUN_140d73270` writes (`0x18FB308`, `0x18FBF48`,
`0x18FBEF0`) have 2, 3 and 4 code slots, and none of them loads
`[this+0x30]` either.

One hop from every slot of `0x18FBD80`: the only callees entered
with the `PlayerGame` that then load `[it+0x30]` are `FUN_140914010`
and `FUN_140d73960`, both already closed. The one remaining slot
that loads a manager while holding the `PlayerGame` is `+0x40`.
`FUN_140d75c40` (184 bytes) calls `FUN_140d75d60` (2917 bytes) at
`0xD75CEA` with `RCX` = the `PlayerGame` (`MOV RCX,RBX`, and `RBX`
is `RCX` from `0xD75C57`). `FUN_140d75d60` saves that to `R15`
(`MOV R15,RCX` at `0xD75D79`) and loads `PlayerManager*`:

```
RVA 0xD767A9 MOV RCX,qword ptr [0x148982c80]
RVA 0xD767B0 LEA RDX,[0x1420f76b0]
RVA 0xD767B7 CALL 0x140916a80
```

`0xD767A9 + 7 + 0x07C0C4D0 = 0x8982C80`. That call passes the
manager and a constant, not the entity. The function body has no
`MOV reg, [R15+0x30]`, and one hop through its direct callees finds
none entered with the `PlayerGame` that then loads `[it+0x30]`.

The single next decompile is `FUN_140d75d60` (`0xD75D60`, 2917
bytes), reached from `PlayerGame::vftable+0x40`. The question is
what `FUN_1400fe310` is given at `0xD76717`: whether `RDX` is the
entity from `[PlayerGame+0x30]`, or some other pointer. No call
until that argument is named.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_140d75d60` appends a global element, not the entity (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0pgi.java` through `Slot2f0pgl.java`;
reports `local/pgslot2.txt`, `local/pgslot2b.txt`,
`local/pgslot2c.txt`, `local/pgslot2d.txt`). No re-analysis, no
symbol scan, no inject, no launch, nothing written under `M:\`. No
new probe mode. No store writes the entity into a manager, so no
call frame was emitted.

`FUN_140d75d60` is 2917 bytes. `RCX` is the `PlayerGame`, saved at
entry and never used as the source of the append:

```
RVA 0xD75D79 MOV R15,RCX
```

The only call of `FUN_1400fe310` in the body is `0xD76717` (53 calls
walked, one hit). `RDX` is not the entity. It is the current element
of a counted pointer array in a static block, and `RCX` is that
block's own second vector, not a field of the `PlayerGame` and not
one of the three managers:

```
RVA 0xD766E7 LEA RDI,[0x148959f08]
RVA 0xD766EE LEA R12,[0x148959f98]
RVA 0xD766F5 MOVSXD RAX,dword ptr [RDI]
RVA 0xD766FC MOV RBX,qword ptr [RDI + 0x8]
RVA 0xD76700 LEA RSI,[RBX + RAX*0x8]
RVA 0xD76710 MOV RDX,qword ptr [RBX]
RVA 0xD76713 LEA RCX,[RDI + 0x10]
RVA 0xD76717 CALL 0x1400fe310
RVA 0xD7671C ADD RBX,0x8
```

`RDI` starts at `0x8959F08` and steps by `0x30` until `0x8959F98`,
three records. Each record is a count at `+0`, a data pointer at
`+8`, and the destination vector at `+0x10`. The loop appends every
element of the first vector onto the second, then zeroes the count
(`MOV dword ptr [RDI],R13D` at `0xD76725`, and `R13D` was zeroed at
`0xD75D72`). Nothing in those instructions loads `R15` or
`[PlayerGame+0x30]`.

The `PlayerManager*` load later in the same function does not close
the gap. It looks a player up by a constant and never takes the
entity:

```
RVA 0xD767A9 MOV RCX,qword ptr [0x148982c80]
RVA 0xD767B0 LEA RDX,[0x1420f76b0]
RVA 0xD767B7 CALL 0x140916a80
```

`0xD767A9 + 7 + 0x07C0C4D0 = 0x8982C80`. `RAX` is then read at `+0x48`
and `+0xa0` (`0xD767C4`, `0xD767C8`) and handed to `FUN_1408fa230`
with another constant (`LEA RDX,[0x1421ea6d0]` at `0xD767CF`). The
two calls after that which still hold the `PlayerGame` were opened
only to check the entity and the three managers. `FUN_140d768d0`
(617 bytes, `MOV RCX,R15` at `0xD76844`, call at `0xD76847`) saves
`RCX` to `RSI` and has no `[reg+0x30]` load and no RIP load of
`EntityManagerGame*` (`0x8982C40`), `SceneManagerGame*`
(`0x8982DD0`), `PlayerManager*` (`0x8982C80`) or `GameModule*`
(`0x8983150`). `FUN_141211f00` (273 bytes, call at `0xD7681D`) is
entered with `RCX` = `[GameModule+0x1c8]` and `RDX` left from the
lookup above; its `[RBP+0x30]` is that argument's own field, and it
loads none of those four globals either.

Miss. This function drains a static pointer array into a second
static vector. It does not write the entity into `EntityManagerGame`,
`SceneManagerGame`, `PlayerManager`, or a counted world list.

The single next decompile is `FUN_140916a80`, called at `0xD767B7`
with `RCX` = `PlayerManager*` and `RDX` = the constant at
`0x20F76B0`. The question is whether the object it returns at
`[RAX+0x48]` is a `PlayerGame`, and whether that object's `+0x30`
is then written into `PlayerManager`. No call until that return
value is named.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_140916a80` returns a player already in the manager (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0pgm.java`, `Slot2f0pgn.java`,
`Slot2f0pgo.java`; reports `local/pgslot3.txt`,
`local/pgslot3b.txt`, `local/pgslot3c.txt`). No re-analysis, no
symbol scan, no inject, no launch, nothing written under `M:\`. No
new probe mode. No store writes the entity into a manager, so no
call frame was emitted.

The call at `0xD767B7` is the fast arm of `FUN_140916a80`. The
window is inside `FUN_140d75d60`, and the two arguments are the
manager and a static key, not the entity:

```
RVA 0xD767A9 MOV RCX,qword ptr [0x148982c80]
RVA 0xD767B0 LEA RDX,[0x1420f76b0]
RVA 0xD767B7 CALL 0x140916a80
RVA 0xD767BC MOV RCX,R13
RVA 0xD767BF TEST RAX,RAX
RVA 0xD767C2 JZ 0x140d767c8
RVA 0xD767C4 MOV RCX,qword ptr [RAX + 0x48]
RVA 0xD767C8 ADD RCX,0xa0
RVA 0xD767CF LEA RDX,[0x1421ea6d0]
RVA 0xD767D6 CALL 0x1408fa230
```

`0xD767A9 + 7 + 0x07C0C4D0 = 0x8982C80`, so `RCX` is
`PlayerManager*`. `R13` is the zero set at `0xD75D72`, which is why
the null return falls through with `RCX` still zero and the next
instruction adds `0xa0` anyway.

`FUN_140916a80` is 168 bytes and returns a `longlong`. The key at
RVA `0x20F76B0` is three dwords then a null qword: index `3`, flag
`1`, mode `2`, pointer `0`. That is the fast arm, and the return is
one load:

```
RVA 0x916A9A MOVSXD RAX,dword ptr [RDX]
RVA 0x916A9D MOV RAX,qword ptr [RCX + RAX*0x8 + 0x48]
RVA 0x916AA2 RET
```

Index `3` makes the address `PlayerManager + 0x48 + 0x18`, so
`RAX` is the qword at `PlayerManager+0x60`. The other arm walks
`[PlayerManager + 0x10 + index*0x10]` and returns the element
pointer (`MOV RAX,R8` at `0x916B24`) or zero (`XOR EAX,EAX` at
`0x916B21`). Neither arm allocates, and neither writes. No vtable
and no RTTI are read on the way out, so the return is not a
`PlayerGame` created here. It is a slot the manager already holds.
The body has no `[reg+0x30]` and no RIP load of `EntityManagerGame*`
(`0x8982C40`), `SceneManagerGame*` (`0x8982DD0`), `PlayerManager*`
(`0x8982C80`) or `GameModule*` (`0x8983150`).

`[RAX+0x48]` is not a `PlayerGame` either. `0xD767C4` loads it into
`RCX` and `0xD767C8` adds `0xa0` before calling `FUN_1408fa230`.
That function is the same component-map search already named: it
binary-searches the map at `object+0xa0`. The key at `0x21EA6D0`
starts with `dword0 = 0xFFFFFFFF`, which is a type id, not an
entity. The hit is a dword at `[result+0x68]` (`0xD767DB`), compared
against a range table (`0xD767F6`, `0xD767FB`). On a hit the code at
`0xD7688F` picks one pointer out of `[record+0x10]` and jumps back
to `0xD76811`:

```
RVA 0xD768B5 MOV RAX,qword ptr [R9 + 0x10]
RVA 0xD768BC MOV RDX,qword ptr [RAX + RCX*0x8]
RVA 0xD768C0 JMP 0x140d76811
```

`0xD76811` tests that pointer and, when it is non-zero, calls
`FUN_141211f00` with `RCX = [RBX+0x1c8]`. `FUN_140d768d0`, reached
later with `RCX = R15` (the `PlayerGame` saved at `0xD75D79`), was
already checked: no `[reg+0x30]` load and no load of the three
managers. Nothing from `0xD767B7` through `0xD768C0` stores
`[return+0x48]`, `[that+0x30]`, or the `0x300` entity into
`PlayerManager`, `EntityManagerGame`, `SceneManagerGame`, a parent
pointer, or a model slot.

Miss. The lookup reads a player the manager already has and then
reads a component map off `[that+0x48]`. It does not register the
allocated entity. `FUN_140d8c2d0` is not the next step: it is the
insert already excluded. Its entry is `MOV RSI,RCX` / `MOV RBX,RDX`
(`0xD8C2D8`, `0xD8C2E4`), the first push is `FUN_1402250c0` of that
`RDX` (`0xD8C2FF`), and the body has no RIP load of `PlayerManager*`,
`EntityManagerGame*` or `SceneManagerGame*`. `RDX` is the
`PlayerGame`, not the `0x300` entity.

`FUN_140916bf0` (35 bytes), the other caller of this lookup, returns
`[player+0x48]` and nothing else. `FUN_141211f00`, reached from the
match arm, was already walked: `[RBP+0x30]` is a count on its own
argument and it loads none of the three managers.

The `PlayerGame` this function does hold (`R15`, from `MOV R15,RCX`
at `0xD75D79`) is stored once, and not into a manager:

```
RVA 0xD7646C MOV qword ptr [RBX + 0xe8],R15
RVA 0xD76473 MOV dword ptr [RBX + 0x94],0x1
RVA 0xD7647D CALL 0x140a325e0
```

`FUN_140a325e0` (318 bytes) saves `RDX` and never reads it. It loads
neither `[reg+0x30]`, `[reg+0xe8]`, nor any of the three manager
globals. The `0x300` entity is not in `R15`.

The single next decompile is `FUN_1401e5b60` (98 bytes, RVA
`0x1E5B60`). It is the function the virtual at `0xD76452` enters:
`RCX` is the TLS record or the static object at RVA `0x1FE1F00`
(vtable RVA `0x17B3148`), and `RDX` is `R12`. The body only walks
that pointer's header and tails into another slot `+0x98`
(`JMP qword ptr [RAX+0x98]` at `0x1E5B99` and `0x1E5BBB`). It loads
none of the three managers. The question is whether the function
that tail returns is given the `0x300` entity, and whether it
writes that entity into `PlayerManager*` (`0x8982C80`),
`EntityManagerGame*` (`0x8982C40`) or `SceneManagerGame*`
(`0x8982DD0`). No call until that return is named.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_1401e5b60` frees a 16-byte slab block, not the entity (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0pha.java`, `Slot2f0phb.java`,
`Slot2f0phc.java`, `Slot2f0phd.java`, `Slot2f0phe.java`,
`Slot2f0phf.java`, `Slot2f0phg.java`, `Slot2f0phh.java`; reports
`local/pgslot4.txt` through `local/pgslot4h.txt`). No re-analysis,
no symbol scan, no inject, no launch, nothing written under `M:\`.
No new probe mode. No store writes the entity into a manager, so no
call frame was emitted.

The virtual at `0xD76452` is slot `+0x98` of `AssetMemoryPool`.
`RCX` is the TLS pool at `GS:[0x58]+0x1A10`, or the static
`AssetMemoryPool` at RVA `0x1FE1F00` when the pointer is in
`[0x1FE0510, 0x1FE0518)` or the TLS slot still holds that object.
`RDX` is `R12`. The object that receives the store after the call
is not the pool:

```
RVA 0xD76449 MOV RCX,RAX
RVA 0xD7644C MOV RAX,qword ptr [RCX]
RVA 0xD7644F MOV RDX,R12
RVA 0xD76452 CALL qword ptr [RAX + 0x98]
RVA 0xD76458 MOV qword ptr [RBX + 0x98],RSI
RVA 0xD7646C MOV qword ptr [RBX + 0xe8],R15
```

`R12` is the block `FUN_1401e6100` returned at `0xD75F8D` with
`ECX = 0x10` (`0xD75F80`). Two `VMOVUPS` copy 16 bytes into it
(`0xD75FB0`, `0xD75FB9`). It is not the `0x300` entity. `R15` is the
`PlayerGame` saved at `0xD75D79`. `RBX` is the `EntitySpawnInfo`
`FUN_140a312c0` returned at `0xD76237` (vtables `0x18BEE50` and
`0x18BE338`, both `.?AVEntitySpawnInfo@@`). So `[RBX+0xe8]` is
`[EntitySpawnInfo+0xe8] = PlayerGame`. The entity is not in that
store.

`AssetMemoryPool::vftable` is RVA `0x17B3148` (COL `0x1419D3468`,
type descriptor RVA `0x2243378`, `.?AVAssetMemoryPool@@`). Slot
`+0x98` is `FUN_1401e5b60` (98 bytes, RVA `0x1E5B60`). The function
aligns `RDX` down to a 1 GiB slab, indexes the `0x40`-byte header
(`(ptr >> 0x11) * 0x40`), and follows a type byte. Type `5` walks
back `*(u16*)(header+2)` headers; type `6` walks back and then adds
`0x40`. Both arms load the owner at `[header+8]` and tail-call its
own slot `+0x98`:

```
RVA 0x1E5BB4 MOV RCX,qword ptr [R8 + 0x8]
RVA 0x1E5BB8 MOV RAX,qword ptr [RCX]
RVA 0x1E5BBB JMP qword ptr [RAX + 0x98]
```

The same jump is at `0x1E5B99` on the type-5 arm. `RDX` is never
written, so the tail is entered with `RCX` = the slab owner and
`RDX` still the 16-byte pointer. The static pool's own slot `+0x98`
is this function, so the tail is not a second copy of it. The owner
is the header of the slab the 16-byte block came from, and that
header is filled when the slab is allocated. The image copy of the
fallback pool global (`0x897A388`) is zero, so the tail target has
no vtable bytes and no RTTI to read. The body has no RIP load of
`EntityManagerGame*` (`0x8982C40`), `SceneManagerGame*`
(`0x8982DD0`) or `PlayerManager*` (`0x8982C80`).

The constructor that built the `EntitySpawnInfo` does not close the
gap. `FUN_140a312c0` (559 bytes) is called at `0xD76232` with the
`0x240`-byte block from the pool's slot `+0x88` (`0xD761FE`,
`EDX = 0x240`). It writes the two vtables and zeroes its own
fields, including `[this+0x30]` (`0xA31315`). No `[reg+0x30]` load
of an entity, no RIP load of the three managers, four calls, none
of them an insert. `FUN_140a33470` (127 bytes), called on the same
object at `0xD76485`, also loads none of the three managers and
does not read `[this+0xe8]`.

The one later read of that field is not a store of the entity
either. At `0xD765E6` the code loads `[PlayerGame+0xe8]` (the
`EntitySpawnInfo`) and copies two bytes, `[it+0x10]` and
`[it+0x11]`, into a stack record. It then takes the critical
section at `[0x8982D88]+0x30` and calls slot `+0x8` of
`[[0x8982D88]+8]+0x18`. That call gets the stack record, not the
entity, and it does not write `EntityManagerGame`,
`SceneManagerGame` or `PlayerManager`.

Miss. The tail frees the 16-byte block. Nothing on this path writes
the `0x300` entity into a manager, a parent pointer, or a model
slot.

The single next decompile is the function at slot `+0x8` of
`[[0x8982D88]+8]+0x18`, called at `0xD7668C`. `RCX` is that object
and `RDX` is the stack record at `[RSP+0x30]`, whose first qword was
set to RVA `0x18FB7A0` at `0xD76605`. The `PlayerGame` is still in
`R15`, and `[PlayerGame+0xe8]` was read two instructions earlier
(`0xD765E6`). The question is whether that slot loads
`[PlayerGame+0x30]` and writes the entity into `EntityManagerGame*`
(`0x8982C40`), `SceneManagerGame*` (`0x8982DD0`) or `PlayerManager*`
(`0x8982C80`). No call until that load is named. `FUN_140d72230` is
not the next step: it was already walked and its `+0x30` is not the
entity. `FUN_14133aa10` is not either: `0xD766C4` passes
`[GameModule+0x558]`, not the `PlayerGame`.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### The slot at `0xD7668C` is `NetReplicatedObjectManager::vftable+0x8` (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0phi.java` through `Slot2f0piu.java`;
reports `local/pgslot5.txt` through `local/pgslot6m.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. No new probe mode. No store writes the entity into a
manager, so no call frame was emitted.

The call, 25 instructions of it:

```
RVA 0xD7664C MOV RDI,qword ptr [0x148982d88]
RVA 0xD7665C LEA RBX,[RDI + 0x30]
RVA 0xD76660 MOV RCX,RBX
RVA 0xD76663 CALL qword ptr [0x14174b4a8]
RVA 0xD76676 MOV RAX,qword ptr [RDI + 0x8]
RVA 0xD7667A LEA RDX,[RSP + 0x30]
RVA 0xD7667F XOR R9D,R9D
RVA 0xD76682 MOV R8B,0xfe
RVA 0xD76685 MOV RCX,qword ptr [RAX + 0x18]
RVA 0xD76689 MOV RAX,qword ptr [RCX]
RVA 0xD7668C CALL qword ptr [RAX + 0x8]
```

`RCX` is `[[[0x8982D88]+8]+0x18]`. `RDX` is the stack record at
`[RSP+0x30]`, whose first qword was set to RVA `0x18FB7A0` at
`0xD76605`. `R8B` is the constant `0xFE`. `R9D` is 0. The `PlayerGame`
is in `R15` and is not an argument.

The global at RVA `0x8982D88` is zero in the image. The one store
that fills it with an object is `MOV [0x148982D88], RBX` at
`0xD65EC6` in `FUN_140d65890`. `RBX` there is the `0xA8` block
allocated at `0xD65D25` (`EDX = 0xA8`), and the vtable written onto
it at `0xD65DCA` is `LEA RAX, [0x14198B608]`. The Complete Object
Locator at that vtable minus 8 (`0x141B10E38`) names
`.?AVNetEntityManagerGame@@`. So the global is
`NetEntityManagerGame*`.

`[NetEntityManagerGame+8]` is filled at `0xD65DA6` from
`[[[0x89581F0]+0xF8]+0xE0]`. The object at `0x89581F0` is the one
`FUN_140d3d0d0` builds (`MOV [RCX], [0x1418F9C78]` at `0xD3D0E3`),
and that vtable names `.?AVNetManagerGame@@`. Its `+0xF8` is the
block `FUN_140d966f0` constructs at `0xD3E8CF` and stores at
`0xD3E8D7`. That constructor writes `LEA RAX, [0x1418FFD88]` at
`0xD96736`, and the COL names `.?AVStateInGame@@`. So
`[NetEntityManagerGame+8]` is `[StateInGame+0xE0]`.

`StateInGame+0xE0` is zero after `FUN_140d966f0`. The store that
fills it is `MOV [RDI+0xE0], RSI` at `0xD99376` in `FUN_140d98b70`,
and `RSI` is what `FUN_140411d80` returned at `0xD99363`. That
constructor writes `LEA RAX, [0x141849CB8]` at `0x411DA4`, COL
`.?AVNetReplicatedObjectManager@@`. It also stores its own `RDX` at
`[this+0x18]` (`0x411DA0`), and that `RDX` is
`[[StateInGame+0xA0]+0x3C58]`. `[StateInGame+0xA0]` is the object
built at `0xD98D5A` with vtable `0x1988070`, COL
`.?AVClientIdentityManagerGame@@`.

Slot `+0x8` of `NetReplicatedObjectManager::vftable` (RVA
`0x1849CB8`) is `FUN_140413df0` (522 bytes, RVA `0x413DF0`). It
reads a signed word at `[RDX+8]` (`0x413E0C`) and returns unless
that word is `0xFFFF8065`. The record passed here has the word
`0x27` (`0xD765F9`), so the call returns at `0x413E2C`. Past that
gate the only thing it does with a pointer is load `[this+0x10]`,
which the constructor set to the byte `R8B` (`0x411D8E`), and call
that object's slot `+0x8` (`0x413F04`) with a stack record whose
first qword is RVA `0x18499E8`. The body has no `[reg+0x30]` load of
an entity and no RIP load of `EntityManagerGame*` (`0x8982C40`),
`SceneManagerGame*` (`0x8982DD0`) or `PlayerManager*` (`0x8982C80`).
The `0x30` hits are `LEA RCX, [RSP+0x30]`.

Miss. The slot posts a `NetMsgPlayerSpawn` (RVA `0x18FB7A0`, COL
`.?AVNetMsgPlayerSpawn@@`) into a replicated-object manager. Nothing
on this path writes the `0x300` entity into a manager, a parent
pointer, or a model slot.

The single next decompile is `FUN_141211f00`, called at `0xD7681D`.
`RCX` is `[GameModule+0x1C8]` (`0xD76816`, `RBX` is `GameModule*`
from `0xD7669D`). `RDX` is the pointer the search at `0xD767F6`
selected, or zero when nothing matched. `R15` still holds the
`PlayerGame`, and `[PlayerGame+0x30]` is the entity. The question is
whether `FUN_141211f00` loads `[PlayerGame+0x30]` and writes the
entity into `EntityManagerGame*` (`0x8982C40`), `SceneManagerGame*`
(`0x8982DD0`) or `PlayerManager*` (`0x8982C80`). No call until that
load is named. `FUN_140916a80` at `0xD767B7` is not the next step: it
was already walked and returns a player already stored.
`FUN_140d768d0` at `0xD76847` is not either: its `RCX` is the
`PlayerGame` and its body loads none of the three managers.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_141211e00` clears a vector, it does not take the entity (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0piw.java` through `Slot2f0pjd.java`;
reports `local/pgslot7.txt` through `local/pgslot7h.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. No new probe mode. No store writes the entity into a
manager, so no call frame was emitted.

`FUN_141211e00` is 237 bytes. `RCX` is `[GameModule+0x1C8]`, saved
to `RSI` at `0x1211E0A`. `RDX`, the search hit, is never read. The
body walks the counted pointer array that object already holds and
then frees the array:

```
RVA 0x1211E06 MOVSXD RAX,dword ptr [RCX + 0x10]
RVA 0x1211E0A MOV RSI,RCX
RVA 0x1211E17 MOV RDI,qword ptr [RCX + 0x18]
RVA 0x1211E1B LEA RBP,[RDI + RAX*0x8]
RVA 0x1211E30 MOV RCX,qword ptr [0x148982a88]
RVA 0x1211E3A MOV RBX,qword ptr [RDI]
RVA 0x1211E3D MOV RDX,RBX
RVA 0x1211E43 CALL qword ptr [RAX + 0x40]
RVA 0x1211E53 CALL qword ptr [RAX + 0x38]
RVA 0x1211E56 ADD RDI,0x8
RVA 0x1211E64 MOV RDX,qword ptr [RSI + 0x18]
RVA 0x1211ECF CALL qword ptr [RAX + 0x98]
RVA 0x1211ED7 LEA RCX,[RSI + 0x8]
RVA 0x1211EDB MOV qword ptr [RSI + 0x18],RAX
RVA 0x1211EDF MOV qword ptr [RSI + 0x10],RAX
RVA 0x1211EE8 JMP 0x1406aecf0
```

`0x148982A88` is the same global the caller uses at `0x1211F1F`.
The slot `+0x98` at `0x1211ECF` is the `AssetMemoryPool` free
already named (`FUN_1401e5b60`), and `RDX` there is
`[[GameModule+0x1C8]+0x18]`, the array pointer, not the entity.
The tail at `0x1211EE8` is `FUN_1406aecf0` (131 bytes) with `RCX` =
`[GameModule+0x1C8]+0x8`. It calls that object's slots `+0x40` and
`+0x28` and then masks `[first+0x8]`. Its two `+0x30` hits are the
saved `RDI` (`MOV qword ptr [RSP+0x30],RDI` at `0x6AED15`). It
loads none of the three manager globals.

The two stores are both inside the object the function was given:

```
RVA 0x1211EDB MOV qword ptr [RSI + 0x18],RAX
RVA 0x1211EDF MOV qword ptr [RSI + 0x10],RAX
```

`RAX` is zero (`XOR EAX,EAX` at `0x1211ED5`). So the function
zeroes the count and the data pointer of `[GameModule+0x1C8]`.
There is no store of the `0x300` entity into `EntityManagerGame`,
`SceneManagerGame`, `PlayerManager`, a parent pointer, or a model
slot. There is no load of `[PlayerGame+0x30]`.

`FUN_140d75d60` does not hide that load after the call either. The
function is 2917 bytes and uses `R15` 26 times. `R15` is the
`PlayerGame` (`MOV R15,RCX` at `0xD75D79`). None of those 26 is
`[R15+0x30]`. After `0xD7681D` there are three calls, and the
function returns at `0xD7688E`:

```
RVA 0xD76844 MOV RCX,R15
RVA 0xD76847 CALL 0x140d768d0
RVA 0xD7685E MOV RCX,qword ptr [R15 + 0x1d8]
RVA 0xD7686A CALL 0x1402084c0
RVA 0xD7686F LEA RCX,[RSP + 0x130]
RVA 0xD7687E CALL 0x140107850
RVA 0xD7688E RET
```

`FUN_140d768d0` was already walked: no `[reg+0x30]` of the entity
and no load of the three managers. `FUN_1402084c0` (87 bytes) and
`FUN_140107850` (189 bytes) have no such load either. The `+0x1d8`
pointer is not the entity.

The one place this function does hold `EntityManagerGame*` together
with the `PlayerGame` is earlier, and it does not load the entity
either. `FUN_1409145c0` (508 bytes) is called at `0xD75DDA` with
`RCX` = `R15`:

```
RVA 0xD75DD3 MOV RCX,R15
RVA 0xD75DDA CALL 0x1409145c0
```

```
RVA 0x9145E9 MOV RDI,qword ptr [0x148982c40]
RVA 0x91478A MOV RCX,qword ptr [R14 + 0x48]
RVA 0x9147A7 MOV RDX,qword ptr [R14 + 0x60]
RVA 0x9147AB CALL 0x140950ba0
```

The bytes at `0x9145E9` are `48 8B 3D 50 E6 06 08`, and
`0x9145E9 + 7 + 0x0806E650 = 0x8982C40`, so `RDI` is
`EntityManagerGame*`. `R14` is the `PlayerGame`. The call passes
`[PlayerGame+0x48]` and `[PlayerGame+0x60]`, not
`[PlayerGame+0x30]`. `FUN_140950ba0` (129 bytes) forwards those two
to `FUN_14094a6f0` at `0x950BFE`, and that function (166 bytes)
writes one field:

```
RVA 0x94A6F6 MOV RAX,qword ptr [RCX + 0x188]
RVA 0x94A700 CMP RAX,RDX
RVA 0x94A709 MOV qword ptr [RCX + 0x188],RDX
```

`RCX` is `[PlayerGame+0x48]` and `RDX` is `[PlayerGame+0x60]`. The
store is `[[PlayerGame+0x48]+0x188] = [PlayerGame+0x60]`. Neither
register is the `0x300` entity, and neither function loads
`[reg+0x30]` of it. `[PlayerGame+0x48]` is the object
`FUN_140916bf0` returns, already named, not the entity at `+0x30`.

Miss. `FUN_141211e00` drops the vector at `[GameModule+0x1C8]` and
never sees the entity.

The single next decompile is `FUN_1409145c0`'s use of the manager
it already holds. `RDI` is `EntityManagerGame*` from `0x9145E9`,
and the body indexes `[RDI+0x1d8]` (`0x914648`) and
`[RDI+0x1e4]` (`0x91463A`) but never reads `[PlayerGame+0x30]`.
The question is whether any element of `[EntityManagerGame+0x1d8]`
is the `0x300` entity, and whether the call at `0x9147AB` is the
only write that array receives. No call until that element is
named. `FUN_140d72230` at `0xD76545` is not the next step: it was
already walked and its `+0x30` is a saved `RBP`. `FUN_140d768d0`
at `0xD76847` is not either.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `[EntityManagerGame+0x1d8]` is a 0x20 hash slot, not the entity (2026-10-22)

Offline only. `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0pjf.java` through `Slot2f0pjl.java`;
reports `local/pgslot8.txt` through `local/pgslot8g.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. No new probe mode. The function only reads the array,
so no call frame was emitted.

The call at `0xD75DDA` is one argument. Twenty instructions before it
compute a pointer into `RDX`, store it at `[PlayerGame+0x68]`, and
then pass the `PlayerGame` in `RCX`:

```
RVA 0xD75DAA MOV RDX,qword ptr [RBX + 0x40]
RVA 0xD75DB3 MOV RAX,qword ptr [RBX + 0x28]
RVA 0xD75DB7 MOV RDX,qword ptr [RAX + 0x28]
RVA 0xD75DBB MOV RAX,qword ptr [0x148982fe8]
RVA 0xD75DC2 MOV RCX,qword ptr [RAX + 0x68]
RVA 0xD75DC6 MOV RAX,qword ptr [RCX + 0x88]
RVA 0xD75DCD CMP qword ptr [R15 + 0x60],RAX
RVA 0xD75DD1 JNZ 0x140d75ddf
RVA 0xD75DD3 MOV RCX,R15
RVA 0xD75DD6 MOV qword ptr [R15 + 0x68],RDX
RVA 0xD75DDA CALL 0x1409145c0
```

`R15` is the `PlayerGame` (`MOV R15,RCX` at `0xD75D79`). The call is
taken only when `[PlayerGame+0x60]` equals `[[0x8982FE8]+0x68]+0x88`.
`RDX` is not an argument of `FUN_1409145c0`.

`FUN_1409145c0` is 508 bytes and returns void. It takes the
`PlayerGame` and, when `[PlayerGame+0x68]` differs from
`[PlayerGame+0x60]`, copies `+0x68` over `+0x60` and then walks the
manager:

```
RVA 0x9145C7 PUSH R14
RVA 0x9145CD MOV RAX,qword ptr [RCX + 0x68]
RVA 0x9145D1 MOV R14,RCX
RVA 0x9145D4 MOV RDX,qword ptr [RCX + 0x60]
RVA 0x9145D8 CMP RAX,RDX
RVA 0x9145DB JZ 0x1409147b5
RVA 0x9145E5 MOV qword ptr [RCX + 0x60],RAX
RVA 0x9145E9 MOV RDI,qword ptr [0x148982c40]
```

The bytes at `0x9145E9` are `48 8B 3D 50 E6 06 08`.
`0x9145E9 + 7 + 0x0806E650 = 0x8982C40`, so `RDI` is
`EntityManagerGame*`. The array is a hash table of `0x20`-byte
records, not a pointer vector of entities. Count is the dword at
`+0x1e4`, data is the qword at `+0x1d8`, stride is `0x20`. The lock
is the `SRWLOCK` at `+0x1e8`:

```
RVA 0x91460D LEA R12,[RDI + 0x1e8]
RVA 0x91463A MOVSXD R15,dword ptr [RDI + 0x1e4]
RVA 0x914648 MOV RAX,qword ptr [RDI + 0x1d8]
RVA 0x914651 CMP dword ptr [RAX + 0x18],0x0
RVA 0x91465C ADD RAX,0x20
RVA 0x914691 MOV RAX,qword ptr [RDI + 0x1d8]
RVA 0x914698 MOVSXD RCX,EBX
RVA 0x91469B SHL RCX,0x5
RVA 0x91469F MOV RSI,qword ptr [RCX + RAX*0x1 + 0x10]
```

A record is 32 bytes. The occupied test is the dword at
`[record+0x18]`. The pointer the walk uses is `[record+0x10]`, and
that pointer is not the `0x300` entity. The body treats it as an
object that already owns the entity lock and the listener list:

```
RVA 0x9146A4 MOV RBP,qword ptr [RSI + 0x70]
RVA 0x914708 LEA RDX,[RSI + 0x298]
RVA 0x914712 LEA RCX,[RSI + 0x2c0]
RVA 0x91471E CALL 0x14097fbe0
```

`+0x298` and `+0x2c0` are the lock and the listener list already
named on a live entity. `RSI` is that entity. The record only holds
a pointer to it. The function never reads `[PlayerGame+0x30]`.

It also never writes a record. The only store of a pointer is
`[PlayerGame+0x60] = [PlayerGame+0x68]` at `0x9145E5`. The walk
compares `[record+0x18]` and reads `[record+0x10]`; it does not
store either. The tail call at `0x9147AB` is the one already walked:
`FUN_140950ba0` writes `[[PlayerGame+0x48]+0x188]`, not this array.

The writer of an element is `FUN_140911320` (634 bytes), called from
`FUN_140901310` at `0x901583` with `RCX = [EntityManagerGame+0x1d8]`.
A new table is `0x40` bytes (`CALL 0x1401e6100` at `0x91134F`, stored
at `0x911358`), and a full record is written as four fields. The
bytes at `0x9114DD` are `48 8B 44 24 60`:

```
RVA 0x9114D8 VMOVUPS XMM0,xmmword ptr [RBP]
RVA 0x9114E2 VMOVUPS xmmword ptr [RBX],XMM0
RVA 0x9114DD MOV RAX,qword ptr [RSP + 0x60]
RVA 0x9114F0 MOV qword ptr [RBX + 0x10],RCX
RVA 0x9114F4 MOV dword ptr [RBX + 0x18],ESI
RVA 0x9114F7 INC dword ptr [R15 + 0x8]
```

`[record+0]` and `[record+8]` are the two-qword hash key copied from
`R9`. The dword at `[record+0x18]` is the hash. The pointer at
`[record+0x10]` is `*param_5`, the fifth argument, read through
`[RSP+0x60]`. `FUN_140911320` pushes three registers and subtracts
`0x20`, so that slot is the caller's `[RSP+0x20]`. At the call in
`FUN_140901310` the fifth argument is the entity:

```
RVA 0x90131F MOV qword ptr [RSP + 0x10],RDX
RVA 0x901324 PUSH RBP
RVA 0x90132D LEA RBP,[RSP + -0x2f]
RVA 0x901339 MOV R15,qword ptr [0x148982c40]
RVA 0x901346 MOV RSI,RDX
RVA 0x901349 OR.LOCK dword ptr [RDX + 0x98],0x10000000
RVA 0x901354 MOV RAX,qword ptr [RBP + 0x67]
RVA 0x901358 VMOVUPS XMM0,xmmword ptr [RAX + 0xd8]
RVA 0x9013AF MOV RBX,qword ptr [RBP + 0x67]
RVA 0x90149E LEA RCX,[R15 + 0xb0]
RVA 0x9014A5 MOV RDX,RBX
RVA 0x9014A8 CALL 0x140ad3b10
RVA 0x90154E LEA R9,[RBX + 0x8]
RVA 0x901557 LEA RAX,[RBP + 0x67]
RVA 0x901563 MOV qword ptr [RSP + 0x20],RAX
RVA 0x901571 LEA RCX,[R15 + 0x1d8]
RVA 0x901583 CALL 0x140911320
```

`RDX` of `FUN_140901310` is saved at `[RSP+0x10]` before `PUSH RBP`.
After that push, `LEA RBP, [RSP-0x2f]` makes `[RBP+0x67]` the same
slot: `-0x2f + 0x67 = 0x38`, and the push moved the saved `RDX` from
`[RSP+0x10]` to `[RSP+0x18]`, which is `[RBP+0x67]`. The bytes are
`48 89 54 24 10` at `0x90131F` and `48 8D 6C 24 D1` at `0x90132D`.
The load at `0x901354` then copies `[RDX+0xD8]` and `[RDX+0xF0]`,
and `0x901349` sets a bit in `[RDX+0x98]`. `RDX` is the entity. The
store `FUN_140911320` makes is `[record+0x10] =` the address of that
saved slot, and the qword there is the entity. The same entity is
also passed to `FUN_140ad3b10` at `0x9014A8` with
`RCX = [EntityManagerGame+0xB0]`, a different container.

`FUN_140e97160` (1910 bytes) also writes `[EntityManagerGame+0x1d8]`,
but it writes the data pointer, not an element. `R12` is zeroed at
`0xE97176` and again at `0xE97736` and `0xE977DF`, and the store is
`MOV qword ptr [RBX+0x1d8], R12` at `0xE978C3` after the old buffer
is freed. It clears the table.

Miss. An element of `[EntityManagerGame+0x1d8]` is a `0x20` hash
record, not the entity. `[record+0x10]` points at a qword that holds
the entity, and `FUN_1409145c0` only reads that qword. It does not
write one.

The single next decompile is `FUN_140901310`. It is the function
that writes the record, and its `RDX` is the entity whose `+0xD8`
and `+0xF0` it copies before the insert. The question is whether
`RDX` is the `0x300` `ControlledEntity`, which means the copy at
`0x901358` is the entity's own `+0xD8`. One caller to name it from
is `FUN_14099f9b0` at `0x99FDB4`: `RDX` there is `[R14+0x50]-0x20`
and `[RSP+0x20]` is zero. Do not return to the quest-marker chain,
the HunterAbilityDeckManager chain, the AssetMemoryPool path, the
NetMsgPlayerSpawn path, or the `GameModule+0x1C8` search.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `FUN_140901310` inserts the entity; the stored pointer is the entity (2026-10-22)

`FUN_140901310` is 1322 bytes. `RDX` is the `0x300` `ControlledEntity`.
The pointer stored at `[record+0x10]` is that entity, not a field of
it. No probe.

The previous note's stack arithmetic was wrong. The prologue saves
four registers before five pushes, so the saved `RDX` is not
`[RBP+0x67]`:

```
RVA 0x901310 MOV qword ptr [RSP + 0x8],RBX
RVA 0x901315 MOV qword ptr [RSP + 0x18],RSI
RVA 0x90131A MOV qword ptr [RSP + 0x20],RDI
RVA 0x90131F MOV qword ptr [RSP + 0x10],RDX
RVA 0x901324 PUSH RBP
RVA 0x901325 PUSH R12
RVA 0x901327 PUSH R13
RVA 0x901329 PUSH R14
RVA 0x90132B PUSH R15
RVA 0x90132D LEA RBP,[RSP + -0x2f]
RVA 0x901332 SUB RSP,0xc0
RVA 0x901339 MOV R15,qword ptr [0x148982c40]
RVA 0x901346 MOV RSI,RDX
RVA 0x901349 OR.LOCK dword ptr [RDX + 0x98],0x10000000
```

The bytes at `0x90132D` are `48 8D 6C 24 D1`. After the five pushes
the incoming `[RSP+0x10]` (the saved `RDX`) sits at `[RSP+0x38]`,
which is `[RBP+0x67]`. `[RBP+0x7f]` is the fifth argument, the
incoming `[RSP+0x28]`. `R15` is `EntityManagerGame*` from the global
at RVA `0x8982C40`. `RSI` is `RDX` and is never replaced.

`+0xD8` is copied from the entity into the entity. `RAX` is reloaded
from the same saved slot, so it is still `RDX`:

```
RVA 0x901354 MOV RAX,qword ptr [RBP + 0x67]
RVA 0x901358 VMOVUPS XMM0,xmmword ptr [RAX + 0xd8]
RVA 0x901360 VMOVUPS xmmword ptr [RAX + 0x118],XMM0
RVA 0x901368 VMOVSD XMM1,qword ptr [RAX + 0xe8]
RVA 0x901370 LEA RCX,[RAX + 0xf0]
RVA 0x901377 LEA RDX,[RAX + 0x130]
RVA 0x90137E VMOVSD qword ptr [RAX + 0x128],XMM1
RVA 0x90138B VMOVUPS YMM0,ymmword ptr [RCX]
RVA 0x90138F VMOVUPS ymmword ptr [RDX],YMM0
RVA 0x901393 MOV EAX,dword ptr [RCX + 0x20]
RVA 0x901396 MOV dword ptr [RDX + 0x20],EAX
```

That is `[entity+0xD8]` and `[entity+0xE0]` into `[entity+0x118]`,
`[entity+0xE8]` into `[entity+0x128]`, and the 0x24 bytes at
`[entity+0xF0]` into `[entity+0x130]`. It is a copy of the entity's
own position, not a pointer. The constructor zeroes those three
qwords, so a fresh entity already has them, and the copy does not
require `+0xD8` to be non-null. The function does not test it.

The hash key is the entity's own UUID, and the listener post uses the
entity's own lock:

```
RVA 0x9013BE MOV RCX,qword ptr [RBX + 0xc8]
RVA 0x9014C9 MOV RDX,qword ptr [RBX + 0x70]
RVA 0x901517 LEA RCX,[RBX + 0x2c0]
RVA 0x901521 LEA RDX,[RBX + 0x298]
RVA 0x90152C CALL 0x14097fbe0
RVA 0x90154E LEA R9,[RBX + 0x8]
```

`+0x298` and `+0x2c0` are the lock and the listener list already named
on a live entity. `[entity+0xC8]` is the post target of
`MsgEntityTeleported`; the call at `0x9013EE` is skipped when it is
null. `[entity+0x70]` is tested the same way before `FUN_14097faf0`.
Neither has to be non-null for the insert.

The call at `0x901583`. `RBX` is the entity. `R14` is zero. The fifth
argument is the address of the saved `RDX` slot, whose qword is the
entity:

```
RVA 0x90154E LEA R9,[RBX + 0x8]
RVA 0x901552 MOV byte ptr [RSP + 0x30],R14B
RVA 0x901557 LEA RAX,[RBP + 0x67]
RVA 0x90155B MOV byte ptr [RSP + 0x28],R14B
RVA 0x901560 MOV R8,R14
RVA 0x901563 MOV qword ptr [RSP + 0x20],RAX
RVA 0x901568 CRC32 R8,qword ptr [R9]
RVA 0x901571 LEA RCX,[R15 + 0x1d8]
RVA 0x901578 CRC32 R8,qword ptr [R9 + 0x8]
RVA 0x90157F LEA RDX,[RBP + -0x39]
RVA 0x901583 CALL 0x140911320
```

| register | of `FUN_140901310` | of `FUN_140911320` |
|---|---|---|
| `RCX` | unused | `&[EntityManagerGame+0x1d8]` |
| `RDX` | the entity | `&local`, the out iterator |
| `R8` | a mode dword | the CRC32 of `[entity+0x8]` and `[entity+0x10]` |
| `R9` | a second mode dword | `&[entity+0x8]`, the 16-byte key |
| `[RSP+0x20]` | a fifth pointer, unused by the insert | `&saved_RDX`, and `[saved_RDX]` is the entity |
| `[RSP+0x28]` | | 0 |
| `[RSP+0x30]` | | 0 |

Inside `FUN_140911320` the store is a dereference, not the address of
the slot. The bytes at `0x9114DD` are `48 8B 44 24 60`:

```
RVA 0x9114D8 VMOVUPS XMM0,xmmword ptr [RBP]
RVA 0x9114DD MOV RAX,qword ptr [RSP + 0x60]
RVA 0x9114E2 VMOVUPS xmmword ptr [RBX],XMM0
RVA 0x9114ED MOV RCX,qword ptr [RAX]
RVA 0x9114F0 MOV qword ptr [RBX + 0x10],RCX
RVA 0x9114F4 MOV dword ptr [RBX + 0x18],ESI
```

`[RSP+0x60]` is the caller's `[RSP+0x20]`, so `RAX` is `&saved_RDX`
and `RCX` is the entity. `[record+0]` and `[record+0x8]` are
`[entity+0x8]` and `[entity+0x10]`. `[record+0x18]` is the hash.
`[record+0x10]` is the entity itself.

The insert is fillable from the global at RVA `0x8982C40` and an
entity we allocate. It is not emitted. The frame is
`FUN_140901310(RCX unused, RDX = entity, R8 = 1, R9 = 0, [RSP+0x20] = 0)`.
`R8 = 1` is the path every caller that has just constructed an entity
takes (`LEA R8D,[R9+0x1]` at `0xB22A45`, `0xB28919`, `0xB88156`,
`0xE8DF64`, `0xE8E29A`, `0x1325246`), and that value skips the
`[entity+0x68]` read at `0x901426`. `R9 = 0` skips the virtual call at
`0x901416`. `[entity+0xD8]` is copied and does not have to be
non-null. `[entity+0xC8]` and `[entity+0x70]` are tested and skipped
when null. What the call does use is `[entity+0x8]` and `[entity+0x10]`
as the UUID key, `[entity+0x98]` for the two locked bit sets, and
`[entity+0x298]` plus `[entity+0x2c0]` for the
`MsgAddedToEntityManager` post. A duplicate UUID hits the fatal path
at `0x901596` (`0xdeadca7` at `0x901647`).

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped.

### `--register-entity` not emitted: a zero UUID is inserted, and the live key is fatal (2026-10-22)

Offline only. Two `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/Slot2f0pjp.java`, `Slot2f0pjq.java`,
`Slot2f0pjr.java`; reports `local/pgslot9b.txt` through
`local/pgslot9d.txt`). No re-analysis, no symbol scan, no inject, no
launch, nothing written under `M:\`. `--register-entity` was not
added. The probe sources are unchanged.

The call frame from the previous section is still the one that would
be emitted, and it was not:

`FUN_140901310(RCX unused, RDX = the 0x300 ControlledEntity, R8 = 1, R9 = 0, [RSP+0x20] = 0)`.

`R8 = 1` skips the `[entity+0x68]` read. The `VMOVUPS` at `0x901358`
copies `[entity+0xD8]` into `[entity+0x118]` and does not test it.
The insert is `FUN_140911320` at `0x901583` with
`RCX = &[EntityManagerGame+0x1d8]`. `EntityManagerGame*` is the
global at RVA `0x8982C40`. The store at `0x9114ED` / `0x9114F0` is
`MOV RCX,[RAX]` / `MOV [RBX+0x10],RCX`, and `RAX` is
`LEA RAX,[RBP+0x67]` at `0x901557`, the saved `RDX` slot, so the
qword written is the entity.

A freshly constructed `ControlledEntity` has `[entity+0x8]` and
`[entity+0x10]` zero. That pair is not rejected. `FUN_140901310`
hashes it with no test:

```
RVA 0x90154E LEA R9,[RBX + 0x8]
RVA 0x901568 CRC32 R8,qword ptr [R9]
RVA 0x901578 CRC32 R8,qword ptr [R9 + 0x8]
RVA 0x901583 CALL 0x140911320
```

`FUN_140911320` then compares the whole 16-byte key, zeros included,
against an occupied slot (`CMP [RBX+0x8], R9` / `CMP [RBX], R8` at
`0x911470` / `0x911477`, and the same pair at `0x911537` / `0x911542`).
An empty slot is `[record+0x18] == 0` (`0x9114C5`), which falls
through to the write at `0x9114D8`. A zero key with the high bit set
on the hash is a normal occupied record. It is inserted.

The same key a second time is not a quiet miss. After the call,
`FUN_140901310` reads the iterator status the insert wrote. The
bytes after the call are `4C 8B 6D 67 44 39 75 D7`:

```
RVA 0x901583 CALL 0x140911320
RVA 0x901588 MOV R13,qword ptr [RBP + 0x67]
RVA 0x90158C CMP dword ptr [RBP + -0x29],R14D
RVA 0x901590 JNZ 0x140901652
```

`R14` is zero. `[RBP-0x29]` is the dword the insert stores at
`[out+0x10]` (`MOV dword ptr [R14+0x10], 0` at `0x91158D` when the
key is already present, `2` at `0x9114FB` when it inserts). The
taken arm of that compare reaches:

```
RVA 0x901647 MOV dword ptr [0x0],0xdeadca7
```

The decompiler prints that arm as
`Entity could not be added to the EntityManager because an Entity with the same UUID is already in the EntityManager`.
The hash uses `[entity+0x8]` and `[entity+0x10]` as an identity that
must be unique. Copying the two qwords from the live player's entity
(`[PlayerGame+0x30]` of the one player already in `PlayerManager`)
would be that duplicate, so it was not copied and no UUID was
invented.

`FUN_140949060` (3511 bytes) is the call immediately before
`FUN_140901310` in `FUN_140b22790` (`CALL 0x140949060` at `0xB22A32`,
then `MOV RDX,RBX` / `LEA R8D,[R15+0x1]` / `CALL 0x140901310` at
`0xB22A3D` / `0xB22A45` / `0xB22A49`). Its first 80 instructions do
not store `[RCX+0x8]`. That is the next decompile: whether
`FUN_140949060` writes `[entity+0x8]` and `[entity+0x10]`, and from
which argument.

### `FUN_140949060` reads the UUID; it does not write it (2026-10-22)

`FUN_140949060` does not write `[entity+0x8]` or `[entity+0x10]`.
The whole 3511-byte body was walked. The only 16-byte touch of the
entity is a read, `VMOVUPS XMM6,xmmword ptr [RDI + 0x8]` at
`0x949341`, copied to a stack slot at `0x949346`. `RDI` is the
incoming `RCX` (`MOV RDI,RCX` at `0x949093`). The two
`MOV qword ptr [RCX+0x8],RAX` / `MOV qword ptr [RCX+0x10],RAX` pairs
at `0x949750` / `0x949754` and `0x9497B0` are a 0x20 record in a
side array: `RCX` there is `ADD RCX,RAX` after `SHL RCX,0x5` of a
count at `[RBX+0x58]`, and `RAX` is zeroed by `XOR EAX,EAX` at
`0x94974E`. Neither is the entity. No probe.

The call frame at `0xB22A32`. `RCX` is the entity:

```
RVA 0xB2297B MOV RBX,RAX
RVA 0xB229FC MOV RCX,RBX
RVA 0xB229FF MOVZX EAX,byte ptr [RDI + 0x30a]
RVA 0xB22A06 MOV byte ptr [RBX + 0x30a],AL
RVA 0xB22A0C MOVZX EAX,byte ptr [RDI + 0x30b]
RVA 0xB22A13 MOV byte ptr [RBX + 0x30b],AL
RVA 0xB22A19 MOV EAX,dword ptr [RDI + 0x30c]
RVA 0xB22A1F MOV dword ptr [RBX + 0x30c],EAX
RVA 0xB22A25 MOVZX EAX,byte ptr [RDI + 0x538]
RVA 0xB22A2C MOV byte ptr [RBX + 0x538],AL
RVA 0xB22A32 CALL 0x140949060
RVA 0xB22A37 XOR R15D,R15D
RVA 0xB22A3A XOR R9D,R9D
RVA 0xB22A3D MOV RDX,RBX
RVA 0xB22A40 MOV qword ptr [RSP + 0x20],R15
RVA 0xB22A45 LEA R8D,[R15 + 0x1]
RVA 0xB22A49 CALL 0x140901310
```

`RDX` and `R8` of `FUN_140949060` are not set in this window. `RDX`
is still the `XOR EDX,EDX` at `0xB229F4`, so the second argument is
zero. `R8` is the stack address left by `LEA R8,[RBP + -0x39]` at
`0xB2296A`, the 16-byte buffer the caller just minted. The function
takes two arguments (`FUN_140949060(longlong *param_1, longlong
param_2)`); `R8` is unused. `param_1[1]` and `param_1[2]` are read
into locals and never stored back.

`RBX` is the entity because it is the return of the call just above:

```
RVA 0xB22949 LEA RDX,[RDI + 0x8]
RVA 0xB2294D MOV R9D,0x4
RVA 0xB22953 LEA R8,[RBP + 0x67]
RVA 0xB22957 LEA RCX,[RBP + -0x39]
RVA 0xB2295B CALL 0x14011b2b0
RVA 0xB22960 LEA RDX,[RDI + 0xd8]
RVA 0xB22967 MOV RCX,R14
RVA 0xB2296A LEA R8,[RBP + -0x39]
RVA 0xB2296E CALL 0x140986bc0
RVA 0xB22973 MOV R13D,dword ptr [RBP + 0x67]
RVA 0xB2297B MOV RBX,RAX
```

`FUN_140986bc0` (407 bytes) is the function that writes the key. It
does not insert into the hash table at `[EntityManagerGame+0x1d8]`.
It allocates the entity, copies position, then stores the 16 bytes:

```
RVA 0x986BD4 MOV RAX,qword ptr [RCX]
RVA 0x986BD7 MOV RBP,R8
RVA 0x986BDA MOV RBX,RDX
RVA 0x986BDD MOV RSI,RCX
RVA 0x986BE0 CALL qword ptr [RAX + 0x20]
RVA 0x986BE6 CALL 0x1402034a0
RVA 0x986BF4 CALL 0x14092ff60
RVA 0x986BF9 VMOVUPS XMM0,xmmword ptr [RBX]
RVA 0x986BFD VMOVUPS xmmword ptr [RDI + 0x118],XMM0
RVA 0x986C3B VMOVUPS XMM0,xmmword ptr [RBP]
RVA 0x986C46 VMOVUPS xmmword ptr [RDI + 0x8],XMM0
RVA 0x986C51 CALL qword ptr [RAX + 0x28]
RVA 0x986D30 MOV RAX,RDI
RVA 0x986D56 RET
```

`RDI` is the pointer returned by `FUN_1402034a0` (the same factory
the allocator uses) and is the value returned in `RAX`. `[RBP]` is
the third argument, the 16 bytes at `[RBP-0x39]`. The store is one
`VMOVUPS`, so it writes `[entity+0x8]` and `[entity+0x10]` together.
The decompiler prints it as `plVar6[1] = *param_3; plVar6[2] =
param_3[1];`.

Those 16 bytes are not a counter, not a GUID call, and not a copy of
another entity's key. `FUN_14011b2b0` (280 bytes) hashes them.
`RCX` is the out buffer. `RDX` is `&[RDI+0x8]`, the parent entity's
own 16-byte key (`RDI` is the incoming `RCX` of `FUN_140b22790`,
`MOV RDI,RCX` at `0xB227A4`). `R8` is `&[RBP+0x67]`, a dword set to
zero at `0xB228FD` (`XOR R15D,R15D` at `0xB227CA`, then
`MOV dword ptr [RBP+0x67],R15D`). `R9` is `4`, the byte count:

```
RVA 0x11B2C2 VMOVUPS XMM0,xmmword ptr [RDX]
RVA 0x11B2D0 MOV dword ptr [RBP + -0x15],0x67452301
RVA 0x11B2DA MOV dword ptr [RBP + -0x11],0xefcdab89
RVA 0x11B2E4 MOV dword ptr [RBP + -0xd],0x98badcfe
RVA 0x11B2EF MOV dword ptr [RBP + -0x9],0x10325476
RVA 0x11B2FE VMOVUPS xmmword ptr [RBP + 0x7],XMM0
RVA 0x11B303 CALL 0x1400f44b0
RVA 0x11B398 VMOVUPS XMM0,xmmword ptr [RBP + -0x29]
RVA 0x11B39D VMOVUPS xmmword ptr [RBX],XMM0
RVA 0x11B3A5 AND byte ptr [RBX + 0x8],0x3f
RVA 0x11B3A9 AND AL,0xf
RVA 0x11B3AB OR AL,0x30
RVA 0x11B3AD OR byte ptr [RBX + 0x8],0x80
RVA 0x11B3B1 MOV byte ptr [RBX + 0x6],AL
```

`0x67452301`, `0xEFCDAB89`, `0x98BADCFE`, `0x10325476` are the MD5
initial state. `FUN_1400f44b0` (209 bytes) folds the 4-byte index
into that state; it does not store through the data pointer. The
digest is then patched: byte 6 becomes `(byte & 0xF) | 0x30` and
byte 8 becomes `(byte & 0x3F) | 0x80`. That is a UUID version nibble
and the RFC variant bits. Nothing increments a process-wide counter.

Two entities from this path differ when the 4-byte input differs.
The index is `[RBP+0x67]`, and the caller writes it from the match
it found:

```
RVA 0xB2292E LEA R8D,[RAX + 0x1]
RVA 0xB22932 MOVSX EAX,word ptr [RCX + 0x2]
RVA 0xB22936 CMP EAX,R8D
RVA 0xB22939 JZ 0x140b22949
RVA 0xB22973 MOV R13D,dword ptr [RBP + 0x67]
RVA 0xB2298A LEA R12D,[R13 + 0x1]
RVA 0xB229B6 MOV dword ptr [RBX + 0x480],R12D
```

The loop walks a 0x40-stride array at `[RBX+0xD0]`, count
`[RBX+0xC8]`, and the hit stores its 1-based slot in `[RBP+0x67]`.
`[entity+0x480]` then receives that slot plus one. Same parent UUID
and a different slot give a different MD5, so a different key. The
same slot on the same parent hashes to the same key, which is the
duplicate `FUN_140901310` rejects. A fresh entity we allocated
ourselves has no parent and no slot, so this frame does not apply
to it.

`FUN_140986bc0` cannot be called on the entity we already allocate.
Its first act is `CALL qword ptr [RAX+0x20]` on `RCX`, then
`FUN_1402034a0` on that return, and the UUID is written to that new
pointer. `RCX` in the caller is `R14`, one of three fields of the
global at RVA `0x8982FB8` (`[+0x78]`, `[+0x80]`, `[+0x88]`, chosen by
a byte at `[type+0x20]`), not an entity. Not emitted.

The call that writes the key, not emitted:

```
FUN_14011b2b0(out16, parent_uuid, &index, 4)   ; MD5, version/variant patched
FUN_140986bc0(factory, &position, &out16)       ; allocates, then
                                                ; VMOVUPS [entity+0x8], XMM0
```

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70`
stay uncalled, `entity+0xD8` stays unwritten, `--add-player` stays
alloc-only and `--link-puppet` stays as shipped. No probe.

### The 16 bytes are MD5(parent UUID || slot) (2026-10-22)

`FUN_140986bc0` writes them with one store. The 40 instructions
before it do not compute the key. `RBP` is the third argument
(`MOV RBP,R8` at `0x986BD7`) and `XMM0` is loaded from it:

```
RVA 0x986C3B VMOVUPS XMM0,xmmword ptr [RBP]
RVA 0x986C40 MOV RAX,qword ptr [RDI]
RVA 0x986C43 MOV RDX,RSI
RVA 0x986C46 VMOVUPS xmmword ptr [RDI + 0x8],XMM0
```

In `FUN_140b22790` that pointer is `[RBP-0x39]`, the out buffer of
`FUN_14011b2b0`. The parent UUID is not a register. It is the 16
bytes already at `[RDI+0x8]`, and `RDI` is the incoming `RCX`
(`MOV RDI,RCX` at `0xB227A4`):

```
RVA 0xB22949 LEA RDX,[RDI + 0x8]
RVA 0xB2294D MOV R9D,0x4
RVA 0xB22953 LEA R8,[RBP + 0x67]
RVA 0xB22957 LEA RCX,[RBP + -0x39]
RVA 0xB2295B CALL 0x14011b2b0
```

The 4-byte slot is the dword at `[RBP+0x67]`. The scan does not
write it. The only store before the hash is zero:

```
RVA 0xB227CA XOR R15D,R15D
RVA 0xB228FD MOV dword ptr [RBP + 0x67],R15D
RVA 0xB22901 MOV R13D,R15D
```

`R13` is the loop counter. After a child is inserted, the function
increments it and writes it back, then jumps to the scan:

```
RVA 0xB22BBB INC R13D
RVA 0xB22BBE MOV EAX,R13D
RVA 0xB22BC1 MOV dword ptr [RBP + 0x67],R13D
RVA 0xB22BC5 CMP R13D,dword ptr [RBP + 0x6f]
RVA 0xB22BC9 JL 0x140b22910
```

The scan walks the 0x40-stride array at `[component+0xD0]`, count
`[component+0xC8]`, and compares a signed word at `[record+0x2]`
with `R13+1`. A hit calls the hash. A miss leaves the zero dword
alone. Neither path reads `[EntityManagerGame+0x1d8]`.

`[RBP+0x6f]` is the bound. It is the signed word at the last
record, 0x40 bytes before the end of that array:

```
RVA 0xB22836 MOVSXD RAX,dword ptr [RBX + 0xc8]
RVA 0xB22848 MOV RAX,qword ptr [RBX + 0xd0]
RVA 0xB2284F SHL RCX,0x6
RVA 0xB22853 MOVSX EDX,word ptr [RCX + RAX*0x1 + -0x3e]
RVA 0xB22858 MOV dword ptr [RBP + 0x6f],EDX
```

`FUN_14011b2b0` copies the parent UUID with one load
(`VMOVUPS XMM0,xmmword ptr [RDX]` at `0x11B2C2`) and stores it at
`[RBP+0x7]` (`0x11B2FE`). The length counter at `[RBP-0x1]` is set
to `0x10` (`0x11B2F6`). `FUN_1400f44b0` then appends. Its first
block is empty (`MOV EAX,dword ptr [RCX+0x18]` at `0xF44CA` is 0),
so the 4 bytes are copied to `[ctx+0x20]`, which is 16 bytes after
the UUID. Length becomes `0x14`. The input is 20 bytes, little
endian, parent UUID then the slot dword. MD5 init is
`0x67452301`, `0xEFCDAB89`, `0x98BADCFE`, `0x10325476`.

The digest is stored little endian (`MOV EAX` of each state dword
into `[RBP-0x29]` at `0x11B380` through `0x11B395`, then
`VMOVUPS` to the out buffer at `0x11B39D`). Two patches:

```
RVA 0x11B3A1 MOVZX EAX,byte ptr [RBX + 0x6]
RVA 0x11B3A5 AND byte ptr [RBX + 0x8],0x3f
RVA 0x11B3A9 AND AL,0xf
RVA 0x11B3AB OR AL,0x30
RVA 0x11B3AD OR byte ptr [RBX + 0x8],0x80
RVA 0x11B3B1 MOV byte ptr [RBX + 0x6],AL
```

Byte 6 of the digest is `(digest[6] & 0x0F) | 0x30`. Byte 8 is
`(digest[8] & 0x3F) | 0x80`. The other 14 bytes are raw MD5.

The recipe, not emitted. Read 16 bytes at `[[PlayerGame+0x30]+0x8]`
(`PlayerManager*` at RVA `0x8982C80`). Pick a slot dword. The game
uses `0` on the first pass and then `1, 2, ...` up to but not
including the signed word at the last record of the component's
0x40 array. Any dword whose 20-byte hash is not already a key in
`[EntityManagerGame+0x1d8]` (`EntityManagerGame*` at RVA
`0x8982C40`) is free; the same parent and the same slot hash to the
same key. MD5 the 16 parent bytes followed by that dword, little
endian, then patch byte 6 and byte 8 as above. Write the 16 bytes
to `[entity+0x8]`. Then `FUN_140901310(RCX unused, RDX = entity,
R8 = 1, R9 = 0, 5th = 0)`.

`FUN_140986bc0` still allocates before the store, so it is not
called on the entity we already have. `FUN_1402250c0`,
`FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70` stay uncalled,
`entity+0xD8` stays unwritten, `--add-player` stays alloc-only,
`--link-puppet` stays as shipped, `--register-entity` stays
unemitted. No probe.

### `--register-entity` emitted (2026-10-22)

Offline only. One `-noanalysis -readOnly -process` pass
(`local/ghidra_scripts/UuidKey9.java`, report `local/uuid9.txt`).
No re-analysis, no symbol scan, no inject, no launch, nothing
written under `M:\`. `--register-entity` is mode byte 7.

The call frame, emitted once, inside the same SEH as the other
one-shot modes:

`FUN_140901310(RCX unused, RDX = the 0x300 ControlledEntity, R8 = 1, R9 = 0, 5th = 0)`.

`R8 = 1` skips the `[entity+0x68]` read. `[entity+0xD8]` is not
written and is not tested. `FUN_140986bc0` is not called. The 16
bytes are a plain store at `[entity+0x8]`.

MD5 is standard RFC 1321. Finalization is in `FUN_14011b2b0`, not
in `FUN_1400f44b0` (that one only appends the 4 slot bytes and adds
4 to the length). After the append, `0x11B320` stores `0x80` at
`[buf + (len & 0x3F)]`. The pad count is `0x37 - (len & 0x3F)`, and
when that goes negative it adds 8 and compresses a full block
(`CALL 0x1400f4590` at `0x11B33B`) before zeroing 0x38 more bytes.
The bit length is `LEA RAX,[RCX*8]` / `SHR RCX,0x1d` at `0x11B357`
/ `0x11B35F`, stored little endian at `[RBP+0x3f]` / `[RBP+0x43]`
(`0x11B36A` / `0x11B363`), then one more compress at `0x11B36D`.
That is the `0x80` pad, zeros, and the 64-bit length in bits, little
endian. Init is still `0x67452301`, `0xEFCDAB89`, `0x98BADCFE`,
`0x10325476`. The compress those calls reach, `FUN_1400f4590`,
contains the first MD5 round constant `0xD76AA478` at file offset
`0xF39E9` (one hit in the exe). The probe uses that padding. It
does not add a library. Byte 6 and byte 8 are patched after the
digest, as at `0x11B3A5` through `0x11B3B1`.

The collision scan reads `[record+0]`, the 16 bytes the compare
loads, not `[[record+0x10]+0x8]`. `FUN_140911320` does:

```
RVA 0x91145C MOV R8,qword ptr [RBP]
RVA 0x911460 MOV R9,qword ptr [RBP + 0x8]
RVA 0x911470 CMP qword ptr [RBX + 0x8],R9
RVA 0x911477 CMP qword ptr [RBX],R8
```

`RBP` is the key pointer (`MOV RBP,R9` at `0x91133C`), and the
caller passed `&[entity+0x8]`. `[RBX]` is the slot. Slots are
stride `0x20` (`SHL RBX,0x5` at `0x9113A9`). An empty slot is
`[record+0x18] == 0` (`0x9114C5`). The table is
`[EntityManagerGame+0x1d8]`. The dword at `+0x1e4` is the bucket count (`MOV EDX,[R15+0xc]` at
`0x91138E`, then `DEC EDX` and the mask, so the table length is
that dword). The dword at `+0x1e0` is the occupied count the insert
increments (`INC dword ptr [R15+0x8]` at `0x9114F7`). The table is
open-addressed, so a key can sit in any bucket. The probe walks all
`+0x1e4` buckets and skips a record whose `[record+0x18]` is zero.
`EntityManagerGame*` is RVA `0x8982C40`. The probe skips a slot
whose digest matches any occupied record's `[record+0]`. First
free dword in `0..63`. All 64 taken returns without the call. A
null `PlayerManager*` or a null live entity (`[PlayerGame+0x30]`
of `[[PlayerManager+0x10]]`) also returns without the call. The
log prints both dwords, before and after. The one that should move
is `+0x1e0`.

`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and
`FUN_14101bd70` stay uncalled. `--add-player` stays alloc-only.
`--link-puppet` stays as shipped.

Run it once. Game open, save loaded, borderless fullscreen. Log is
`hfw-probe.log` in the game's working directory. From
`games/horizon-forbidden-west/probe/bin/Release/net10.0` (the
`net10.0` `OutputPath` the csproj's `TargetFramework` already
uses):

```
Start-Process .\hfw-probe.exe -ArgumentList '--exe','M:\Horizon Forbidden West\HorizonForbiddenWest.exe','--register-entity'
```

The line to look for is `register-entity slot=` with
`seh=0x00000000`. `count_*` is the dword at `+0x1e4` (capacity).
`occupied_*` is the dword at `+0x1e0`, and that one should be one
higher after the call.

### `--register-entity` faulted after the insert (2026-10-22)

The owner ran it once. The game crashed with an access violation
after the hash insert. Copied from the owner's log:

```
register-entity start one-shot gate=GameModule@0x8983150 steps=ctor-from-record,size-from-record,alloc,place,md5,register record=0x21DA6C0 factory-not-called
register-entity gate module=0x2697F502300 waited_s=0 faults=0
register-entity parent pm=0x263E02F3170 player=0x269FA0D1900 live=0x26511637280
register-entity table em=0x264EB7E0000 table=0x26A14140980 count_before=4096 occupied_before=2229
register-entity slot=0 key=E22883B1002431058EA85780767FB5EB entity=0x264E93F2400 vtable=0x0 size=0x300 ctor=0x7FF7D0670980 seh=0xC0000005 count_before=4096 count_after=4096 occupied_before=2229 occupied_after=2230 (FUN_140986bc0 not called, entity+0xD8 not written)
```

`seh=0xC0000005`. Occupied went `2229 -> 2230` and the bucket count
stayed `4096`, so `FUN_140911320` took the empty-slot arm and stored
the record. The duplicate-key arm at `0x901590` (`0xdeadca7` through
null at `0x901647`) did not run. The fault is after that store,
still inside `FUN_140901310`.

`vtable=0x0` is a log-order bug, not a real zero. In
`register_entity` the vtable read sits after the `__except`:

```
} __except (EXCEPTION_EXECUTE_HANDLER) {
 seh = (unsigned int)GetExceptionCode();
}
if (entity && !seh) vtable = rd64(entity);
```

`seh` was `0xC0000005`, so `rd64` never ran and the logged qword is
the zero it was declared as. The constructor did run:
`ctor=0x7FF7D0670980` is `FUN_1411a0980` (RVA `0x11A0980`, image base
`0x7FF7CF4D0000`). It writes the vtable before it returns:

```
RVA 0x11A0995 LEA RAX,[0x141958958]
RVA 0x11A099C MOV qword ptr [RBX],RAX
```

That is `ControlledEntity::vftable`. The call order in the probe is
alloc, then `place` (`FUN_1411a0980`), then the key store, then
`FUN_140901310`. A zero vtable would mean the constructor did not
run. This log does not say that.

The faulting instruction is `CALL qword ptr [RAX]` at RVA
`0x9015A9`. It is the first instruction after the insert that
dereferences the entity's vtable. The success path (`[RBP-0x29] == 0`
is false, so `JNZ 0x140901652` is not taken) is:

```
RVA 0x901588 MOV R13,qword ptr [RBP + 0x67]
RVA 0x90158C CMP dword ptr [RBP + -0x29],R14D
RVA 0x901590 JNZ 0x140901652
RVA 0x901596 LEA RCX,[0x141795260]
RVA 0x90159D CALL 0x140042570
RVA 0x9015A2 MOV RAX,qword ptr [R13]
RVA 0x9015A6 MOV RCX,R13
RVA 0x9015A9 CALL qword ptr [RAX]
```

`[RBP+0x67]` is the saved `RDX` of `FUN_140901310`, the entity
(`MOV [RSP+0x10], RDX` at `0x90131F`, `LEA RBP,[RSP-0x2f]` at
`0x90132D`). `R14` is zero. `FUN_140911320` writes `2` to
`[out+0x10]` on a fresh insert (`0x9114FB`) and `0` when the key is
already present (`0x91158D`). Occupied increased, so the compare
falls through. `RAX` is loaded from `[entity+0]`, slot 0 of the
vtable, and the call dereferences that qword. `RCX` is the entity.

`FUN_140042570` before it is a logging helper (`CALL 0x1416eff2c`,
`CALL 0x1416f6aec`). It does not touch the entity. Slot 0 of
`ControlledEntity::vftable` is `FUN_141186f10`, which returns
`&DAT_1421da6c0`. The crash is the load of the slot, not that
function. The later entity uses on the same path (`[entity+0x98]`
at `0x901660`, `[entity+0x160]` at `0x901681`, `LEA R8,[entity+0xD8]`
at `0x9016E0`) are not reached.

Offline only. Six `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/RegFault.java` through `RegFault6.java`,
reports `local/regfault.txt` through `local/regfault6.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. The probe was not changed. The call frame is unchanged.
`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and
`FUN_14101bd70` stay uncalled.

The single next fix: read the vtable before `FUN_140901310` returns
into the SEH handler, and stop treating the logged zero as the
constructor's store.

### `R13` at `0x9015A2` is the entity, and that call did not run (2026-10-22)

`R13` at `0x9015A2` is the `0x300` `ControlledEntity` from `RDX`.
`[R13]` is `ControlledEntity::vftable`. Nothing between the
constructor's store and this load writes `[entity+0]`, and the
probe does not either: `hfw_probe.c` copies 16 bytes at
`entity+0x8` (`memcpy((void *)(entity + OFF_ENTITY_UUID), key, 16)`).
The fresh-insert status skips `0x9015A9`.

The only write to `R13` in `FUN_140901310` is the load after the
insert. `PUSH R13` at `0x901327` spills the incoming register;
`POP R13` at `0x901834` restores it.

```
RVA 0x90131F MOV qword ptr [RSP + 0x10],RDX
RVA 0x90132D LEA RBP,[RSP + -0x2f]
RVA 0x901346 MOV RSI,RDX
RVA 0x901588 MOV R13,qword ptr [RBP + 0x67]
RVA 0x9015A2 MOV RAX,qword ptr [R13]
RVA 0x9015A6 MOV RCX,R13
RVA 0x9015A9 CALL qword ptr [RAX]
```

`[RBP+0x67]` is the saved `RDX` (`MOV [RSP+0x10], RDX` at
`0x90131F`, five pushes, `LEA RBP,[RSP-0x2f]`). The same slot is
reloaded into `RAX` at `0x901354` and into `RBX` at `0x9013AF` and
`0x9013FE`. `FUN_140901310` has no `MOV [R13], ...` and no
`MOV [RBX], ...` / `MOV [RSI], ...` / `MOV [RDI], ...` at offset 0.
The stores aimed at the entity are `[entity+0x98]` (the locked bit
sets at `0x901349`, `0x901399`, `0x9013A4`), the position copy at
`0x901360` / `0x90137E` / `0x90138F`, and nothing at `+0`.

The constructor's store is still the last write of that qword.
`FUN_140948a10` does put a vtable at `[RCX]` (`MOV [RCX], RAX` at
`0x948A9F`, then `MOV [RCX], RBP` at `0x948BB9`), but
`FUN_1411a0980` calls it and then overwrites `+0`:

```
RVA 0x11A0990 CALL 0x140948a10
RVA 0x11A0995 LEA RAX,[0x141958958]
RVA 0x11A099C MOV qword ptr [RBX],RAX
```

The three callees that see the entity before `0x9015A2` do not
replace it. `FUN_14094c8b0` (`0x9013B9`) writes `[entity+0x160]` and
`[entity+0x170]` and tests `[entity+0x70]` (`TEST RCX, RCX` /
`JZ 0x14094ca2e` at `0x94C9E3`). `ControlledEntity::vftable+0x50` is
`FUN_14094fd10` (`CALL [RAX+0x50]` at `0x901416`); it ORs
`[entity+0x98]` and also tests `[entity+0x70]`. `FUN_140ad3b10`
(`0x9014A8`) writes `[entity+0x220]` and fields off that, not `+0`.
`FUN_140042570` (`0x90159D`) only formats a log line.

`CALL [RAX]` at `0x9015A9` is the duplicate-key arm, and this run
did not take it. `FUN_140911320` writes the iterator status at
`[out+0x10]`, which is `[RBP-0x29]` because the out pointer is
`LEA RDX,[RBP-0x39]` at `0x90157F`:

```
RVA 0x9114F0 MOV qword ptr [RBX + 0x10],RCX
RVA 0x9114F7 INC dword ptr [R15 + 0x8]
RVA 0x9114FB MOV dword ptr [R14 + 0x10],0x2
RVA 0x911579 MOV dword ptr [R14 + 0x10],0x1
RVA 0x91158D MOV dword ptr [R14 + 0x10],0x0
```

`2` is the empty-slot insert (occupied increments in the same
straight line). `1` replaces an existing record and does not
increment. `0` is the duplicate. Occupied went `2229 -> 2230`, so
the dword is `2`. `R14` in `FUN_140901310` is zero (`XOR R14D, R14D`
at `0x9013C5`):

```
RVA 0x90158C CMP dword ptr [RBP + -0x29],R14D
RVA 0x901590 JNZ 0x140901652
```

`2 != 0`, so the jump is taken. `0x901596` through `0x9015A9` is
the fall-through: a log, then slot 0 of the vtable, then the fatal
`0xdeadca7` at `0x901647`. Slot 0 is `FUN_141186f10`, which returns
the type record; that call would not fault, and it did not run.
The inserted record does not need it. `[record+0x10]` already holds
the entity, and `[hash+0x8]` already counts it.

The path that did run is `0x901652`. It unlocks
`[EntityManagerGame+0x1e8]`, reloads the entity, and the first call
back into the entity is `FUN_14094c8b0` at `0x90167C` when bit 23 of
`[entity+0x98]` is clear (the constructor's `0x93` leaves it clear).
That function returns. The virtual call after it is the listener
fanout, not `[R13]`:

```
RVA 0x9017C6 MOV RBX,qword ptr [R15 + 0x1f18]
RVA 0x9017E0 MOV RCX,qword ptr [RBX]
RVA 0x9017E3 MOV RDX,RDI
RVA 0x9017E6 MOV RAX,qword ptr [RCX]
RVA 0x9017E9 CALL qword ptr [RAX]
```

`RDI` is the entity (`MOV RDI,[RBP+0x67]` at `0x90166E`). `RCX` is
a listener from `[EntityManagerGame+0x1f18]`.

Offline only. Four `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/RegFault8.java` through `RegFault11.java`,
reports `local/regfault8.txt` through `local/regfault11.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. The probe was not changed. The call frame is unchanged.
`FUN_1402250c0`, `FUN_14134c950`, `FUN_1413d8470` and
`FUN_14101bd70` stay uncalled.

The single next fix: follow the taken branch at `0x901652` and name
the listener virtual call at `0x9017E9`, which is the first
`CALL [RAX]` the fresh insert actually reaches.

### `0x9017E9` is `SpawnedEntityMapping` slot 0, and the hash record is already linked (2026-10-22)

`CALL qword ptr [RAX]` at `0x9017E9` is slot 0 of a
`SpawnedEntityMapping` listener. `RCX` is one element of the pointer
vector at `[EntityManagerGame+0x1f18]` (count at `+0x1f10`, lock at
`+0x1f08`). `RDX` is the entity. The call dereferences the listener,
not the entity: `MOV RAX, [RCX]` then `CALL [RAX]`.

The fresh-insert branch from `0x901652` to the return, every `CALL`:

```
RVA 0x90165A CALL qword ptr [0x14174b1c0]   ; unlock, only if R12 != 0
RVA 0x90167C CALL 0x14094c8b0               ; entity, skipped if bit 23 of [entity+0x98] is set
RVA 0x9016FE CALL 0x140132d10               ; doubles, not the entity
RVA 0x901755 CALL qword ptr [0x14174b2f8]
RVA 0x901762 CALL qword ptr [0x14174b1c8]
RVA 0x90176B CALL qword ptr [0x14174b2f8]
RVA 0x901778 CALL qword ptr [0x14174b1c8]
RVA 0x901788 CALL 0x14090aba0               ; RDX = entity+0x38, a pointer the ctor wrote
RVA 0x901795 CALL qword ptr [0x14174b1c0]
RVA 0x9017A3 CALL qword ptr [0x14174b1c0]
RVA 0x9017B3 CALL qword ptr [0x14174b300]
RVA 0x9017C0 CALL qword ptr [0x14174b1d8]
RVA 0x9017E9 CALL qword ptr [RAX]           ; listener slot 0
RVA 0x9017FC CALL qword ptr [0x14174b1d0]
RVA 0x901814 CALL qword ptr [RAX + 0x48]    ; [0x148982FE8] slot +0x48, skipped when that global is 0
```

The twenty instructions in front of the call name both registers:

```
RVA 0x9017A9 LEA R14,[R15 + 0x1f08]
RVA 0x9017B0 MOV RCX,R14
RVA 0x9017B3 CALL qword ptr [0x14174b300]
RVA 0x9017B9 TEST AL,AL
RVA 0x9017BB JNZ 0x1409017c6
RVA 0x9017BD MOV RCX,R14
RVA 0x9017C0 CALL qword ptr [0x14174b1d8]
RVA 0x9017C6 MOV RBX,qword ptr [R15 + 0x1f18]
RVA 0x9017CD MOVSXD RAX,dword ptr [R15 + 0x1f10]
RVA 0x9017D4 LEA RSI,[RBX + RAX*0x8]
RVA 0x9017D8 CMP RBX,RSI
RVA 0x9017DB JZ 0x1409017f4
RVA 0x9017E0 MOV RCX,qword ptr [RBX]
RVA 0x9017E3 MOV RDX,RDI
RVA 0x9017E6 MOV RAX,qword ptr [RCX]
RVA 0x9017E9 CALL qword ptr [RAX]
```

`R15` is `EntityManagerGame*` (the load at `0x901339`). `RDI` is the
entity (`MOV RDI, [RBP+0x67]` at `0x90166E`, the saved `RDX`). `RBX`
walks the vector. `RCX` is the listener. `RAX` is `[listener+0]`, and
the call is slot 0 (`CALL [RAX]`, displacement 0). An empty vector
jumps over it (`CMP RBX, RSI` / `JZ 0x1409017f4`).

The one object this vector is filled with, in `FUN_140d5e2c0`, is
built at `0xD5F0D9` (`MOV EDX, 0xC8`) and pushed by `FUN_1400fe310`
at `0xD5F156` (`LEA RCX, [EntityManagerGame+0x1f10]` /
`MOV [data + index*8], RDX` at `0xFE385`). The template it would
clone, `[[0x148983128]+0x220]`, is zero: `FUN_140d49bd0` writes
`MOV [RCX+0x220], RDX` at `0xD49D8E` and `RDX` is the same zero it
stores from `+0x88` through `+0x218`. The `JNZ 0x140d5f0a0` at
`0xD5F000` is therefore not taken, and the object that is pushed is
the fallback:

```
RVA 0xD5F066 MOV qword ptr [RBX + 0x8],R15
RVA 0xD5F06A LEA RAX,[0x1418c8748]
RVA 0xD5F071 MOV qword ptr [RBX],RAX
```

`R15` is zero (`XOR` earlier in that constructor). The Complete
Object Locator at `0x1418C8748 - 8` (`0x141A4A900`, type descriptor
RVA `0x2271778`) names `.?AVSpawnedEntityMapping@@`. Slot 0 of that
vtable is `FUN_140abe130` (313 bytes). Slot `+0x8` is
`FUN_140abe270`, which is the matching call on the unparent path
(`CALL [RAX+0x8]` at `0x9026F9`), not this one.

`FUN_140abe130` takes the listener in `RCX` and the entity in `RDX`.
Its first act is the component map:

```
RVA 0xABE142 LEA RCX,[RDX + 0xa0]
RVA 0xABE149 LEA RDX,[0x142124320]
RVA 0xABE150 CALL 0x1408fa230
RVA 0xABE155 TEST RAX,RAX
RVA 0xABE158 JZ 0x140abe261
```

`0x142124320` is the `SpawnSetupComponent` type record (name at
`+0x40`, RVA `0x1E84750`). `FUN_1408fa230` loads the map with no null
test:

```
RVA 0x8FA235 MOV R9,qword ptr [RCX + 0x18]
RVA 0x8FA23C MOVSXD R8,dword ptr [RCX + 0x10]
```

`RCX` is `entity+0xa0`, so that first load is `[entity+0xb8]`, and
the count is `[entity+0xb0]`. `FUN_140948a10` writes both, and
`FUN_1411a0980` does not touch them:

```
RVA 0x948A2A XOR EBP,EBP
RVA 0x948A91 MOV qword ptr [RCX + 0xb0],RBP
RVA 0x948A98 MOV qword ptr [RCX + 0xb8],RBP
```

A zero count makes `TEST R8D, R8D` / `JLE 0x1408fa28a` skip the walk,
then `CMP R9, RBX` / `JZ 0x1408fa2b4` returns `XOR EAX, EAX`. The
`JZ 0x140abe261` that follows is the function's `RET`. So a
constructor-fresh entity does not fault inside this slot: the map is
present and empty, and the lookup returns before it reads a
component. The later uses in the same function (`[RAX+0x58]` at
`0xABE17F`, then `FUN_1401ecd50` at `0xABE22F` with
`R9 = &entity+0x8`) are behind that null test.

The hash record is already linked before `0x9017E9`. The empty-slot
store `MOV [RBX+0x10], RCX` at `0x9114F0` and `INC [R15+0x8]` at
`0x9114F7` both ran: occupied went `2229 -> 2230` and the bucket
count stayed `4096`. `[record+0x10]` is the entity. Nothing between
that store and `0x9017E9` writes the record again. The call is the
listener notify, and it does not have to succeed for the record to
exist.

Offline only. Sixteen `-noanalysis -readOnly -process` passes
(`local/ghidra_scripts/RegFault12.java` through `RegFault27.java`,
reports `local/regfault12.txt` through `local/regfault27.txt`). No
re-analysis, no symbol scan, no inject, no launch, nothing written
under `M:\`. The probe was not changed. `FUN_1402250c0`,
`FUN_14134c950`, `FUN_1413d8470` and `FUN_14101bd70` stay uncalled.

The single next fix: the hash record is already linked, so skip
`FUN_140901310` for an entity this probe inserted and call
`SpawnedEntityMapping` slot 0 (`FUN_140abe130`, `RCX` = the listener
at `[EntityManagerGame+0x1f18]`, `RDX` = the entity) on its own,
which is the notify `0x9017E9` still has to finish.

## Sources

- https://github.com/JustAddMP/HorizonForbiddenWest (2026-10-17, README only)
- https://github.com/JustAddMP/core (v0.13.0, pushed 2026-10-20)
- https://github.com/everydayaussie/Strikers (MIT, pushed 2026-10-18)
- https://www.nexusmods.com/horizonforbiddenwest/mods/209 (Strikers release)
- https://www.ggrecon.com/articles/leaked-horizon-forbidden-west-multiplayer-shows-co-op-mode
- https://www.gematsu.com/2026/08/bloomberg-horizon-hunters-gathering-being-rebooted-following-negative-player-feedback
- https://github.com/cauldronloader/cauldron (GPL-3.0, updated 2026-04-29)
- https://github.com/Jayveer/Decima-Explorer (updated Feb 2024)
- https://github.com/ShadelessFox/decima (Decima Workshop)
- https://www.nexusmods.com/horizonforbiddenwest/mods/categories (167 mods, no MP category)
- https://www.gamepressure.com/horizon-forbidden-west/multiplayer-co-op/z7f7b8 (no MP in game)
- https://www.reddit.com/r/horizon/comments/syqhr2/ (fan co-op demand, 2022)
- Binary observations: `local/pe_scan.py` + `local/pe_scan_out.txt` (this session)
