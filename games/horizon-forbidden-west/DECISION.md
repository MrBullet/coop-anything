# Phase 4 decision — which route for Horizon Forbidden West co-op

Status: **decision memo for owner review** — 2026-10-20. Claim issue:
cranebeaker/coop-anything#83. Branch: `game/horizon-forbidden-west`.
Evidence base: Ghidra 12.1.4 headless analysis of the retail exe (workspace copy,
identity in [NOTES.md](NOTES.md)), plus byte-level verification of the Strikers
memory map against our binary. Raw logs/scripts live in the session's git-ignored
`local/` state (Ghidra project `local/ghidra_proj`, reports
`local/ghidra_net_reachability*.txt`).

Game build examined: Steam AppId 2420110, exe file version 1.5.80.0, PE identity
`667B1777-949F000`, SHA-256 `9cec6626…02055429e` (full hash in NOTES.md).

## The question

Three candidate routes, from AGENTS.md phase 4:

- **(a)** revive the engine's own dormant net layer (switch `NetSessionManagerNoOp`
  → real, or call into it),
- **(b)** build a game module on JustAddMP/core (D3D11 injection + Steam lobbies/P2P),
- **(c)** own transport feeding the `Net*State` classes puppet-style
  (Strikers-style in-process injection, using their published memory map).

## Observations from the binary (Ghidra headless, this session)

Everything below is observed on our exact build; function addresses are RVAs
(`0x140000000` + RVA).

1. **The startup chain constructs the net subsystem every run.** The exe exports
   `MainProg` and `GameMainProg` (PE export table). Call graph:
   `MainProg` → `GameMainProg` → `FUN_140d2fca0` → `FUN_140d3d960`, a ~920-line
   network-subsystem constructor. `FUN_140d3d960` is inside the forward call
   closure of `MainProg` (2,584 functions), so this is normal startup, not debug
   code.
2. **The session manager slot gets the NoOp.** `FUN_140d3d960` writes
   `NetSessionManagerNoOp::vftable` (vtable @ RVA `0x184AEE0`) into the session
   manager slot (decompiled line L0906 of that function). The NoOp object is a
   0x50-byte stub: its slot `+0x08` is a deleting destructor, `+0x10` a name
   getter returning `"NetSessionManagerNoOp"`, `+0x00` a shared base-class
   listener-list dispatch. It is wired unconditionally — no config branch in the
   construction path.
3. **The real `NetSessionManager` has no code in this binary.** Its MSVC RTTI
   TypeDescriptor exists (RVA `0x224E948`) and is referenced **exactly once in
   the whole file** — inside the NoOp's RTTI base-class hierarchy (Base-Class
   Descriptor @ RVA `0x19F4658`). There is **no Complete-Object Locator and no
   vtable for it anywhere in the file** (raw scan, all sections), and no bare
   name string (only `"NetSessionManagerNoOp"` exists as a name). With RTTI on
   in this build, no COL ⇒ no vtable ⇒ no constructor or virtual implementation
   was linked. The real path was stripped at link time; there is nothing to
   "switch on".
4. **The rest of the party/matchmaking layer is real and constructed at every
   startup**, from the same subsystem constructor:
   - `NetPartyManager` ctor `FUN_140426d30` (called) and `NetPartyManagerPC`
     vtables written inline (L0622-624) — the PC party manager is built;
   - `MatchmakingService` ctor `FUN_140b116b0` — registers two events with the
     event bus at construction;
   - `StateMatchmaking` ctor `FUN_1413a75c0` — 5 vtables, copies its config,
     registers listeners;
   - plus a family of NoOp stubs wired alongside: `NetNpAvailabilityCheckNoOp`,
     `NetFriendManagerNoOp`, `NetSignInDialogServiceNoOp`, `CommerceManagerNoOp`,
     `UnifiedEntitlementManagerNoOp` (decompile lines L0133-L0449).
   Destructors for all of these have real callers too (teardown paths), i.e. the
   objects are constructed *and* destroyed during a normal run.
5. **The Steam P2P transport interface string is orphaned.**
   `"SteamNetworkingSockets012"` (RVA `0x178D3B4`) has **zero references in the
   whole image — neither a code reference nor a data-table pointer**. The
   game's own Steam-networking-sockets acquisition was compiled out. (The bare
   string `"NetPartyManagerPC"`/`"NetSessionManagerNoOp"` name getters *do* have
   code xrefs — so the orphan finding is real, not a scanning artifact.)
   Meanwhile the ordinary Steam bootstrap is live: `FUN_1400ba630` runs
   `SteamAPI_RestartAppIfNecessary`, `SteamInternal_SteamAPI_Init("SteamUtils010")`
   etc. at startup (it is invoked indirectly — no direct CALL xrefs, consistent
   with a registered startup hook).
6. **`JOIN_SESSION` is a StateMatchmaking virtual.** The string (RVA
   `0x19897B0`) is built by `FUN_1413a8ae0`, which is a virtual method in
   `StateMatchmaking::vftable` (slot table @ RVA `0x19886C8`; also referenced
   from a runtime `.bss` pointer) and constructs a `TelemetryLaunchAction` tagged
   `"invitation"`/`"JOIN_SESSION"`. The join-session flow exists in code and its
   owner object *is* constructed — but it dispatches into the NoOp session
   manager for anything transport-shaped.
