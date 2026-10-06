param([Parameter(Mandatory=$true)][string]$BuildRoot,[Parameter(Mandatory=$true)][string]$RunRoot)
$taskLive=Get-Process -Name Unity,HappyToyV2 -ErrorAction SilentlyContinue
if($taskLive){throw 'Unity/player process unexpectedly exists before controlled native review'}
$taskCaptureRoot=Join-Path $RunRoot 'native-art-visible'
New-Item -ItemType Directory -Path $taskCaptureRoot -ErrorAction Stop | Out-Null
$taskNativeArgs=@('-v3-graphics-output',$taskCaptureRoot,'-screen-width','1600','-screen-height','900','-logFile',(Join-Path $taskCaptureRoot 'player.log'))
$taskNative=Start-Process -FilePath (Join-Path $BuildRoot 'HappyToyV2.exe') -ArgumentList $taskNativeArgs -WorkingDirectory $BuildRoot -WindowStyle Normal -PassThru
$taskWatch=[System.Diagnostics.Stopwatch]::StartNew()
$taskRecord=[ordered]@{scope='Explicit root visible native-only lease; supported observer camera art review, no survival/performance claim';pid=$taskNative.Id;startedUtc=[DateTime]::UtcNow.ToString('o');executable=Join-Path $BuildRoot 'HappyToyV2.exe';output=$taskCaptureRoot;arguments=$taskNativeArgs;windowStyle='Normal';maximumNaturalWallSeconds=250;status='RUNNING';exitCode=$null}
$taskRecord | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $RunRoot 'native-process.json') -Encoding UTF8
Write-Output ('NATIVE_VISIBLE_STARTED exactPID='+$taskNative.Id)
$taskNextReport=30
while(-not $taskNative.WaitForExit(5000)){
 if($taskWatch.Elapsed.TotalSeconds-ge250){Stop-Process -Id $taskNative.Id -ErrorAction Stop;$taskRecord.status='STOPPED_250_SECOND_DEADLINE';break}
 if($taskWatch.Elapsed.TotalSeconds-ge$taskNextReport){Write-Output ('NATIVE_VISIBLE_RUNNING exactPID='+$taskNative.Id+' naturalWallSeconds='+[Math]::Round($taskWatch.Elapsed.TotalSeconds,2));$taskNextReport+=30}
}
$taskNative.Refresh()
if($taskNative.HasExited){if($taskRecord.status-eq'RUNNING'){$taskRecord.status='EXITED'};$taskRecord.exitCode=$taskNative.ExitCode}
$taskRecord.completedUtc=[DateTime]::UtcNow.ToString('o');$taskRecord.naturalWallSeconds=[Math]::Round($taskWatch.Elapsed.TotalSeconds,3)
$taskRecord | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $RunRoot 'native-process.json') -Encoding UTF8
$taskRecord | ConvertTo-Json -Depth 4
if($taskRecord.status-ne'EXITED'-or$taskRecord.exitCode-ne0){exit 2}
