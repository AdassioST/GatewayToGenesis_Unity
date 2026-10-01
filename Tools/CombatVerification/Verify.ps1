param([switch]$World, [switch]$UI, [string]$EditorPath)
$ErrorActionPreference = 'Stop'
$combatRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$combatDir = Join-Path $combatRoot 'Logs/symphony-verify'
$combatBin = Join-Path $combatDir 'bin'
New-Item -ItemType Directory -Path $combatBin -Force | Out-Null
if (-not $EditorPath) {
    $combatVersion = ((Get-Content (Join-Path $combatRoot 'ProjectSettings/ProjectVersion.txt') -TotalCount 1) -split ': ')[1]
    $EditorPath = Join-Path $env:ProgramFiles ('Unity/Hub/Editor/' + $combatVersion + '/Editor')
}
$combatSdk = Join-Path $EditorPath 'Data/DotNetSdk'
$combatDotnet = Join-Path $combatSdk 'dotnet.exe'
$combatCompiler = Get-ChildItem (Join-Path $combatSdk 'sdk') -Recurse -Filter csc.dll | Select-Object -First 1 -ExpandProperty FullName
$combatStandard = Get-ChildItem (Join-Path $combatSdk 'packs/NETStandard.Library.Ref') -Recurse -Filter netstandard.dll | Select-Object -First 1 -ExpandProperty DirectoryName
$combatFrameworkRefs = Get-ChildItem $combatStandard -Filter '*.dll' | ForEach-Object FullName

function Get-CombatReferences([xml]$project, [bool]$editor) {
    $references = @($project.SelectNodes('//Reference/HintPath') | ForEach-Object {
        $path = $_.InnerText
        if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $combatRoot $path }
        if ($path -notmatch 'UnityReferenceAssemblies' -and [IO.Path]::GetFileName($path) -ne 'Assembly-CSharp.dll') { $path }
    })
    $references += @($project.SelectNodes('//ProjectReference') | ForEach-Object {
        if ([IO.Path]::GetFileNameWithoutExtension($_.Include) -ne 'Assembly-CSharp') {
            Join-Path $combatRoot ('Library/ScriptAssemblies/' + [IO.Path]::GetFileNameWithoutExtension($_.Include) + '.dll')
        }
    })
    if ($editor) { $references += Join-Path $combatBin 'Assembly-CSharp.dll' }
    foreach ($path in $references) { if (-not (Test-Path -LiteralPath $path)) { throw "Missing installed reference: $path" } }
    return $references | Sort-Object -Unique
}

function Invoke-CombatCompile([string]$name, [string[]]$sources, [string[]]$references, [string]$defines, [string]$target = 'library') {
    $lines = @('/nologo', ('/target:' + $target), '/langversion:9.0', '/nostdlib+', '/unsafe+', ('/define:' + $defines), ('/out:"' + (Join-Path $combatBin ($name + '.dll')) + '"'))
    $lines += @($combatFrameworkRefs + $references | Sort-Object -Unique | ForEach-Object { '/reference:"' + $_ + '"' })
    $lines += @($sources | Sort-Object -Unique | ForEach-Object { '"' + $_ + '"' })
    $response = Join-Path $combatDir ($name + '.rsp')
    $log = Join-Path $combatDir ($name + '.log')
    [IO.File]::WriteAllLines($response, $lines)
    & $combatDotnet $combatCompiler ('@' + $response) *> $log
    if ($LASTEXITCODE -ne 0) { Get-Content $log -Tail 30; throw "Compilation failed: $name" }
    Write-Output ("Compiled $name sources: " + ($sources | Sort-Object -Unique).Count)
}

[xml]$combatRuntimeProject = Get-Content (Join-Path $combatRoot 'Assembly-CSharp.csproj')
$combatRuntimeRefs = @(Get-CombatReferences $combatRuntimeProject $false)
$combatRuntimeSources = @($combatRuntimeProject.SelectNodes('//Compile') | ForEach-Object { Join-Path $combatRoot $_.Include } | Where-Object { Test-Path -LiteralPath $_ })
$combatRuntimeSources += @(Get-ChildItem (Join-Path $combatRoot 'Assets/GatewayToGenesis/Scripts') -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\Editor\\|~\\' } | ForEach-Object FullName)
Invoke-CombatCompile 'Assembly-CSharp' $combatRuntimeSources $combatRuntimeRefs $combatRuntimeProject.SelectSingleNode('//DefineConstants').InnerText

[xml]$combatEditorProject = Get-Content (Join-Path $combatRoot 'Assembly-CSharp-Editor.csproj')
$combatEditorRefs = @(Get-CombatReferences $combatEditorProject $true)
$combatEditorSources = @($combatEditorProject.SelectNodes('//Compile') | ForEach-Object { Join-Path $combatRoot $_.Include } | Where-Object { Test-Path -LiteralPath $_ })
$combatEditorSources += @(Get-ChildItem (Join-Path $combatRoot 'Assets/GatewayToGenesis') -Recurse -Filter '*.cs' | Where-Object { $_.FullName -match '\\Editor\\' } | ForEach-Object FullName)
Invoke-CombatCompile 'Assembly-CSharp-Editor' $combatEditorSources $combatEditorRefs $combatEditorProject.SelectSingleNode('//DefineConstants').InnerText

foreach ($path in ($combatRuntimeRefs + $combatEditorRefs | Sort-Object -Unique)) {
    if ((Split-Path $path -Parent) -ne $combatBin) { Copy-Item -LiteralPath $path -Destination $combatBin -Force }
}
Invoke-CombatCompile 'CombatVerify' @((Join-Path $PSScriptRoot 'Runner.cs')) @($combatEditorRefs + (Join-Path $combatBin 'Assembly-CSharp-Editor.dll')) 'UNITY_EDITOR' 'exe'
$combatRuntimeVersion = Get-ChildItem (Join-Path $combatSdk 'shared/Microsoft.NETCore.App') -Directory | Sort-Object Name | Select-Object -Last 1 -ExpandProperty Name
[IO.File]::WriteAllText((Join-Path $combatBin 'CombatVerify.runtimeconfig.json'), ('{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"' + $combatRuntimeVersion + '"}}}'))
$combatArguments = @()
if ($World) { $combatArguments += '--world' }
if ($UI) { $combatArguments += '--ui' }
& $combatDotnet (Join-Path $combatBin 'CombatVerify.dll') @combatArguments | Tee-Object -FilePath (Join-Path $combatDir 'results.txt')
exit $LASTEXITCODE
