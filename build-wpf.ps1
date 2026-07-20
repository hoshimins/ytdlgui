param(
    [string]$OutputDirectory = "artifacts\publish\win-x64"
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$publishCandidate = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $projectRoot $OutputDirectory
}
$publishPath = [IO.Path]::GetFullPath($publishCandidate)
if (-not $publishPath.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "出力先はプロジェクト内を指定してください: $publishPath"
}

$userDotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (Test-Path -LiteralPath $userDotnet) {
    $dotnetPath = (Resolve-Path -LiteralPath $userDotnet).Path
} else {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $dotnetCommand) {
        throw ".NET 10 SDK が見つかりません。https://dotnet.microsoft.com/download/dotnet/10.0 から SDK をインストールしてください。"
    }
    $dotnetPath = $dotnetCommand.Source
}

& $dotnetPath test "YtdlGUI.slnx" -c Release
if ($LASTEXITCODE -ne 0) { throw "テストに失敗しました。" }

# 以前の発行物に外部 GPL バイナリが残留しないよう、発行先を毎回空にする。
if (Test-Path -LiteralPath $publishPath) {
    Get-ChildItem -LiteralPath $publishPath -Force | Remove-Item -Recurse -Force
}

& $dotnetPath publish "src\YtdlGUI.Wpf\YtdlGUI.Wpf.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishPath
if ($LASTEXITCODE -ne 0) { throw "発行に失敗しました。" }

Write-Host "アプリ本体を作成しました: $publishPath"
Write-Host "ローカル実行用の外部ツールは .\setup-tools.ps1 で取得できます。"
