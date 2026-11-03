# 🐶 Buldog Boost

Безпечний бустер продуктивності для Windows: моніторинг CPU/RAM, очищення тимчасових файлів,
режим «Висока продуктивність» з відкатом, закриття зайвих фонових програм (системні процеси захищені),
ігрові профілі, трей та автозапуск.

## Можливості
- Живий графік CPU на Direct2D (60+ FPS) і показники RAM.
- **Boost трьох рівнів** (комбо біля кнопки ⚡ BOOST):
  - *Звичайний* — очищення temp + «Висока продуктивність»;
  - *Сильний* — + глибоке очищення (дампи, WER, кеш інтернету), скидання DNS, **приглушення фонових програм** (BelowNormal, без закриття);
  - *Максимум* — + кеш Chrome/Edge, скидання робочих наборів RAM, закриття програм, відмічених у списку.
- «Висока продуктивність» + **відкат** схеми живлення й пріоритетів (кнопка «Відкат»).
- **Ігрові профілі**: старт гри → закрити вибрані програми й увімкнути високу продуктивність; вихід з гри → відкат.
- Трей-іконка (відкрити / BOOST / вихід), автозапуск із Windows згорнутим, одна копія програми.
- Налаштування зберігаються в `%APPDATA%\BuldogBoost\settings.json` (там же `KeepProcesses` — програми, які Boost ніколи не приглушує; типово OBS і Discord).

## Склад

| Папка | Технологія | Призначення |
|---|---|---|
| `gui-csharp/` | C# (.NET 8, WPF) | Графічний інтерфейс |
| `engine-cpp/` | C++20 | Optimization Engine → `bb_engine.dll` |
| `safe-rust/` | Rust | Безпечні системні модулі → `bb_safe.dll` |
| `render-d2d/` | C++ + Direct2D | Плавний графік → `bb_render.dll` |
| `scripts/` | PowerShell | Повна збірка |
| `installer/` | Inno Setup | Інсталятор |
| `docs/` | — | Архітектура |

## Збірка (Windows)

Потрібно: Visual Studio Build Tools (C++), CMake 3.20+, Rust (cargo), .NET 8 SDK.

    scripts\build.ps1

Результат: `dist\app\BuldogBoost.exe` (DLL лежать поруч). Якщо встановлено [Inno Setup 6](https://jrsoftware.org/isinfo.php), додатково з'явиться `dist\BuldogBoost-Setup-0.2.0.exe`.

Для запуску на іншому ПК потрібен [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

Окремо:

    cmake -S . -B build -A x64 && cmake --build build --config Release
    ctest --test-dir build -C Release
    cargo test --manifest-path safe-rust/Cargo.toml
    dotnet build gui-csharp -c Release

## Примітки
- Закриття деяких процесів потребує запуску від імені адміністратора.
- Графік Direct2D вбудовано через `HwndHost`, тому WPF-елементи не можна накладати поверх нього.

## Ліцензія
MIT — див. [LICENSE](LICENSE).
