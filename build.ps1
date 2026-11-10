param(
    [string]$Configuration = "Release",
    [string]$Output = (Join-Path $PSScriptRoot "Publish")
)
$ErrorActionPreference = "Stop"
$outputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)

$projects = @(
    @{ Path = "src/OD.EditMediaInfo"; Executable = "odmedia.exe" }
    @{ Path = "src/OD.EditMediaInfo.Gui"; Executable = "odmedia-gui.exe" }
)

Write-Host "Publishing the CLI and GUI to $outputDirectory" -ForegroundColor Cyan
foreach ($project in $projects) {
    Write-Host "Publish $($project.Executable) $Configuration win-x64..." -ForegroundColor Cyan
    dotnet publish (Join-Path $PSScriptRoot $project.Path) -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $outputDirectory -m:1
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

foreach ($project in $projects) {
    $exe = Get-Item -LiteralPath (Join-Path $outputDirectory $project.Executable)
    Write-Host "OK $($exe.FullName) ($([math]::Round($exe.Length / 1MB, 1)) MB)" -ForegroundColor Green
}

$cli = Get-Item -LiteralPath (Join-Path $outputDirectory "odmedia.exe")
& $cli.FullName --help
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $cli.FullName --version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Publish OK" -ForegroundColor Green
