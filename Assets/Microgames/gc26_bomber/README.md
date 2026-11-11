# gc26_bomber — «Последний сигнал»

Top-down shooter / roguelite: ремонтный дрон пробивается через захваченную ИИ орбитальную станцию.
Главная механика — нагрев ядра вместо патронов.

- Ветка: `game/gc26_bomber`
- Неймспейс: `GC26.Bomber`
- Стартовая сцена: `Scenes/gc26_bomber_Main.unity` — один объект с компонентом `BomberGame`, всё остальное игра создаёт сама
- Команда:

## Документы

- [Game Design Document](Docs~/GDD.md)
- [Technical Design Document](Docs~/TDD.md)
- [Арт-гайд](Docs~/ArtGuide.md)
- Играбельный прототип: `Prototype~/last-signal.html` (открыть в браузере) — эталон баланса и поведения

## Структура кода

| Папка | Что |
|---|---|
| `Scripts/BomberGame.cs` | `IMicrogame`, точка входа и выход в меню |
| `Scripts/Core/` | `BomberContext` — корень сессии, все объекты игры — его дети |
| `Scripts/Domain/` | Чистый C# без Unity: `HeatModel` (нагрев), `HeatGovernor` (автоогонь на телефоне) |
| `Scripts/Gameplay/` | Дрон, снаряды, комната, мишени |
| `Scripts/Art/` | Временная пиксельная графика, рисуется кодом |
| `Scripts/UI/` | Временный HUD (IMGUI) и пауза |
| `Tests/EditMode/` | Тесты нагрева: Window → General → Test Runner → EditMode |

## Как играть (шаг 1)

WASD — движение, мышь — прицел, ЛКМ — огонь, ПКМ — заряженный выстрел ×3, Пробел — рывок, R — сброс нагрева, Esc — пауза / в меню.
Стреляй по мишеням: выше жёлтой черты — Форсаж (+30% урона), на 100% — перегрев и 2 с без оружия.
