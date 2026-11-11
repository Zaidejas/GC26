# Tower Defense Mobile Game — 2-Month Plan (Unity + C#)
# План мобильной игры Tower Defense на 2 месяца (Unity + C#)

**Contents / Содержание**
- [English version](#english-version)
- [Русская версия](#русская-версия)

---

<a id="english-version"></a>
# 🇬🇧 English version

**Assumptions:** one developer (or a small team) working about 20–30 hours a week, targeting Android and iOS, 2D or stylized 3D, using free or Asset Store art. The scope target is **one polished game with 3 worlds × 5 levels, 5 towers and 6 enemy types** rather than a large unfinished one.

---

## Week 0 (2–3 days): Pre-production
- **Game design doc (1–2 pages):** core loop, win/lose conditions, economy (gold per kill, tower costs), the list of towers and enemies, and how the player progresses.
- **Tech decisions:**
  - Unity 6 LTS with the URP (2D or 3D) template
  - New Input System for touch
  - TextMeshPro for text, Cinemachine for the camera
  - Git with Unity's `.gitignore` and Git LFS for art
- **Backlog:** set up a board in Trello, Notion or GitHub Projects with these milestones.

---

## Phase 1: Core Prototype (Weeks 1–2)
**Goal:** one ugly but playable level made of cubes.

**Week 1: Foundations**
- Project structure: `Scripts/{Core, Towers, Enemies, UI, Data}`, `Prefabs`, `ScriptableObjects`
- Path system: waypoints, or a NavMesh/grid with A* if you want mazing
- `Enemy`: follows the path, takes damage, dies, and reaches the base
- `WaveSpawner` driven by a `WaveData` ScriptableObject (enemy type, count, interval)
- `GameManager`: lives, gold and game states (Build → Wave → Win/Lose)

**Week 2: Towers**
- `TowerData` ScriptableObject (cost, range, fire rate, damage, projectile, upgrade path)
- Placement: tap a build spot to open a radial menu and place the tower (predefined slots are simplest on mobile)
- Targeting modes: First, Last, Strongest, Closest
- `Projectile` with **object pooling** (needed for mobile performance)
- Minimal HUD showing gold, lives, wave number and a "Start Wave" button

✅ **Milestone:** you can win or lose one level with 2 towers and 2 enemy types.

---

## Phase 2: Content & Systems (Weeks 3–4)
**Week 3: Variety**
- 5 towers, for example Archer (single target), Cannon (splash), Frost (slow), Tesla (chain), and Sniper (long range)
- 6 enemies: basic, fast, tank, flying (ignores the path), healer, and a boss with shields or armor
- Status effects system (slow, burn, poison) using a reusable `StatusEffect` base class
- Tower upgrades (2–3 tiers) and selling at a 70% refund

**Week 4: Meta systems**
- Level select screen and a `LevelData` ScriptableObject (map, waves, starting gold)
- Star rating based on lives left (1–3 stars)
- Save system: JSON in `Application.persistentDataPath` for unlocked levels, stars and settings
- Game speed controls (1×/2×) and pause
- Camera pan and pinch-zoom for touch

✅ **Milestone:** a vertical slice with 3–5 levels you can play from start to finish.

---

## Phase 3: Art, Audio & Feel (Weeks 5–6)
**Week 5: Visuals**
- Replace placeholders with final assets (Kenney.nl is free; Asset Store TD packs also work)
- Animations for idle, attack and death, done in the Animator or with DOTween
- VFX: hits, explosions and frost using the particle system, kept to low particle counts
- World themes, such as grass, desert and snow

**Week 6: UI/UX and juice**
- Full UI flow: Splash → Main Menu → Level Select → Game → Results → Settings
- Safe-area handling for notches; a Canvas Scaler reference resolution of 1920×1080 with match 0.5
- Audio: music, SFX and an `AudioManager` with volume settings
- Game feel: damage numbers, screen shake, coin pop-ups, and tower range previews
- Tutorial for the first level, using guided tooltips

✅ **Milestone:** the game looks and sounds like a finished product.

---

## Phase 4: Content Completion & Balancing (Week 7)
- Build all 15 levels (3 worlds × 5)
- Balancing spreadsheet: enemy HP and wave values against tower DPS-per-gold, and gold income per wave
- Difficulty curve: introduce a new enemy or mechanic every 2–3 levels
- Optional extras if time allows: an endless mode, achievements, or daily rewards
- **Playtest with 3–5 people** and watch them play without helping

---

## Phase 5: Optimization, Polish & Release (Week 8)
**Performance (test on a low-end Android device):**
- Unity Profiler and Frame Debugger
- Target 60 FPS (or 30 as a minimum) with `Application.targetFrameRate`
- Sprite atlases, texture compression (ASTC), and fewer draw calls
- Avoid per-frame `GetComponent`, `Find` and LINQ, and avoid GC allocations in `Update`

**Release:**
- App icon, splash screen, store screenshots, a short trailer, and a privacy policy
- Android: IL2CPP, ARM64, an AAB build, and an internal testing track on Google Play
- iOS (if targeted): Xcode build and TestFlight; this requires a Mac and an Apple Developer account
- Optional monetization: Unity Ads rewarded videos (for example "double gold" or "revive"); integrate it early in week 7 if you plan to add it
- Fix bugs from the testers, then release 🎉

---

## Core Architecture (short version)
```
GameManager (singleton/state machine)
├── EconomyManager      – gold, events: OnGoldChanged
├── WaveManager         – reads LevelData → WaveData[]
├── BuildManager        – placement, upgrade, sell
├── PoolManager         – enemies, projectiles, VFX
├── UIManager           – listens to C# events, no direct coupling
└── SaveManager         – JSON persistence

ScriptableObjects: TowerData, EnemyData, WaveData, LevelData
Patterns: Observer (C# events), Object Pool, State, Strategy (targeting)
```

## Risk Management
| Risk | Mitigation |
|---|---|
| Scope creep | Freeze features at the end of week 4; new ideas go to a "v1.1" list |
| Art takes too long | Use asset packs; keep a consistent style over custom art |
| Poor performance on phones | Test on a real device every week starting in week 2 |
| Balancing takes too long | Keep all numbers in ScriptableObjects so you can tune without code |
| Falling behind | Weeks 7–8 have buffer; cut World 3 or endless mode first |

## Weekly Routine
- **Monday:** plan the week's tasks
- **Daily:** commit to Git
- **Friday:** build to a phone and play it

---
---

<a id="русская-версия"></a>
# 🇷🇺 Русская версия

**Допущения:** один разработчик (или небольшая команда) работает примерно 20–30 часов в неделю, целевые платформы — Android и iOS, графика 2D или стилизованное 3D, арт бесплатный или из Asset Store. Цель по объёму — **одна отполированная игра: 3 мира × 5 уровней, 5 башен и 6 типов врагов**, а не большая незаконченная.

---

## Неделя 0 (2–3 дня): препродакшн
- **Дизайн-документ (1–2 страницы):** основной игровой цикл, условия победы и поражения, экономика (золото за убийство, стоимость башен), список башен и врагов, система прогрессии игрока.
- **Технические решения:**
  - Unity 6 LTS с шаблоном URP (2D или 3D)
  - New Input System для управления касаниями
  - TextMeshPro для текста, Cinemachine для камеры
  - Git с `.gitignore` для Unity и Git LFS для арта
- **Бэклог:** доска в Trello, Notion или GitHub Projects с этими этапами.

---

## Фаза 1: базовый прототип (недели 1–2)
**Цель:** один некрасивый, но играбельный уровень из кубиков.

**Неделя 1: основа**
- Структура проекта: `Scripts/{Core, Towers, Enemies, UI, Data}`, `Prefabs`, `ScriptableObjects`
- Система пути: вейпоинты, либо NavMesh/сетка с A*, если нужен «лабиринт»
- `Enemy`: идёт по пути, получает урон, умирает и доходит до базы
- `WaveSpawner` на основе ScriptableObject `WaveData` (тип врага, количество, интервал)
- `GameManager`: жизни, золото и состояния игры (Строительство → Волна → Победа/Поражение)

**Неделя 2: башни**
- ScriptableObject `TowerData` (стоимость, радиус, скорострельность, урон, снаряд, ветка улучшений)
- Размещение: нажатие на точку строительства открывает радиальное меню, через него ставится башня (на мобильных проще всего заранее заданные слоты)
- Режимы выбора цели: первый, последний, сильнейший, ближайший
- `Projectile` с **пулом объектов** (это нужно для производительности на мобильных)
- Минимальный HUD: золото, жизни, номер волны и кнопка «Начать волну»

✅ **Этап:** один уровень можно выиграть или проиграть, есть 2 башни и 2 типа врагов.

---

## Фаза 2: контент и системы (недели 3–4)
**Неделя 3: разнообразие**
- 5 башен, например: лучник (одна цель), пушка (урон по площади), ледяная (замедление), тесла (цепная молния), снайпер (большая дальность)
- 6 врагов: обычный, быстрый, танк, летающий (игнорирует путь), лекарь и босс со щитами или бронёй
- Система статус-эффектов (замедление, горение, яд) с переиспользуемым базовым классом `StatusEffect`
- Улучшения башен (2–3 уровня) и продажа с возвратом 70% стоимости

**Неделя 4: мета-системы**
- Экран выбора уровня и ScriptableObject `LevelData` (карта, волны, стартовое золото)
- Рейтинг в звёздах по оставшимся жизням (1–3 звезды)
- Система сохранений: JSON в `Application.persistentDataPath` для открытых уровней, звёзд и настроек
- Управление скоростью игры (1×/2×) и пауза
- Перемещение камеры и масштабирование щипком

✅ **Этап:** вертикальный срез из 3–5 уровней, которые можно пройти от начала до конца.

---

## Фаза 3: графика, звук и ощущения (недели 5–6)
**Неделя 5: визуал**
- Замена заглушек на финальные ассеты (бесплатные есть на Kenney.nl, подойдут и TD-паки из Asset Store)
- Анимации ожидания, атаки и смерти через Animator или DOTween
- Эффекты: попадания, взрывы и лёд на системе частиц, с небольшим количеством частиц
- Тематика миров, например: луга, пустыня и снег

**Неделя 6: UI/UX и «сочность»**
- Полный поток интерфейса: заставка → главное меню → выбор уровня → игра → результаты → настройки
- Учёт безопасной зоны для вырезов экрана; в Canvas Scaler эталонное разрешение 1920×1080 с match 0.5
- Звук: музыка, звуковые эффекты и `AudioManager` с настройками громкости
- Ощущение от игры: всплывающие цифры урона, тряска экрана, вылетающие монеты и предпросмотр радиуса башни
- Обучение на первом уровне через подсказки-указатели

✅ **Этап:** игра выглядит и звучит как готовый продукт.

---

## Фаза 4: завершение контента и баланс (неделя 7)
- Создать все 15 уровней (3 мира × 5)
- Таблица баланса: здоровье врагов и параметры волн против урона башен в секунду на единицу золота, а также доход золота за волну
- Кривая сложности: новый враг или механика каждые 2–3 уровня
- Дополнительно, если хватит времени: бесконечный режим, достижения или ежедневные награды
- **Плейтест с 3–5 людьми:** наблюдайте за игрой и не подсказывайте

---

## Фаза 5: оптимизация, полировка и релиз (неделя 8)
**Производительность (тестируйте на слабом Android-устройстве):**
- Unity Profiler и Frame Debugger
- Цель — 60 FPS (минимум 30) через `Application.targetFrameRate`
- Атласы спрайтов, сжатие текстур (ASTC) и меньше вызовов отрисовки
- Не вызывайте `GetComponent`, `Find` и LINQ каждый кадр и избегайте выделений памяти под GC в `Update`

**Релиз:**
- Иконка приложения, заставка, скриншоты для магазина, короткий трейлер и политика конфиденциальности
- Android: IL2CPP, ARM64, сборка AAB и внутреннее тестирование в Google Play
- iOS (если планируется): сборка в Xcode и TestFlight; для этого нужны Mac и аккаунт Apple Developer
- Монетизация по желанию: видео с наградой через Unity Ads (например, «удвоить золото» или «возрождение»); если планируете её, интегрируйте в начале недели 7
- Исправить баги, найденные тестировщиками, и выпустить игру 🎉

---

## Базовая архитектура (кратко)
```
GameManager (синглтон / машина состояний)
├── EconomyManager      – золото, события: OnGoldChanged
├── WaveManager         – читает LevelData → WaveData[]
├── BuildManager        – размещение, улучшение, продажа
├── PoolManager         – враги, снаряды, эффекты
├── UIManager           – слушает C#-события, без прямых связей
└── SaveManager         – сохранение в JSON

ScriptableObjects: TowerData, EnemyData, WaveData, LevelData
Паттерны: Observer (C#-события), Object Pool, State, Strategy (выбор цели)
```

## Управление рисками
| Риск | Как снизить |
|---|---|
| Разрастание объёма | Заморозить список функций в конце 4-й недели; новые идеи — в список для версии 1.1 |
| Арт занимает слишком много времени | Использовать готовые паки; единый стиль важнее собственного арта |
| Низкая производительность на телефонах | Тестировать на реальном устройстве каждую неделю, начиная со 2-й |
| Баланс занимает слишком много времени | Хранить все числа в ScriptableObjects, чтобы настраивать без изменения кода |
| Отставание от графика | Недели 7–8 служат запасом; первыми вырезать мир 3 или бесконечный режим |

## Еженедельный распорядок
- **Понедельник:** спланировать задачи на неделю
- **Каждый день:** делать коммиты в Git
- **Пятница:** собрать билд на телефон и поиграть
