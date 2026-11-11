# GC26 — как начать (для новых участников)

Короткая пошаговая инструкция. Полные правила — [RULES.md](RULES.md), описание скриптов — [tools/README.md](tools/README.md).

## Перед стартом: придумать игру (VICKY)

Если идеи ещё нет, проведите с командой часовой брейншторм по методу VICKY:
**V**alue → **I**nterests → **C**razy 8s → **K**eepers → **Y**es, and. Страница ведёт сессию по шагам с таймером,
собирает идеи и голоса (у каждого 3 голоса) и выдаёт итог в Markdown — его удобно вставить в `README.md` своей игры.

👉 **https://ivanou-dzmitry.github.io/mikrozaidimai/vicky/** — откройте на одном экране для всей команды, RU / EN.

## 0. Что нужно установить

- [ ] **Unity 6000.5.6f1** (точно эта версия — см. `ProjectSettings/ProjectVersion.txt`), через Unity Hub
- [ ] **Git** (на Windows — Git for Windows, вместе с Git Bash)
- [ ] **Python 3.8+** — нужен для проверок; `python --version` должен работать в терминале
- [ ] Доступ к репозиторию `https://github.com/Zaidejas/GC26` (попросить организатора)
- [ ] Узнать у организатора **ID своей игры** — он есть в `tools/games.txt` (например `gc26_tetris`)

## 1. Клонировать и настроить (один раз)

```bash
git clone https://github.com/Zaidejas/GC26.git
cd GC26
git config core.hooksPath tools/hooks
```

Последняя команда включает проверку перед каждым `git push`. Без неё ошибки найдёт только CI на PR.

## 2. Создать свою ветку и папку игры

Подставьте свой ID вместо `gc26_tetris`:

```bash
git switch -c game/gc26_tetris origin/main
python tools/new_microgame.py gc26_tetris
```

Скрипт создаст `Assets/Microgames/gc26_tetris/` с паспортом `microgame.json`, своим `asmdef`,
`README.md` и стартовым скриптом `Scripts/TetrisGame.cs`.

> Только одна ветка на игру. Если ветка уже создана коллегой по команде — не создавайте новую, а возьмите её:
> `git switch game/gc26_tetris`

## 3. Первый запуск в Unity

1. Открыть папку `GC26` через Unity Hub (версия 6000.5.6f1).
2. В `Assets/Microgames/<id>/Scenes/` создать сцену **`<id>_Main.unity`** (например `gc26_tetris_Main`).
3. Создать на сцене пустой объект и повесить на него компонент из `Scripts/` (например `TetrisGame`).
4. Нажать Play — игра стартует сама, без меню (для отладки).
5. Заполнить в `microgame.json`: `title`, `description`, `authors`.
6. В `README.md` своей папки — состав команды и «как играть».

## 4. Как писать код — 8 правил коротко

| ✅ Можно | ❌ Нельзя |
| --- | --- |
| Менять всё внутри `Assets/Microgames/<id>/` | Трогать `ProjectSettings/`, `Packages/`, `Assets/Core/`, `Assets/Shared/`, Build Settings |
| Весь код в `namespace GC26.<Name>` (свой asmdef) | Ссылаться на код чужих игр |
| Сцены `<id>_Что-то.unity` | Сцены `Main.unity`, `Level1.unity` |
| `Resources/<id>/...` и `Resources.Load("<id>/...")` | Файлы прямо в `Resources/` |
| Сохранения в `MicrogameSave.Folder(Id)` | `Application.persistentDataPath` напрямую |
| `PlayerPrefs` с ключом `<id>_best` или `MicrogameSave.Key(Id, "best")` | `PlayerPrefs.SetInt("best", ...)` |
| Уникальные имена: `TetrisBoard`, `TetrisPiece` | Общие имена: `GameManager`, `Player`, `Enemy`, `UI`, `Menu`, `Settings`… |
| Всё внутри своей сцены | `DontDestroyOnLoad`, статические синглтоны, `Time.timeScale` / `Physics.gravity` без возврата |

Нужен новый слой, тег, пакет или правка в `Core` — **попросите организатора**, сами не добавляйте.

Чужой ассет нужен? Дублируйте его в Unity (**Ctrl+D**). Не копируйте файлы вместе с `.meta` из другой ветки.

## 5. Связь с меню (обязательно)

- Стартовая сцена содержит **ровно один** компонент с `IMicrogame` (его создал скрипт из шага 2).
- В игре должны быть своя **пауза** и кнопка **«В меню»**.
- Кнопка «В меню» и экран «Game Over» вызывают `ExitToMenu(score)` — перед этим сохраните прогресс.

## 6. Каждый день: коммит и пуш

```bash
git status
git add Assets/Microgames/gc26_tetris Assets/Microgames/gc26_tetris.meta
git commit -m "tetris: падение фигур"
git push -u origin game/gc26_tetris
```

Перед пушем хук сам запустит проверки. Можно и вручную:

```bash
python tools/check_scope.py
python tools/check_names.py gc26_tetris
```

Добавляйте файлы **только своей папкой** — не `git add .`: Unity часто сам меняет файлы в `ProjectSettings/`.

## 7. Раз в неделю: подтянуть `main`

```bash
git fetch
git merge origin/main
```

Чем чаще — тем меньше конфликтов.

## 8. Если проверка упала

| Сообщение | Что делать |
| --- | --- |
| `outside your folder: ProjectSettings/...` | `git checkout origin/main -- ProjectSettings/...` и попросить изменение у организатора |
| `generic name 'Player'` | Переименовать: `TetrisPlayer` |
| `no namespace, wrap code in 'namespace GC26.Tetris'` | Обернуть код в неймспейс |
| `scene name must start with 'gc26_tetris_'` | Переименовать сцену |
| `PlayerPrefs key 'best' must start with 'gc26_tetris_'` | Использовать `MicrogameSave.Key(Id, "best")` |
| `'gc26_xxx' is not in tools/games.txt` | Проверить ID / имя ветки; новый ID добавляет только организатор |
| `Python not found, checks skipped` | Установить Python и добавить в PATH |

Больше примеров — [tools/README.md](tools/README.md).

## 9. Сдача игры

1. Подтянуть свежий `main` (шаг 7), обе проверки проходят.
2. Открыть Pull Request `game/<id>` → `main` на GitHub. CI проверит его ещё раз.
3. Организатор мержит, делает **GC26 → Sync Microgames** и проверяет запуск игры из меню и выход обратно.

## Чек-лист готовности игры

- [ ] Сцена `<id>_Main.unity` запускается отдельно и из меню
- [ ] Есть пауза и кнопка «В меню», выход вызывает `ExitToMenu(score)`
- [ ] Прогресс сохраняется через `MicrogameSave`
- [ ] `microgame.json` заполнен (title, description, authors)
- [ ] `README.md` игры заполнен
- [ ] `check_scope.py` и `check_names.py` — без ERROR
- [ ] Ветка содержит свежий `main`
