param(
    [Parameter(Mandatory = $true)][string]$Python,
    [Parameter(Mandatory = $true)][string]$Blender
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$state = Join-Path $project 'Builds\LocalMcp'
$venv = Join-Path $state 'venv'
$venvPython = Join-Path $venv 'Scripts\python.exe'
$blenderPath = (Resolve-Path -LiteralPath $Blender).Path
$pythonPath = (Get-Command $Python -ErrorAction Stop).Source
New-Item -ItemType Directory -Path (Join-Path $state 'tmp') -Force | Out-Null
$priorTemp = $env:TEMP
$priorTmp = $env:TMP
try {
    $env:TEMP = Join-Path $state 'tmp'
    $env:TMP = $env:TEMP
    if (-not (Test-Path -LiteralPath $venvPython)) {
        & $pythonPath -m venv $venv
        if ($LASTEXITCODE -ne 0) { throw 'Creating the MCP environment failed' }
    }
    & $venvPython -m ensurepip --upgrade
    if ($LASTEXITCODE -ne 0) { throw 'Installing pip failed' }
    & $venvPython -m pip install --disable-pip-version-check -r (Join-Path $PSScriptRoot 'requirements-windows-py312.lock')
    if ($LASTEXITCODE -ne 0) { throw 'Installing Blender MCP failed' }
    $addon = Join-Path $venv 'Lib\site-packages\blender_mcp\bundled\addon.py'
    & $blenderPath --background --python-exit-code 1 --python (Join-Path $PSScriptRoot 'install_blender_addon.py') -- --addon-file $addon --status-file (Join-Path $state 'blender-addon.json')
    if ($LASTEXITCODE -ne 0) { throw 'Enabling the Blender addon failed' }
    $unity = (Get-Command unity -ErrorAction Stop).Source
    $codex = (Get-Command codex -ErrorAction Stop).Source
    & $codex mcp add unity -- $unity mcp --project-path $project
    if ($LASTEXITCODE -ne 0) { throw 'Registering Unity MCP failed' }
    & $codex mcp add blender --env DISABLE_TELEMETRY=true --env BLENDER_HOST=127.0.0.1 --env BLENDER_PORT=9876 -- (Join-Path $venv 'Scripts\mcp-for-blender.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Registering Blender MCP failed' }
    Write-Output 'MCP configuration saved. Open Unity and Blender, then restart MCP servers in Codex Settings.'
} finally {
    $env:TEMP = $priorTemp
    $env:TMP = $priorTmp
}
