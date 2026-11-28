# Colours — Game Design Document (gc26_2048_nastya)

**Language / Язык:** English (below) · [Русский](#русская-версия)

| | |
| --- | --- |
| Microgame ID | `gc26_2048_nastya` |
| Working title | **Colours** (menu title; can be changed in `microgame.json`) |
| Genre | Single-player sliding-tile number puzzle |
| Platforms | PC (keyboard, mouse) and mobile (touch), portrait or landscape |
| Engine | Unity 6000.5.6f1, URP, uGUI + TextMeshPro, Input System |
| Session length | 3 to 15 minutes; the player can stop at any time and resume later |
| Host | Runs inside the GC26 shell menu through `IMicrogame` (see the TDD) |

---

## 1. Concept

A 4×4 board holds number tiles. Each move slides **every** tile as far as it can go in one direction.
When two tiles with the same number collide, they merge into one tile with double the value, and that value
is added to the score. After each move a new small tile appears in an empty cell.
The goal is to build a **2048** tile. The game ends when the board is full and no move can merge anything.

**Pitch:** "One swipe moves everything. Plan ahead, keep your big tile in a corner, and double your way up."

### Design pillars
1. **Instant to learn:** a single input (direction), no tutorial wall. A first-time player understands it within 3 moves.
2. **Readable at a glance:** every value has its own color and the numbers are large, so the board state is clear without reading.
3. **Satisfying feedback:** short slide, merge pop and spawn animations, plus a score popup, so every move feels responsive.
4. **Fair randomness:** spawns are random but always follow the rules below. Nothing is hidden from the player.

---

## 2. Core rules

### 2.1 Board
- A grid of **4×4** cells.
- A cell is either empty or holds one tile with a value 2, 4, 8, … (powers of two).

### 2.2 Start of a game
- Two tiles spawn in random empty cells.
- Spawned tile value: **2 with 90% probability, 4 with 10%**.

### 2.3 A move
1. The player picks a direction: **Up, Down, Left, Right**.
2. Every tile slides in that direction until it hits the board edge or another tile.
3. **Merging:** if a tile slides into a tile with the same value, they merge into one tile with double the value.
   - A tile created by a merge **cannot merge again in the same move**.
     Example: row `2 2 2 2` moved Left → `4 4 _ _` (not `8 _ _ _`).
   - When three equal tiles line up, the pair **nearest the wall the tiles move toward** merges first.
     Example: `2 2 2 _` moved Left → `4 2 _ _`; moved Right → `_ _ 2 4`.
4. If **no tile moved and nothing merged**, the move is invalid: no spawn, no score, the move counter does not change, and the board gives a small "bump" feedback.
5. If the move was valid, **one** new tile spawns in a random empty cell (90% 2 / 10% 4).

### 2.4 Score
- Each merge adds the **value of the new tile** to the score (two 8s merging into a 16 give +16).
- **Best score** is saved between sessions.
- The **final score** goes to the GC26 shell when the player leaves the game.

### 2.5 Win and lose
- **Win:** the first time a 2048 tile appears, a "You made 2048!" overlay shows with two buttons:
  **Keep going** (play on with no limit; the win overlay never shows again in this game) and **New game**.
- **Lose (game over):** the board is full **and** no two neighboring tiles (horizontally or vertically) share a value.
  The "No more moves" overlay shows the final score and best score, with **Try again** and **To menu**.

---

## 3. Controls

| Platform | Input | Action |
| --- | --- | --- |
| PC | Arrow keys / WASD | Move |
| PC | Mouse drag (press, drag, release) | Move in the drag direction |
| Mobile | Swipe | Move in the swipe direction |
| All | Esc / Pause button on the HUD | Open the pause menu |
| All | N key / New game button | Start a new game (asks to confirm if a game is in progress) |

- A swipe counts only when it is longer than about **5% of the screen's short side**; a shorter one is ignored.
  The direction is the dominant axis of the drag.
- **Input buffering:** if the player enters a direction while a move animation is still playing, the animation finishes instantly and the buffered move runs.
  Only **one** move is buffered, so fast players never feel input lag.

---

## 4. Game flow and screens

```
Shell menu ──Begin()──► [Splash] ──► [Start menu] ──► [Game screen] ◄──► [Pause menu] ──"To menu"──► exit(score) ──► Shell menu
                                            │
                                            ├──► [Win overlay]       (first 2048: Keep going / New game)
                                            └──► [Game over overlay] (Try again / To menu)
```

### 4.0 Splash screen
- Every launch starts with a short splash: the title **Colours** with **2048** under it, right-aligned,
  in a left-to-right gradient Amaranth → Thulian Pink → Chalk, on the game background.
  It stays 1.2 s and fades out in 0.4 s, revealing the start menu.
- A tap, a click or any key skips it at once.

### 4.0.1 Start menu
- The same gradient title, then four buttons: **New Game**, **Continue**, **Tutorial**, **Settings**.
  The game version (for example `v1.0.0`) is shown at the bottom.
- **Continue** is available only when a saved game was loaded; it returns to that board.
- **New Game** starts a fresh game; when a saved game exists it first asks "Start a new game?".
- **Tutorial**: a short "How to Play" page with the rules and controls, and a **Back** button.
- **Settings**: **Sound Effects: On/Off** (saved between sessions) and **Back**.

### 4.0.2 Windows and language
- Every button in the start menu and in every window (win, game over, pause, tutorial, settings,
  "Start a new game?") has the same size and is stacked vertically.
- All text in the game is in English.

### 4.1 Game screen (HUD)
- **Top bar:** game title (**Colours**, with **2048** on the line below, right-aligned, same font and size),
  **Score** box, **Best** box, and the buttons **Undo**, **New game** and **Pause**.
- **Undo** takes back the last move only (board, score and move count; the best score stays). It is greyed out
  when there is nothing to undo: in a new game, right after a resume, and right after an undo.
- **Center:** the board, square, as large as fits the screen while leaving room for the HUD.
- Short hint under the board on the first launch only: "Swipe or use the arrow keys to combine tiles."

### 4.2 Pause menu
- Buttons: **Resume**, **New Game**, **Sound Effects: On/Off**, **To Menu**.
- Opening the pause menu blocks board input. It does **not** change `Time.timeScale` (see the TDD).
- **To menu:** saves the game in progress and calls `exit(score)`.

### 4.3 Resume
- If a game was in progress when the player left, the next launch **continues it** (board, score, the "already won" flag).
- If the last game ended in game over, the next launch starts a new game.

---

## 5. Visual style

The look is the team's own, deliberately unlike other games in the genre.

### 5.1 Direction
**"Mediterranean Coast":** a dark plum-stone background, a slightly lighter board, low tiles in calm natural tones (chalk, sea sage, olive pebble) that move to bright berry accents (Thulian Pink, Amaranth) with a glow on high tiles. Reference colors: Amaranth `#933B5B`, Thulian Pink `#B5728A`, Brook Green `#AABAAE`, Chalk `#E3D6BF`, Pomelo Olive `#9F9679`. The splash title uses a gradient Amaranth → Thulian Pink → Chalk.
Flat shapes, soft shadows, no textures; everything is built from uGUI images with rounded sprites.

### 5.2 Palette (starting proposal; tune in the editor)

| Element | Color | Text color |
| --- | --- | --- |
| Background | `#231C20` | — |
| Board | `#31272D` | — |
| Empty cell | `#42353C` | — |
| 2 | `#E3D6BF` | `#231C20` |
| 4 | `#AABAAE` | `#1D2420` |
| 8 | `#9F9679` | `#FFF9F2` |
| 16 | `#848C7A` | `#FFF9F2` |
| 32 | `#C28A8E` | `#231C20` |
| 64 | `#B5728A` | `#FFF9F2` |
| 128 | `#A45372` | `#FFF9F2` (+ soft glow) |
| 256 | `#933B5B` | `#FFF9F2` (+ glow) |
| 512 | `#AC3662` | `#FFF9F2` (+ glow) |
| 1024 | `#C44569` | `#FFF9F2` (+ stronger glow) |
| 2048 | `#E3D6BF` | `#933B5B` (+ pulsing glow) |
| > 2048 | `#FFF9F2` | `#933B5B` (+ pulsing glow) |

- Colors live in a ScriptableObject theme (see the TDD), so artists can tweak them without touching code.
- Font: LiberationSans SDF (already in the project), bold. The number shrinks automatically for 4- and 5-digit values.
- **Accessibility:** besides color, higher tiles get a brighter outline and glow, so tiles can be told apart even with color-vision deficiency.

### 5.3 Animation timing

| Event | Animation | Duration |
| --- | --- | --- |
| Slide | Tile moves from its old cell to its new one (ease-out) | 0.10 s |
| Merge | New tile scales 1.0 → 1.2 → 1.0 | 0.12 s, after the slide |
| Spawn | Tile scales 0 → 1 | 0.12 s, after the slide |
| Invalid move | Board shifts 6 px toward the direction and back | 0.08 s |
| Score | The number just updates (no "+N" popup) | instant |
| Splash | Holds, then fades out | 1.2 s + 0.4 s |
| 2048 reached | Tile pulses; win overlay fades in | 0.4 s |

A whole move must finish in **≤ 0.25 s**.

---

## 6. Audio

| Event | Sound |
| --- | --- |
| Slide (valid move) | Soft short "whoosh" |
| Merge | "Pop"; pitch rises slightly with the tile value |
| Invalid move | Muted "thud" |
| Win | Short bright jingle |
| Game over | Short falling tone |
| Buttons | Light click |

- A sound on/off toggle in the pause menu, saved between sessions.
- All clips are original or under a free license (CC0); their sources are listed in the game's `Audio/CREDITS.md`.

---

## 7. Saved data

| Data | When it is saved |
| --- | --- |
| Best score | When the current score exceeds it |
| Game in progress (board, score, moves, "won" flag) | After every valid move, and on "To menu" |
| Settings (sound on/off), "hint shown" flag | When changed |

---

## 8. Scope

### 8.1 Must have (MVP)
- 4×4 board, all rules in section 2.
- Keyboard + swipe/drag input with buffering.
- HUD with score, best, pause and new game.
- Win overlay with "Keep going", game over overlay.
- Slide, merge and spawn animations.
- Best score and resume of a game in progress.
- Correct exit to the shell with the score.

### 8.2 Should have
- Sounds and the sound toggle.
- Invalid-move bump, glow on high tiles.
- **Undo** of one move (button on the HUD; one step only, and it cannot undo past a new game).

### 8.3 Could have (only if time remains)
- Board size choice: 3×3, 5×5, 6×6, each with its own best score.
- Statistics: games played, highest tile reached.

### 8.4 Out of scope
- Online leaderboards, accounts, ads, in-app purchases.
- Multiplayer.

---

## 9. Milestones

| # | Milestone | Done when |
| --- | --- | --- |
| M1 | Rules engine | Board model and move logic pass all EditMode tests; no graphics yet |
| M2 | Playable | Board drawn with uGUI, keyboard input, tiles update without animation |
| M3 | Feel | Animations, swipe input, buffering, score popup |
| M4 | Complete loop | Win/game over overlays, pause menu, save/resume, exit to shell |
| M5 | Polish | Theme, sounds, glow, undo, device testing on PC and mobile |
| M6 | Release | `check_scope.py` and `check_names.py` pass, `main` merged in, tested from the shell menu |

---

## 10. Acceptance checklist

- [ ] `2 2 2 2` moved Left gives `4 4 _ _`; `2 2 2 _` moved Left gives `4 2 _ _`.
- [ ] An invalid move spawns nothing and does not change the score.
- [ ] A 2048 tile shows the win overlay exactly once per game.
- [ ] Game over appears only when no move in any direction is possible.
- [ ] Closing via "To menu" and launching again restores the exact board and score.
- [ ] Best score survives restarting the application.
- [ ] Rapid key mashing never skips moves, desyncs the board or leaves tiles in the wrong place.
- [ ] The game runs at 60 FPS on a mid-range phone, and a move feels instant.
- [ ] Launching from the shell and returning leaves no objects behind, and `Time.timeScale` is still 1.

---
---

# Русская версия

# Colours — документ по геймдизайну (GDD, gc26_2048_nastya)

| | |
| --- | --- |
| ID микроигры | `gc26_2048_nastya` |
| Рабочее название | **Colours** (название в меню; можно поменять в `microgame.json`) |
| Жанр | Однопользовательская головоломка со сдвигом числовых плиток |
| Платформы | ПК (клавиатура, мышь) и мобильные (сенсорный экран), портретная и альбомная ориентация |
| Движок | Unity 6000.5.6f1, URP, uGUI + TextMeshPro, Input System |
| Длина сессии | От 3 до 15 минут; игрок может остановиться в любой момент и продолжить позже |
| Запуск | Внутри общего меню GC26 через `IMicrogame` (см. TDD) |

---

## 1. Концепция

На поле 4×4 лежат плитки с числами. Каждый ход сдвигает **все** плитки в одном направлении до упора.
Когда две плитки с одинаковым числом сталкиваются, они сливаются в одну с удвоенным значением, и это значение
добавляется к счёту. После каждого хода в пустой клетке появляется новая маленькая плитка.
Цель — собрать плитку **2048**. Игра заканчивается, когда поле заполнено и ни один ход ничего не может слить.

**Питч:** «Один свайп двигает всё. Думай наперёд, держи большую плитку в углу и удваивай до победы».

### Опоры дизайна
1. **Сразу понятно:** одно действие (направление), без стены обучения. Новый игрок понимает игру за 3 хода.
2. **Читается с одного взгляда:** у каждого значения свой цвет, числа крупные, поэтому состояние поля понятно без чтения.
3. **Приятный отклик:** короткие анимации сдвига, слияния и появления — каждый ход ощущается отзывчивым.
4. **Честная случайность:** плитки появляются случайно, но всегда по правилам ниже. От игрока ничего не скрыто.

---

## 2. Основные правила

### 2.1 Поле
- Сетка **4×4** клетки.
- Клетка либо пустая, либо содержит одну плитку со значением 2, 4, 8, … (степени двойки).

### 2.2 Начало игры
- В случайных пустых клетках появляются две плитки.
- Значение новой плитки: **2 с вероятностью 90%, 4 — 10%**.

### 2.3 Ход
1. Игрок выбирает направление: **вверх, вниз, влево, вправо**.
2. Каждая плитка сдвигается в этом направлении, пока не упрётся в край поля или в другую плитку.
3. **Слияние:** если плитка налетает на плитку с тем же значением, они сливаются в одну с удвоенным значением.
   - Плитка, созданная слиянием, **не может слиться ещё раз в этом же ходу**.
     Пример: ряд `2 2 2 2` влево → `4 4 _ _` (а не `8 _ _ _`).
   - Если в линию стоят три одинаковые плитки, первой сливается пара, **ближайшая к стенке, к которой движутся плитки**.
     Пример: `2 2 2 _` влево → `4 2 _ _`; вправо → `_ _ 2 4`.
4. Если **ни одна плитка не сдвинулась и ничего не слилось**, ход недействителен: новой плитки нет, счёт и счётчик ходов не меняются, поле слегка «вздрагивает».
5. Если ход действителен, в случайной пустой клетке появляется **одна** новая плитка (90% — 2, 10% — 4).

### 2.4 Счёт
- Каждое слияние добавляет к счёту **значение новой плитки** (две 8 сливаются в 16 → +16).
- **Лучший счёт** сохраняется между сессиями.
- **Итоговый счёт** передаётся в меню GC26, когда игрок выходит из игры.

### 2.5 Победа и поражение
- **Победа:** когда впервые появляется плитка 2048, показывается окно «You made 2048!» («Ты собрал 2048!») с двумя кнопками:
  **Keep going** («Играть дальше» — без ограничений; окно победы в этой партии больше не появится) и **New game** («Новая игра»).
- **Поражение (конец игры):** поле заполнено **и** никакие две соседние плитки (по горизонтали или вертикали) не равны.
  Показывается окно «No more moves» («Ходов больше нет») с итоговым и лучшим счётом и кнопками **Try again** («Ещё раз») и **To menu** («В меню»).

---

## 3. Управление

| Платформа | Ввод | Действие |
| --- | --- | --- |
| ПК | Стрелки / WASD | Ход |
| ПК | Перетаскивание мышью (нажать, потянуть, отпустить) | Ход в направлении перетаскивания |
| Мобильные | Свайп | Ход в направлении свайпа |
| Все | Esc / кнопка паузы на HUD | Открыть меню паузы |
| Все | Клавиша N / кнопка «Новая игра» | Начать новую игру (если партия идёт, спросить подтверждение) |

- Свайп засчитывается, только если он длиннее примерно **5% короткой стороны экрана**; более короткий игнорируется.
  Направление — по преобладающей оси перетаскивания.
- **Буфер ввода:** если игрок вводит направление, пока ещё идёт анимация хода, анимация мгновенно завершается и выполняется отложенный ход.
  Буферизуется только **один** ход, поэтому быстрые игроки не чувствуют задержки.

---

## 4. Поток игры и экраны

```
Меню оболочки ──Begin()──► [Заставка] ──► [Стартовое меню] ──► [Игровой экран] ◄──► [Пауза] ──«В меню»──► exit(score) ──► Меню оболочки
                                                 │
                                                 ├──► [Окно победы]       (первая 2048: Играть дальше / Новая игра)
                                                 └──► [Окно конца игры]   (Ещё раз / В меню)
```

### 4.0 Заставка
- Каждый запуск начинается с короткой заставки: название **Colours**, под ним **2048** по правому краю,
  в градиенте слева направо Amaranth → Thulian Pink → Chalk, на фоне игры.
  Она держится 1,2 с и плавно исчезает за 0,4 с, открывая стартовое меню.
- Касание, клик или любая клавиша сразу её пропускают.

### 4.0.1 Стартовое меню
- То же название с градиентом и четыре кнопки: **New Game** (новая игра), **Continue** (продолжить),
  **Tutorial** (обучение), **Settings** (настройки). Внизу — версия игры (например `v1.0.0`).
- **Continue** доступна, только если загружена сохранённая партия; возвращает к ней.
- **New Game** начинает новую партию; если есть сохранённая, сначала спрашивает «Start a new game?».
- **Tutorial**: короткая страница «How to Play» с правилами и управлением и кнопкой **Back**.
- **Settings**: **Sound Effects: On/Off** (сохраняется между запусками) и **Back**.

### 4.0.2 Окна и язык
- Все кнопки стартового меню и всех окон (победа, конец игры, пауза, обучение, настройки,
  «Start a new game?») одного размера и стоят друг под другом.
- Весь текст в игре — на английском.

### 4.1 Игровой экран (HUD)
- **Верхняя панель:** название игры (**Colours**, под ним **2048** по правому краю, тем же шрифтом и того же размера),
  блок **Score** (счёт), блок **Best** (лучший) и кнопки **Undo** (отмена хода), **New game** (новая игра) и **Pause** (пауза).
- **Undo** отменяет только последний ход (поле, счёт и число ходов; рекорд не меняется). Кнопка серая,
  когда отменять нечего: в новой игре, сразу после продолжения из сохранения и сразу после отмены.
- **Центр:** поле, квадратное, максимального размера, при котором остаётся место для HUD.
- Короткая подсказка под полем только при первом запуске: «Swipe or use the arrow keys to combine tiles» («Свайпай или жми стрелки, чтобы соединять плитки»).

### 4.2 Меню паузы
- Кнопки: **Resume** («Продолжить»), **New Game** («Новая игра»), **Sound Effects: On/Off** (звуковые эффекты), **To Menu** («В меню»).
- Пока открыта пауза, ввод на поле заблокирован. `Time.timeScale` при этом **не меняется** (см. TDD).
- **В меню:** сохраняет текущую партию и вызывает `exit(score)`.

### 4.3 Продолжение
- Если игрок вышел посреди партии, при следующем запуске игра **продолжается** (поле, счёт, флаг «уже победил»).
- Если прошлая партия закончилась поражением, при следующем запуске начинается новая.

---

## 5. Визуальный стиль

Внешний вид — собственный, сознательно непохожий на другие игры жанра.

### 5.1 Направление
**«Средиземноморский берег» (Mediterranean Coast):** тёмный сливово-каменный фон, чуть более светлое поле, младшие плитки в спокойных природных тонах (мел, морской шалфей, оливковая галька), которые переходят в яркие ягодные акценты (Thulian Pink, Amaranth) со свечением у старших плиток. Опорные цвета: Amaranth `#933B5B`, Thulian Pink `#B5728A`, Brook Green `#AABAAE`, Chalk `#E3D6BF`, Pomelo Olive `#9F9679`. Название на заставке — градиент Amaranth → Thulian Pink → Chalk.
Плоские формы, мягкие тени, без текстур; всё собрано из изображений uGUI со скруглёнными спрайтами.

### 5.2 Палитра (стартовое предложение; подбирается в редакторе)

| Элемент | Цвет | Цвет текста |
| --- | --- | --- |
| Фон | `#231C20` | — |
| Поле | `#31272D` | — |
| Пустая клетка | `#42353C` | — |
| 2 | `#E3D6BF` | `#231C20` |
| 4 | `#AABAAE` | `#1D2420` |
| 8 | `#9F9679` | `#FFF9F2` |
| 16 | `#848C7A` | `#FFF9F2` |
| 32 | `#C28A8E` | `#231C20` |
| 64 | `#B5728A` | `#FFF9F2` |
| 128 | `#A45372` | `#FFF9F2` (+ мягкое свечение) |
| 256 | `#933B5B` | `#FFF9F2` (+ свечение) |
| 512 | `#AC3662` | `#FFF9F2` (+ свечение) |
| 1024 | `#C44569` | `#FFF9F2` (+ сильное свечение) |
| 2048 | `#E3D6BF` | `#933B5B` (+ пульсирующее свечение) |
| > 2048 | `#FFF9F2` | `#933B5B` (+ пульсирующее свечение) |

- Цвета хранятся в ScriptableObject-теме (см. TDD), чтобы художники могли менять их без кода.
- Шрифт: LiberationSans SDF (уже есть в проекте), жирный. Для 4- и 5-значных чисел размер автоматически уменьшается.
- **Доступность:** кроме цвета, у старших плиток ярче обводка и свечение, поэтому их можно различить и при нарушениях цветового зрения.

### 5.3 Тайминги анимаций

| Событие | Анимация | Длительность |
| --- | --- | --- |
| Сдвиг | Плитка едет из старой клетки в новую (ease-out) | 0,10 с |
| Слияние | Новая плитка масштабируется 1,0 → 1,2 → 1,0 | 0,12 с, после сдвига |
| Появление | Плитка масштабируется 0 → 1 | 0,12 с, после сдвига |
| Недействительный ход | Поле сдвигается на 6 px в сторону хода и обратно | 0,08 с |
| Счёт | Число просто обновляется (без всплывающего «+N») | сразу |
| Заставка | Держится, затем плавно исчезает | 1,2 с + 0,4 с |
| Собрана 2048 | Плитка пульсирует; окно победы плавно появляется | 0,4 с |

Весь ход должен укладываться в **≤ 0,25 с**.

---

## 6. Звук

| Событие | Звук |
| --- | --- |
| Сдвиг (действительный ход) | Мягкий короткий «вжух» |
| Слияние | «Поп»; высота тона немного растёт со значением плитки |
| Недействительный ход | Глухой «тук» |
| Победа | Короткий яркий джингл |
| Конец игры | Короткий нисходящий звук |
| Кнопки | Лёгкий щелчок |

- Переключатель звука вкл/выкл в меню паузы, сохраняется между сессиями.
- Все звуки собственные или под свободной лицензией (CC0); источники перечислены в `Audio/CREDITS.md` игры.

---

## 7. Сохраняемые данные

| Данные | Когда сохраняются |
| --- | --- |
| Лучший счёт | Когда текущий счёт его превышает |
| Текущая партия (поле, счёт, ходы, флаг «победил») | После каждого действительного хода и при выходе «В меню» |
| Настройки (звук вкл/выкл), флаг «подсказка показана» | При изменении |

---

## 8. Объём работ

### 8.1 Обязательно (MVP)
- Поле 4×4, все правила из раздела 2.
- Клавиатура + свайп/перетаскивание с буфером ввода.
- HUD со счётом, лучшим счётом, паузой и новой игрой.
- Окно победы с «Играть дальше», окно конца игры.
- Анимации сдвига, слияния и появления.
- Лучший счёт и продолжение начатой партии.
- Корректный выход в меню оболочки со счётом.

### 8.2 Желательно
- Звуки и переключатель звука.
- «Вздрагивание» при недействительном ходе, свечение старших плиток.
- **Отмена** одного хода (кнопка на HUD; только один шаг, нельзя отменить начало новой игры).

### 8.3 Если останется время
- Выбор размера поля: 3×3, 5×5, 6×6, у каждого свой лучший счёт.
- Статистика: сыграно партий, самая большая плитка.

### 8.4 Не входит в проект
- Онлайн-таблицы рекордов, аккаунты, реклама, покупки внутри игры.
- Мультиплеер.

---

## 9. Этапы

| # | Этап | Готово, когда |
| --- | --- | --- |
| M1 | Движок правил | Модель поля и логика хода проходят все EditMode-тесты; графики пока нет |
| M2 | Играбельно | Поле нарисовано на uGUI, ввод с клавиатуры, плитки обновляются без анимации |
| M3 | Ощущения | Анимации, свайпы, буфер ввода |
| M4 | Полный цикл | Окна победы/поражения, пауза, сохранение/продолжение, выход в оболочку |
| M5 | Полировка | Тема, звуки, свечение, отмена хода, проверка на ПК и телефоне |
| M6 | Релиз | `check_scope.py` и `check_names.py` проходят, `main` влит, игра проверена из меню оболочки |

---

## 10. Чек-лист приёмки

- [ ] `2 2 2 2` влево даёт `4 4 _ _`; `2 2 2 _` влево даёт `4 2 _ _`.
- [ ] Недействительный ход не создаёт плитку и не меняет счёт.
- [ ] Плитка 2048 показывает окно победы ровно один раз за партию.
- [ ] Конец игры наступает только тогда, когда ход невозможен ни в одном направлении.
- [ ] Выход «В меню» и повторный запуск восстанавливают точно то же поле и счёт.
- [ ] Лучший счёт сохраняется после перезапуска приложения.
- [ ] Быстрое нажатие клавиш никогда не пропускает ходы, не рассинхронизирует поле и не оставляет плитки не на своих местах.
- [ ] Игра идёт со скоростью 60 FPS на телефоне среднего уровня, ход ощущается мгновенным.
- [ ] После запуска из оболочки и возврата не остаётся лишних объектов, а `Time.timeScale` равен 1.
