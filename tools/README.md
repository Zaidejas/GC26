# tools — скрипты GC26

Нужен только Python 3.8+ (без библиотек). Запускать из корня репозитория.

| Файл | Кто запускает | Зачем |
| --- | --- | --- |
| `new_microgame.py` | команда, один раз | Создать папку своей игры |
| `check_scope.py` | хук / CI / вручную | Ветка трогает только свою папку |
| `check_names.py` | хук / CI / вручную | Имена, паспорт, asmdef, неймспейсы |
| `hooks/pre-push` | Git, сам | Обе проверки перед `git push` |
| `games.txt` | организатор | Список разрешённых ID |
| `gc26.py` | — | Общий код для скриптов, не запускается |

## new_microgame.py

```bash
python tools/new_microgame.py gc26_tetris
```

Создаёт `Assets/Microgames/gc26_tetris/`:

```
microgame.json          паспорт: id, title, description, entry_scene, authors
GC26.Tetris.asmdef      своя сборка, ссылается на GC26.Core
README.md               ветка, неймспейс, команда
Scripts/TetrisGame.cs   заготовка IMicrogame: Begin(exit), ExitToMenu(score), константа Id
Scenes/ Prefabs/ Art/ Audio/
```

После: в Unity создать сцену `Scenes/gc26_tetris_Main.unity`, повесить на объект `TetrisGame`,
заполнить `title`, `description`, `authors` в `microgame.json`. Кнопку «В меню» в своей паузе
повесить на `ExitToMenu(score)`.
Сцену можно запускать отдельно — игра стартует сама, если оболочки нет.

Ошибки: ID нет в `games.txt` → попросить организатора; папка уже есть → ничего не создаёт.

## check_scope.py

```bash
python tools/check_scope.py                     # текущая ветка против origin/main
python tools/check_scope.py --base main         # если нет remote
python tools/check_scope.py --branch game/gc26_tetris
```

Берёт все изменения с момента, когда ветка отошла от `main` (коммиты + незакоммиченное + новые файлы),
и проверяет, что они лежат в `Assets/Microgames/<id>/` или это `Assets/Microgames/<id>.meta`.
Ветки не `game/*` (например `core/*`) пропускает.

```
  ERROR outside your folder: ProjectSettings/TagManager.asset
```

→ откатить файл: `git checkout origin/main -- ProjectSettings/TagManager.asset`,
а нужное изменение попросить у организатора.

## check_names.py

```bash
python tools/check_names.py                 # все игры
python tools/check_names.py gc26_tetris     # только одна
```

| Проверка | Пример ошибки |
| --- | --- |
| Папка игры есть в `games.txt` | `folder is not a known id` |
| `microgame.json` есть, поля и типы верные | `field 'description' missing or wrong type` |
| `id` в паспорте = имя папки | `id 'tetris' != folder 'gc26_tetris'` |
| `entry_scene` начинается с ID (нет файла — только WARN) | `entry_scene 'Main' must start with 'gc26_tetris_'` |
| `GC26.<Name>.asmdef` есть, `name` верный | `missing GC26.Tetris.asmdef` |
| Сцены `*.unity` начинаются с ID | `scene name must start with 'gc26_tetris_'` |
| В `Resources/` только подпапка ID | `may contain only the folder 'gc26_tetris/'` |
| Нет общих имён `.cs/.prefab/.unity/.asset` | `generic name 'Player'` |
| Каждый `.cs` в `namespace GC26.<Name>` | `no namespace, wrap code in 'namespace GC26.Tetris'` |
| Ключи `PlayerPrefs.X("...")` начинаются с ID | `PlayerPrefs key 'best' must start with 'gc26_tetris_'` |
| `Application.persistentDataPath` напрямую — только WARN | `use MicrogameSave.Folder(Id) instead of ...` |

Запрещённые имена: `GameManager`, `Manager`, `Game`, `Player`, `Enemy`, `Main`, `UI`, `HUD`, `Level`, `Level1`,
`Menu`, `MainMenu`, `AudioManager`, `SoundManager`, `InputManager`, `GameController`, `Controller`, `Settings`,
`Constants`. Список — `GENERIC` в начале `check_names.py`.

## hooks/pre-push

Включить один раз на каждый клон:

```bash
git config core.hooksPath tools/hooks
```

Перед `git push` подтягивает `origin/main` и запускает обе проверки; ошибка — пуш отменяется.
Нет Python — проверки пропускаются с предупреждением. Обойти в крайнем случае: `git push --no-verify`
(CI всё равно проверит PR).

## games.txt

Один ID на строку, `#` — комментарий. Формат: `gc26_` + строчные латинские буквы, цифры, `_`.
Меняет только организатор, через `main`.

## Коды выхода

`0` — всё хорошо (WARN не мешает), `1` — есть ERROR. Так их понимают хук и CI.
