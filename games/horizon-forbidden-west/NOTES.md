# Horizon Forbidden West (2024 PC, Decima / Nixxes port) — co-op notes

Status: **phase 5 milestone 1 probed live** (2026-10-21) — the injected
read-only sampler ran 1,500 samples. The entity pointer is stable. The
float Vec3 at `entity+0xD8` is not a transform, and `[[scene+0x248]]` is a
counter, not a world id. The 30 Hz `fflush` loop stalled the game; the
sampler is now 10 Hz. Offline selftest still 11/11.
Runtime injection is blocked: Steam has no library entry for this install, so
`-applaunch` does not start the game. Manual steps below. Claim issue:
cranebeaker/coop-anything#83. Route **(c)** — see [DECISION.md](DECISION.md).

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
| position | `entity+0xF0`, Mat34, 0x3C bytes | `FUN_140962640` (`Entity_ExportedGetWorldTransform`), `vmovups ymm0, [rdi+0xF0]` at RVA `0x962697` |
| translation | last row, floats at `+0x114/+0x118/+0x11C` | same copy; row-major 3x4 |
| heading | row 2, floats at `+0x108/+0x10C/+0x110` | `FUN_140962440` transposes the same matrix |
| rejected | `entity+0xD8`, three qwords | `FUN_140d4f750` copies them; the live log is not a position |
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
 set only from the translation floats.
- `hfw-probe.cs` — loader. Refuses to run unless the exe's SHA-256 is
 `9cec6626…02055429e` **and** the PE stamp is `667B1777-949F000` (Strikers'
 guard plus the hash). `--selftest` checks the anchor bytes above, including
 three Strikers rows, against the exe on disk.

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
3. From `probe/bin/Release/net10.0`:
 `hfw-probe.exe --exe "<game dir>\HorizonForbiddenWest.exe" --seconds 45`.
 The loader checks the hash, injects `hfw_probe.dll`, and waits. The log is
 `hfw-probe.log` in the game's working directory. A good sample has `sane=1`
 and a `pos=(x,y,z)` in metres, with `fwd=(x,y,z)` a unit-ish direction.
 `old=` is the rejected `+0xD8` qwords, kept so the two reads can be compared.
 `sane=0` means the Mat34 row is still wrong — do not keep the game running
 to retry. `counter=` is `[[scene+0x248]]` and is expected to keep climbing.
4. Close the game. Do not leave it running.

## Not tried (next session)

- Runtime confirmation of the Mat34 translation (`sane=1` on `pos=`).
 The entity pointer is live; the `+0xD8` read is not a position; the export
 copies a Mat34 from `+0xF0`. No stable world id yet — `counter=` climbs.
- Socket activity observation, single-instance mutex survey for two copies.
- Map WS2_32 call sites; map the tables that invoke the NetGameDummy factory.
- Ask JustAddMP/core author about a license (owner approval for contact).
- Milestone 2: one puppet driven by our own transport, two instances.

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
