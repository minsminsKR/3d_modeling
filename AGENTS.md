# HappyToyV2 development workspace

This workspace is for developing and testing HappyToyV2, a horror game inspired
by Shadow Corridor (그림자복도), at the spin-off-comparable scope described by
the user. The Unity project is under `HappyToyV2/`.

These are persistent project instructions that must travel with the repository
so development can continue on another machine. The repository is
https://github.com/minsminsKR/3d_modeling ; HappyToyV2 pull and push requests
target its GitHub `origin/main` branch.

Persistent project preferences are recorded in `MEMORY.md`; read it when
continuing work. The safeguards and operational instructions below still apply.

# Do not interrupt the user's desktop — user instruction, 2026-10-25

Keep testing, building and authoring in the background. Do not minimize, focus,
activate, close or send OS input to the user's running game or other work.
Do not open a new visible Unity/Blender/player window for verification.
Use batch/background execution, hidden windows, BelowNormal priority and the
project's desktop-input/cursor isolation flag. Feature and retry runners enable
these safeguards by default; preserve that default. Hiding a startup window alone
does not guarantee that later Unity windows or GPU work cannot disrupt the user.
While the user is gaming, continue source work and lightweight CPU checks; defer
GPU renders and native-player/FPS audits unless separately requested by the user.
Do not use visible-window capture runners or native routes as a background fallback.

# Maintainable HappyToyV2 code — user instruction, 2026-10-13

For game code changes, use `HappyToyV2/CODE_MAP.md` to find the affected feature
and its checks. Read the relevant modules instead of routinely loading every
script, historical verification report or the entire repository.

Keep one clear responsibility per module. UI screens own their controls and
receive presentation data/actions; AI collaborators own sensing, attacks and
navigation state. Use explicit inputs, results and callbacks at boundaries.
Keep independent rules in `HappyToy.V2.Core`, with no game/session/Unity dependency.
Adding `partial` files alone does not establish a module boundary. Preserve
scene script GUIDs, serialized names and save compatibility when refactoring.

Use the feature verification profiles documented in `HappyToyV2/CODE_MAP.md`.
Run fast rule checks and relevant integration checks for each change; broaden
coverage when shared behavior, assembly boundaries or unresolved failures require
it. Run both complete Unity test suites and a player build for broad refactors
or release readiness. Update the code map/profiles when ownership or entry points
change. Keep diagnostic code opt-in and preserve the quiet verification policy.

# School anomaly judgment loop — user direction, 2026-10-24

The user has now authorized implementation of the previously planned loop.
- A new run's first passage must be normal. Later passages use a 50% anomaly
  draw, with an anomaly guaranteed after three consecutive normal passages.
  The three initial anomaly kinds have equal weights; these are this game's
  initial tuning values, not a claim about Exit 8's proprietary probabilities.
- Normal + forward and anomaly + backward advance one hour. Either wrong
  judgment resets the clock to 00:00. Judge only at the actual masked transfer.
- The study-room sign crossing triggers Baby or the far-to-near blackout.
  As updated by the user on 2026-10-28, Baby is scaled to two thirds and appears
  crying behind the player at the shop as the school lights go out. A witnessed
  heartbeat and low ambience build tension; pursuit begins after 60 simulation
  seconds and contact causes defeat. Reuse the authored model/cry/chase assets.
- As updated on 2026-10-28, the pedestrian anomaly uses the existing rig: a
  steady red-light pose trembles, while irregular dark pulses change the limbs
  and advance it in steps. Preserve the solid player-blocking capsule and
  stop-on-obstruction behavior.
- Blackout extinguishes the nine aisle lamps at one-second intervals. Five
  simulation seconds after the final light goes out causes defeat. A completed
  backward transfer resolves the old event and restores normal lighting.
  Keep normal exposure during blackout so the player flashlight remains usable;
  the existing five-second deadline still starts at the ninth extinguished lamp.
- On a Baby transfer, immediately stop pursuit/attacks, carry the retiring actor
  through the same rigid seam transform as the player, and fade its body/cry
  over 1.8 simulation seconds. Pause and talisman hold this cosmetic tail.
  Checkpoints record the next logical passage, not the already-resolved threat.
- Preserve RNG, current event, trigger/time, pursuit and Baby pose in optional
  v3 checkpoint data. Older saves without it resume a normal passage without
  guessing past judgments or losing saved clock hours.
