# Lines 98 Mobile — Development Plan / План разработки

Revised Nov 2, 2026 / Обновлено 2 ноября 2026

- [English version](#english-version)
- [Русская версия](#русская-версия)

---

## English version

# Lines 98: 8-Week Unity C\# Plan

**Assumptions:** one developer working about 20–25 hours a week, Unity 6 LTS. Platforms in order: **browser first** (Unity Web build), **iOS second**, **Android third**. The plan covers the MVP described in the GDD; features marked *Later* in the GDD are not scheduled. A team or a full-time effort could shorten the timeline or pull in some later features.

### Core rules to implement

- A 9×9 board with 7 ball colors. The game starts with 3 balls in random cells.
- After every move that doesn't clear a line, 3 new balls appear. Their colors **and the cells where they will appear** are shown in advance (small preview dots on the board).
- If the player moves a ball onto a preview cell, that new ball appears in another random empty cell.
- The player taps a ball, then taps an empty cell. The ball moves only if a path exists (up, down, left, right; no diagonals). The path is found with breadth-first search (BFS).
- If no path exists, the ball shakes, a "No path" alert appears, and the ball stays selected. Tapping the selected ball again does nothing.
- Lines of 5 or more in a row, column or diagonal disappear and score points. If a move clears a line, no new balls spawn that turn.
- Balls that spawn can also complete lines, which then clear.
- Undo takes back the last move (one step). Reset starts a new game after a confirmation.
- The game is over when the board is full.
- The game in progress is saved automatically and restored on the next launch.

### Architecture (set up in Week 1)

Keep the game logic in plain C\# with no `MonoBehaviour`, so it can be unit-tested and stays separate from the visuals:

```
Scripts/
  Core/        Board, Cell, BallColor, Pathfinder (BFS), LineDetector,
               Spawner (seeded RNG, next colors + cells), ScoreCalculator,
               GameState
  Game/        GameController (turn state machine), InputHandler (pointer)
  View/        BoardView, BallView (pooling), PreviewDots, Animations
  UI/          HUD (score, preview, Undo, Reset), GameOverPopup,
               ConfirmDialog, AlertToast
  Services/    SaveService (JSON)
Tests/         EditMode tests for Core
```

Later (not MVP): AudioService, SettingsService, AdsService/AnalyticsService in `Services/`.

- **Turn flow:** `Idle → BallSelected → Moving → Resolving → Spawning → CheckGameOver → Idle`.
- **Communication:** the core logic raises C\# events (`OnBallMoved`, `OnMoveBlocked`, `OnLinesCleared`, `OnBallsSpawned`, `OnGameOver`), and the view layer listens to them.
- **Libraries:** DOTween for animations, TextMeshPro for text, and the new Input System for pointer input (works for both touch and mouse).

### Week-by-week plan

#### Week 1: Setup and core logic

- Create the Unity project, Git repository (with Git LFS and a Unity `.gitignore`) and folder structure.
- Build `Board`, `Pathfinder` (BFS) and `LineDetector` (4 directions, 5 or more in a line).
- Write the `Spawner` with a seedable RNG. It picks the next 3 colors and their target cells in advance, and picks a new cell when the player takes a preview cell.
- Write EditMode unit tests for pathfinding, line detection (including crossing lines), preview cells being taken, and game over.
- **Done when:** a complete game can be simulated in tests with no graphics.

#### Week 2: Playable prototype in the browser

- Draw the board grid with placeholder sprites, and make it scale to fit the screen (safe area, phone and desktop aspect ratios).
- Add tap/click to select and to move, with a bouncing animation on the selected ball.
- Animate the ball moving along the BFS path, cell by cell.
- Connect the full turn loop: move, clear lines, spawn, check for game over.
- Show the score, the 3-color preview and the preview dots on the board.
- **Done when:** the game is playable from start to finish in a browser on desktop and on a phone (first Web build this week).

#### Week 3: Game feel and full rules

- Animate spawning (balls grow in), clearing (shrink, burst, particles) and blocked moves (shake plus the "No path" alert).
- Scoring: a bonus for lines longer than 5 and for clearing several lines in one move.
- Undo (one step), implemented as snapshots of the board, score and preview.
- Reset button with a confirmation dialog.
- Block input while animations are playing, and handle edge cases.

#### Week 4: Game over and MVP release on the web

- Game-over pop-up: final score and a "Start new game" button.
- Save the game in progress (board, score, preview, Undo step) as JSON in `Application.persistentDataPath` after each move and on pause/quit, and restore it on launch. Clear the save when the game ends. Check that it works in the browser (IndexedDB) after reloading the page.
- Fix bugs found while playing, and tidy the layout on different screen sizes.
- Publish the Web build on a host such as itch.io or GitHub Pages.
- **Milestone (end of month 1):** MVP playable in the browser.

#### Week 5: Art and first iOS build

- Final ball sprites (glossy, with clearly distinct colors), the preview dots, and the board and background style. One theme. Pick one approach: original art or a free asset pack with a suitable license.
- Polish UI transitions. Add the app icon and splash screen.
- First iOS build through Xcode, running on a real iPhone.

#### Week 6: Preview modes

- Add the two other preview modes from the classic game:
  - **Colors only:** the next 3 colors are shown, but not the cells where they will appear.
  - **No preview:** nothing is shown about the next balls.
- Add a simple way to choose the mode when starting a new game.
- If time is left, pick from the GDD's Later list. The best score is the cheapest and most useful.
- **Milestone:** content complete (beta).

#### Week 7: Optimization and testing

- Profile the Web build on a low-end phone browser and the iOS build on an older iPhone: object pooling, sprite atlases, a 60 FPS target, fewer draw calls, and a small Web build (code stripping, compression).
- Test on several screen sizes, notched phones, and desktop browsers (Chrome, Safari, Firefox).
- Share the Web build with 5–10 testers and start TestFlight for iOS. Collect feedback and fix bugs.
- Re-check the scoring balance.
- Create the Google Play Console account and make the first Android build (see the risks below).

#### Week 8: Release

- Fix the remaining bugs and freeze the code.
- Update the Web build to version 1.0.
- App Store page: screenshots, description, privacy policy and the age rating questionnaire. Build the iOS archive and submit.
- Use the buffer time: App Store review can take a few days.
- **Milestone:** Web 1.0 live, iOS 1.0 submitted. Android follows after the closed test.

### Milestones at a glance

| Week | Deliverable |
| :---- | :---- |
| 1 | Core logic, tested |
| 2 | Playable in the browser |
| 4 | MVP released on the web |
| 6 | Beta: all three preview modes |
| 8 | Web 1.0 live, iOS submitted |
| After 8 | Android release |

### Main risks and how to handle them

- **Scope creep:** later features only if time is left in Week 6. If you fall behind, drop them first.
- **Input bugs while animations run:** the strict state machine plus an input lock solves this.
- **Web build differences:** browsers on phones can be slower and handle touch differently. Test the Web build on a real phone from Week 2.
- **Android release delay:** new personal Play Console accounts need a 14-day closed test with 12 testers before a production release. Start that test as soon as the Android build works.
- **Art taking too long:** use placeholders until Week 5 and keep the style simple. Plain glossy circles are enough.

---

## Русская версия

# Lines 98: план разработки на 8 недель, Unity C\#

**Исходные условия:** один разработчик, примерно 20–25 часов в неделю, Unity 6 LTS. Порядок платформ: **сначала браузер** (сборка Unity Web), **затем iOS**, **затем Android**. План охватывает MVP, описанный в GDD; функции, помеченные в GDD как *Later* («позже»), в план не входят. Если работает команда или разработка идёт на полную ставку, сроки можно сократить или добавить часть отложенных функций.

### Основные правила игры

- Поле 9×9, шары 7 цветов. В начале игры на поле 3 шара в случайных клетках.
- После каждого хода, который не убрал линию, появляются 3 новых шара. Их цвета **и клетки, где они появятся**, показываются заранее (маленькие точки-подсказки на поле).
- Если игрок ставит шар в клетку с подсказкой, этот новый шар появится в другой случайной пустой клетке.
- Игрок нажимает на шар, затем на пустую клетку. Шар перемещается, только если к клетке есть путь (вверх, вниз, влево, вправо, без диагоналей). Путь ищется поиском в ширину (BFS).
- Если пути нет, шар трясётся, появляется сообщение «Нет пути», и шар остаётся выбранным. Повторное нажатие на выбранный шар ничего не делает.
- Линии из 5 и более шаров по горизонтали, вертикали или диагонали исчезают и приносят очки. Если ход убрал линию, новые шары в этот ход не появляются.
- Появившиеся шары тоже могут собрать линию, и она исчезает.
- «Отмена» возвращает последний ход (на один шаг). «Сброс» начинает новую игру после подтверждения.
- Игра заканчивается, когда поле заполнено.
- Текущая партия сохраняется автоматически и восстанавливается при следующем запуске.

### Архитектура (закладывается на 1-й неделе)

Игровую логику стоит писать на чистом C\# без `MonoBehaviour`: так её можно покрыть юнит-тестами, и она не смешивается с отображением:

```
Scripts/
  Core/        Board, Cell, BallColor, Pathfinder (BFS), LineDetector,
               Spawner (RNG с seed, следующие цвета + клетки),
               ScoreCalculator, GameState
  Game/        GameController (машина состояний хода), InputHandler (указатель)
  View/        BoardView, BallView (пул объектов), PreviewDots, Animations
  UI/          HUD (счёт, превью, Отмена, Сброс), GameOverPopup,
               ConfirmDialog, AlertToast
  Services/    SaveService (JSON)
Tests/         EditMode-тесты для Core
```

Позже (не в MVP): AudioService, SettingsService, AdsService/AnalyticsService в `Services/`.

- **Порядок хода:** `Idle → BallSelected → Moving → Resolving → Spawning → CheckGameOver → Idle`.
- **Связь между слоями:** логика вызывает события C\# (`OnBallMoved`, `OnMoveBlocked`, `OnLinesCleared`, `OnBallsSpawned`, `OnGameOver`), а слой отображения на них подписывается.
- **Библиотеки:** DOTween для анимаций, TextMeshPro для текста, новая Input System для ввода указателем (работает и с касаниями, и с мышью).

### План по неделям

#### Неделя 1: настройка проекта и игровая логика

- Создать проект Unity, Git-репозиторий (с Git LFS и `.gitignore` для Unity) и структуру папок.
- Написать `Board`, `Pathfinder` (BFS) и `LineDetector` (4 направления, линии из 5 и более).
- Написать `Spawner` с RNG, которому можно задать seed. Он заранее выбирает 3 следующих цвета и их клетки, а если игрок занял клетку с подсказкой, выбирает другую.
- Написать EditMode-тесты на поиск пути, поиск линий (включая пересекающиеся), занятие клетки с подсказкой и конец игры.
- **Итог:** полную партию можно прогнать в тестах без графики.

#### Неделя 2: играбельный прототип в браузере

- Нарисовать сетку поля на временных спрайтах и подогнать её под экран (безопасная зона, пропорции телефона и компьютера).
- Добавить выбор шара и перемещение касанием или щелчком, выбранный шар подпрыгивает.
- Анимировать движение шара по найденному пути, клетка за клеткой.
- Собрать полный цикл хода: перемещение, удаление линий, появление шаров, проверка конца игры.
- Показать счёт, превью 3 цветов и точки-подсказки на поле.
- **Итог:** игру можно пройти от начала до конца в браузере на компьютере и на телефоне (первая Web-сборка на этой неделе).

#### Неделя 3: отзывчивость и все правила

- Анимации появления шаров (вырастают), удаления (сжимаются, взрыв, частицы) и заблокированного хода (тряска и сообщение «Нет пути»).
- Очки: бонус за линии длиннее 5 и за несколько линий одним ходом.
- Отмена хода (на один шаг) через снимки поля, счёта и превью.
- Кнопка «Сброс» с окном подтверждения.
- Блокировать ввод во время анимаций и обработать пограничные случаи.

#### Неделя 4: конец игры и выпуск MVP в вебе

- Окно конца игры: итоговый счёт и кнопка «Начать новую игру».
- Сохранять текущую партию (поле, счёт, превью, шаг для отмены) в JSON в `Application.persistentDataPath` после каждого хода и при сворачивании или закрытии, и восстанавливать её при запуске. Удалять сохранение, когда игра закончена. Проверить, что это работает в браузере (IndexedDB) после перезагрузки страницы.
- Исправить ошибки, найденные в игре, и привести в порядок раскладку на разных экранах.
- Опубликовать Web-сборку на хостинге, например itch.io или GitHub Pages.
- **Контрольная точка (конец 1-го месяца):** MVP можно играть в браузере.

#### Неделя 5: графика и первая сборка под iOS

- Финальные спрайты шаров (глянцевые, с хорошо различимыми цветами), точки-подсказки, стиль поля и фона. Одна тема. Выбрать один вариант: своя графика или бесплатный пакет ассетов с подходящей лицензией.
- Доработать переходы в интерфейсе. Сделать иконку приложения и заставку.
- Первая сборка под iOS через Xcode, запуск на настоящем iPhone.

#### Неделя 6: режимы подсказок

- Добавить два других режима подсказок из классической игры:
  - **Только цвета:** показываются цвета 3 следующих шаров, но не клетки, где они появятся.
  - **Без подсказок:** о следующих шарах ничего не показывается.
- Добавить простой выбор режима при начале новой игры.
- Если останется время, взять что-то из списка Later в GDD. Рекорд — самое дешёвое и полезное.
- **Контрольная точка:** весь контент готов (бета).

#### Неделя 7: оптимизация и тестирование

- Профилирование Web-сборки в браузере слабого телефона и iOS-сборки на старом iPhone: пул объектов, атласы спрайтов, цель 60 FPS, меньше вызовов отрисовки, небольшой размер Web-сборки (удаление неиспользуемого кода, сжатие).
- Проверить на разных размерах экрана, телефонах с вырезом и в браузерах на компьютере (Chrome, Safari, Firefox).
- Дать Web-сборку 5–10 тестировщикам и запустить TestFlight для iOS. Собрать отзывы и исправить ошибки.
- Ещё раз проверить баланс очков.
- Зарегистрировать аккаунт в Google Play Console и сделать первую сборку под Android (см. риски ниже).

#### Неделя 8: релиз

- Исправить оставшиеся ошибки и заморозить код.
- Обновить Web-сборку до версии 1.0.
- Страница в App Store: скриншоты, описание, политика конфиденциальности и анкета возрастного рейтинга. Собрать архив для iOS и отправить на проверку.
- Оставить запас времени: проверка в App Store может занять несколько дней.
- **Контрольная точка:** Web 1.0 опубликована, iOS 1.0 отправлена на проверку. Android выходит после закрытого тестирования.

### Контрольные точки

| Неделя | Результат |
| :---- | :---- |
| 1 | Игровая логика, покрытая тестами |
| 2 | Игра работает в браузере |
| 4 | MVP опубликован в вебе |
| 6 | Бета: все три режима подсказок |
| 8 | Web 1.0 опубликована, iOS отправлена на проверку |
| После 8-й | Релиз под Android |

### Основные риски и что с ними делать

- **Разрастание объёма работ:** отложенные функции — только если на 6-й неделе останется время. Если вы отстаёте от графика, их убирают первыми.
- **Ошибки ввода во время анимаций:** решаются строгой машиной состояний и блокировкой ввода.
- **Особенности Web-сборки:** браузеры на телефонах могут работать медленнее и по-другому обрабатывать касания. Проверяйте Web-сборку на настоящем телефоне начиная со 2-й недели.
- **Задержка релиза под Android:** новые личные аккаунты в Play Console должны пройти 14-дневное закрытое тестирование с 12 тестировщиками до выпуска в продакшен. Начните его, как только заработает сборка под Android.
- **Графика отнимает слишком много времени:** используйте временные спрайты до 5-й недели и держите стиль простым. Простых глянцевых кругов достаточно.
