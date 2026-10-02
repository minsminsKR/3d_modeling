<#
Runs only when invoked explicitly, against an installed, licensed Unity editor.
Never installs Unity, signs in, generates/expands a scene, pushes, or publishes.
Runtime audits require a rendered Windows desktop. Do not add -nographics.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [switch]$BuildPlayer,
    [string[]]$Audit = @(),
    [switch]$AllowExistingPlayer,
    [int]$EditorTimeoutSeconds = 1200
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'audit_manifest.json') -Raw | ConvertFrom-Json
$baseline = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'scene_baseline.json') -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw 'Provide the actual installed Unity editor executable path.' }
if ($env:OS -ne 'Windows_NT' -and ($BuildPlayer -or $Audit.Count -gt 0)) { throw 'This runner builds/runs the Windows player. Run these options on Windows with the matching Unity build module.' }
foreach ($name in $Audit) {
    if (-not ($manifest.audits | Where-Object name -eq $name)) { throw "Unknown audit '$name'. See audit_manifest.json." }
}
function Assert-PreservedScene {
    foreach ($entry in $baseline.protected_files.PSObject.Properties) {
        $path = Join-Path $project $entry.Name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Protected file is missing: $($entry.Name)" }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
            throw "Authored file changed: $($entry.Name). Review the change; do not regenerate the scene or baseline."
        }
    }
}
function Invoke-CheckedProcess([string]$Executable, [string]$Arguments, [int]$TimeoutSeconds) {
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WorkingDirectory $project -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        # This process was created by this invocation; leave unrelated Unity instances alone.
        Stop-Process -Id $process.Id -Force
        throw "Timed out after $TimeoutSeconds seconds: $Executable"
    }
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "Process failed with exit code $($process.ExitCode): $Executable. Read its log." }
}
Assert-PreservedScene
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$output = Join-Path $project "Verification/quality-runs/$stamp"
New-Item -ItemType Directory -Path $output | Out-Null
$editorReport = Join-Path $project 'Verification/quality-unity/editor-validation.json'
# Do not accept a report left by a previous successful invocation.
if (Test-Path -LiteralPath $editorReport) { Move-Item -LiteralPath $editorReport -Destination (Join-Path $output 'previous-editor-validation.json') }
$method = if ($BuildPlayer) { 'HappyToy.V2.Editor.QualityValidation.BuildWindows' } else { 'HappyToy.V2.Editor.QualityValidation.Validate' }
$editorLog = Join-Path $output 'editor.log'
$arguments = '-batchmode -quit -projectPath "{0}" -executeMethod {1} -logFile "{2}"' -f $project, $method, $editorLog
Invoke-CheckedProcess $UnityEditor $arguments $EditorTimeoutSeconds
Assert-PreservedScene
if (-not (Test-Path -LiteralPath $editorReport)) { throw "Unity produced no current editor validation report. Read $editorLog" }
$validated = Get-Content -LiteralPath $editorReport -Raw | ConvertFrom-Json
if ($validated.status -ne 'PASS') { throw 'Unity editor validation did not pass.' }
Copy-Item -LiteralPath $editorReport -Destination $output
if ($validated.unityVersion -ne $manifest.unity_version) { Write-Warning "Ran Unity $($validated.unityVersion); project declares $($manifest.unity_version)." }
$player = Join-Path $project 'Builds/Windows/HappyToyV2.exe'
$buildManifestPath = Join-Path $project 'Builds/Windows/quality-build.json'
$verifiedBuild = $false
if ($Audit.Count -gt 0) {
    if (-not (Test-Path -LiteralPath $player)) { throw 'No Windows player found. Rerun with -BuildPlayer and the Windows build module installed.' }
    if (Test-Path -LiteralPath $buildManifestPath) {
        $buildManifest = Get-Content -LiteralPath $buildManifestPath -Raw | ConvertFrom-Json
        $verifiedBuild = $buildManifest.status -eq 'PASS'
        $inputCount = 0
        foreach ($directory in @('Assets','ProjectSettings','Packages')) { $inputCount += @(Get-ChildItem -LiteralPath (Join-Path $project $directory) -File -Recurse).Count }
        if ($inputCount -ne @($buildManifest.inputs).Count) { $verifiedBuild = $false }
        foreach ($entry in $buildManifest.inputs) {
            $path = Join-Path $project $entry.path
            if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { $verifiedBuild = $false; break }
        }
    }
    if (-not $verifiedBuild -and -not $AllowExistingPlayer) { throw 'Player freshness cannot be verified. Use -BuildPlayer, or explicitly -AllowExistingPlayer for diagnostic-only old-build checks.' }
    if (-not $verifiedBuild) { Write-Warning 'Existing player freshness is UNVERIFIED. Results cannot validate the current source.' }
}
$results = @()
foreach ($name in $Audit) {
    $entry = $manifest.audits | Where-Object name -eq $name
    $auditOutput = Join-Path $output $name
    New-Item -ItemType Directory -Path $auditOutput | Out-Null
    $resultPath = Join-Path $auditOutput $entry.result
    $target = if ($entry.argument_kind -eq 'file') { $resultPath } else { $auditOutput }
    $log = Join-Path $auditOutput 'player.log'
    $arguments = '{0} "{1}" -logFile "{2}" -screen-fullscreen 0 -screen-width 1600 -screen-height 900' -f $entry.flag, $target, $log
    $status = 'FAIL'; $reason = ''
    try {
        Invoke-CheckedProcess $player $arguments $entry.timeout_seconds
        if (-not (Test-Path -LiteralPath $resultPath)) { throw 'Audit exited without its required result file.' }
        $null = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        $runtimeErrors = Select-String -LiteralPath $log -Pattern 'NullReferenceException|MissingReferenceException|MissingMethodException|IndexOutOfRangeException|InvalidOperationException|AssertionException|error CS[0-9]+'
        if ($runtimeErrors) { throw 'Player log contains an exception, assertion or compilation error.' }
        $status = 'PASS'
    } catch { $reason = $_.Exception.Message; Write-Warning "$name failed: $reason" }
    Assert-PreservedScene
    $results += [pscustomobject]@{ audit=$name; status=$status; currentBuildVerified=$verifiedBuild; result=$resultPath; log=$log; reason=$reason }
    $results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'run-summary.json') -Encoding UTF8
}
Write-Output "Unity validation output: $output"
if (@($results | Where-Object status -ne 'PASS').Count -gt 0) { throw 'One or more runtime audits failed. Inspect run-summary.json and each log.' }
