param([string]$Repository = 'juragii/deskflow-windows', [string]$Notes = 'Private DeskFlow update')
$ErrorActionPreference = 'Stop'
if ($Repository -notmatch '^[A-Za-z0-9-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid repository name' }
& (Join-Path $PSScriptRoot 'build.ps1')
$test = Start-Process (Join-Path $PSScriptRoot 'DeskFlow.exe') -ArgumentList '--self-test' -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'Tests failed; update package was not published' }
$output = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force $output | Out-Null
$files = @('DeskFlow.exe', 'DeskFlow.exe.config', 'DeskFlow.ico', 'DeskFlow.Updater.exe', 'DeskFlow.Updater.exe.config') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$zip = Join-Path $output 'DeskFlow-update.zip'
Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
$bytes = [IO.File]::ReadAllBytes($zip)
$header = [Text.Encoding]::ASCII.GetBytes('blob ' + $bytes.Length + [char]0)
$gitBytes = [byte[]]::new($header.Length + $bytes.Length)
[Array]::Copy($header, 0, $gitBytes, 0, $header.Length)
[Array]::Copy($bytes, 0, $gitBytes, $header.Length, $bytes.Length)
$sha1 = [Security.Cryptography.SHA1]::Create()
try { $blob = [BitConverter]::ToString($sha1.ComputeHash($gitBytes)).Replace('-', '').ToLowerInvariant() } finally { $sha1.Dispose() }
$version = (Get-Item (Join-Path $PSScriptRoot 'DeskFlow.exe')).VersionInfo.FileVersion
$manifest = [ordered]@{schema=1;repository=$Repository;version=$version;blob_sha=$blob;sha256=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant();notes=$Notes}
[IO.File]::WriteAllText((Join-Path $output 'latest.json'), ($manifest | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
Write-Output ('Prepared private update ' + $version)
Write-Output ('Runtime Git blob SHA: ' + $blob)
