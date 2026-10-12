# Build (and optionally test or launch) the installer.
#
# -Launch runs the built dll through dotnet.exe rather than starting GenMate.PluginInstaller.exe:
# the exe's app.manifest asks for requireAdministrator, so starting it (which is what `dotnet run`
# does) needs elevation, while dotnet.exe runs the same app as the current user with no UAC prompt,
# so a launched dev build is never elevated.
param(
  [string]$Configuration = "Debug",
  [switch]$Launch,
  [switch]$Test,
  [switch]$Restore,
  [switch]$Clean,
  [ValidateSet("quiet","minimal","normal","detailed","diagnostic")]
  [string]$Verbosity = "minimal"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "GenMate.PluginInstaller.csproj"
$testProject = Join-Path $PSScriptRoot "Tests\GenMate.PluginInstaller.Tests.csproj"

function Invoke-Dotnet([string[]]$arguments) {
  & dotnet @arguments
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if ($Clean) {
  Write-Host "Cleaning..."
  Invoke-Dotnet @("clean", $project, "--configuration", $Configuration, "--verbosity", "quiet")
}

if ($Restore) {
  Write-Host "Restoring packages..."
  Invoke-Dotnet @("restore", $project)
}

Write-Host "Building ($Configuration)..."
Invoke-Dotnet @("build", $project, "--configuration", $Configuration, "--verbosity", $Verbosity)

if ($Test) {
  Write-Host "Testing ($Configuration)..."
  Invoke-Dotnet @("test", $testProject, "--configuration", $Configuration, "--verbosity", $Verbosity)
}

if ($Launch) {
  $dll = Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows\GenMate.PluginInstaller.dll"
  Write-Host "Launching $dll..."
  Invoke-Dotnet @($dll)
}

exit 0
