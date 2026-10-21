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
$priorPip = @{}
foreach ($name in @('PIP_CONFIG_FILE','PIP_INDEX_URL','PIP_EXTRA_INDEX_URL')) {
    $priorPip[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:TEMP = Join-Path $state 'tmp'
    $env:TMP = $env:TEMP
    # These are public, locked dependencies. Ignore unrelated workstation indexes
    # for this process only; restore the user's environment in finally.
    $env:PIP_CONFIG_FILE = 'nul'
    $env:PIP_INDEX_URL = 'https://pypi.org/simple'
    $env:PIP_EXTRA_INDEX_URL = ''
    if (-not (Test-Path -LiteralPath $venvPython)) {
        & $pythonPath -m venv $venv
        if ($LASTEXITCODE -ne 0) { throw 'Creating the MCP environment failed' }
    }
    & $venvPython -m ensurepip --upgrade
    if ($LASTEXITCODE -ne 0) { throw 'Installing pip failed' }
    & $venvPython -m pip install --index-url https://pypi.org/simple --extra-index-url https://pypi.org/simple --disable-pip-version-check -r (Join-Path $PSScriptRoot 'requirements-windows-py312.lock')
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
    & $pythonPath (Join-Path $PSScriptRoot 'configure_mcp_limits.py')
    if ($LASTEXITCODE -ne 0) { throw 'Configuring bounded MCP editor timeouts failed' }
    Write-Output 'MCP configuration saved. Open Unity and Blender, then restart MCP servers in Codex Settings.'
} finally {
    $env:TEMP = $priorTemp
    $env:TMP = $priorTmp
    foreach ($name in $priorPip.Keys) {
        [Environment]::SetEnvironmentVariable($name, $priorPip[$name], 'Process')
    }
}
