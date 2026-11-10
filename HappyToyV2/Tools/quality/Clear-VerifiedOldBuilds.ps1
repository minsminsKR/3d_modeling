param(
    [Parameter(Mandatory = $true)][string]$RetainedBuild
)

$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$buildRoot = (Resolve-Path -LiteralPath (Join-Path $project 'Builds')).Path
$prefix = $buildRoot.TrimEnd('\') + '\'
if ((Get-Item -LiteralPath $buildRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'Builds directory is a reparse point.'
}
$retained = (Resolve-Path -LiteralPath $RetainedBuild).Path
if ((Split-Path -Parent $retained) -ne $buildRoot -or
    ((Get-Item -LiteralPath $retained -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Retained build must be a real immediate child of Builds.'
}
if (-not (Test-Path -LiteralPath (Join-Path $retained 'HappyToyV2.exe'))) {
    throw 'Retained executable is missing.'
}

# Independently recheck the complete source-input manifest immediately before
# touching an older build. The verification is read-only except for its report.
$report = Join-Path $retained ('cleanup-input-verification-' + [guid]::NewGuid().ToString('N') + '.json')
$verify = Join-Path $PSScriptRoot 'verify_build_inputs.py'
& python $verify --project $project --build $retained --output $report | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Retained build did not pass fresh source-input verification.' }

function Check-PlayerFolder([string]$path) {
    $item = Get-Item -LiteralPath $path -Force
    if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { return $false }
    foreach ($required in @('HappyToyV2.exe', 'HappyToyV2_Data', 'quality-build.json', 'UnityPlayer.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $path $required))) { return $false }
    }
    # Unknown top-level entries could be user data. Keep the entire folder if
    # they are present rather than assuming everything inside is disposable.
    $known = @('D3D12', 'HappyToyV2_BackUpThisFolder_ButDontShipItWithYourGame',
        'HappyToyV2_Data', 'MonoBleedingEdge', 'Audio-Credits.txt', 'Graphics-Credits.txt',
        'HappyToyV2.exe', 'UnityPlayer.dll', 'UnityCrashHandler64.exe', 'quality-build.json',
        'build-input-verification.json', 'dstorage.dll', 'dstoragecore.dll',
        'WinPixEventRuntime.dll', 'build-cli.log', 'build-cli.err', 'unity-build.log')
    foreach ($entry in Get-ChildItem -LiteralPath $path -Force) {
        if ($entry.Name -notin $known -and $entry.Name -notlike 'cleanup-input-verification-*.json') { return $false }
    }
    $protectedExtensions = @('.blend', '.fbx', '.obj', '.glb', '.gltf', '.psd', '.kra',
        '.spp', '.sbsar', '.sav', '.save', '.sqlite', '.sqlite3', '.db')
    $protectedFolders = @('SourceArt', 'Saves', 'SaveGames', 'Authoring')
    $unsafe = @(Get-ChildItem -LiteralPath $path -Recurse -Force | Where-Object {
        ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        ($_.PSIsContainer -and $_.Name -in $protectedFolders) -or
        (-not $_.PSIsContainer -and $_.Extension.ToLowerInvariant() -in $protectedExtensions)
    } | Select-Object -First 1)
    return $unsafe.Count -eq 0
}

function Test-PlayerRunning([string]$path) {
    foreach ($process in Get-Process -Name HappyToyV2 -ErrorAction SilentlyContinue) {
        if (-not $process.Path) { throw 'A running player path could not be checked; cleanup is deferred.' }
        if ($process.Path.StartsWith($path.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

$holding = Join-Path $buildRoot '구버전_삭제대기'
if (-not (Test-Path -LiteralPath $holding)) { New-Item -ItemType Directory -Path $holding | Out-Null }
$holding = (Resolve-Path -LiteralPath $holding).Path
if ((Split-Path -Parent $holding) -ne $buildRoot -or
    ((Get-Item -LiteralPath $holding -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Holding directory is not a real immediate child of Builds.'
}
$deleted = [Collections.Generic.List[string]]::new()
$deferred = [Collections.Generic.List[string]]::new()
foreach ($entry in Get-ChildItem -LiteralPath $buildRoot -Directory -Force) {
    if ($entry.FullName -eq $retained -or $entry.FullName -eq $holding) { continue }
    if (-not (Test-Path -LiteralPath (Join-Path $entry.FullName 'HappyToyV2.exe'))) { continue }
    $resolved = (Resolve-Path -LiteralPath $entry.FullName).Path
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Check-PlayerFolder $resolved) -or (Test-PlayerRunning $resolved)) {
        $deferred.Add($entry.Name); continue
    }
    $destination = Join-Path $holding $entry.Name
    if (Test-Path -LiteralPath $destination) { $deferred.Add($entry.Name); continue }
    Move-Item -LiteralPath $resolved -Destination $destination
}
foreach ($entry in Get-ChildItem -LiteralPath $holding -Force) {
    $resolved = (Resolve-Path -LiteralPath $entry.FullName).Path
    if ((Split-Path -Parent $resolved) -ne $holding -or
        -not (Check-PlayerFolder $resolved) -or (Test-PlayerRunning $resolved)) {
        $deferred.Add($entry.Name); continue
    }
    # The exact final absolute path and every nested entry have been checked in
    # this PowerShell process. Never recurse into a computed unchecked path.
    Remove-Item -LiteralPath $resolved -Recurse -Force
    $deleted.Add($entry.Name)
}
if (-not (Get-ChildItem -LiteralPath $holding -Force | Select-Object -First 1)) {
    Remove-Item -LiteralPath $holding -Force
}
[PSCustomObject]@{ Retained = $retained; Deleted = @($deleted); Deferred = @($deferred) } |
    ConvertTo-Json -Depth 3
