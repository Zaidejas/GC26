# Lines 98 Mobile — Development Plan / План разработки

Revised Nov 2, 2026 / Обновлено 2 ноября 2026

- [English version](#english-version)
- [Русская версия](#русская-версия)

---

## English version

# Lines 98: 8-Week Unity C\# Plan

**Assumptions:** one developer working about 20–25 hours a week together with Claude, Unity 6 (6000.5.6f1). Platforms: **iOS first**, **Android second**. This is a study project: the game is installed directly on our own phones and is **not published** to any store. The plan covers the MVP described in the GDD; features marked *Later* in the GDD are not scheduled.

**How we work**

- **Code first.** Everything (board, balls, UI) is created by code. The Unity editor is only used to look at the result.
- **Claude writes the code and runs the checks:** unit tests and builds from the command line, then the game in the iOS Simulator or Android Emulator, with screenshots.
- **Unity stays closed** while Claude works, because an open editor locks the project.
- **Real phones** are checked regularly, from Week 5.
- **Tasks** live on the [gc26_lines project board](https://github.com/users/Zaidejas/projects/14), numbered 01–39 in work order (later features: L01–L16).
- **Commits** go to the `gc26_lines` branch and name the task they belong to ("Task 05: Pathfinder").

### Core rules to implement

- A 9×9 board with 7 ball colors. The game starts with 3 balls in random cells.
- After every move that doesn't clear a line, 3 new balls appear. Their colors **and the cells where they will appear** are shown in advance (small preview dots on the board).
- If the player moves a ball onto a preview cell, that new ball appears in another random empty cell.
- The player taps a ball, then taps an empty cell. The ball moves only if a path exists (up, down, left, right; no diagonals). The path is found with breadth-first search (BFS).
- If no path exists, the ball shakes, a "No path" alert appears, and the ball stays selected. Tapping the selected ball again does nothing.
- Lines of 5 or more in a row, column or diagonal disappear and score points. If a move clears a line, no new balls spawn that turn.
- Balls that spawn can also complete lines, which then clear.
- Undo takes back the last move (one step only; disabled at the start of a game, right after an undo, and after game over). Reset starts a new game after a confirmation.
- The game is over when the board is full.
- The game in progress is saved automatically and restored on the next launch.

### Architecture (set up in Week 1)

Keep the game logic in plain C\# with no `MonoBehaviour`, so it can be unit-tested and stays separate from the visuals:

```
Scenes/        Main (nearly empty; a bootstrap object starts the game)
Scripts/
  Core/        Board, Cell, BallColor, Pathfinder (BFS), LineDetector,
               Spawner (seeded RNG, next colors + cells), ScoreCalculator,
               GameState
  Game/        GameBootstrap, GameController (turn state machine),
               InputHandler (touch)
  View/        BoardView, BallView (pooling), BallSpriteGenerator,
               PreviewDots, Tween (small animation helper)
  UI/          HUD (score, preview, Undo, Reset), GameOverPopup,
               ConfirmDialog, AlertToast
  Services/    SaveService (JSON)
Tests/         EditMode tests for Core
```

- **Turn flow:** `Idle → BallSelected → Moving → Resolving → Spawning → CheckGameOver → Idle`.
- **Communication:** the core logic raises C\# events (`OnBallMoved`, `OnMoveBlocked`, `OnLinesCleared`, `OnBallsSpawned`, `OnGameOver`), and the view layer listens to them.
- **Libraries:** only what comes with Unity: TextMeshPro for text and the new Input System for touch. Animations use our own small helper instead of a third-party library.

### Week-by-week plan

#### Week 1: Setup and core logic

- Tools: Xcode with the iOS Simulator, and Unity's iOS Build Support module.
- Project setup: remove the template's sample files, one `Main` scene, 2D settings, code folders, test setup. Add a `CLAUDE.md` with the project rules.
- Build `Board`, `Pathfinder` (BFS) and `LineDetector` (4 directions, 5 or more in a line).
- Write the `Spawner` with a seedable RNG. It picks the next 3 colors and their target cells in advance, and picks a new cell when the player takes a preview cell.
- Write `ScoreCalculator` and the game rules (move → clear or spawn → game over).
- Write EditMode unit tests for all of the above.
- **Done when:** a complete game can be simulated in tests with no graphics.

#### Week 2: Playable in the iOS Simulator

- A small animation helper (move, scale, shake).
- Board and ball views created from code, with simple generated circles for the balls. The board fits the screen (safe area, different phone sizes).
- Turn state machine and input lock during animations.
- Tap to select and to move, with a bouncing animation on the selected ball.
- Animate the ball moving along the BFS path, cell by cell.
- Show the score, the 3-color preview and the preview dots on the board.
- **Done when:** the game is playable from start to finish in the iOS Simulator.

#### Week 3: Game feel and full rules

- Animate spawning (balls grow in), clearing (shrink, burst, particles) and blocked moves (shake plus the "No path" alert).
- Undo (one step), implemented as snapshots of the board, score and preview.
- Reset button with a confirmation dialog.
- Edge cases: full board, fewer than 3 free cells, lines made by spawned balls.

#### Week 4: Saving, game over and Android

- Game-over pop-up: final score and a "Start new game" button.
- Save the game in progress (board, score, preview, Undo step) as JSON in `Application.persistentDataPath` after each move and when the app goes to the background, and restore it on launch. Clear the save when the game ends.
- Layout check on several simulated phone sizes, including notched phones.
- Tools: Unity's Android Build Support, Android Emulator. First Android build running in the emulator.
- **Milestone (end of month 1):** MVP feature-complete, running in the iOS Simulator and the Android Emulator.

#### Week 5: Art and real phones

- Final ball look generated in code (glossy circles with shading and a highlight, 7 fixed colors), preview dots, board and background style. One theme.
- Polish UI transitions. Add the app icon and splash screen.
- Install the game on your iPhone (Xcode with a free Apple account) and on an Android phone (APK).

#### Week 6: Buffer

- Catch up on anything that slipped.
- If time is left, pick from the GDD's Later list. The preview modes and the best score are the cheapest and most useful.

#### Week 7: Optimization and testing

- Profile on real phones: object pooling, a 60 FPS target, fewer draw calls.
- Test on different screen sizes and notched phones (simulators and real devices).
- Playtest with friends. Android: share the APK. iOS: on your own iPhone (a free Apple account can't install on other people's phones). Collect feedback and fix bugs.
- Re-check the scoring balance.

#### Week 8: Final version

- Fix the remaining bugs and freeze the code.
- Final builds installed on your iPhone and Android phone.
- Update the docs to match the finished game.
- **Milestone:** version 1.0 running on both phones.

### Milestones at a glance

| Week | Deliverable |
| :---- | :---- |
| 1 | Core logic, tested |
| 2 | Playable in the iOS Simulator |
| 4 | MVP complete on the iOS Simulator and Android Emulator |
| 5 | Running on real phones |
| 8 | Version 1.0 on both phones |

### Main risks and how to handle them

- **Scope creep:** later features only if time is left in Week 6. If you fall behind, drop them first.
- **Input bugs while animations run:** the strict state machine plus an input lock solves this.
- **Simulators aren't real phones:** speed and touch feel differ. Check on a real phone from Week 5.
- **Free Apple account limits:** the app stops working on your iPhone after 7 days and must be reinstalled from Xcode. Only your own devices can be used.
- **Unity editor left open:** it locks the project, so tests and builds from the command line fail. Close Unity before handing work to Claude.
- **Art taking too long:** the balls are generated in code, so the style stays simple. Plain glossy circles are enough.

---

## Русская версия

# Lines 98: план разработки на 8 недель, Unity C\#

**Исходные условия:** один разработчик, примерно 20–25 часов в неделю, вместе с Claude, Unity 6 (6000.5.6f1). Платформы: **сначала iOS**, **затем Android**. Это учебный проект: игра устанавливается прямо на наши телефоны и **не публикуется** в магазинах приложений. План охватывает MVP, описанный в GDD; функции, помеченные в GDD как *Later* («позже»), в план не входят.

**Как мы работаем**

- **Всё через код.** Поле, шары и интерфейс создаются кодом. Редактор Unity нужен только чтобы посмотреть результат.
- **Claude пишет код и проверяет его:** запускает юнит-тесты и сборки из командной строки, затем игру в iOS Simulator или Android Emulator и делает скриншоты.
- **Unity закрыт**, пока работает Claude: открытый редактор блокирует проект.
- **Настоящие телефоны** проверяем регулярно, начиная с 5-й недели.
- **Задачи** находятся на [доске проекта gc26_lines](https://github.com/users/Zaidejas/projects/14) и пронумерованы 01–39 в порядке работы (отложенные функции: L01–L16).
- **Коммиты** идут в ветку `gc26_lines` и называют задачу, к которой относятся («Task 05: Pathfinder»).

### Основные правила игры

- Поле 9×9, шары 7 цветов. В начале игры на поле 3 шара в случайных клетках.
- После каждого хода, который не убрал линию, появляются 3 новых шара. Их цвета **и клетки, где они появятся**, показываются заранее (маленькие точки-подсказки на поле).
- Если игрок ставит шар в клетку с подсказкой, этот новый шар появится в другой случайной пустой клетке.
- Игрок нажимает на шар, затем на пустую клетку. Шар перемещается, только если к клетке есть путь (вверх, вниз, влево, вправо, без диагоналей). Путь ищется поиском в ширину (BFS).
- Если пути нет, шар трясётся, появляется сообщение «Нет пути», и шар остаётся выбранным. Повторное нажатие на выбранный шар ничего не делает.
- Линии из 5 и более шаров по горизонтали, вертикали или диагонали исчезают и приносят очки. Если ход убрал линию, новые шары в этот ход не появляются.
- Появившиеся шары тоже могут собрать линию, и она исчезает.
- «Отмена» возвращает последний ход (только на один шаг; недоступна в начале игры, сразу после отмены и после конца игры). «Сброс» начинает новую игру после подтверждения.
- Игра заканчивается, когда поле заполнено.
- Текущая партия сохраняется автоматически и восстанавливается при следующем запуске.

### Архитектура (закладывается на 1-й неделе)

Игровую логику стоит писать на чистом C\# без `MonoBehaviour`: так её можно покрыть юнит-тестами, и она не смешивается с отображением:

```
Scenes/        Main (почти пустая; объект-загрузчик запускает игру)
Scripts/
  Core/        Board, Cell, BallColor, Pathfinder (BFS), LineDetector,
               Spawner (RNG с seed, следующие цвета + клетки),
               ScoreCalculator, GameState
  Game/        GameBootstrap, GameController (машина состояний хода),
               InputHandler (касания)
  View/        BoardView, BallView (пул объектов), BallSpriteGenerator,
               PreviewDots, Tween (небольшой помощник для анимаций)
  UI/          HUD (счёт, превью, Отмена, Сброс), GameOverPopup,
               ConfirmDialog, AlertToast
  Services/    SaveService (JSON)
Tests/         EditMode-тесты для Core
```

- **Порядок хода:** `Idle → BallSelected → Moving → Resolving → Spawning → CheckGameOver → Idle`.
- **Связь между слоями:** логика вызывает события C\# (`OnBallMoved`, `OnMoveBlocked`, `OnLinesCleared`, `OnBallsSpawned`, `OnGameOver`), а слой отображения на них подписывается.
- **Библиотеки:** только то, что входит в Unity: TextMeshPro для текста и новая Input System для касаний. Анимации делаются нашим собственным небольшим помощником, без сторонних библиотек.

### План по неделям

#### Неделя 1: настройка и игровая логика

- Инструменты: Xcode с iOS Simulator и модуль iOS Build Support для Unity.
- Настройка проекта: убрать примеры из шаблона, одна сцена `Main`, настройки 2D, папки для кода, настройка тестов. Добавить `CLAUDE.md` с правилами проекта.
- Написать `Board`, `Pathfinder` (BFS) и `LineDetector` (4 направления, линии из 5 и более).
- Написать `Spawner` с RNG, которому можно задать seed. Он заранее выбирает 3 следующих цвета и их клетки, а если игрок занял клетку с подсказкой, выбирает другую.
- Написать `ScoreCalculator` и правила игры (ход → удаление линии или появление шаров → конец игры).
- Написать EditMode-тесты для всего перечисленного.
- **Итог:** полную партию можно прогнать в тестах без графики.

#### Неделя 2: игра работает в iOS Simulator

- Небольшой помощник для анимаций (перемещение, масштаб, тряска).
- Поле и шары создаются кодом, шары — простые сгенерированные круги. Поле подгоняется под экран (безопасная зона, разные размеры телефонов).
- Машина состояний хода и блокировка ввода во время анимаций.
- Выбор шара и перемещение касанием, выбранный шар подпрыгивает.
- Анимировать движение шара по найденному пути, клетка за клеткой.
- Показать счёт, превью 3 цветов и точки-подсказки на поле.
- **Итог:** игру можно пройти от начала до конца в iOS Simulator.

#### Неделя 3: отзывчивость и все правила

- Анимации появления шаров (вырастают), удаления (сжимаются, взрыв, частицы) и заблокированного хода (тряска и сообщение «Нет пути»).
- Отмена хода (на один шаг) через снимки поля, счёта и превью.
- Кнопка «Сброс» с окном подтверждения.
- Пограничные случаи: полное поле, меньше 3 свободных клеток, линии из появившихся шаров.

#### Неделя 4: сохранение, конец игры и Android

- Окно конца игры: итоговый счёт и кнопка «Начать новую игру».
- Сохранять текущую партию (поле, счёт, превью, шаг для отмены) в JSON в `Application.persistentDataPath` после каждого хода и при сворачивании приложения, и восстанавливать её при запуске. Удалять сохранение, когда игра закончена.
- Проверить раскладку на нескольких размерах телефонов в симуляторе, включая телефоны с вырезом.
- Инструменты: модуль Android Build Support для Unity, Android Emulator. Первая сборка под Android запускается в эмуляторе.
- **Контрольная точка (конец 1-го месяца):** MVP готов полностью и работает в iOS Simulator и Android Emulator.

#### Неделя 5: графика и настоящие телефоны

- Финальный вид шаров, сгенерированный кодом (глянцевые круги с тенью и бликом, 7 постоянных цветов), точки-подсказки, стиль поля и фона. Одна тема.
- Доработать переходы в интерфейсе. Сделать иконку приложения и заставку.
- Установить игру на ваш iPhone (Xcode с бесплатным аккаунтом Apple) и на телефон с Android (APK).

#### Неделя 6: резерв

- Наверстать то, что не успели.
- Если останется время, взять что-то из списка Later в GDD. Режимы подсказок и рекорд — самые дешёвые и полезные.

#### Неделя 7: оптимизация и тестирование

- Профилирование на настоящих телефонах: пул объектов, цель 60 FPS, меньше вызовов отрисовки.
- Проверить на разных размерах экрана и телефонах с вырезом (симуляторы и настоящие устройства).
- Дать поиграть друзьям. Android: отправить APK. iOS: на вашем iPhone (с бесплатным аккаунтом Apple нельзя установить игру на чужие телефоны). Собрать отзывы и исправить ошибки.
- Ещё раз проверить баланс очков.

#### Неделя 8: финальная версия

- Исправить оставшиеся ошибки и заморозить код.
- Установить финальные сборки на ваш iPhone и телефон с Android.
- Обновить документацию под готовую игру.
- **Контрольная точка:** версия 1.0 работает на обоих телефонах.

### Контрольные точки

| Неделя | Результат |
| :---- | :---- |
| 1 | Игровая логика, покрытая тестами |
| 2 | Игра работает в iOS Simulator |
| 4 | MVP готов в iOS Simulator и Android Emulator |
| 5 | Игра работает на настоящих телефонах |
| 8 | Версия 1.0 на обоих телефонах |

### Основные риски и что с ними делать

- **Разрастание объёма работ:** отложенные функции — только если на 6-й неделе останется время. Если вы отстаёте от графика, их убирают первыми.
- **Ошибки ввода во время анимаций:** решаются строгой машиной состояний и блокировкой ввода.
- **Симулятор — не настоящий телефон:** скорость и ощущение касаний отличаются. Проверяйте на настоящем телефоне начиная с 5-й недели.
- **Ограничения бесплатного аккаунта Apple:** через 7 дней игра на iPhone перестаёт запускаться, и её нужно заново установить из Xcode. Устанавливать можно только на свои устройства.
- **Открытый редактор Unity:** он блокирует проект, и тесты и сборки из командной строки не запускаются. Закрывайте Unity, прежде чем передавать работу Claude.
- **Графика отнимает слишком много времени:** шары генерируются кодом, поэтому стиль остаётся простым. Простых глянцевых кругов достаточно.
