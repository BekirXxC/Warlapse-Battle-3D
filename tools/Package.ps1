param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'dist' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$version = (Get-Content -LiteralPath (Join-Path $repo 'VERSION.txt') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION.txt gecersiz.' }
$name = "Warlapse-Battle-3D-$version-Windows"
$stageRoot = Join-Path $repo '.package-work'
$stage = Join-Path $stageRoot ([Guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage $name
New-Item -ItemType Directory -Path $payload,$OutputDirectory -Force | Out-Null
try {
    $required = @('Kur.cmd', 'Kur.ps1', 'Oyunu-Ac.cmd', 'WarlapseBattle3D.dll', 'Mono.Cecil.dll', 'Mono.Cecil-LICENSE.txt', 'OKU.txt', 'README.md', 'CHANGELOG.md', 'THIRD-PARTY-NOTICES.md', 'VERSION.txt', 'assets', 'docs')
    foreach ($item in $required) {
        $source = Join-Path $repo $item
        if (!(Test-Path -LiteralPath $source)) { throw "Paket dosyasi eksik: $item" }
        Copy-Item -LiteralPath $source -Destination $payload -Recurse
    }
    $sums = foreach ($file in (Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName)) {
        $relative = $file.FullName.Substring($payload.Length + 1).Replace('\', '/')
        '{0}  {1}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
    }
    [IO.File]::WriteAllLines((Join-Path $payload 'SHA256SUMS.txt'), [string[]]$sums, [Text.Encoding]::ASCII)
    $archive = Join-Path $OutputDirectory "$name.zip"
    Compress-Archive -LiteralPath $payload -DestinationPath $archive -CompressionLevel Optimal -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$archive.sha256", "$hash  $name.zip`n", [Text.Encoding]::ASCII)
    Get-Item -LiteralPath $archive,"$archive.sha256" | Select-Object FullName,Length
    $noticeFolder = Join-Path $repo 'workshop/battle3d-github-info'
    if (Test-Path -LiteralPath (Join-Path $noticeFolder 'mod.json')) {
        $notice = Get-Content -LiteralPath (Join-Path $noticeFolder 'mod.json') -Raw | ConvertFrom-Json
        if ($notice.id -ne 'battle3d-github-info' -or $notice.version -notmatch '^\d+\.\d+\.\d+$') { throw 'Duyuru paketi kimligi veya surumu gecersiz.' }
        $noticeFiles = @(Get-ChildItem -LiteralPath $noticeFolder -File -Recurse)
        if ($noticeFiles.Count -ne 3 -or @($noticeFiles | Where-Object { $_.Name -notin @('mod.json', 'preview.png', 'README.txt') -or $_.DirectoryName -ne $noticeFolder }).Count) { throw 'Duyuru paketinde yalnizca mod.json, preview.png ve README.txt olmali.' }
        if ([Text.Encoding]::UTF8.GetByteCount([string]$notice.description) -gt 8000) { throw 'Workshop aciklamasi yayin sinirini asiyor.' }
        $noticeName = "Warlapse-Battle-3D-Workshop-Duyuru-$($notice.version).zip"
        $noticeArchive = Join-Path $OutputDirectory $noticeName
        Compress-Archive -LiteralPath $noticeFolder -DestinationPath $noticeArchive -CompressionLevel Optimal -Force
        $noticeHash = (Get-FileHash -LiteralPath $noticeArchive -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText("$noticeArchive.sha256", "$noticeHash  $noticeName`n", [Text.Encoding]::ASCII)
        Get-Item -LiteralPath $noticeArchive,"$noticeArchive.sha256" | Select-Object FullName,Length
    }
} finally {
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    $resolvedRoot = [IO.Path]::GetFullPath($stageRoot).TrimEnd('\') + '\'
    if ($resolvedStage.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedStage)) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
}
