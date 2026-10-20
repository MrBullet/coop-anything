# Horizon Forbidden West (2024 PC, Decima / Nixxes port) — co-op notes

Status: **phase 1 research complete, phase 2 partial (binaries-only workspace), phase 3
orientation started** — 2026-10-20. Claim issue: cranebeaker/coop-anything#83.
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
  states Steam build `14835813` vs our recorded `13801562` (see Unknown — verify one RVA
  before trusting).
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
- Tools present (checked 2026-10-20): Python 3.14 + `pefile`, Java 17 (Ghidra-ready),
  Rust/cargo, .NET SDK, git, gh CLI (authenticated). Missing: Ghidra, `strings`, DIE,
  7z, MSVC cl. Nothing installed this session (scan used what was already there).

## Derived (labelled guesses)

- **Path 2 (build from zero) still stands**, but the cost dropped: the retail exe keeps
  the whole co-op stack (session/party/matchmaking/replication + script exports), so the
  plan is to check whether those paths can be re-driven (see Unknown) rather than
  designing netcode from nothing. Phase 4 will choose between: (a) revive engine net
  layer, (b) JustAddMP core game module, (c) own transport feeding the `Net*State`
  replication classes puppet-style (Strikers pattern).
- Strikers' published RVAs likely match our exe (identical PE timestamp + SizeOfImage) —
  *assumed*, not yet verified against a single function.

## Unknown

- Whether any `Net*`/lobby/matchmaking path is **reachable at runtime** in the retail
  single-player build, or compiled-out behind `NetSessionManagerNoOp` — needs a runtime
  trace or Ghidra headless cross-reference pass.
- Whether the game's WS2_32 usage includes a live UDP transport or only telemetry/
  telemetry-over-TCP + WinHTTP.
- Steam buildId discrepancy: our `flt.ini` says 13801562, Strikers documents 14835813
  for build `667B1777-949F000` — same exe identity, different recorded build id; verify
  one documented RVA (`g = [base + 0x8983150]` chain) before trusting the memory map.
- JustAddMP/core license (none stated) — matters before building on it (rule 7).
- Whether Decima's async/strand-like structures exist in HFW (Death Stranding does).

## Not tried (next session)

- Ghidra headless import + decompile-all of the exe (Java 17 ready; install into
  `local/`, follow methods.md: background run, long timeout, output kept in project).
- Runtime launch of the workspace copy (needs full install or instance plan), socket
  activity observation, single-instance mutex survey for two copies.
- Verify one Strikers RVA against our binary to unlock its memory map.

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
