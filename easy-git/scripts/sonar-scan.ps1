<#
.SYNOPSIS
  Runs Clippy and test coverage for the easy-git Rust workspace on this machine,
  then sends everything to a local SonarQube server through the scanner container.

.DESCRIPTION
  Steps:
    1. Loads the Visual Studio 2022 C++ build environment (x64) when it is not
       already loaded, so cargo links the same way it does in the
       "Developer Command Prompt for VS 2022".
    2. cargo clippy  -> clippy.json   (UTF-8, read by the Rust analyzer)
    3. cargo llvm-cov -> lcov.info    (Windows paths rewritten to /usr/src)
    4. sonarsource/sonar-scanner-cli container on the docker-compose network,
       using the settings in sonar-project.properties.

  The script can be started from a normal PowerShell window or from the
  Developer Command Prompt; it always works from the repository root.

.EXAMPLE
  $env:SONAR_TOKEN = "squ_..."
  .\scripts\sonar-scan.ps1

.EXAMPLE
  .\scripts\sonar-scan.ps1 -Token "squ_..." -SkipCoverage

.EXAMPLE
  # Execution policy blocks the script? Start it like this:
  powershell -ExecutionPolicy Bypass -File scripts\sonar-scan.ps1
#>
[CmdletBinding()]
param(
    # SonarQube token (User, Global Analysis or Project Analysis token of this project).
    [string]$Token = $env:SONAR_TOKEN,
    # Server address as seen from inside the scanner container.
    [string]$HostUrl = $(if ($env:SONAR_HOST_URL) { $env:SONAR_HOST_URL } else { "http://sonarqube:9000" }),
    # docker-compose network the SonarQube container is attached to. Empty = no --network.
    [string]$Network = $(if ($env:SONAR_DOCKER_NETWORK) { $env:SONAR_DOCKER_NETWORK } else { "northwind-platform_northwind-net" }),
    [switch]$SkipClippy,
    [switch]$SkipCoverage
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Text) { Write-Host "`n==> $Text" -ForegroundColor Cyan }

function Assert-LastExit([string]$What) {
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit code $LASTEXITCODE)." }
}

# Runs a native command silently and returns its exit code. ErrorAction is relaxed
# because Windows PowerShell 5.1 turns redirected stderr into terminating errors.
function Invoke-Silently([scriptblock]$Block) {
    $old = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { & $Block *> $null } finally { $ErrorActionPreference = $old }
    return $LASTEXITCODE
}

