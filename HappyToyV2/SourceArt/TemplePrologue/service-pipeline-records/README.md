# Portable temple generation-stage records

These nine JSON files preserve the complete stage-record structures for the
three generated temple-prologue models already described in
`../generation-pipeline-records.json`. They add the fields omitted from that
settings summary; the existing summary remains the source of the project model
and reference links and the previously recorded original SHA-256 values.

The manifest records each request and job ID, original archive-entry SHA-256,
portable record SHA-256, and each replacement's record-relative filename and
JSON-pointer location. Every
absolute server filesystem path in a stage JSON is replaced in full by an
opaque `<SERVER_ABSOLUTE_PATH_NN>` symbol. Equal paths within a job retain the
same symbol; numbering follows the order of jobs' stage records and JSON
fields. No original server path text or authentication value is retained here.
All other keys, array ordering, settings and non-path result values are kept.
These symbols identify relationships in the original records; they are not
filesystem paths and do not enable reconstruction of the original path values
or original byte-for-byte JSON. SHA-256 of a portable file uses its UTF-8/LF
bytes, so checkout newline conversion does not invalidate verification.

The existing summary's `model_identifier` for the Hunyuan shape stage is a
separately recorded identifier, not a field in that stage's source JSON.

Verify the portable files and their consistency with the existing index from a
fresh clone; no local download archive is needed:

```powershell
python HappyToyV2/SourceArt/TemplePrologue/service-pipeline-records/verify_service_records.py
```

When the original local job ZIPs are independently available, additionally
check the source archive-entry hashes and normalization. Supply the directory
containing the three `<job-id>.zip` files; this option reads but does not copy
or modify the archives:

```powershell
python HappyToyV2/SourceArt/TemplePrologue/service-pipeline-records/verify_service_records.py --archives <archive-directory>
```

Without archives, the verifier checks the two recorded original-hash lists for
consistency; it cannot re-read the original bytes or recover removed paths.
Models, reference inputs, editable Blender sources and reproduction tools remain
under `../pine/`, `../temple/` and their related authoring folders.
