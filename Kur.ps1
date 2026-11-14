param(
    [string]$GameFolder,
    [string]$InstallFolder = (Join-Path $env:LOCALAPPDATA 'Warlapse-Battle3D-761'),
    [switch]$Launch,
    [switch]$ValidateOnly,
    [switch]$SkipDesktopShortcut
)
$ErrorActionPreference = 'Stop'
$version = '7.6.1'
$locationFile = Join-Path $PSScriptRoot 'install-location.json'

if ($Launch) {
    if (Test-Path -LiteralPath $locationFile -PathType Leaf) {
        $saved = Get-Content -LiteralPath $locationFile -Raw | ConvertFrom-Json
        if ($saved.InstallFolder) { $InstallFolder = $saved.InstallFolder }
    }
    $installedExe = Join-Path $InstallFolder 'Warlapse.exe'
    if (!(Test-Path -LiteralPath $installedExe -PathType Leaf)) { throw 'Modlu oyun bulunamadi. Once Kur.cmd dosyasini calistirin.' }
    Start-Process -FilePath $installedExe -WorkingDirectory $InstallFolder
    return
}

function Find-Warlapse {
    $steamRoots = @()
    foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        $entry = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($entry.SteamPath) { $steamRoots += $entry.SteamPath }
        if ($entry.InstallPath) { $steamRoots += $entry.InstallPath }
    }
    if (${env:ProgramFiles(x86)}) { $steamRoots += Join-Path ${env:ProgramFiles(x86)} 'Steam' }
    $libraries = @($steamRoots)
    foreach ($steamRoot in ($steamRoots | Select-Object -Unique)) {
        $vdf = Join-Path $steamRoot 'steamapps/libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf -PathType Leaf) {
            $content = Get-Content -LiteralPath $vdf -Raw
            foreach ($match in [regex]::Matches($content, '"path"\s*"([^"]+)"')) { $libraries += $match.Groups[1].Value.Replace('\\', '\') }
        }
    }
    foreach ($library in ($libraries | Select-Object -Unique)) {
        $candidate = Join-Path $library 'steamapps/common/Warlapse'
        if (Test-Path -LiteralPath (Join-Path $candidate 'Warlapse.exe') -PathType Leaf) { return $candidate }
    }
}