function Test-Command([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

# Loads the VS 2022 x64 C++ environment into this PowerShell session.
# Skipped when the script already runs inside a Developer Command Prompt.
function Import-VsDevEnvironment {
    if ($env:VSCMD_VER -and $env:VCToolsInstallDir) {
        Write-Host "Visual Studio build environment already loaded (VS $env:VSCMD_VER)."
        return
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe not found. Install Visual Studio 2022 Build Tools with the 'Desktop development with C++' workload."
    }

    # Only VS 2022 installs that really contain the C++ x64 tools
    # (skips e.g. an Insiders install without the C++ workload).
    $vsPath = & $vswhere -version "[17.0,18.0)" -products * `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
        -latest -property installationPath
    if (-not $vsPath) {
        throw "No Visual Studio 2022 install with the C++ x64 tools was found."
    }

    $devCmd = Join-Path $vsPath "Common7\Tools\VsDevCmd.bat"
    Write-Host "Loading build environment from: $vsPath"
    $envDump = & cmd.exe /c "`"$devCmd`" -arch=x64 -host_arch=x64 -no_logo && set"
    Assert-LastExit "VsDevCmd.bat"

    foreach ($line in $envDump) {
        $i = $line.IndexOf("=")
        if ($i -gt 0) {
            [Environment]::SetEnvironmentVariable($line.Substring(0, $i), $line.Substring($i + 1), "Process")
        }
    }
}

# Writes lines as UTF-8 without BOM (Windows PowerShell 5.1 would add a BOM).
function Write-Utf8NoBom([string]$Path, [string[]]$Lines) {
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($Path, $Lines, $utf8)
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    Write-Step "Checking prerequisites"
    if (-not (Test-Path "sonar-project.properties")) { throw "sonar-project.properties not found in $repoRoot." }
    if (-not $Token) { throw "No token. Set `$env:SONAR_TOKEN or pass -Token." }
    foreach ($cmd in "cargo", "docker") {
        if (-not (Test-Command $cmd)) { throw "'$cmd' is not on PATH." }
    }
    if (-not $SkipCoverage) {
        if ((Invoke-Silently { cargo llvm-cov --version }) -ne 0) {
            throw "cargo-llvm-cov is missing. Install it once with:`n  rustup component add llvm-tools-preview`n  cargo install cargo-llvm-cov"
        }
    }
    if ($Network) {
        if ((Invoke-Silently { docker network inspect $Network }) -ne 0) {
            throw "Docker network '$Network' not found. Is docker-compose up? (docker network ls)"
        }
    }

    Write-Step "Preparing the Visual Studio C++ build environment"
    Import-VsDevEnvironment

    if (-not $SkipClippy) {
        Write-Step "cargo clippy -> clippy.json"
        # cmd redirection writes plain bytes; PowerShell's '>' would write UTF-16.
        & cmd.exe /c "cargo clippy --workspace --all-targets --message-format=json > clippy.json"
        Assert-LastExit "cargo clippy"
        $warnings = (Select-String -Path "clippy.json" -Pattern '"reason":"compiler-message"' | Measure-Object).Count
        Write-Host "Clippy messages in report: $warnings"
    }

    if (-not $SkipCoverage) {
        Write-Step "cargo llvm-cov -> lcov.info"
        & cargo llvm-cov --workspace --lcov --output-path lcov.info
        Assert-LastExit "cargo llvm-cov"

        # SF:C:\Users\...\easy-git\crates\x\src\lib.rs  ->  SF:/usr/src/crates/x/src/lib.rs
        $rootSlash = ($repoRoot -replace '\\', '/').TrimEnd('/')
        $rootPattern = "^SF:" + [regex]::Escape($rootSlash) + "/"
        $lines = Get-Content "lcov.info" | ForEach-Object {
            if ($_.StartsWith("SF:")) { ($_ -replace '\\', '/') -ireplace $rootPattern, "SF:/usr/src/" } else { $_ }
        }
        Write-Utf8NoBom (Join-Path $repoRoot "lcov.info") $lines

        $unmapped = @($lines | Where-Object { $_.StartsWith("SF:") -and -not $_.StartsWith("SF:/usr/src/") })
        if ($unmapped.Count -gt 0) {
            Write-Warning "$($unmapped.Count) file(s) in lcov.info are outside the repository and will be ignored, e.g. $($unmapped[0])"
        }
    }

    Write-Step "Running sonar-scanner-cli in Docker"
    $dockerArgs = @("run", "--rm")
    if ($Network) { $dockerArgs += @("--network", $Network) }
    # The token is passed by name so it never shows up in the command line.
    $env:SONAR_TOKEN = $Token
    $dockerArgs += @(
        "-e", "SONAR_HOST_URL=$HostUrl",
        "-e", "SONAR_TOKEN",
        "-v", "${repoRoot}:/usr/src",
        "sonarsource/sonar-scanner-cli"
    )
    & docker @dockerArgs
    Assert-LastExit "sonar-scanner"

    $projectKey = (Select-String -Path "sonar-project.properties" -Pattern '^sonar.projectKey=(.+)$').Matches[0].Groups[1].Value
    Write-Host "`nDone. Results: http://localhost:9000/dashboard?id=$projectKey" -ForegroundColor Green
}
finally {
    Pop-Location
}
