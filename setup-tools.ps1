param(
    [string]$Destination = "artifacts\publish\win-x64"
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$destinationCandidate = if ([IO.Path]::IsPathRooted($Destination)) {
    $Destination
} else {
    Join-Path $projectRoot $Destination
}
$destinationPath = [IO.Path]::GetFullPath($destinationCandidate)
if (-not $destinationPath.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "配置先はプロジェクト内を指定してください: $destinationPath"
}

New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("YtdlGUI-tools-" + [Guid]::NewGuid())
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null

try {
    $ytDlpPath = Join-Path $destinationPath "yt-dlp.exe"
    Write-Host "yt-dlp.exe を公式 GitHub Releases から取得しています..."
    & curl.exe --location --fail --retry 3 --progress-bar `
        --output $ytDlpPath `
        "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
    if ($LASTEXITCODE -ne 0) { throw "yt-dlp.exe の取得に失敗しました。" }

    $ffmpegArchive = Join-Path $temporaryDirectory "ffmpeg-release-essentials.7z"
    $ffmpegExtracted = Join-Path $temporaryDirectory "ffmpeg"
    Write-Host "FFmpeg Essentials Build を Gyan の公式 GitHub ミラーから取得しています..."
    $releaseHeaders = @{ "User-Agent" = "YtdlGUI-setup" }
    $ffmpegRelease = Invoke-RestMethod `
        -Uri "https://api.github.com/repos/GyanD/codexffmpeg/releases/latest" `
        -Headers $releaseHeaders
    $ffmpegAsset = $ffmpegRelease.assets |
        Where-Object { $_.name -match '^ffmpeg-.*-essentials_build\.7z$' } |
        Select-Object -First 1
    if (-not $ffmpegAsset) {
        throw "Gyan の最新リリースに FFmpeg Essentials Build がありません。"
    }
    & curl.exe --location --fail --retry 3 --progress-bar `
        --output $ffmpegArchive `
        $ffmpegAsset.browser_download_url
    if ($LASTEXITCODE -ne 0) { throw "FFmpeg の取得に失敗しました。" }

    New-Item -ItemType Directory -Path $ffmpegExtracted | Out-Null
    & tar.exe -xf $ffmpegArchive -C $ffmpegExtracted
    if ($LASTEXITCODE -ne 0) { throw "FFmpeg アーカイブの展開に失敗しました。" }

    $downloadedFfmpeg = Get-ChildItem -LiteralPath $ffmpegExtracted -Recurse -Filter "ffmpeg.exe" -File |
        Select-Object -First 1
    $downloadedFfprobe = Get-ChildItem -LiteralPath $ffmpegExtracted -Recurse -Filter "ffprobe.exe" -File |
        Select-Object -First 1
    if (-not $downloadedFfmpeg) {
        throw "ダウンロードしたアーカイブに ffmpeg.exe がありません。"
    }
    if (-not $downloadedFfprobe) {
        throw "ダウンロードしたアーカイブに ffprobe.exe がありません。"
    }
    Copy-Item -LiteralPath $downloadedFfmpeg.FullName -Destination (Join-Path $destinationPath "ffmpeg.exe") -Force
    Copy-Item -LiteralPath $downloadedFfprobe.FullName -Destination (Join-Path $destinationPath "ffprobe.exe") -Force

    Write-Host "外部ツールを配置しました: $destinationPath"
    Write-Warning "このフォルダを第三者へ再配布する場合は THIRD_PARTY_NOTICES.md を確認し、GPLの条件を満たしてください。"
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
