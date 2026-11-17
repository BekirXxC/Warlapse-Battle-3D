param([Parameter(Mandatory = $true)][string]$GameFolder, [string]$OutputPath)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (!$OutputPath) { $OutputPath = Join-Path $repo 'WarlapseBattle3D.dll' }
$managed = Join-Path (Resolve-Path -LiteralPath $GameFolder).Path 'Warlapse_Data/Managed'
$gameDll = Join-Path $managed 'Assembly-CSharp.dll'
$expected = '9DDD3D1EA5188E4D600877E3A7939FB35180464A448F5568AB13C269B825EF20'
if (!(Test-Path -LiteralPath $gameDll) -or (Get-FileHash -LiteralPath $gameDll -Algorithm SHA256).Hash -ne $expected) { throw 'Desteklenen Steam Warlapse derlemesi gerekli.' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $csc)) { throw '.NET Framework 4 C# derleyicisi bulunamadi.' }
$assemblies = @('UnityEngine.CoreModule', 'UnityEngine.IMGUIModule', 'UnityEngine.TextRenderingModule', 'Unity.InputSystem', 'UnityEngine.PhysicsModule', 'UnityEngine.ImageConversionModule', 'UnityEngine.InputLegacyModule', 'Unity.RenderPipelines.Core.Runtime', 'Unity.RenderPipelines.Core.Runtime.Shared', 'Unity.RenderPipelines.Universal.Runtime', 'netstandard')
$refs = foreach ($assembly in $assemblies) {
    $assemblyPath = Join-Path $managed "$assembly.dll"
    if (!(Test-Path -LiteralPath $assemblyPath)) { throw "Gerekli oyun bagimliligi eksik: $assembly.dll" }
    "/r:$assemblyPath"
}
$vehicles = Join-Path $repo 'Vehicles.w3mesh.gz'
$logo = Join-Path $repo 'assets/logo.png'
foreach ($resource in @($vehicles, $logo)) { if (!(Test-Path -LiteralPath $resource)) { throw "Gomulu kaynak eksik: $resource" } }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Filter 'Battle3D*.cs' -File | Sort-Object Name | ForEach-Object FullName)
if (!$sources.Count) { throw 'Mod kaynak kodu bulunamadi.' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$outputParent = Split-Path -Parent $OutputPath
if (!(Test-Path -LiteralPath $outputParent)) { New-Item -ItemType Directory -Path $outputParent -Force | Out-Null }
& $csc /nologo /optimize+ /codepage:65001 /target:library "/out:$OutputPath" "/resource:$vehicles,Warlapse.Vehicles" "/resource:$logo,Warlapse.Logo" $refs $sources
if ($LASTEXITCODE -ne 0) { throw "C# derlemesi basarisiz: $LASTEXITCODE" }
Write-Host "Derlendi: $OutputPath"
Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256