7. **Round/lobby states are live code too:** `FUN_140d6faf0` formats
   `IN_PRELOBBY`/`RUNNING`/`IN_POSTLOBBY` into "Round state { State: %s }" dumps
   (round-state machine report; reached indirectly).
8. **Co-op script surface retained:** `MaxCoopPlayerCount` (ptr in `.data`
   `0x1EC1AC8`), `CoopShared` (`.data` `0x1F48C90`),
   `CoopPlayerControllerDisconnected` (`.data` `0x1FC8728`) are referenced from
   data tables (script symbol tables), so the script layer still exposes those
   keys; `host unreachable` is in a message table (`.rdata 0x176C648`). They are
   not orphans — the earlier "no xref" reading was a Ghidra data-ref artifact,
   corrected by raw pointer scan.
9. **`NetGameDummy` ("NETGAME 'DUMMY_ONLINE' GAMENAME")**: factory `FUN_140d96060`
   builds 0x3CA8-byte session objects; the factory has no direct CALL xrefs and
   is the only referrer of all four `NetGameDummy` vtables — invoked indirectly
   (table-driven), plausibly the dummy/local session object used by the dormant
   online mode.
10. **Renderer is D3D12 (static proof):** the export table is
    `D3D12SDKPath`, `D3D12SDKVersion`, `NVSDK_NGX_D3D12_*`, `NvOptimusEnablement`;
    the install root ships DX12 FSR/DLSS backends and DirectStorage. This matters
    for route (b) below.
11. `WS2_32` full socket API + `WINHTTP` are imported; which subsystems call the
    sockets (telemetry vs anything gameplay) is **not yet mapped** (unknown).

## Observation: the Strikers memory map is byte-exact on our build

Verification script `local/verify_strikers_rva.py` (pefile) against our exe —
18 of 19 documented checks pass; the single "fail" was the checker's own
assumption (see below):

| Check (docs/memory-tables.md & memory-map.md) | RVA | Result |
|---|---|---|
| patch-site bytes `80 79 28 00 74 52` | `0xE39A41` | exact match |
| coin-flip instruction run (19 B) | `0xE54F95` | exact match |
| move-limit check 1 (21 B) | `0xE45F8E` | exact match |
| move-limit check 3 (21 B) | `0xE025A5` | exact match |
| combat square stepper (24 B) | `0xE04FCD` | exact match |
| terrain table fill (11 B) | `0xE3B8B9` | exact match |
| Blight rule site (5 B) | `0xE01E4F` | exact match |
| full-Blight end site (6 B) | `0xE050D9` | exact match |
| turn-start loop site (9 B) | `0xE3F680` | exact match |
| confirm-activate prologue (5 B) | `0xE404F0` | exact match |
| AI controller vtable slot → `0x140E3F3B0` | `0x190D718` | exact match |
| RTTI name behind vtable | `0x190F750` | `.?AVBoardGameChallengeInstance@@` |
| RTTI name behind vtable | `0x190E7A8` | `.?AVAIBoardGamePlayer@@` |
| RTTI name behind vtable | `0x190E5F8` | `.?AVBoardGameUnitCollectionComponentResource@@` |
| RTTI name behind vtable | `0x1796148` | `.?AVRTTIObject@@` |
| globals in `.data` | `0x8983150`, `0x8982A88` | present |
| chain function in `.text` (prologue `40 53 48 83 EC 20 …`) | `0xE3DF40` | present, executable |
| "type description" record | `0x21769D0` | record present at exactly this RVA, but it is a Decima class-info record, not an MSVC TypeDescriptor — my RTTI-layout assumption was wrong, the RVA is right |

