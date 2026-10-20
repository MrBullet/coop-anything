# Horizon Forbidden West (2024 PC, Decima / Nixxes port) — co-op notes

Status: **phase 1 research complete, phase 2 (binaries-only workspace), phase 3
complete (Ghidra headless reachability), phase 4 decision memo written** —
2026-10-20. Claim issue: cranebeaker/coop-anything#83.
Phase-4 recommendation: **route (c)** — see [DECISION.md](DECISION.md).
No code, no mod, no test run yet.

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

## Not tried (next session)

- Runtime launch of the workspace copy (needs full install or instance plan),
  socket activity observation, single-instance mutex survey for two copies.
- Phase 5 first milestone: injected module that logs per frame + reads the local
  player's transform/world id (how-coop-mods-work.md stage 1 exit test), with a
  runtime build-id guard copied from Strikers.
- Map WS2_32 call sites; map the tables that invoke the NetGameDummy factory.
- Ask JustAddMP/core author about a license (owner approval for contact).

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
