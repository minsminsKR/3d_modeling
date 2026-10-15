# HappyToyV2 development workspace

This workspace is for developing and testing HappyToyV2, a horror game inspired
by Shadow Corridor (그림자복도), at the spin-off-comparable scope described by
the user. The Unity project is under `HappyToyV2/`.

These are persistent project instructions that must travel with the repository
so development can continue on another machine. The repository is
https://github.com/minsminsKR/3d_modeling ; HappyToyV2 pull and push requests
target its GitHub `origin/main` branch.

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

# Clean superseded HappyToyV2 builds after pushing

User reaffirmed on 2026-10-12: after pushing, keep only the newest local build
and delete past builds to reclaim disk space, following the safeguards below.

Persistent user instruction, 2026-10-12: after a successful HappyToyV2 push,
remove superseded local HappyToyV2 player builds to prevent disk accumulation.

- Keep one latest verified playable build, including its executable, _Data,
  DLLs, credits and build/input manifest. Documentation-only pushes may retain
  the existing verified player when its gameplay inputs are unchanged.
- After confirming the remote branch matches the pushed commit and the retained
  player is valid, delete older build directories only inside HappyToyV2/Builds.
- Resolve and validate every deletion target as a child of that exact Builds
  directory. Do not follow reparse points or delete the retained build, source
  assets, authoring originals, saves or unrelated projects.
- Do not delete a running player's build; report it and defer that directory.
  If no verified replacement exists or the push failed, keep the last playable
  build until a valid replacement is available.
- Report the cleanup and provide the retained executable link when relevant.

# Preferred 3D model generation workflow

Persistent user instruction, 2026-10-12: create or regenerate 3D models with
this repository's modeling webapp, using the user's open right-sidebar browser.

- Source reference: https://github.com/minsminsKR/3d_modeling ; application code
  is under hunyuan3d-webapp. Read its current setup and UI capabilities first.
- User-designated live site: https://agrees-mic-icon-contribution.trycloudflare.com/ .
  Use that site in the existing in-app browser for new model generation or
  regeneration. If a future tunnel changes, use the user-provided current URL.
- When a reference image is needed, generate the image, submit it through that
  webapp, and generate the base 3D model there. Keep the actual input image and
  its generation prompt/settings with the resulting model's provenance.
- Preserve the generated original GLB/PBR, reference inputs, generation settings
  and job provenance in source control. Do not substitute another generator.
- Copy all required game-related outputs from the webapp's temporary or ignored
  job directories into tracked project authoring sources, such as
  `HappyToyV2/SourceArt/`. A server job or download link alone is not a source
  handoff; preserve originals, textures, rig/animation files and provenance.
- Existing models generated by this app may be completed locally for rigging,
  skin repair, UVs, game LODs, animation, editable Blender sources and Unity
  integration. If the underlying model needs to be generated again, return to
  the designated webapp. Do not hand-build a substitute base model instead.

- Follow-up user preference, 2026-10-12: choose the rigging/editing workflow that
  gives better quality and is more efficient. The webapp also supports AI rigging
  and joint editing; local Blender rig/weight/animation finishing is authorized.
  Preserve the editable source and reproducible tools with the resulting game.

User preference, 2026-10-15: the designated modeling webapp login normally has the password already populated. Click the workspace-open button directly; do not ask the user to log in before trying the populated form. Never log or disclose the password.
