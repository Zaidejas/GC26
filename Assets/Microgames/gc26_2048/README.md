# gc26_2048 — «Мышкин сыр»

- Ветка: `game/gc26_2048`
- Неймспейс: `GC26._2048`
- Стартовая сцена: `Scenes/gc26_2048_Main.unity` (один объект `gc26_2048` с компонентом `Game2048`)
- Команда:

Как играть: сдвигайте предметы на поле 4×4 и объединяйте одинаковые — от травы до сыра
(трава → сено → корова → бидон молока → бутылка молока → сметана → творог → сыр).
Готовый сыр мышка уносит в норку. На партию 150 ходов. Управление: свайпы, стрелки / WASD, Esc — меню.

## Устройство

| Папка | Что внутри |
| --- | --- |
| `Scripts/Core/` | Логика на чистом C#: `CheeseRules` (правила и очки), `Board` (поле, ход, слияние), `MoveResult` (события хода), `CheeseSession` (партия: счёт, ходы, норка) |
| `Scripts/Save/` | `CheeseStorage`: рекорд, лучший предмет и звук в PlayerPrefs (`gc26_2048_*`), партия в `MicrogameSave.Folder(Id)/game.json` |
| `Scripts/View/` | Интерфейс собирается кодом: `CheeseUi` (помощники uGUI), `CheeseFrame` (кадр 9:16 в Safe Area), `BoardView` / `TileView` (поле, анимации на корутинах, пул плиток), `CheeseSwipe`, `CheeseAudio` (звуки-заглушки), `CheeseArt` (палитра и спрайты) |
| `Scripts/Game2048.cs` | Точка входа `IMicrogame`: экраны, ввод, сохранения, выход в оболочку |
| `Resources/gc26_2048/` | Спрайты 8 предметов и мышки (перенесены из HTML-прототипа) |
| `Editor/` | `CheeseSceneBuilder`: меню **GC26 → gc26_2048 → Build Main Scene** пересобирает сцену |
| `Tests/Editor/` | Unit-тесты логики (Window → General → Test Runner → EditMode) |
| `Docs~/` | `gc26_2048_docs.zip`: GDD, план, задачи, HTML-прототип. Unity эту папку не импортирует |

Всё, что игра создаёт, — дети объекта `gc26_2048` в её сцене; камера и EventSystem создаются,
только если сцена запущена отдельно (без оболочки). `Time.timeScale`, `DontDestroyOnLoad` и статическое
состояние не используются.

## Командная строка (Unity закрыт)

```
Unity.exe -batchmode -quit -projectPath <проект> -executeMethod GC26._2048.EditorTools.CheeseSceneBuilder.Build
Unity.exe -batchmode -projectPath <проект> -runTests -testPlatform EditMode -assemblyNames GC26._2048.Tests -testResults results.xml
```
