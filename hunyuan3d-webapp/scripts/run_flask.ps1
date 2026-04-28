param(
    [int]$Port = 5000
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $ProjectRoot
$env:FLASK_PORT = "$Port"

conda activate 3d
python app.py

