# gc26_tanki — «Стальной рубеж»

- Ветка: `gc26_tanki`
- Неймспейс: `GC26.Tanki` (правила — `GC26.Tanki.Rules`, редактор — `GC26.Tanki.EditorTools`)
- Стартовая сцена: `Scenes/gc26_tanki_Main.unity`, на ней один объект с `TankiGame`
- Команда:

Танковая игра для телефона (вертикально, 9:16): защищай реактор от волн вражеских танков.

## Как играть

- Стик слева: движение. Справа: огонь. Кнопка «УДАР» посередине: удар реактора.
- Клавиатура: стрелки или WASD, пробел для огня, E или Shift для удара, Esc для паузы.
- «ВЫХОД» в главном меню возвращает в меню GC26 и отдаёт оболочке рекорд.

## Что есть в игре

- **Кампания:** 20 уровней. На каждом 5-м (5, 10, 15, 20) в конце выходит босс.
- **Здоровье и жизни:** 3 деления здоровья плюс броня из мастерской. Сердечки показывают запасные танки.
- **Удар реактора:** заряжается за убийства, от 4 на 1-м уровне до 10 к 20-му.
- **Мастерская:** броня, пушка, скорострельность, скорость. Платишь деталями.
- **Звезда:** временная, 20 секунд.
- **Выживание:** бесконечные волны, после каждой выбор 1 улучшения из 3, босс каждую 5-ю волну.
- **Настройки:** звук, вибрация, сложность.

## Папки

| Папка | Что там |
|---|---|
| `Scripts/Rules/` | Правила и ИИ на чистом C# без Unity (`GameSim`, `GameTypes` с числами `Tuning`, `Levels`) |
| `Scripts/Game/` | Unity-часть: `TankiGame` (вход для оболочки), `GameRunner`, `GameHud` (меню на IMGUI), `WorldView*`, `Controls`, `SaveData`, `SoundBank`, `CameraRig` |
| `Scripts/Editor/` | `TankiAssetImport`: настройки импорта картинок и моделей этой игры |
| `Resources/gc26_tanki/` | Модели, текстуры, фоны меню, материалы (URP Lit) |
| `Tests~/` | Проверка правил без Unity: `dotnet run --project "Assets/Microgames/gc26_tanki/Tests~"` |
| `Web~/` | Браузерная версия на three.js с теми же правилами |
| `Art~/` | Python-скрипты (Pillow, numpy), которые рисуют модели, тайлы, бонусы, фоны и значок |

Папки с `~` на конце Unity не импортирует.

## Где что менять

| Что | Файл |
|---|---|
| Скорости, жизни, таймеры, очки, прокачка, босс | `Scripts/Rules/GameTypes.cs` (класс `Tuning`) |
| Карты и волны врагов (`X` в волне — босс) | `Scripts/Rules/Levels.cs` |
| Правила и ИИ | `Scripts/Rules/GameSim.cs` |
| Модели и цвета | `Scripts/Game/WorldView.cs` |
| Камера и свет | `Scripts/Game/CameraRig.cs` |
| Управление | `Scripts/Game/Controls.cs` (новая Input System) |
| Интерфейс и меню | `Scripts/Game/GameHud.cs` |

Правила есть в двух местах: в C# (`Scripts/Rules`) и в JS (`Web~/steel-line.html`). Меняешь одно — меняй и другое.

## Браузерная версия

```bash
python -m http.server 8765 --directory "Assets/Microgames/gc26_tanki/Web~"
```

Потом открыть `http://localhost:8765/steel-line.html`.

## Арт

Скрипты запускаются из папки `Art~`:

```bash
python build_models.py ../Resources/gc26_tanki/Tanks ../Resources/gc26_tanki/Tanks ../Web~/tanks
python build_tiles.py ../Resources/gc26_tanki/Tiles ../Web~/tiles
python build_bonuses.py ../Resources/gc26_tanki/Bonuses ../Web~/bonuses
python build_backgrounds.py ../Web~/bg
python build_icon.py
```
