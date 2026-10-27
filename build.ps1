# Builds TaskbarClocks.exe using the C# compiler that ships with Windows.
# Nothing to install: no SDK, no NuGet, no .NET download.
#
#   build.ps1            builds a TEST copy in C:\Tools\_dev\TaskbarClocks and starts it
#   build.ps1 -Install   builds, then installs it here on H: (the copy the desktop shortcut runs)
#
# CHANGES ON DISK: writes (overwrites) C:\Tools\_dev\TaskbarClocks\TaskbarClocks.exe.
#                  With -Install, also overwrites TaskbarClocks.exe in this folder.
#                  Either way it first closes any copy of the clocks that is running.
#                  settings.ini files are never touched: the test copy keeps its own.
#
# Why test in C:\Tools\_dev: it is local disk, and C:\Tools is the folder AVG has an
# exception for. Every rebuild is a brand-new executable to an antivirus, and AVG flags
# new executables it finds anywhere else. (AVG refuses exceptions under AppData.)

param([switch]$Install)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$dev  = 'C:\Tools\_dev\TaskbarClocks'
$out  = Join-Path $dev 'TaskbarClocks.exe'

if (-not (Test-Path $csc)) {
    throw "C# compiler not found at $csc"
}

$sources = Get-ChildItem -Path (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw "No .cs files found in $root\src" }

# Only one copy of the clocks can run, and a running exe cannot be overwritten.
Get-Process TaskbarClocks -ErrorAction SilentlyContinue | ForEach-Object {
    $_.CloseMainWindow() | Out-Null
    Start-Sleep -Milliseconds 400
    if (-not $_.HasExited) { $_.Kill() }
}
Start-Sleep -Milliseconds 400

New-Item -ItemType Directory -Path $dev -Force | Out-Null
Write-Host "Compiling $($sources.Count) source files..." -ForegroundColor Cyan

$refs = @(
    '/reference:System.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Web.Extensions.dll'   # JSON reader for the place search
)

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/warn:3',
    "/out:$out"
) + $refs + $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "Compile failed with exit code $LASTEXITCODE" }

Write-Host "Built: $out" -ForegroundColor Green
Write-Host ("Size:  {0:N0} bytes" -f (Get-Item $out).Length)

if ($Install) {
    $installed = Join-Path $root 'TaskbarClocks.exe'
    Copy-Item $out $installed -Force
    Write-Host "Installed: $installed" -ForegroundColor Green
    Start-Process -FilePath $installed -WorkingDirectory $root
}
else {
    Start-Process -FilePath $out -WorkingDirectory $dev
    Write-Host 'Started the test copy. Run with -Install to update the one on H:.' -ForegroundColor DarkGray
}
