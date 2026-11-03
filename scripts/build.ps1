# Повна збірка Buldog Boost на Windows: C++ -> Rust -> C# -> dist/
# Потрібно: Visual Studio Build Tools (C++), CMake, Rust (cargo), .NET 8 SDK.
$ErrorActionPreference = "Stop"

function Run {
    & $args[0] $args[1..($args.Length - 1)]
    if ($LASTEXITCODE -ne 0) { throw "Команда завершилась з кодом ${LASTEXITCODE}: $($args -join ' ')" }
}

$root   = Split-Path -Parent $PSScriptRoot
$dist   = Join-Path $root "dist"
$native = Join-Path $dist "native"

Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $native | Out-Null

Write-Host "== C++ (engine + Direct2D) =="
Run cmake -S $root -B "$root/build" -A x64
Run cmake --build "$root/build" --config Release
Run ctest --test-dir "$root/build" -C Release --output-on-failure
Copy-Item "$root/build/bin/Release/*.dll" $native

Write-Host "== Rust (safe modules) =="
Run cargo test --manifest-path "$root/safe-rust/Cargo.toml"
Run cargo build --release --manifest-path "$root/safe-rust/Cargo.toml"
Copy-Item "$root/safe-rust/target/release/bb_safe.dll" $native

Write-Host "== C# (GUI) =="
Run dotnet publish "$root/gui-csharp" -c Release -r win-x64 --self-contained false -o "$dist/app"

Write-Host "== Інсталятор (Inno Setup, якщо встановлено) =="
$iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
            Where-Object { Test-Path $_ } | Select-Object -First 1
}
if ($iscc) { Run $iscc "$root\installer\BuldogBoost.iss" }
else { Write-Host "Inno Setup не знайдено — інсталятор пропущено." }

Write-Host "`nГотово: $dist\app\BuldogBoost.exe"