- Pause and talisman time stop hold event timers, actors and their sound.
  No eight-hour victory/escape rule is introduced by these initial events.

# Repository handoff and push policy

The user requires every related source needed to pull this repository on another
machine and continue editing to be committed with the work.

Persistent user instruction, 2026-10-12: push to a remote only when the user
explicitly asks to push (for example, "푸시해" or "push"). Completing, fixing,
building, pulling, updating, committing, or continuing work does not authorize
a push. Earlier requests or policies that implied automatic pushes do not
authorize future pushes; this instruction supersedes them. Do not routinely
ask for push approval when no push was requested. Keep completed work local
until the user explicitly requests a push.

Persistent user instruction: when a HappyToyV2 push is explicitly requested,
push to GitHub `origin/main`, regardless of the current local branch. Fetch
`origin/main` at request time and preserve/integrate remote changes before
pushing. A feature branch push alone does not fulfill a HappyToyV2 push request.

Persistent user instruction, 2026-10-12: whenever a push is explicitly requested,
include all related models and resources, including original generated models,
PBR textures, editable Blender/rig/animation sources, Unity assets and .meta files,
reference inputs, generation settings/provenance, reproducible tools and licenses.
Audit ignored and untracked authoring files and verify source completeness before
pushing; a code-only push that omits required model/resource sources is incomplete.
This does not authorize automatic pushes or inclusion of builds, caches, secrets,
redundant previews, or personal machine settings.
- Include project code, Unity Assets and their .meta files, Packages, ProjectSettings,
  editable authoring sources (including .blend), textures, shaders, source generators,
  import/export and validation tools, dependency instructions, and asset licenses.
- Do not rely on ignored files, another local checkout, local-only generator scripts,
  absolute workstation paths, or installed caches for required project sources.
- Check ignored authoring sources as well as untracked files before committing.
  For HappyToyV2, run `python HappyToyV2/Tools/quality/check_source_completeness.py`
  after staging. Resolve missing required sources before a requested push.
- Keep builds, Library, Temp, caches, redundant staging/preview outputs, secrets and
  personal machine approval settings outside source commits. They must be
  reproducible or optional; they cannot be the only copy of an editable source.
- Document pinned tools and clone/pull setup. Use repository-relative paths for
  authoring tools. Use Git LFS where configured and preserve attributed assets.
- A HappyToyV2 push is complete only after GitHub `origin/main` matches the
  intended local commit. Do not force-push to replace remote work.

# HappyToyV2 pull means latest playable GitHub main

User clarification, 2026-10-15: a pull/update request means update latest GitHub
main and build the Windows player once. Do not run Unity test suites, launch the
player, or run play/native diagnostics for a pull/update request unless the user
separately asks for those tests. Confirm the build command succeeded and return
the executable link. This clarification takes precedence over broader release
verification requirements for pull/update tasks.

User reaffirmed on 2026-10-12: every Git pull must use the latest remote `main`
branch. Always fetch `origin/main` at request time, regardless of the current branch.

Persistent user instruction, 2026-10-12: whenever the user asks to pull/update
HappyToyV2 (including "happy toy v2 풀해줘"), treat it as a request to make the
latest GitHub `origin/main` playable on this machine, not merely update sources.

- Fetch the actual GitHub main branch at request time. Preserve local changes
  and integrate without discarding or force-overwriting local/remote work.
- Hydrate the required Git LFS assets, then build a fresh Windows player from
  that updated main using the pinned Unity version and project build pipeline.
- Verify that required models, textures and editable resources are present as
  actual files; unresolved LFS pointers or missing resources block delivery.
- Verify build success and current source-input hashes. Check GitHub main again
  before delivery; if newer main commits arrived, incorporate them and rebuild
  before calling the player the latest version.
- Provide a clickable link to the newly built executable. Never substitute an
  older existing build or claim a source-only pull is a playable update.
- If a build or integration is blocked, report the exact blocker and that a
  latest-main playable build is not yet ready. Do not label an older executable
  as the latest main build.

# Group superseded HappyToyV2 builds after every verified new build

Persistent user instruction, 2026-10-24: after successfully creating and verifying
a new player build, create `HappyToyV2/Builds/구버전_삭제대기/` when necessary and
move superseded player build folders into it. This applies whether or not a Git
push was requested. It supersedes the older automatic post-push deletion policy.

- Keep the latest verified playable build directly under `HappyToyV2/Builds/`,
  including its executable, _Data, DLLs, credits and build/input manifest. Update
  `HappyToyV2-Latest.lnk`, when present, to that retained executable.
