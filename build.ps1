# Builds TaskbarClocks.exe using the C# compiler that ships with Windows.
# Nothing to install: no SDK, no NuGet, no .NET download.
#
# CHANGES ON DISK: writes (overwrites) TaskbarClocks.exe in this folder, and copies it to C:\Tools\TaskbarClocks
#                 (closing any copy of the clocks already running there).

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out  = Join-Path $root 'TaskbarClocks.exe'

if (-not (Test-Path $csc)) {
    throw "C# compiler not found at $csc"
}

$sources = Get-ChildItem -Path (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw "No .cs files found in $root\src" }

Write-Host "Compiling $($sources.Count) source files..." -ForegroundColor Cyan

$refs = @(
    '/reference:System.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll'
)

$args = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/warn:3',
    "/out:$out"
) + $refs + $sources

& $csc $args
if ($LASTEXITCODE -ne 0) { throw "Compile failed with exit code $LASTEXITCODE" }

Write-Host "Built: $out" -ForegroundColor Green
Write-Host ("Size:  {0:N0} bytes" -f (Get-Item $out).Length)

# Deploy to local disk and run from there. Running off H: means Google Drive has to
# materialise the file on every launch, which reads as a brand-new binary to the
# antivirus and earns a fresh scan each time. Local disk gets scanned once.
#
# C:\Tools deliberately, NOT AppData\Local\Programs: AVG refuses folder exceptions
# anywhere under AppData because that is where most malware stages itself.
$install = 'C:\Tools\TaskbarClocks'
New-Item -ItemType Directory -Path $install -Force | Out-Null

Get-Process TaskbarClocks -ErrorAction SilentlyContinue | ForEach-Object {
    $_.CloseMainWindow() | Out-Null
    Start-Sleep -Milliseconds 400
    if (-not $_.HasExited) { $_.Kill() }
}
Start-Sleep -Milliseconds 400

Copy-Item $out $install -Force
Write-Host "Deployed to: $install" -ForegroundColor Green
Write-Host 'Run it from there, not from H:.' -ForegroundColor DarkGray
