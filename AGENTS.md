# Repository handoff and push policy

The user requires every related source needed to pull this repository on another
machine and continue editing to be committed and pushed with the work.

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
- A push is complete only after the intended remote branch matches the local commit.
  Push to the branch requested by the user; do not force-push to replace remote work.

# HappyToyV2 pull means latest playable GitHub main

Persistent user instruction, 2026-10-12: whenever the user asks to pull/update
HappyToyV2 (including "happy toy v2 풀해줘"), treat it as a request to make the
latest GitHub `origin/main` playable on this machine, not merely update sources.

- Fetch the actual GitHub main branch at request time. Preserve local changes
  and integrate without discarding or force-overwriting local/remote work.
- Hydrate the required Git LFS assets, then build a fresh Windows player from
  that updated main using the pinned Unity version and project build pipeline.
- Verify build success and current source-input hashes. Check GitHub main again
  before delivery; if newer main commits arrived, incorporate them and rebuild
  before calling the player the latest version.
- Provide a clickable link to the newly built executable. Never substitute an
  older existing build or claim a source-only pull is a playable update.
- If a build or integration is blocked, report the exact blocker and that a
  latest-main playable build is not yet ready. Do not label an older executable
  as the latest main build.

# Clean superseded HappyToyV2 builds after pushing

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
