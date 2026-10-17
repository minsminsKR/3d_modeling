# Local entrance lighting chooser

The viewer scrolls through **actual Unity renders**, not an image brightness
filter. One value multiplies all nine local entrance lights by 0–2 in 0.05 steps.
It offers the real start camera, a gate view and a raised vault view. Player
hands/HUD and flashlight are omitted, game simulation is paused, and both owned
reflection probes are recaptured for each level. The maze and game sources are
not changed by the chooser. A selected gain is applied to gameplay separately
after the user reports it.

From the repository root, choose new output directories and run:

```powershell
$env:HAPPYTOY_ENTRANCE_LIGHTING_REVIEW_DIR = Join-Path (Resolve-Path HappyToyV2).Path 'Builds/LightingReview-new'
$env:HAPPYTOY_REVIEW_COMMIT = git rev-parse HEAD
unity test HappyToyV2 --editor-version 6000.6.0f1 --mode PlayMode --filter HappyToy.V2.CloudTests.CloudPlayModeTests.EntranceStartCameraRevealsTheVaultWithoutIncreasingTheShadowBudget --output HappyToyV2/Builds/Verification/LightingReview-new.xml --timeout 900 -- -force-d3d11 -v2-audit-no-desktop-interaction -job-worker-count 2
python HappyToyV2/Tools/lighting_review/serve.py --directory HappyToyV2/Builds/LightingReview-new
```

The last command binds only `127.0.0.1`, chooses a free port, and writes its URL
and PID to `viewer-server.json` inside the generated output. Open that URL in the
in-app browser. The wheel, arrow up/down and slider select levels; the copy
button prepares the exact text to report. The viewer uses no external service.
Run a background server with a hidden window on Windows. Stop its recorded PID
when the review is no longer needed.

`manifest.json` records Unity/game revision, source light names/intensities,
camera poses, frame gains and SHA256. JPEGs, progress, server state and logs are
derived local artifacts under `Builds/`; keep the exporter and viewer source in
Git. The export path is explicitly opt-in; ordinary tests do not render a bank.
