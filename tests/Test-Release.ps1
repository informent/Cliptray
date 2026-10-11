param([switch]$KeepArtifact)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = if ($KeepArtifact) { Join-Path $root 'test-output' } else { Join-Path $root ('test-output-' + [Guid]::NewGuid().ToString('N')) }
$p = $null
try {
    dotnet publish (Join-Path $root 'Cliptray.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $out --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
    $exe = Join-Path $out 'Cliptray.exe'; if (!(Test-Path -LiteralPath $exe)) { throw 'Cliptray executable missing' }
    $p = Start-Process -FilePath $exe -PassThru; Start-Sleep -Milliseconds 1200; if ($p.HasExited) { throw "Cliptray exited with code $($p.ExitCode)" }; Stop-Process -Id $p.Id -Force; Wait-Process -Id $p.Id -ErrorAction SilentlyContinue; Write-Output 'PASS: clean build, publish, and launch (Cliptray)'
}
finally {
    if ($null -ne $p -and !$p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; Wait-Process -Id $p.Id -ErrorAction SilentlyContinue }
    for ($attempt = 0; !$KeepArtifact -and $attempt -lt 25 -and (Test-Path -LiteralPath $out); $attempt++) {
        try { Remove-Item -LiteralPath $out -Recurse -Force -ErrorAction Stop }
        catch { if ($attempt -eq 24) { throw }; Start-Sleep -Milliseconds 200 }
    }
}