So: **same bytes, same exe**. Strikers' Steam build id `14835813` vs our
recorded `13801562` is a depot/manifest discrepancy only — the binary content
matches at every sampled location (also consistent with the identical PE
timestamp + SizeOfImage). Strikers itself handles updates by refusing writes
unless the runtime build matches (`memory-tables.md`: "Check the build before
any write, and refuse on any other") — we should copy that guard.

## Observation: JustAddMP/core today

- v0.13.0 (pushed 2026-10-20), one author, working MafiaDE two-PC test: launcher
  with DLL injection, optional `version.dll` proxy, MinHook, **D3D11 Present
  hook** + overlay, Steam P2P and UDP transports, host-authoritative sessions,
  Steam lobbies/invites, co-op panels, remote motion controllers, SWF patcher,
  fuzz/loopback test suites. Architecture/protocol/interest docs present.
- **No license file** (`license: null`, no LICENSE in repo root as of
  2026-10-20). Hub ground rule 7: cannot vendor/build-on inside a PR without
  asking the author or shipping our work as an independent patch.
- **Renderer gap:** their README states D3D12 games "need another Present hook".
  HFW is D3D12 (observation 10) → the overlay/hook layer would have to be ported
  by us regardless.
- Build prerequisite: VS2022 x64 C++ toolset — not installed on this machine
  (owner approval needed per ground rule 1).

## Derived: route comparison

**(a) Revive the engine's own net layer.**
*Simple form — flip NoOp to real — is dead:* the real `NetSessionManager` was
link-stripped (no vtable, no COL, no code; observation 3), and the game's
`SteamNetworkingSockets012` acquisition is compiled out (observation 5). There
is no dormant transport to wake; re-implementing a class the linker removed
means writing it ourselves.
*Hybrid form — implement against the engine's interface — is real but bounded:*
the NoOp vtable **is** the exact contract the engine's matchmaking/party code
calls (observable from call sites), and `MatchmakingService`/`StateMatchmaking`/
`NetPartyManagerPC` are constructed every run (observations 4, 6), so a
replacement object could in principle re-drive engine-internal flows. Cost:
the engine expects a session/backend world (matchmaking services, telemetry,
Guerrilla backend semantics) that no longer exists; nobody has mapped what pumps
`Net*State` replication or whether any pump runs in single-player. High
uncertainty, weeks of RE before the first two-client test.
Verdict: **not the primary route**, but harvest the NoOp vtable interface and
the constructed matchmaking objects as intelligence, and as a possible later
optimization once a transport exists.

**(b) Game module on JustAddMP/core.**
Fastest route to lobbies/invites/P2P/session UI with tests — but two hard
blockers today: no license (rule 7) and a D3D11-only Present hook vs our D3D12
game (observation 10; would need us to write the D3D12 hook anyway). MSVC
toolchain also needs owner approval. Everything game-specific (puppets, state
sync, quest sync) remains ours either way; core only ships transport/lobby/panel
plumbing.
Verdict: **parked**. Action: ask the author about a license; if one appears and
a D3D12 hook lands, re-evaluate as the transport/lobby layer.

**(c) Own transport feeding the `Net*State` classes puppet-style.**
Evidence for: Strikers proves in-process injection into HFW ships and works (MIT,
shipped on Nexus, our build identity identical, memory map byte-verified above);
the whole `Net*State`/`NetReplicatedObject` replication surface and script MP
exports survive in the exe (phase-1 RTTI harvest); steam_api64 is already loaded
by the game, so our own module can do Steam P2P/lobbies without the game's
stripped `SteamNetworkingSockets012` path; hub playbook stage order
(how-coop-mods-work.md §4) fits this: hook → puppet → authority → world state.
Cost: this is the "closed native engine, no loader" row — weeks to first puppet,
months to combat/quest sync. Risks: unknown entity-spawn surface, unknown
`Net*State` pump, D3D12 overlay choice, per-build address churn (mitigated by
Strikers' re-find methodology + our build guard).
Verdict: **primary route**.

## Decision

**Build route (c):** an injected module (native DLL or .NET, Strikers-style)
with its own transport — Steam P2P/lobbies via `steam_api64` plus UDP fallback
(hub playbook networking-steam-spacewar.md) — driving puppets first and the
engine's `Net*State` structures where they prove usable. Use the engine's own
dormant layer as *reference material* (route (a) intel: NoOp vtable = the
engine's session interface; constructed matchmaking objects = a later
accelerant for lobby/flow code, not a phase-5 dependency). Route (b) is parked
until JustAddMP/core gains a license and a D3D12 hook.

First milestone (playbook stage 1 exit test): our code runs inside HFW, logs
each frame, reads the local player's transform and world id — on a build-verified
copy, with a runtime build-id guard copied from Strikers. Then stage 2: one
puppet, our transport, two instances.

## Unknowns carried into phase 5

- Whether anything pumps `Net*State` replication in single-player runs (if not,
  we serialize those structures ourselves — treat as data, per Strikers).
- WS2_32 socket call sites (telemetry/WinHTTP vs gameplay leftovers).
- Which renderer the game creates at runtime by default (static evidence says
  D3D12; confirm at runtime before choosing an overlay/hook layer).
- What invokes `FUN_140d96060` (NetGameDummy factory) and
  `FUN_1400ba630` (Steam bootstrap) — indirect-only today; map the tables.
- `host unreachable`/telemetry paths behind `StateMatchmaking` — what backend
  they expect (gone services vs local).
- JustAddMP/core license (contact author) — open question from phase 1.

## Sources

- Ghidra 12.1.4 headless (official release zip, SHA-256 verified), analysis of
  the workspace exe copy; post-scripts `local/ghidra_scripts/NetReachability*.java`;
  reports `local/ghidra_net_reachability*.txt` (private state).
- `local/verify_strikers_rva.py`, `local/pe_rtti_ptrcheck.py` (byte checks vs
  Strikers docs and raw RTTI/pointer scans) — outputs in this memo.
- https://github.com/everydayaussie/Strikers — `docs/memory-map.md`,
  `docs/memory-tables.md` (main branch, fetched 2026-10-20, MIT).
- https://github.com/JustAddMP/core — README + repo contents API (2026-10-20).
- PE export table of our exe (35 exports: `MainProg`, `GameMainProg`,
  `D3D12SDK*`, `NVSDK_NGX_D3D12_*`, …).
