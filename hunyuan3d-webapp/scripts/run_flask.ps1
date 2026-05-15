param(
    [int]$Port = 5000
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $ProjectRoot
$env:FLASK_PORT = "$Port"

$CondaExe = Join-Path $env:USERPROFILE "anaconda3\Scripts\conda.exe"
if (Test-Path -LiteralPath $CondaExe) {
    & $CondaExe run -n 3d python app.py
} else {
    conda run -n 3d python app.py
}