- Group each old player build as a complete folder under `구버전_삭제대기`,
  preserving its name. Never overwrite an existing destination; defer a name
  collision and report it. Already grouped builds stay in that folder.
- Resolve and validate source and destination paths as children of the exact
  project's Builds directory before moving. Reject reparse points. Identify
  actual player builds; do not group source assets, editable authoring originals,
  modeling downloads, tools, test reports, saves or unrelated projects as builds.
- Never move the retained latest build or a running player's build. Report and
  defer a running old build until it has exited. If no verified replacement
  exists or the new build fails, preserve the last playable build in place.
- Grouping is automatic under this standing instruction; permanent deletion is
  a separate action requiring an explicit user request. A Git push alone does
  not authorize deletion. Grouping does not reclaim disk space.
- Report the grouping and provide the retained executable link when relevant.

# Preferred 3D model generation workflow

Persistent user instruction, 2026-10-23: use the modeling CLI for model generation,
status checks, completion waits, result downloads and generation-history queries.
Use the user's existing right-sidebar web browser for inspecting model appearance,
materials and motion, and for visual joint/skeleton editing. This updates the
2026-10-12 browser-based generation preference; both interfaces must use the same
designated A100 modeling service, job queue and stored results.

Persistent user instruction, 2026-10-23: use this default end-to-end modeling
workflow for future modeling work:
**CLI -> direct webapp inspection -> Blender finishing/optimization -> Unity
integration/verification**.

1. Use the modeling CLI to generate, track, recover and download models from the
   designated service. Preserve the original request/job IDs and provenance.
2. Open the same job in the user's existing right-sidebar webapp and inspect the
   actual interactive 3D result. Rotate it to check silhouette, geometry and
   materials; inspect skeleton/joints and motion when present. Use the web joint
   editor where useful, then collect the updated result with `--refresh`.
3. Use Blender MCP and reproducible authoring tools to finish rigging, skin
   weights, UVs and animation, and optimize geometry, textures and game LODs for
   the asset's intended use. Apply only necessary processing. Preserve editable
   `.blend` sources and the tools/settings needed to reproduce exports. If the
   base shape needs regeneration, return to the designated service through CLI.
4. Use the project's supported Unity CLI/MCP tools to import and integrate the
   asset. Verify scale/orientation, materials, animation, collision and LOD setup
   as applicable, then inspect its appearance and behavior in the game context
   and check relevant performance. Follow `HappyToyV2/CODE_MAP.md` verification
   profiles for the affected Unity feature.

Keep the generated original GLB/PBR, reference inputs, generation settings/job
provenance, editable Blender sources, reproducible tools, and Unity assets with
their `.meta` files together in the repository's authoring/source handoff. Record
what was visually inspected, modified and verified, including remaining issues.

- CLI entry point: `hunyuan3d-webapp/modeling_cli.py`; usage and recovery:
  `hunyuan3d-webapp/CLI-A100.md`. Prefer `--json` and documented exit codes for
  agent automation. Retain request/job IDs and reuse the original request ID
  when recovering an uncertain submission to prevent duplicate generation.
- Follow-up user instruction, 2026-10-23: actively use capabilities/doctor and
  collect/resume in future modeling work. Commands below run from the repository
  root; select the current service with `--server URL` or the saved CLI setting.
  At the start of a modeling session, run
  `python hunyuan3d-webapp/modeling_cli.py capabilities --json` and
  `python hunyuan3d-webapp/modeling_cli.py doctor --json`. Read supported models,
  parameters, actual API defaults, upload limits and default GPU validity before
  selecting generation options. Refresh these checks when the server, model
  configuration or relevant error changes; do not repeat them on every poll.
  Doctor checks configuration/CUDA/storage readiness without loading models or
  running inference; it does not establish visual quality or generation success.
- For immediate batch recovery, use
  `python hunyuan3d-webapp/modeling_cli.py collect REQUEST_UUID --output DIR --json`.
  It collects completed results once and reports pending/failed jobs. To wait
  for remaining work and collect it, use
  `python hunyuan3d-webapp/modeling_cli.py resume REQUEST_UUID --output DIR --json`
  with an appropriate `--timeout` and `--poll-interval`. Keep the same UUID and
  original server. Collect never submits generation; resume only replays a
  locally preserved uncertain submission with its original UUID/inputs after
  confirming that no corresponding server jobs exist. Neither regenerates
  failed or missing previously submitted jobs automatically.
