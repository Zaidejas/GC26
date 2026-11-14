# Lines — Development Plan / План разработки

Revised Nov 2, 2026 / Обновлено 2 ноября 2026

- [English version](#english-version)
- [Русская версия](#русская-версия)

---

## English version

# Lines: 8-Week Unity C\# Plan

**Assumptions:** one developer working about 20–25 hours a week together with Claude, Unity 6 (6000.5.6f1). Platforms: **iOS first**, **Android second**. This is a study project: the game is installed directly on our own phones and is **not published** to any store. The plan covers the MVP described in the GDD; features marked *Later* in the GDD are not scheduled.

**How we work**

- **Code first.** Everything (board, balls, UI) is created by code. The Unity editor is only used to look at the result.
- **Two parts.** Part 1 (Weeks 2–4): every feature of the prototype, built and checked in Unity's Play mode. Part 2 (from Week 5 at the latest): iOS Simulator, Android Emulator and real phones.
- **Claude writes the code and runs the checks:** unit and play-mode tests and layout screenshots (16:9, 20:9, real iPhone sizes) from the command line; in Part 2 also builds that run in the iOS Simulator or Android Emulator, with screenshots.
- **Unity stays closed** while Claude works, because an open editor locks the project.
- **Real phones** are checked regularly, from Week 6.
- **Tasks** live on the [gc26_lines project board](https://github.com/users/Zaidejas/projects/14), numbered 01–41 in work order (later features: L01–L14; the playtest with friends moved from 37 to L14, after all development).
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
  UI/          ScreenManager, MainMenu, HowToPlay, LeaderboardView,
               HUD (Menu, score, preview, Undo, Reset), GameOverPopup,
               ConfirmDialog, AlertToast
  Services/    SaveService (JSON: game in progress, leaderboard)
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

#### Week 2: Playable in the Unity editor

- A small animation helper (move, scale, shake).
- Board and pieces created from code (piece images, with generated glossy balls as a fallback). The board fits the screen (safe area, 16:9 and 20:9).
- Turn state machine and input lock during animations.
- Tap to select and to move, with a bouncing animation on the selected piece.
- Animate the piece moving along the BFS path, cell by cell.
- Show the score, the next 3 pieces and the preview dots on the board.
- **Done when:** the game is playable in Unity's Play mode.

#### Week 3: Game feel and full rules (Part 1)

- Animate spawning (pieces grow in) and clearing (shrink, burst, particles).
- Blocked move: shake plus the "No path" alert.
- Undo (one step), implemented as snapshots of the board, score and preview.
- Reset button with a confirmation dialog.
- Edge cases: full board, fewer than 3 free cells, lines made by spawned pieces.

#### Week 4: Saving, screens and polish (Part 1)

- Save the game in progress (board, score, preview, Undo step) as JSON in the game's own save folder (`MicrogameSave.Folder("gc26_lines")`) after each move and when the app goes to the background, and restore it with Continue.
- Main menu (Continue, New game, How to play, Leaderboard) as the start screen, and a back button (arrow) on the game, How to play and Leaderboard screens.
- How to play: 3 cards with small board pictures.
- Game over and leaderboard: the game-over pop-up only asks for a name (pre-filled with the last typed name or PlayerN) and Save opens the Leaderboard: local top 10 finished games with name, score and date, a result line and Play again. Older scores win ties; a score left unsaved when the app closes is added at the next launch. Ranking rules in Core, with unit tests.
- UI transitions and the prototype's fonts.
- **Milestone (end of month 1):** the whole game plays in Unity like the prototype (MVP features complete).

#### Week 5: Builds and simulators (Part 2)

- Build scripts for the iOS Simulator, your iPhone and an Android APK.
- First full iOS Simulator build; layout check on the two target shapes: iPhone SE (3rd generation) for 16:9 and a modern iPhone (19.5:9, close to 20:9), including notched phones.
- Tools: Unity's Android Build Support, Android Emulator with two custom phones: 1080×1920 (16:9) and 1080×2400 (20:9). First Android build running on both.
- Part 2 starts here at the latest, even if a small Part 1 item is still open.
- **Milestone:** the game runs in the iOS Simulator and the Android Emulator.

#### Week 6: Art and real phones (Part 2)

- Magic-forest look (GDD → Art): export the 7 glowing mushrooms (transparent background, 256 px or more) and the two forest backgrounds (start screen with tall mushrooms, other screens without) from the browser prototype as PNGs; switch the UI to the dark theme (glass board, glowing buttons, light text) and the Mystery Quest font. One theme.
- App icon and splash screen.
- Install the game on your iPhone (Xcode with a free Apple account) and on an Android phone (APK).

#### Week 7: Optimization and testing

- Profile on real phones: object pooling, a 60 FPS target, fewer draw calls.
- Final check of both target screen shapes, 16:9 and 20:9, plus notched phones (simulators, emulators and real devices).
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
| 2 | Playable in the Unity editor |
| 4 | Whole game in Unity, like the prototype (MVP features complete) |
| 5 | Running in the iOS Simulator and Android Emulator |
| 6 | Running on real phones |
| 8 | Version 1.0 on both phones |

### Main risks and how to handle them

- **Scope creep:** there is no buffer week, so later features wait until after Week 8. If Part 1 runs late, Part 2 still starts at the beginning of Week 5; How to play is the first piece to move back to Later.
- **Phone-only problems found late** (touch, text, code stripped from phone builds, speed): layout is checked by screenshots from Week 2; the rest is checked in Part 2.
- **Input bugs while animations run:** the strict state machine plus an input lock solves this.
- **Simulators aren't real phones:** speed and touch feel differ. Check on a real phone from Week 6.
- **Free Apple account limits:** the app stops working on your iPhone after 7 days and must be reinstalled from Xcode. Only your own devices can be used.
- **Unity editor left open:** it locks the project, so tests and builds from the command line fail. Close Unity before handing work to Claude.
- **Art taking too long:** the mushrooms, backgrounds and fonts are already chosen and drawn in the browser prototype; only the PNG export and the dark UI styling in Unity are left.

---

## Русская версия

# Lines: план разработки на 8 недель, Unity C\#

**Исходные условия:** один разработчик, примерно 20–25 часов в неделю, вместе с Claude, Unity 6 (6000.5.6f1). Платформы: **сначала iOS**, **затем Android**. Это учебный проект: игра устанавливается прямо на наши телефоны и **не публикуется** в магазинах приложений. План охватывает MVP, описанный в GDD; функции, помеченные в GDD как *Later* («позже»), в план не входят.

**Как мы работаем**

- **Всё через код.** Поле, шары и интерфейс создаются кодом. Редактор Unity нужен только чтобы посмотреть результат.
- **Две части.** Часть 1 (недели 2–4): все функции прототипа, сделанные и проверенные в режиме Play в Unity. Часть 2 (не позже 5-й недели): iOS Simulator, Android Emulator и настоящие телефоны.
- **Claude пишет код и проверяет его:** запускает юнит-тесты, play-mode-тесты и скриншоты раскладки (16:9, 20:9, размеры настоящих iPhone) из командной строки; в части 2 также сборки, которые запускаются в iOS Simulator или Android Emulator, со скриншотами.
- **Unity закрыт**, пока работает Claude: открытый редактор блокирует проект.
- **Настоящие телефоны** проверяем регулярно, начиная с 6-й недели.
- **Задачи** находятся на [доске проекта gc26_lines](https://github.com/users/Zaidejas/projects/14) и пронумерованы 01–41 в порядке работы (отложенные функции: L01–L14; тест с друзьями перенесён из 37 в L14, после всей разработки).
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
  UI/          ScreenManager, MainMenu, HowToPlay, LeaderboardView,
               HUD (Меню, счёт, превью, Отмена, Сброс), GameOverPopup,
               ConfirmDialog, AlertToast
  Services/    SaveService (JSON: текущая партия, таблица рекордов)
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

#### Неделя 2: игра работает в редакторе Unity

- Небольшой помощник для анимаций (перемещение, масштаб, тряска).
- Поле и фишки создаются кодом (картинки фишек, а сгенерированные глянцевые шары — запасной вариант). Поле подгоняется под экран (безопасная зона, 16:9 и 20:9).
- Машина состояний хода и блокировка ввода во время анимаций.
- Выбор фишки и перемещение касанием, выбранная фишка подпрыгивает.
- Анимировать движение фишки по найденному пути, клетка за клеткой.
- Показать счёт, 3 следующие фишки и точки-подсказки на поле.
- **Итог:** в игру можно играть в режиме Play в Unity.

#### Неделя 3: отзывчивость и все правила (часть 1)

- Анимации появления фишек (вырастают) и удаления (сжимаются, взрыв, частицы).
- Заблокированный ход: тряска и сообщение «Нет пути».
- Отмена хода (на один шаг) через снимки поля, счёта и превью.
- Кнопка «Сброс» с окном подтверждения.
- Пограничные случаи: полное поле, меньше 3 свободных клеток, линии из появившихся фишек.

#### Неделя 4: сохранение, экраны и доработка (часть 1)

- Сохранять текущую партию (поле, счёт, превью, шаг для отмены) в JSON в собственной папке сохранений игры (`MicrogameSave.Folder("gc26_lines")`) после каждого хода и при сворачивании приложения, и продолжать её кнопкой «Продолжить».
- Главное меню (Продолжить, Новая игра, Как играть, Таблица рекордов) как стартовый экран и кнопка «назад» (стрелка) на экранах игры, «Как играть» и таблицы рекордов.
- «Как играть»: 3 карточки с маленькими картинками поля.
- Конец игры и таблица рекордов: окно конца игры только спрашивает имя (заполнено последним введённым именем или PlayerN), «Сохранить» открывает таблицу: 10 лучших законченных игр с именем, счётом и датой, строка с результатом и кнопка «Играть снова». При равенстве остаётся более старый результат; несохранённый при закрытии приложения счёт добавляется при следующем запуске. Правила рейтинга в Core, с юнит-тестами.
- Переходы в интерфейсе и шрифты прототипа.
- **Контрольная точка (конец 1-го месяца):** вся игра работает в Unity как прототип (все функции MVP готовы).

#### Неделя 5: сборки и симуляторы (часть 2)

- Скрипты сборки для iOS Simulator, вашего iPhone и Android (APK).
- Первая полная сборка в iOS Simulator; проверка раскладки на двух целевых форматах: iPhone SE (3-го поколения) для 16:9 и современный iPhone (19,5:9, близко к 20:9), включая телефоны с вырезом.
- Инструменты: модуль Android Build Support для Unity, Android Emulator с двумя своими телефонами: 1080×1920 (16:9) и 1080×2400 (20:9). Первая сборка под Android запускается на обоих.
- Часть 2 начинается здесь не позже, даже если небольшой пункт части 1 ещё не закрыт.
- **Контрольная точка:** игра работает в iOS Simulator и Android Emulator.

#### Неделя 6: графика и настоящие телефоны (часть 2)

- Оформление «волшебный лес» (GDD → Art): экспортировать из браузерного прототипа в PNG 7 светящихся грибов (прозрачный фон, 256 px или больше) и два фона леса (стартовый экран с высокими грибами, остальные экраны без них); перевести интерфейс на тёмную тему (стеклянное поле, светящиеся кнопки, светлый текст) и шрифт Mystery Quest. Одна тема.
- Иконка приложения и заставка.
- Установить игру на ваш iPhone (Xcode с бесплатным аккаунтом Apple) и на телефон с Android (APK).

#### Неделя 7: оптимизация и тестирование

- Профилирование на настоящих телефонах: пул объектов, цель 60 FPS, меньше вызовов отрисовки.
- Финальная проверка обоих целевых форматов экрана, 16:9 и 20:9, и телефонов с вырезом (симуляторы, эмуляторы и настоящие устройства).
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
| 2 | Игра работает в редакторе Unity |
| 4 | Вся игра в Unity, как прототип (все функции MVP готовы) |
| 5 | Игра работает в iOS Simulator и Android Emulator |
| 6 | Игра работает на настоящих телефонах |
| 8 | Версия 1.0 на обоих телефонах |

### Основные риски и что с ними делать

- **Разрастание объёма работ:** резервной недели нет, поэтому отложенные функции ждут окончания 8-й недели. Если часть 1 затягивается, часть 2 всё равно начинается в начале 5-й недели; первым в Later уходит «Как играть».
- **Проблемы, которые видны только на телефоне** (касания, текст, код, вырезанный из сборки, скорость): раскладку проверяют скриншоты со 2-й недели; остальное — в части 2.
- **Ошибки ввода во время анимаций:** решаются строгой машиной состояний и блокировкой ввода.
- **Симулятор — не настоящий телефон:** скорость и ощущение касаний отличаются. Проверяйте на настоящем телефоне начиная с 6-й недели.
- **Ограничения бесплатного аккаунта Apple:** через 7 дней игра на iPhone перестаёт запускаться, и её нужно заново установить из Xcode. Устанавливать можно только на свои устройства.
- **Открытый редактор Unity:** он блокирует проект, и тесты и сборки из командной строки не запускаются. Закрывайте Unity, прежде чем передавать работу Claude.
- **Графика отнимает слишком много времени:** грибы, фоны и шрифты уже выбраны и нарисованы в браузерном прототипе; осталось только экспортировать PNG и сделать тёмное оформление в Unity.
