# Check what a merge to main publishes, printing one line saying why and exiting 0 for pass.
#
# Default: the post-PR check, run in the ticket's worktree at the PR's head with origin/main just
# fetched. A changed <Version> must be greater than main's and have no tag yet, or
# build-release-prod.yml skips the publish with only a notice.
#
# -Published: the after-merge check, run in the main checkout once it is on main. Waits for the
# release of <Version> to list the exe; 240 s keeps it inside clean-up's 300 s limit on an
# after-merge command.
param(
  [switch]$Published
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path -Parent $PSScriptRoot)

$csproj = "GenMate.PluginInstaller.csproj"
$asset = "GenMate.PluginInstaller.exe"
$timeoutSeconds = 240
$pollSeconds = 15

function Pass([string]$why) { Write-Output $why; exit 0 }
function Fail([string]$why) { Write-Output $why; exit 1 }

function Read-Version([string]$rev) {
  $text = (& git show "${rev}:$csproj" 2>$null) -join "`n"
  if ($LASTEXITCODE -ne 0) { Fail "Cannot read $csproj at $rev." }
  if ($text -notmatch '<Version>([^<]+)</Version>') { Fail "$csproj at $rev has no <Version>." }
  $v = $Matches[1].Trim()
  $parsed = $null
  if (-not [System.Version]::TryParse($v, [ref]$parsed)) { Fail "Version '$v' in $csproj at $rev is not a version number." }
  [pscustomobject]@{ Text = $v; Value = $parsed }
}

# gh-axi prints the response, or an error with its code, on stdout.
function Invoke-GitHub([string[]]$arguments) {
  $out = (& gh-axi api @arguments 2>$null) -join "`n"
  [pscustomobject]@{
    Ok = $LASTEXITCODE -eq 0
    NotFound = $LASTEXITCODE -ne 0 -and $out -match '(?m)^code: NOT_FOUND$'
    Output = $out
  }
}

function Get-ErrorText($response) {
  if ($response.Output -match '(?m)^error: "?(.+?)"?$') { return $Matches[1] }
  if ($response.Output) { return ($response.Output -split "`n")[0] }
  "gh-axi failed with no output"
}

$url = & git remote get-url origin 2>$null
if ($LASTEXITCODE -ne 0 -or $url -notmatch 'github\.com[:/]([^/]+)/(.+?)(\.git)?/?$') { Fail "Cannot read the GitHub repo from origin's URL '$url'." }
$repo = "$($Matches[1])/$($Matches[2])"

if ($Published) {
  $version = (Read-Version "HEAD").Text
  $tag = "v$version"
  $deadline = (Get-Date).AddSeconds($timeoutSeconds)
  while ($true) {
    $release = Invoke-GitHub @("repos/$repo/releases/tags/$tag", "--jq", ".assets[].name")
    if ($release.Ok -and ($release.Output -split "`n") -contains $asset) { Pass "Release $tag lists $asset." }
    if (-not $release.Ok -and -not $release.NotFound) { Fail "Cannot read release ${tag}: $(Get-ErrorText $release)." }
    if ((Get-Date) -ge $deadline) {
      Fail "Release $tag does not list $asset after $timeoutSeconds s; the release workflow may still be running, and retrying clean-up re-runs this check."
    }
    Start-Sleep -Seconds $pollSeconds
  }
}

$old = Read-Version "origin/main"
$new = Read-Version "HEAD"
if ($new.Text -eq $old.Text) { Pass "Version $($new.Text) unchanged: merging publishes nothing" }
if ($new.Value -le $old.Value) { Fail "Version $($new.Text) is not greater than main's $($old.Text)." }
$tag = "v$($new.Text)"
$ref = Invoke-GitHub @("repos/$repo/git/ref/tags/$tag")
if ($ref.Ok) { Fail "Tag $tag already exists: build-release-prod.yml would skip the publish. Bump past it." }
if (-not $ref.NotFound) { Fail "Cannot check tag ${tag}: $(Get-ErrorText $ref)." }
Pass "Version $($old.Text) -> $($new.Text): merging publishes $tag"