if (!$GameFolder) { $GameFolder = Find-Warlapse }
if (!$GameFolder) { $GameFolder = (Read-Host 'Warlapse.exe dosyasinin bulundugu Steam klasoru').Trim('"') }
if (!$GameFolder) { throw 'Oyun klasoru belirtilmedi.' }
$source = (Resolve-Path -LiteralPath $GameFolder).Path.TrimEnd('\')
$destination = [IO.Path]::GetFullPath($InstallFolder).TrimEnd('\')
if (!(Test-Path -LiteralPath (Join-Path $source 'Warlapse.exe') -PathType Leaf)) { throw 'Warlapse.exe bulunamadi.' }
if ($destination.Equals($source, [StringComparison]::OrdinalIgnoreCase) -or
    $destination.StartsWith($source + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $source.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Mod Steam oyun klasorunun disinda, ayri ve bos bir klasore kurulmali.' }
if (Test-Path -LiteralPath $destination) { throw "Hedef zaten var: $destination. -InstallFolder ile yeni bir hedef secin." }

$managed = 'Warlapse_Data/Managed'
$gameDll = Join-Path $source "$managed/Assembly-CSharp.dll"
$expected = '9DDD3D1EA5188E4D600877E3A7939FB35180464A448F5568AB13C269B825EF20'
if (!(Test-Path -LiteralPath $gameDll -PathType Leaf) -or (Get-FileHash -LiteralPath $gameDll -Algorithm SHA256).Hash -ne $expected) {
    throw 'Bu Warlapse derlemesi desteklenmiyor. Steam kurulumu degistirilmedi. GitHub Releases sayfasindaki uyumlu surumu kontrol edin.'
}
foreach ($required in @('WarlapseBattle3D.dll', 'Mono.Cecil.dll', 'assets/logo.png', 'OKU.txt')) {
    if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot $required) -PathType Leaf)) { throw "Paket eksik: $required. ZIP dosyasinin tamamini cikarin." }
}
Add-Type -Path (Join-Path $PSScriptRoot 'Mono.Cecil.dll')
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $source $managed))
$reader = [Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver = $resolver
$game = $null
$plugin = $null
try {
    $game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameDll, $reader)
    $pluginPath = Join-Path $PSScriptRoot 'WarlapseBattle3D.dll'
    $plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($pluginPath)
    $start = @($game.MainModule.GetType('Warlapse.UI.MainMenuToolkit').Methods | Where-Object { $_.Name -eq 'Start' -and !$_.HasParameters })
    $entry = @($plugin.MainModule.GetType('Battle3D').Methods | Where-Object Name -eq Install)
    if ($start.Count -ne 1 -or $entry.Count -ne 1 -or !$start[0].HasBody -or !$entry[0].IsStatic) { throw 'Mod baslatma noktasi bulunamadi.' }
    $il = $start[0].Body.GetILProcessor()
    $call = $il.Create([Mono.Cecil.Cil.OpCodes]::Call, $game.MainModule.ImportReference($entry[0]))
    $il.InsertBefore($start[0].Body.Instructions[0], $call)
    $scar = $game.MainModule.GetType('Warlapse.Battle.BattleScarManager')
    $routes = [ordered]@{ PlaceDroplet = 'RouteBloodDrop'; SpawnSpurt = 'RouteBloodBurst' }
    foreach ($nativeName in $routes.Keys) {
        $native = @($scar.Methods | Where-Object Name -eq $nativeName)
        $handler = @($plugin.MainModule.GetType('Battle3D').Methods | Where-Object Name -eq $routes[$nativeName])
        if ($native.Count -ne 1 -or $handler.Count -ne 1 -or !$native[0].HasBody -or !$handler[0].IsStatic -or $handler[0].Parameters.Count -ne $native[0].Parameters.Count + 1 -or $handler[0].ReturnType.FullName -ne 'System.Boolean') { throw "Kan efekti baglanti noktasi bulunamadi: $nativeName" }
        $il = $native[0].Body.GetILProcessor()
        $first = $native[0].Body.Instructions[0]
        $il.InsertBefore($first, $il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
        foreach ($parameter in $native[0].Parameters) { $il.InsertBefore($first, $il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg, $parameter)) }
        $il.InsertBefore($first, $il.Create([Mono.Cecil.Cil.OpCodes]::Call, $game.MainModule.ImportReference($handler[0])))
        $il.InsertBefore($first, $il.Create([Mono.Cecil.Cil.OpCodes]::Brfalse, $first))
        $il.InsertBefore($first, $il.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    }
    $patched = [IO.MemoryStream]::new()
    try { $game.Write($patched); $bytes = $patched.ToArray() } finally { $patched.Dispose() }
    if ($ValidateOnly) { Write-Host "Paket ve oyun uyumlu. $version yama dogrulamasi tamamlandi; kurulum yapilmadi."; return }
    Write-Host 'Oyun dosyalari ayri bir klasore kopyalaniyor...'
    Copy-Item -LiteralPath $source -Destination $destination -Recurse
    Copy-Item -LiteralPath $pluginPath -Destination (Join-Path $destination $managed)
    [IO.File]::WriteAllBytes((Join-Path $destination "$managed/Assembly-CSharp.dll"), $bytes)
    $bundle = Join-Path $destination 'Battle3D-Package'
    New-Item -ItemType Directory -Path $bundle | Out-Null
    foreach ($item in @('assets', 'docs', 'OKU.txt', 'README.md', 'CHANGELOG.md', 'THIRD-PARTY-NOTICES.md', 'Mono.Cecil-LICENSE.txt', 'VERSION.txt')) {
        $bundleSource = Join-Path $PSScriptRoot $item
        if (Test-Path -LiteralPath $bundleSource) { Copy-Item -LiteralPath $bundleSource -Destination $bundle -Recurse }
    }
    $record = [ordered]@{ Version = $version; InstallFolder = $destination; SourceFolder = $source; InstalledAt = [DateTime]::UtcNow.ToString('o') }
    $record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bundle 'installation.json') -Encoding UTF8
    try { $record | ConvertTo-Json | Set-Content -LiteralPath $locationFile -Encoding UTF8 }
    catch { Write-Warning 'Paket klasorune konum kaydedilemedi. Oyunu kurulum klasorundeki Warlapse.exe ile acabilirsiniz.' }
    if (!$SkipDesktopShortcut) {
        try {
            $desktop = [Environment]::GetFolderPath('DesktopDirectory')
            $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $desktop "Warlapse Battle 3D $version.lnk"))
            $shortcut.TargetPath = Join-Path $destination 'Warlapse.exe'
            $shortcut.WorkingDirectory = $destination
            $icon = Join-Path $bundle 'assets/logo.ico'
            $shortcut.IconLocation = if (Test-Path -LiteralPath $icon) { $icon } else { "$destination\Warlapse.exe,0" }
            $shortcut.Description = "Warlapse Battle 3D $version - GitHub surumu"
            $shortcut.Save()
        } catch { Write-Warning 'Masaustu kisayolu olusturulamadi. Oyunu-Ac.cmd dosyasini kullanin.' }
    }
    Write-Host "Kuruldu: $destination\Warlapse.exe"
    Write-Host 'Oyunu-Ac.cmd veya masaustu kisayoluyla baslatin. Cevrimdisi savasta konuslandirmadan sonra G tusuna basin.'
} finally {
    if ($plugin) { $plugin.Dispose() }
    if ($game) { $game.Dispose() }
    $resolver.Dispose()
}
