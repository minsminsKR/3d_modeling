# Run manually when ready for an interactive game window. No automated movement.
$reviewProject = Split-Path -Parent $PSScriptRoot
$reviewExecutable = Join-Path $reviewProject 'Builds/Windows/HappyToyV2.exe'
if (-not (Test-Path -LiteralPath $reviewExecutable)) { throw 'Build the Windows player first.' }
$reviewStamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$reviewOutput = Join-Path $reviewProject "Verification/ManualRuns/$reviewStamp"
New-Item -ItemType Directory -Path $reviewOutput -ErrorAction Stop | Out-Null
$reviewLog = Join-Path $reviewOutput 'player.log'
$reviewArguments = '-v2-play-review "{0}" -logFile "{1}"' -f $reviewOutput, $reviewLog
Start-Process -FilePath $reviewExecutable -ArgumentList $reviewArguments -WindowStyle Normal -Wait
Write-Output "Review output: $reviewOutput"
