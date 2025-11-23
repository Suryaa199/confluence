param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Project = "src/InterviewCopilot/InterviewCopilot.csproj",
    [string]$Output = "dist/win-x64"
)

dotnet restore $Project
dotnet publish $Project -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o $Output
Write-Host "Published to $Output"

