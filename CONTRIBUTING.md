# Внесок у Buldog Boost

1. Форкніть репозиторій і створіть гілку: `git checkout -b feature/назва`.
2. Зберіть і запустіть тести: `scripts/build.ps1` (Windows) або `cmake` + `ctest` та `cargo test`.
3. Нова функція рушія — з тестом у `engine-cpp/tests`; нова логіка Rust — з `#[test]`.
4. Опишіть зміни в `CHANGELOG.md`.
5. Відкрийте Pull Request.

Стиль: C++20, Rust 2021 (`cargo fmt`), C# (див. `.editorconfig`).
