# Перенос проекта Emo Party на другой компьютер

Архив `EmoParty_perenos_2026-11-04.zip` содержит всё, что хранится на этом компьютере.
Часть проекта живёт онлайн — она доступна с любого компьютера после входа в аккаунт.

## Что внутри архива

```
EmoParty/
  game_01/                    — документы, скрипты, арт и прототип
    README.md                 — описание проекта, сборка сцены в Unity, план
    PERENOS.md                — эта инструкция
    GDD_Emo_Party.pdf         — актуальный GDD (6 стр.)
    Plan_Emo_Party.pdf        — актуальный план разработки
    Docs/plan.html            — исходник плана (из него собирается PDF)
    prototype/index.html      — играбельный HTML-прототип
    Assets/Scripts/*.cs       — C#-скрипты игры для Unity
    Assets/Art/Cards/*.svg    — эскизы карточек (старая тема, будут заменены)
    tools/fill_board_emo.sh   — скрипт, которым заполнялась GitHub-доска
    GDD_Lesnaya_para.pdf, Plan_razrabotki.pdf, PDF/ — старые версии («Лесная пара»)
  Unity_gs26_findepair/       — Unity-проект вместе с git-историей (.git)
```

## Что хранится онлайн (в архив не входит)

| Что | Где | Доступ |
|---|---|---|
| GDD (редактируемый документ) | https://claude.ai/artifact/D4X336hT623Xs85ybFWYd9 | аккаунт claude.ai |
| Доска задач (37 задач) | https://github.com/users/Zaidejas/projects/12 | аккаунт GitHub с доступом к проекту |
| Репозиторий Unity-проекта | https://github.com/Zaidejas/GC26, ветка `gs26_findepair` | аккаунт GitHub |

## Шаги на новом компьютере

1. Распакуйте архив, например в `Документы`. Папку `game_01` можно положить в `Документы\game_01`.
2. **Прототип:** откройте `game_01/prototype/index.html` двойным щелчком — игра запустится в браузере.
3. **Unity:** установите Unity Hub и Unity 6 LTS (модули Android/iOS Build Support),
   затем в Unity Hub нажмите **Add → Add project from disk** и выберите `Unity_gs26_findepair`.
   Первая загрузка займёт несколько минут: Unity заново соберёт папку `Library`.
   Вместо копии из архива можно взять проект из GitHub:

   ```bash
   git clone -b gs26_findepair https://github.com/Zaidejas/GC26.git
   ```

   В копии из архива есть незакоммиченное изменение `Assets/TutorialInfo/Icons/URP.png` — его нет на GitHub.
4. **GitHub CLI** (нужен только для работы с доской из терминала):

   ```bash
   winget install --id GitHub.cli -e --scope user
   ```

   ```bash
   gh auth login --hostname github.com --git-protocol https --web --scopes project
   ```

5. **Python** (необязательно) — чтобы открыть прототип с телефона по Wi-Fi:

   ```bash
   python -m http.server 8000 --directory game_01/prototype
   ```

## Что не переносится архивом

- Вход в аккаунты (GitHub, claude.ai) — войдите заново.
- Рекорд и настройки прототипа — они хранятся в браузере старого компьютера.
- История переписки с Claude остаётся в приложении Claude на этом компьютере. Чтобы продолжить с Claude
  на новом компьютере, откройте там папку `game_01` и дайте ссылку на GDD — README и PERENOS.md описывают проект.
