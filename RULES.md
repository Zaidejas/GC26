# GC26 Microgames — правила репозитория (Unity · C# · Git)

Один Unity-проект, 11 полноценных игр, у каждой своя ветка и команда. Игры запускаются из общего меню (оболочки).
В конце всё сливается в `main` без конфликтов, если каждая команда работает **только в своей папке**,
со **своим asmdef и неймспейсом**, и называет глобальные вещи **своим ID**.

## Игры

| ID | Ветка | Папка | Неймспейс / asmdef |
| --- | --- | --- | --- |
| `gc26_spinv` | `game/gc26_spinv` | `Assets/Microgames/gc26_spinv/` | `GC26.Spinv` |
| `gc26_minesweeper` | `game/gc26_minesweeper` | `Assets/Microgames/gc26_minesweeper/` | `GC26.Minesweeper` |
| `gc26_subwaysurfers` | `game/gc26_subwaysurfers` | `Assets/Microgames/gc26_subwaysurfers/` | `GC26.Subwaysurfers` |
| `gc26_tetris` | `game/gc26_tetris` | `Assets/Microgames/gc26_tetris/` | `GC26.Tetris` |
| `gc26_timetraveler` | `game/gc26_timetraveler` | `Assets/Microgames/gc26_timetraveler/` | `GC26.Timetraveler` |
| `gc26_findepair` | `game/gc26_findepair` | `Assets/Microgames/gc26_findepair/` | `GC26.Findepair` |
| `gc26_towerdefence` | `game/gc26_towerdefence` | `Assets/Microgames/gc26_towerdefence/` | `GC26.Towerdefence` |
| `gc26_lines` | `game/gc26_lines` | `Assets/Microgames/gc26_lines/` | `GC26.Lines` |
| `gc26_science_snake` | `game/gc26_science_snake` | `Assets/Microgames/gc26_science_snake/` | `GC26.ScienceSnake` |
| `gc26_2048` | `game/gc26_2048` | `Assets/Microgames/gc26_2048/` | `GC26._2048` |
| `gc26_flpdrgn` | `game/gc26_flpdrgn` | `Assets/Microgames/gc26_flpdrgn/` | `GC26.Flpdrgn` |

ID — `gc26_` + строчные латинские буквы, цифры и `_`. Список ID — в `tools/games.txt`, меняет только организатор.
Неймспейс получается из ID автоматически: части после `gc26_` с заглавной буквы (`science_snake` → `ScienceSnake`);
если начинается с цифры — с `_` впереди (`2048` → `_2048`).

## Структура проекта

```
Assets/
    Core/                      общие скрипты: меню, загрузка игр, место для сохранений. Только через main.
        MicrogameAPI/          IMicrogame, MicrogameManifest, MicrogameRunner, MicrogameSave
        Editor/                GC26 → Sync Microgames (Build Settings + реестр)
    Shared/                    общие ассеты и утилиты. Только через main.
    Microgames/
        gc26_tetris/
            microgame.json     паспорт игры (обязательно)
            GC26.Tetris.asmdef своя сборка (обязательно)
            README.md
            Scenes/gc26_tetris_Main.unity
            Scripts/  Prefabs/  Art/  Audio/
ProjectSettings/  Packages/    только через main
tools/                         скрипты проверки
```

## 8 правил

1. **Только своя папка.** Ветка `game/<id>` меняет только `Assets/Microgames/<id>/` и `Assets/Microgames/<id>.meta`.
   Всё остальное — отдельный PR в `main`.
2. **Свой asmdef и неймспейс.** `GC26.<Name>.asmdef` в корне папки игры, весь код в `namespace GC26.<Name>`.
   Ссылаться можно только на `GC26.Core` и пакеты из `main`, не на чужие игры.
3. **Сцены — с префиксом ID.** `gc26_tetris_Main.unity`, `gc26_tetris_Test.unity`. Имя сцены в Unity глобально.
4. **Resources — только в подпапке ID.** `Resources/gc26_tetris/...` и `Resources.Load("gc26_tetris/Block")`.
   Имена Addressables, input actions — тоже с префиксом `gc26_tetris_`.
   **Сохранения** — как угодно внутри игры, но только в `MicrogameSave.Folder(Id)` (это `persistentDataPath/gc26_tetris/`),
   а ключи `PlayerPrefs` — с префиксом `gc26_tetris_` (или `MicrogameSave.Key(Id, "best")`).
5. **Не трогать проект целиком.** `ProjectSettings/*` (теги, слои, физика, input, качество), `Packages/manifest.json`,
   Build Settings, `Assets/Core`, `Assets/Shared`. Нужен слой, тег или пакет — просьба организатору, он добавит в `main`.
   Слои и теги для своих объектов лучше не заводить вовсе: используйте ссылки и компоненты.
6. **Без глобального состояния.** Никаких `DontDestroyOnLoad`, статических синглтонов на сессию, изменений
   `Time.timeScale` / `Physics.gravity` без возврата обратно в `OnDisable`.
