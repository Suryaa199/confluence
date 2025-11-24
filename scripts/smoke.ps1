param(
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [string]$Project = "src/InterviewCopilot/InterviewCopilot.csproj",
  [string]$Solution = "InterviewCopilot.sln",
  [string]$Output = "dist/win-x64",
  [int]$MinSizeMB = 20
)

$ErrorActionPreference = "Stop"
Write-Host "== Dotnet info =="
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes

Write-Host "== Restore =="
dotnet restore $Solution --nologo -v minimal

Write-Host "== Build =="
dotnet build $Solution -c $Configuration --no-restore -v minimal

Write-Host "== Publish ($Runtime) =="
New-Item -ItemType Directory -Force -Path $Output | Out-Null
dotnet publish $Project -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -v minimal -o $Output

Write-Host "== Verify artifact =="
$exe = Join-Path $Output "InterviewCopilot.exe"
if (-not (Test-Path $exe)) { throw "Smoke: EXE not found: $exe" }
$size = (Get-Item $exe).Length
$min = $MinSizeMB * 1MB
Write-Host ("EXE Size: {0:N0} bytes (min {1:N0})" -f $size, $min)
if ($size -lt $min) { throw "Smoke: EXE too small: $size < $min" }

Write-Host "== List output =="
Get-ChildItem $Output -Recurse | ForEach-Object { Write-Host $_.FullName }
Write-Host "Smoke OK"
