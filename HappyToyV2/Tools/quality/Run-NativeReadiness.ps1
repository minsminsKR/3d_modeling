param(
    [ValidateSet('Build','Routes','All')][string]$Phase='Build',
    [string]$Project='',
    [string]$BuildDirectory='',
    [string]$EvidenceDirectory=''
)
$ErrorActionPreference='Stop'
if (!$Project) {$Project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))}
if (!$EvidenceDirectory) {$EvidenceDirectory=Join-Path $Project 'Temp/NativeReadiness'}
$taskSession=Join-Path $EvidenceDirectory ('native-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $taskSession | Out-Null
if (!$BuildDirectory) {
    if ($Phase -eq 'Routes') {throw 'Routes requires the exact fresh build directory; no automatic older-build selection.'}
    $BuildDirectory=Join-Path (Join-Path $Project 'Builds') (Split-Path -Leaf $taskSession)
}
if ($Phase -in @('Build','All')) {
    if (Test-Path -LiteralPath $BuildDirectory) {throw 'Choose a fresh unique output directory instead of overwriting/reusing an older review build.'}
    # Root agent only. This custom build method retains variants via AssetDatabase,
    # validates the preserved scene and captures fingerprints after anchor creation.
    & unity run $Project --editor-version 6000.6.0f1 --timeout 1200 -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-build-output $BuildDirectory -logFile (Join-Path $taskSession 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Unity build failed; retain its log and do not run this player.' }
}
if ($Phase -in @('Routes','All')) {
    $taskExe=Join-Path $BuildDirectory 'HappyToyV2.exe'
    $taskManifest=Join-Path $BuildDirectory 'quality-build.json'
    if (!(Test-Path -LiteralPath $taskExe) -or !(Test-Path -LiteralPath $taskManifest)) {throw 'Build executable/manifest missing'}
    $taskCases=@(
        @{Name='corridor73';Flag='-v2-corridor-play-output';Seed='73';Report='native-corridor-play.json'},
        @{Name='corridor211';Flag='-v2-corridor-play-output';Seed='211';Report='native-corridor-play.json'},
        @{Name='school';Flag='-v2-school-play-output';Seed='';Report='native-school-play.json'}
    )
    foreach ($taskCase in $taskCases) {
        $taskOutput=Join-Path $taskSession $taskCase.Name
        New-Item -ItemType Directory -Path $taskOutput | Out-Null
        $taskArgs=@($taskCase.Flag,$taskOutput,'-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-logFile',(Join-Path $taskOutput 'Player.log'))
        if ($taskCase.Seed) {$taskArgs+=@('-v2-audit-seed',$taskCase.Seed)}
        # Start-Process does not quote array elements itself. Reject embedded quotes
        # and quote each complete argument; all generated paths have no trailing slash.
        if ($taskArgs | Where-Object { $_ -match '["\r\n]' }) {throw 'Unsupported quote/newline in native command argument'}
        $taskQuoted=$taskArgs | ForEach-Object { '"'+$_+'"' }
        # No -batchmode/-nographics/-nosound. The audits enable runInBackground and
        # use the real graphics device/listener clock. These hidden runs are not
        # foreground hardware presentation performance measurements.
        $taskProcess=Start-Process -FilePath $taskExe -ArgumentList $taskQuoted -WorkingDirectory $BuildDirectory -WindowStyle Hidden -PassThru
        $taskDeadline=[DateTime]::UtcNow.AddSeconds(660)
        while (!$taskProcess.WaitForExit(1000)) {
            if ($taskProcess.HasExited) {break}
            if ([DateTime]::UtcNow -ge $taskDeadline) {
                # Stop only the exact process started by this case; preserve evidence.
                Stop-Process -Id $taskProcess.Id
                throw ('Native '+$taskCase.Name+' exceeded 660 seconds; inspect '+$taskOutput)
            }
        }
        if ($taskProcess.ExitCode -ne 0) {throw ('Native '+$taskCase.Name+' exited '+$taskProcess.ExitCode+'; inspect '+$taskOutput)}
        & python (Join-Path $PSScriptRoot 'verify_native.py') --project $Project --manifest $taskManifest --report (Join-Path $taskOutput $taskCase.Report) --output (Join-Path $taskOutput 'retained-evidence-check.json')
        if ($LASTEXITCODE -ne 0) {throw ('Retained evidence check failed; inspect '+$taskOutput)}
    }
}
Write-Output ('Evidence session: '+$taskSession)