7. **Раз в неделю — `main` в свою ветку.** `git fetch && git merge origin/main`. Конфликты маленькие и по одному.
8. **Перед пушем — проверка.** Хук `pre-push` и CI запускают `tools/check_scope.py` и `tools/check_names.py`.

Не копируйте ассеты из чужой ветки вместе с `.meta` — получите два файла с одним GUID и сломанные ссылки.
Нужен чужой ассет — дублируйте его в Unity (Ctrl+D), чтобы появился новый GUID.

## Паспорт игры: `microgame.json`

Оболочка находит игры сама по `Assets/Microgames/*/microgame.json`. Общего списка, который все правят, нет.

```json
{
  "id": "gc26_tetris",
  "title": "Tetris",
  "description": "Классический тетрис с уровнями",
  "entry_scene": "gc26_tetris_Main",
  "authors": ["Имя Фамилия"]
}
```

| Поле | Что это |
| --- | --- |
| `id` | Совпадает с именем папки |
| `title` | Название в меню |
| `description` | Одна строка под названием в меню |
| `entry_scene` | Имя стартовой сцены в `Scenes/`, начинается с ID |
| `authors` | Имена для титров |

## Общие скрипты (`Assets/Core`)

Лежат в `main`, все игры используют их через ссылку на сборку `GC26.Core`. Команды их **не меняют** —
нужна правка, PR организатору.

| Скрипт | Для чего |
| --- | --- |
| `IMicrogame` | Интерфейс, который реализует игра — единственная точка связи с меню |
| `MicrogameRunner` | Меню-оболочка: загружает сцену игры, ждёт выхода, выгружает |
| `MicrogameSave` | Где хранить сохранения: `Folder(Id)` и `Key(Id, "...")`. Формат и логика — свои у каждой игры |
| `MicrogameManifest` | Чтение `microgame.json` и реестра игр |
| `Editor/MicrogameSync` | Меню **GC26 → Sync Microgames**: Build Settings + реестр |

## Контракт с оболочкой

Стартовая сцена игры содержит ровно один компонент, реализующий `GC26.Core.IMicrogame`:

```csharp
public interface IMicrogame
{
    // exit: вызвать один раз, когда игрок выходит в меню; score — итоговый счёт (0, если счёта нет)
    void Begin(Action<int> exit);
}
```

- Оболочка грузит сцену аддитивно, находит `IMicrogame`, вызывает `Begin`. Сколько длится игра — решает игра.
- Пауза и кнопка **«В меню»** — в каждой игре свои. По кнопке игра сохраняется и вызывает `exit(score)`.
- После `exit` оболочка выгружает сцену и возвращает `Time.timeScale = 1`. Всё, что игра создала, должно быть в
  этой сцене.
- Для отладки сцену можно запустить отдельно: игра стартует сама, если оболочки нет (это уже есть в шаблоне).

## Скрипты (`tools/`, Python 3.8+, без библиотек)

Подробно, с примерами ошибок и как их чинить — [tools/README.md](tools/README.md).

| Команда | Что делает |
| --- | --- |
| `python tools/new_microgame.py gc26_tetris` | Создаёт папку игры: паспорт, asmdef, README, стартовый скрипт |
| `python tools/check_scope.py` | Ветка `game/<id>` меняет только свою папку |
| `python tools/check_names.py` | ID, паспорта, asmdef, неймспейсы, имена сцен, Resources, ключи PlayerPrefs, общие имена |
| `git config core.hooksPath tools/hooks` | Включает проверку перед каждым `git push` (один раз на клон) |

На GitHub то же запускает `.github/workflows/microgames.yml` на каждый PR в `main`.

## Поток работы

```bash
git clone <repo> && cd <repo>
git config core.hooksPath tools/hooks
git switch -c game/gc26_tetris origin/main
python tools/new_microgame.py gc26_tetris
# открыть Unity, переименовать сцену/скрипт при желании, работать
git fetch && git merge origin/main
git push -u origin game/gc26_tetris
```

Сцену создайте в Unity: `Scenes/gc26_tetris_Main.unity`, повесьте на объект компонент из `Scripts/`.
Нужно что-то в `Core`, `Shared` или настройках — ветка `core/<что>` от `main`, маленький PR организатору.

## Настройка репозитория (один раз, организатор)

1. Unity: Project Settings → Editor → Asset Serialization **Force Text**, Version Control **Visible Meta Files**.
2. Положить в корень `.gitattributes` и Unity-шный `.gitignore` (есть в этом наборе) и включить Smart Merge:

   ```bash
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver "'<путь к Unity>/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
   ```

3. Закоммитить в `main`: `Assets/Core`, `Assets/Shared`, пустую `Assets/Microgames/` с её `.meta`, `tools/`.
   Только после этого создавать ветки игр.
4. Защитить `main` (merge только через PR) и, если GitHub, включить `CODEOWNERS`.

## Финальное слияние

1. Все ветки подтягивают свежий `main` и проходят обе проверки.
2. Организатор мержит по одной ветке в `main`.
3. В Unity: **GC26 → Sync Microgames** — добавляет все стартовые сцены в Build Settings и пишет реестр игр.
4. Запуск из меню, проверка игры и выхода в меню, следующая ветка.
