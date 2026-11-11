# Last Bastion — Game Design Document (short)
# Last Bastion — Дизайн-документ игры (краткий)

**Contents / Содержание**
- [English version](#english-version)
- [Русская версия](#русская-версия)

---

<a id="english-version"></a>
# 🇬🇧 English version

## 1. Overview
| | |
|---|---|
| **Working title** | Last Bastion |
| **Genre** | Tower Defense (fixed path, build slots) |
| **Platform** | Android, iOS (landscape) |
| **Engine** | Unity 6 LTS, C#, URP |
| **Audience** | Casual and mid-core players, 10+ |
| **Session length** | 3–6 minutes per level |
| **Business model** | Free; optional rewarded ads, no paywalls |
| **Scope (v1.0)** | 3 worlds × 5 levels, 5 towers, 6 enemy types |

**Pitch:** Monsters march along a winding road toward the last castle in the kingdom. Build and upgrade towers on stone platforms by the road to stop every wave before it breaks through the gates.

## 2. Core Loop
1. **Build:** spend gold on towers at build slots.
2. **Defend:** start the wave; towers fire at enemies automatically.
3. **Earn:** kills and cleared waves give gold.
4. **Improve:** upgrade, sell or reposition towers between waves.
5. Repeat until the last wave is beaten (**win**) or lives reach 0 (**lose**).

**Meta loop:** winning a level earns 1–3 stars and unlocks the next level. New towers unlock as you progress.

## 3. Rules
- **Lives:** 20 per level. An enemy that reaches the castle removes lives (1 for most enemies, more for big ones).
- **Waves:** 8–15 per level. Between waves there is a build phase with no time limit.
- **Early call:** starting the next wave early gives bonus gold equal to the seconds left on the timer.
- **Speed:** 1× / 2×, plus pause.
- **Stars:** 3★ with 18 or more lives left, 2★ with 10 or more, 1★ for any win.

## 4. Controls (touch)
| Action | Gesture |
|---|---|
| Build | Tap an empty slot to open a radial menu, then tap a tower |
| Upgrade / Sell / Target mode | Tap a tower |
| Move camera | Drag with one finger |
| Zoom | Pinch |
| Start wave | Button in the bottom-right corner |

## 5. Towers
Every tower has 3 levels (base + 2 upgrades). Each upgrade adds about +40% damage and +10% range. Upgrades cost 75% and then 100% of the base price. Selling refunds 70% of the gold spent.

| Tower | Cost | Role | Damage | Fire rate | Range | Hits air | Unlock |
|---|---|---|---|---|---|---|---|
| **Archer** | 100 | Cheap single target | 10 | 1.0/s | 3.5 | ✅ | Start |
| **Cannon** | 150 | Splash (radius 1.0) | 25 | 0.5/s | 3.0 | ❌ | Start |
| **Frost** | 125 | 40% slow for 2 s | 4 | 1.0/s | 3.0 | ✅ | Level 1-3 |
| **Tesla** | 200 | Chains to 3 targets | 15 | 0.8/s | 3.0 | ✅ | Level 2-1 |
| **Sniper** | 250 | Long range, ignores armor | 60 | 0.33/s | 7.0 | ✅ | Level 3-1 |

**Target modes:** First (default), Last, Strongest, Closest.

## 6. Enemies
| Enemy | HP | Speed | Special | Gold | Lives lost | First appears |
|---|---|---|---|---|---|---|
| **Grunt** | 50 | 1.0 | — | 5 | 1 | 1-1 |
| **Runner** | 30 | 2.0 | Fast | 6 | 1 | 1-2 |
| **Brute** | 250 | 0.6 | 30% armor | 15 | 2 | 1-4 |
| **Bat** | 40 | 1.4 | Flying, ignores the path | 8 | 1 | 2-1 |
| **Shaman** | 80 | 0.9 | Heals nearby enemies 5 HP/s | 12 | 1 | 2-3 |
| **Warlord** (boss) | 2000 | 0.5 | Shield absorbs the first 300 damage | 100 | 10 | 3-5 (and World 1 & 2 finales with less HP) |

Enemy HP is multiplied by **1 + 0.08 × (wave number − 1)** to scale difficulty within a level.

## 7. Economy
- **Starting gold:** 250 (enough for 2 towers)
- **Income:** gold per kill (see the table) plus a wave-clear bonus of **20 + 5 × wave number**
- **Balance target:** a player who builds sensibly ends a level with about 10–15% of their gold unspent
- All values live in ScriptableObjects so they can be tuned without code changes

## 8. World & Level Progression
| World | Theme | New mechanic | Levels |
|---|---|---|---|
| **1. Green Valley** | Meadows and forest | Basics, Grunt, Runner, Brute, Frost tower | 1-1 … 1-5 |
| **2. Scorched Desert** | Dunes and ruins | Flying Bats, healing Shamans, Tesla tower | 2-1 … 2-5 |
| **3. Frozen Peaks** | Snow and ice cliffs | Split paths, Warlord boss, Sniper tower | 3-1 … 3-5 |

- Level 1-1 includes a guided tutorial (build, start wave, upgrade).
- Each world's final level (x-5) has a boss wave.
- Difficulty rises gradually: one new element every 2–3 levels.

## 9. Art & Audio
- **Style:** bright, stylized low-poly 3D seen from an angled top-down camera, with readable silhouettes and strong color coding (blue for frost, yellow for tesla, and so on).
- **Feedback:** damage numbers, coin pop-ups, hit flashes, a range circle when placing a tower, and light screen shake on explosions.
- **Audio:** a calm build-phase music track, a more intense wave track, and short SFX for every shot, hit, kill and UI tap.

## 10. UI Screens
Splash → Main Menu → World/Level Select → Game (HUD) → Victory/Defeat → Settings (music, SFX, language: EN/RU)

**HUD:** gold and lives at the top left, wave counter at the top center, pause and speed at the top right, Start Wave at the bottom right.

## 11. Monetization (optional)
- Rewarded ad after a defeat: **Revive** with +5 lives (once per level)
- Rewarded ad on the victory screen: **double the wave-clear gold** (cosmetic score only, or used in a future meta-currency)
- No forced ads during gameplay

## 12. Out of Scope for v1.0
Endless mode, hero units, global upgrade tree, multiplayer, in-app purchases, level editor. These go to the v1.1 list.

---
---

<a id="русская-версия"></a>
# 🇷🇺 Русская версия

## 1. Общие сведения
| | |
|---|---|
| **Рабочее название** | Last Bastion («Последний бастион») |
| **Жанр** | Tower Defense (фиксированный путь, слоты для строительства) |
| **Платформа** | Android, iOS (горизонтальная ориентация) |
| **Движок** | Unity 6 LTS, C#, URP |
| **Аудитория** | Казуальные и мидкорные игроки, 10+ |
| **Длина сессии** | 3–6 минут на уровень |
| **Модель** | Бесплатная; видео с наградой по желанию, без платных барьеров |
| **Объём (v1.0)** | 3 мира × 5 уровней, 5 башен, 6 типов врагов |

**Описание:** чудовища идут по извилистой дороге к последнему замку королевства. Стройте и улучшайте башни на каменных площадках у дороги, чтобы остановить каждую волну до того, как она прорвётся к воротам.

## 2. Основной игровой цикл
1. **Строительство:** тратьте золото на башни в слотах.
2. **Оборона:** запустите волну; башни стреляют по врагам автоматически.
3. **Доход:** убийства и пройденные волны приносят золото.
4. **Развитие:** между волнами улучшайте, продавайте или переставляйте башни.
5. Повторяйте, пока не будет отбита последняя волна (**победа**) или жизни не закончатся (**поражение**).

**Мета-цикл:** победа на уровне даёт 1–3 звезды и открывает следующий уровень. По мере прохождения открываются новые башни.

## 3. Правила
- **Жизни:** 20 на уровень. Враг, дошедший до замка, отнимает жизни (1 у большинства врагов, больше у крупных).
- **Волны:** 8–15 на уровень. Между волнами идёт фаза строительства без ограничения по времени.
- **Ранний вызов:** если запустить следующую волну раньше, игрок получает бонусное золото, равное оставшимся секундам таймера.
- **Скорость:** 1× / 2×, а также пауза.
- **Звёзды:** 3★, если осталось 18 и больше жизней, 2★ — 10 и больше, 1★ — за любую победу.

## 4. Управление (касания)
| Действие | Жест |
|---|---|
| Строительство | Нажать на пустой слот, откроется радиальное меню, затем нажать на башню |
| Улучшение / продажа / режим цели | Нажать на башню |
| Перемещение камеры | Провести одним пальцем |
| Масштаб | Щипок |
| Запуск волны | Кнопка в правом нижнем углу |

## 5. Башни
У каждой башни 3 уровня (базовый + 2 улучшения). Каждое улучшение даёт примерно +40% урона и +10% радиуса. Улучшения стоят 75%, а затем 100% базовой цены. При продаже возвращается 70% потраченного золота.

| Башня | Цена | Роль | Урон | Скорострельность | Радиус | По воздуху | Открывается |
|---|---|---|---|---|---|---|---|
| **Лучник** | 100 | Дешёвый урон по одной цели | 10 | 1,0/с | 3,5 | ✅ | С начала |
| **Пушка** | 150 | Урон по площади (радиус 1,0) | 25 | 0,5/с | 3,0 | ❌ | С начала |
| **Ледяная** | 125 | Замедление на 40% на 2 с | 4 | 1,0/с | 3,0 | ✅ | Уровень 1-3 |
| **Тесла** | 200 | Цепь на 3 цели | 15 | 0,8/с | 3,0 | ✅ | Уровень 2-1 |
| **Снайпер** | 250 | Большая дальность, игнорирует броню | 60 | 0,33/с | 7,0 | ✅ | Уровень 3-1 |

**Режимы выбора цели:** первый (по умолчанию), последний, сильнейший, ближайший.

## 6. Враги
| Враг | Здоровье | Скорость | Особенность | Золото | Отнимает жизней | Появляется |
|---|---|---|---|---|---|---|
| **Гоблин** | 50 | 1,0 | — | 5 | 1 | 1-1 |
| **Бегун** | 30 | 2,0 | Быстрый | 6 | 1 | 1-2 |
| **Громила** | 250 | 0,6 | Броня 30% | 15 | 2 | 1-4 |
| **Летучая мышь** | 40 | 1,4 | Летает, игнорирует путь | 8 | 1 | 2-1 |
| **Шаман** | 80 | 0,9 | Лечит врагов рядом на 5 ед./с | 12 | 1 | 2-3 |
| **Военачальник** (босс) | 2000 | 0,5 | Щит поглощает первые 300 урона | 100 | 10 | 3-5 (и в финалах миров 1 и 2 с меньшим здоровьем) |

Здоровье врагов умножается на **1 + 0,08 × (номер волны − 1)**, чтобы сложность росла внутри уровня.

## 7. Экономика
- **Стартовое золото:** 250 (хватает на 2 башни)
- **Доход:** золото за убийство (см. таблицу) плюс бонус за волну **20 + 5 × номер волны**
- **Цель баланса:** игрок, который строит разумно, заканчивает уровень с запасом примерно 10–15% золота
- Все значения хранятся в ScriptableObjects, чтобы их можно было настраивать без изменения кода

## 8. Миры и прогрессия уровней
| Мир | Тема | Новая механика | Уровни |
|---|---|---|---|
| **1. Зелёная долина** | Луга и лес | Основы, гоблин, бегун, громила, ледяная башня | 1-1 … 1-5 |
| **2. Выжженная пустыня** | Дюны и руины | Летучие мыши, шаманы-лекари, башня тесла | 2-1 … 2-5 |
| **3. Ледяные пики** | Снег и ледяные скалы | Разветвлённые пути, босс-военачальник, снайперская башня | 3-1 … 3-5 |

- На уровне 1-1 есть пошаговое обучение (строительство, запуск волны, улучшение).
- Последний уровень каждого мира (x-5) содержит волну с боссом.
- Сложность растёт постепенно: новый элемент каждые 2–3 уровня.

## 9. Графика и звук
- **Стиль:** яркое стилизованное low-poly 3D с наклонной камерой сверху, хорошо читаемые силуэты и чёткое цветовое кодирование (синий — лёд, жёлтый — тесла и т. д.).
- **Обратная связь:** цифры урона, вылетающие монеты, вспышки при попадании, круг радиуса при установке башни и лёгкая тряска экрана при взрывах.
- **Звук:** спокойная музыка в фазе строительства, более напряжённая во время волны и короткие звуковые эффекты для каждого выстрела, попадания, убийства и нажатия в интерфейсе.

## 10. Экраны интерфейса
Заставка → главное меню → выбор мира/уровня → игра (HUD) → победа/поражение → настройки (музыка, звуки, язык: EN/RU)

**HUD:** золото и жизни слева вверху, счётчик волн сверху по центру, пауза и скорость справа вверху, «Начать волну» справа внизу.

## 11. Монетизация (по желанию)
- Видео с наградой после поражения: **возрождение** с +5 жизнями (один раз за уровень)
- Видео с наградой на экране победы: **удвоение золота за волны** (только для счёта или для будущей мета-валюты)
- Никакой принудительной рекламы во время игры

## 12. Не входит в v1.0
Бесконечный режим, герои, глобальное дерево улучшений, мультиплеер, внутриигровые покупки, редактор уровней. Всё это — в список для v1.1.
