# Horizon Forbidden West (2024 PC, Decima / Nixxes port) — co-op notes

Status: **phase 5, READ-CAM READY** (2026-10-21) — `--read-cam` is
opt-in and read-only. It waits until `GameModule*` at `0x148983150`
is non-null (same gate as `--read-slot`), then samples the qword at
`0x148982CB0` for 10 s at 2 Hz and logs `cam ptr=0x.. lock20=0x..`.
`lock20` is the first qword at `[ptr+0x20]` only when `ptr` is
non-null, inside SEH. It does not call `AcquireSRWLockExclusive`,
`FUN_140d73270`, `FUN_14134c950`, or `FUN_1413d8470`, and it writes
nothing. A line with `ptr` not 0 means the manager is alive. The live
value after a save load has not been read yet; this flag is that
read. `--add-player` still does not call `FUN_1401e6100`,
`FUN_140d73270`, or the player-list insert. `--spawn-once` still does
not call `FUN_14134c950`. `--read-slot` is unchanged. Default is still
the 45 s sampler. Claim issue: cranebeaker/coop-anything#83. See
[DECISION.md](DECISION.md).

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
- Milestone 2, revised again: `[0x148982CB0]` is a
 `CameraPredictionManager*` published by `FUN_140d5e2c0` at
 `0xD5FA19` and cleared by `FUN_140d60010` at `0xD60547`. Next is a
 read-only probe of that one qword after a save load. If it is
 non-null, the constructor's `AcquireSRWLockExclusive(RDI+0x20)` has
 a lock to take; if it is null, the startup store did not survive.
 Do not call `FUN_14134c950`. Do not call `FUN_1413d8470`. Do not
 call `FUN_140d73270` from that probe. Do not write the double3. Do
 not call `FUN_14101bd70`. Do not store to `entity+0xD8`. Do not emit
 `FUN_140d8c2d0`.

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

The constructor is still not called. `--read-cam` is the read that
was missing: mode byte 4, same `GameModule*` gate as `--read-slot`,
then 10 s at 2 Hz of `cam ptr=0x.. lock20=0x..`. `ptr` is
`[0x148982CB0]`. `lock20` is `[ptr+0x20]` only when `ptr` is
non-null, inside SEH, so a live manager shows the SRWLOCK was
initialized without calling `AcquireSRWLockExclusive`. A line with
`ptr` not 0 is the manager alive. It calls no game function and
writes nothing. `--add-player` stays the blocked log.
`--spawn-once` and `--read-slot` are unchanged.

After a save load, from `probe\bin\Release\net10.0` (the shell
returns immediately; the log is `M:\Horizon Forbidden West\hfw-probe.log`;
if that file is missing, read `hfw-probe-run.txt` beside the exe):

```
Start-Process .\hfw-probe.exe -ArgumentList '--exe','<game dir>\HorizonForbiddenWest.exe','--read-cam'
```

This only reads. No hitch expected. Alt-f4 first only if a previous
probe DLL is still wedged in the game; otherwise it can run on the
current session. The loader still prefers `hfw_probe_new.dll` when
that file sits beside a mapped `hfw_probe.dll`.

Selftest is 27/27 on the offline exe. The new anchor is the publish
store (`48 89 1D 90 32 C2 07` at `0xD5FA19`). The constructor load
(`48 8B 3D E7 F8 C0 07` at `0xD733C2`) was already checked.

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
