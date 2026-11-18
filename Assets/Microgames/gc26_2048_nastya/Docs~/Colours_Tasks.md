# Colours (gc26_2048_nastya): задачи

Пошаговая разбивка по [TDD](Colours_TDD.md). Шаги идут в том порядке, в котором их удобно делать. Отмечай выполненные: `[ ]` → `[x]`.

Шаги 1–8 — чистый C#, их можно писать и тестировать без сцены. Шаги 9–11 и 18 делаются руками в редакторе Unity.

| ✓ | № | Этап | Задача | Файлы | Готово, когда… |
|---|---|---|---|---|---|
| [ ] | 0 | Подготовка | Включить git-хук, проверить ссылки в `.asmdef` (GC26.Core, InputSystem, TextMeshPro, UI) | `GC26._2048Nastya.asmdef` | Проект компилируется, `git config core.hooksPath tools/hooks` выполнен |
| [ ] | 1 | Модель | `Direction2048`, `Tile2048` (Id + Value) | `Model/Direction2048.cs`, `Model/Board2048.cs` | Компилируется без `UnityEngine` |
| [ ] | 2 | Модель | `Board2048`: `Get`, `Place`, `EmptyCells`, `HasMoves`, `Clone` | `Model/Board2048.cs` | Методы работают на поле 4×4 |
| [ ] | 3 | Модель | `MoveResult2048` (Moved, ScoreGained, Moves, Merges, MaxValue) | `Model/MoveResult2048.cs` | Списки переиспользуются, при ходе только очищаются |
| [ ] | 4 | Модель | Алгоритм `Board2048.Move(dir)`: проход по линиям от стенки, одно слияние за ход | `Model/Board2048.cs` | `2 2 2 2` → `4 4`, `2 2 2 _` вправо → `_ _ 2 4` |
| [ ] | 5 | Модель | `TileSpawner2048` на `System.Random` с сидом, шанс четвёрки 10% | `Model/TileSpawner2048.cs` | На заполненном поле возвращает `null` |
| [ ] | 6 | Модель | `GameState2048` + `UndoSnapshot` | `Model/GameState2048.cs` | Хранит поле, счёт, число ходов, WonShown, IsOver |
| [ ] | 7 | Тесты | Тестовая сборка и тесты модели (TDD 12.1: ходы, симметрия, HasMoves, спавнер) | `Tests/EditMode/GC26._2048Nastya.Tests.asmdef`, `Tests/EditMode/Board2048Tests.cs` | Все тесты в Test Runner зелёные |
| [ ] | 8 | Сохранения | `SaveData2048` + `SaveStore2048`: JSON, запись через `.tmp`, проверка при загрузке, лучший счёт в PlayerPrefs | `Persistence/SaveStore2048.cs`, `Tests/EditMode/SaveStore2048Tests.cs` | Тесты `Save_RoundTrip` и `Save_Corrupted_StartsNewGame` зелёные |
| [ ] | 9 | Unity-ассеты | Сцена `gc26_2048_nastya_Main`, Canvas (Screen Space – Camera, 1080×1920), корневой объект `gc26_2048_nastya_Root` | `Scenes/gc26_2048_nastya_Main.unity` | Сцена открывается, на ней висит `Game2048Nastya` |
| [ ] | 10 | Unity-ассеты | `Theme2048` (ScriptableObject) + ассет с палитрой из GDD | `View/Theme2048.cs`, `Art/gc26_2048_nastya_Theme.asset` | Цвета и шрифты соответствуют GDD |
| [ ] | 11 | Unity-ассеты | Скруглённый спрайт с 9-slice, префабы `Cell` и `Tile` (Image + TMP) | `Art/gc26_2048_nastya_RoundedRect.png`, `Prefabs/` | Префабы выглядят как в прототипе |
| [ ] | 12 | Отображение | `TileView2048.Set(value, theme)`, кэш строк чисел | `View/TileView2048.cs` | Плитка правильно красится для любого значения |
| [ ] | 13 | Отображение | `TilePool2048`: пул без `Instantiate`/`Destroy` во время игры | `View/TilePool2048.cs` | Плитки создаются только при прогреве |
| [ ] | 14 | Отображение | `BoardView2048`: расчёт позиций клеток, словарь Id → TileView, `Render` из состояния | `View/BoardView2048.cs` | Поле из модели корректно рисуется |
| [ ] | 15 | Отображение | Анимации на корутинах: сдвиг 0,10 с → поп/появление 0,12 с, `Bump`, `FinishNow()` | `View/BoardView2048.cs` | Анимации плавные, `FinishNow` сразу доводит плитки до конца |
| [ ] | 16 | Отображение | `Hud2048`: счёт, лучший счёт, кнопки (пауза, новая игра, отмена) | `View/Hud2048.cs` | Счёт выводится через `SetText` без аллокаций |
| [ ] | 17 | Отображение | `Overlays2048`: пауза, победа («Играть дальше»), конец игры («Ещё раз», «В меню») | `View/Overlays2048.cs` | Окна открываются и закрываются, кнопки отправляют события |
| [ ] | 18 | Ввод | Ассет `inputactions`: Move, PointerPress, PointerPosition, Pause, NewGame | `Input/gc26_2048_nastya_Controls.inputactions` | Привязки на стрелки, WASD, Esc, N и геймпад |
| [ ] | 19 | Ввод | `Input2048`: преобладающая ось, порог свайпа 5%, игнор нажатий на UI, события | `Scripts/Input/Input2048.cs` | Работают и клавиши, и свайп мышью |
| [ ] | 20 | Ввод | Защита от второго EventSystem (отключаем свой, если уже есть активный) | `Scripts/Input/` | В оболочке остаётся ровно один EventSystem |
| [ ] | 21 | Игровой цикл | `Game2048Nastya`: машина состояний (Playing / Animating / Paused / Win / GameOver / Exiting) | `Scripts/Game2048Nastya.cs` | Состояния переключаются по схеме из TDD 5.1 |
| [ ] | 22 | Игровой цикл | Ход по шагам из TDD 5.2: снимок для отмены, счёт, спавн, анимация, сохранение, проверка победы и поражения | `Scripts/Game2048Nastya.cs` | Игра полностью играбельна в редакторе |
| [ ] | 23 | Игровой цикл | Буфер ввода на одно направление, отмена хода (U), новая игра (N) | `Scripts/Game2048Nastya.cs` | Быстрый ввод не ломает поле |
| [ ] | 24 | Игровой цикл | Закрыть `TODO`: загрузка в `Begin`, сохранение в `ExitToMenu`, ровно один вызов `exit(score)` | `Scripts/Game2048Nastya.cs` | После перезапуска партия продолжается |
| [ ] | 25 | Звук | `Sfx2048`: сдвиг, слияние с повышением тона, недействительный ход, переключатель звука | `Audio/Sfx2048.cs`, `Audio/*.ogg`, `Audio/CREDITS.md` | Звук выключается через `mute`, `AudioListener` не трогаем |
| [ ] | 26 | Проверка | Отладочный `[ContextMenu]` «заполнить поле» (только в редакторе) | `Scripts/Game2048Nastya.cs` | Можно быстро дойти до 2048 |
| [ ] | 27 | Проверка | Ручные тесты из TDD 12.2: запуск из оболочки, Device Simulator, быстрый ввод, `timeScale == 1` | — | После выхода в иерархии не остаётся объектов |
| [ ] | 28 | Проверка | Производительность: 0 байт аллокаций за ход, меньше 30 draw calls | Profiler | Цели из TDD 11 выполнены |
| [ ] | 29 | Сдача | `python tools/check_scope.py`, `python tools/check_names.py gc26_2048_nastya`, коммит, пуш, PR | — | Скрипты проходят, PR открыт |
