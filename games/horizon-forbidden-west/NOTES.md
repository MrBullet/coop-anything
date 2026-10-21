# Horizon Forbidden West (2024 PC, Decima / Nixxes port) — co-op notes

Status: **phase 5 milestone 1 corrected** (2026-10-21) — the walk run
showed the world position is the double3 at `entity+0xD8`, not the Mat34.
The probe now reads that double3. A setter that takes the
`entity+0x298` lock was not found; the functions that do write those
qwords are the constructor and a struct clone. Route **(c)** is still
blocked on a write of that double3. Claim issue:
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

## Not tried (next session)
- Socket activity observation, single-instance mutex survey for two copies.
- Map WS2_32 call sites; map the tables that invoke the NetGameDummy factory.
- Ask JustAddMP/core author about a license (owner approval for contact).
- Milestone 2: a second entity. The call is in the probe as
 `--spawn-once` (see "Spawn call convention" above). It has not been
 run against a live game. Do not write the double3 until a log shows
 `[pm+8]` going from 1 to 2. Do not call `FUN_14101bd70`; it takes
 `entity+0x298`, which the sim thread also takes.

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
