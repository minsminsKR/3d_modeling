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