- Inspect `summary`, `failed_jobs`, `pending_jobs`, `download_errors` and
  `status_errors`, not just the process exit code. Codes are 0=success,
  1=access/network/input/readiness error, 2=job failure/partial success (or CLI
  syntax error), 3=wait timeout. Preserve completed downloads on partial failure
  or timeout. Follow `code`, `retryable`, `next_action` and any retry delay;
  inspect per-job errors even when aggregate `PARTIAL_FAILURE.retryable` is false.
  Verified ZIP/receipt reuse is normal. Use `--refresh` when later web rigging or
  joint edits must be included in a new archive, while retaining authoring sources.
- Follow-up user instruction, 2026-10-23: use automatic archive caching and byte
  resume during modeling work. After a disconnected download, rerun
  `python hunyuan3d-webapp/modeling_cli.py download JOB_ID --output DIR --json`,
  or the same `collect`/`resume` command with the original UUID and output folder.
  Keep `.JOB_ID.zip.part` and `.JOB_ID.zip.part.json` together; they contain the
  durable checkpoint used by the next process. Inspect `archive_etag`,
  `resumed_bytes`, `received_bytes` and error `details.resume_available`.
  The CLI verifies the stored prefix, server version and completed ZIP hashes;
  a changed snapshot or server that ignores Range starts a fresh full response.
  Failed integrity checks preserve the last verified final ZIP and receipt.
- The A100 service shares immutable, versioned ZIP snapshots between record and
  download requests. It supports strong ETag, Range/If-Range and HEAD. Web rigging,
  joint edits, changed inputs or authoring files invalidate the snapshot. Use
  collect/resume `--refresh` to fetch those later edits instead of reusing a local
  verified receipt. Server cache eviction can require rebuilding a ZIP and does
  not delete generation originals. `MODELING_ARCHIVE_CACHE_MAX_BYTES` defaults
  to 10 GiB; an oversized archive requires an adequate server cache limit.
- Status polling persists meaningful changes only; a changing `elapsed_seconds`
  display alone does not rewrite the request. Cookies persist when their content
  changes. Local `history --request-id UUID --json` reads that request directly;
  use `history --remote --limit 50 --offset 0 --json` for server records. Preserve
  the state directory across sessions. Performance checks and fixture conditions
  are documented in `hunyuan3d-webapp/CLI-PERFORMANCE.md`; transport/storage gains
  do not imply faster GPU generation.
- Open a CLI result's `web_url` in the existing in-app browser for visual review
  and joint editing, and continue using the same job rather than generating a
  second model for the web. Preserve the existing private workspace login and
  never print or commit passwords, authentication cookies or personal CLI state.

- Source reference: https://github.com/minsminsKR/3d_modeling ; application code
  is under hunyuan3d-webapp. Read its current setup and CLI/UI capabilities first.
- User-designated live site: https://agrees-mic-icon-contribution.trycloudflare.com/ .
  Use this service origin for CLI operations and the existing in-app browser for
  visual inspection/editing. If the tunnel changes, use the current user-provided
  URL for both interfaces.
- When a reference image is needed, generate the image, submit it through the CLI
  to the designated modeling service, and generate the base 3D model there. Keep
  the actual input image and its generation prompt/settings with the resulting
  model's provenance.
- Preserve the generated original GLB/PBR, reference inputs, generation settings
  and job provenance in source control. Do not substitute another generator.
- Copy all required game-related outputs from the webapp's temporary or ignored
  job directories into tracked project authoring sources, such as
  `HappyToyV2/SourceArt/`. A server job or download link alone is not a source
  handoff; preserve originals, textures, rig/animation files and provenance.
- Existing models generated by this app may be completed locally for rigging,
  skin repair, UVs, game LODs, animation, editable Blender sources and Unity
  integration. If the underlying model needs to be generated again, use the CLI
  with the designated modeling service. Do not hand-build a substitute base model
  instead.

- Follow-up user preference, 2026-10-12: choose the rigging/editing workflow that
  gives better quality and is more efficient. The webapp also supports AI rigging
  and joint editing; local Blender rig/weight/animation finishing is authorized.
  Preserve the editable source and reproducible tools with the resulting game.

User preference, 2026-10-15: the designated modeling webapp login normally has the password already populated. Click the workspace-open button directly; do not ask the user to log in before trying the populated form. Never log or disclose the password.
