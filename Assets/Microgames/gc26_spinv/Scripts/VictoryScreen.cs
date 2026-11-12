using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // Аркадный экран победы после финального босса. Всё печатается по очереди:
    // шапка, заголовок, подсчёт очков (числа «накручиваются»), NEW HIGH SCORE,
    // таблица TOP 5 и ввод имени (до 8 символов с клавиатуры). Кнопок меню и рестарта нет:
    // в начале печати таблицы открывается аркадный режим («???: Arcade mode unlocked.», дальше по тапу),
    // после ввода имени результат сохраняется, автомат выключается (POWER OFF) и игра закрывается.
    // Всё на реальном времени: игра стоит на Time.timeScale = 0
    public class VictoryScreen : MonoBehaviour
    {
        [Header("Заголовок")]
        public TMP_Text titleText;
        public TMP_Text subtitleText;

        [Header("Подсчёт")]
        public TMP_Text scoreLabel;
        public TMP_Text scoreValue;
        public TMP_Text livesLabel;
        public TMP_Text livesValue;
        public TMP_Text coinsLabel;
        public TMP_Text coinsValue;
        public TMP_Text totalLabel;
        public TMP_Text totalValue;
        public TMP_Text newHighScore;

        [Header("Таблица рекордов")]
        public GameObject tableRoot;
        public TMP_Text tableTitle;
        public TMP_Text[] rankTexts;
        public TMP_Text[] nameTexts;
        public TMP_Text[] scoreTexts;
        public Image[] rowHighlights;
        public GameObject initialsPanel;
        public TMP_Text initialsHint;
        [Tooltip("Поле ввода: на телефоне открывает экранную клавиатуру, на ПК печатается с клавиатуры")]
        public TMP_InputField initialsInput;
        public PressableButton initialsOkButton;

        [Header("Конец")]
        [Tooltip("Выключение автомата после ввода имени, затем игра закрывается")]
        public QuitEffect quitEffect;

        // Попытка записаться под именем автора: красное NAME IS TAKEN, глюк экрана, «???: You are not Gekata.»,
        // в таблице красным печатается IMPOSTOR (только на экране, в таблицу лидеров не пишется),
        // корабль рассыпается в пыль, POWER OFF. Личный рекорд при этом сохраняется
        [Header("Самозванец")]
        public GameObject impostorDialog;
        public TMP_Text impostorLine;
        [Tooltip("Корабль игрока: после IMPOSTOR вздрагивает и рассыпается по пикселям на ветру")]
        public Image impostorShip;
        public TMP_Text impostorCaption;
        public string impostorCaptionText = "YOU WERE NEVER HERE";
        public int dustColumns = 12;
        public int dustRows = 12;
        public string impostorMessage = "You are not Gekata.";
        public float nameTakenTime = 3f;
        public float glitchTime = 1.2f;
        public const string ImpostorName = "IMPOSTOR";

        // В начале печати таблицы в том же окне печатается «???: Arcade mode unlocked.», по тапу печать продолжается
        [Header("Открытие аркады")]
        public string arcadeMessage = "Arcade mode unlocked.";
        [Tooltip("Мигающая подсказка TAP в окне, как в сценке перед вторым боссом")]
        public TMP_Text dialogTapHint;
        const string ImpostorColor = "#FF4A3D";
        static readonly Color ImpostorRed = new Color(1f, 0.29f, 0.24f);

        [Header("Скорость печати")]
        public float titleCharTime = 0.06f;
        public float charTime = 0.03f;
        public float pauseBetweenLines = 0.08f;

        const int MaxInitials = 8; // имя в таблице рекордов: до 8 символов
        static readonly string[] Ranks = { "1ST", "2ND", "3RD", "4TH", "5TH" };

        SpinvGameManager gm;
        bool editing;
        bool finished;
        bool impostor;
        bool tableReady;
        List<HighScoreTable.Entry> table;

        void Awake()
        {
            initialsInput.characterLimit = MaxInitials;
            // Только английские буквы, без цифр и прочего, сразу заглавными
            initialsInput.onValidateInput = (text, index, c) =>
                (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ? char.ToUpperInvariant(c) : '\0';
            initialsInput.onValueChanged.AddListener(_ =>
            {
                // OK нажимается, только когда что-то введено (имя автора тоже можно «отправить» - см. Impostor)
                if (!impostor) initialsOkButton.SetInteractable(TypedInitials().Length > 0);
                RefreshTable();
            });
            initialsInput.onSubmit.AddListener(_ => FinishInitials());
            initialsOkButton.onPress.AddListener(FinishInitials);
        }

        public void Show(SpinvGameManager manager)
        {
            gm = manager;
            gameObject.SetActive(true);

            foreach (TMP_Text text in AllTexts())
            {
                text.text = "";
                text.maxVisibleCharacters = 99999;
            }
            SetBlink(titleText, false);
            SetBlink(newHighScore, false);
            newHighScore.gameObject.SetActive(false);
            tableRoot.SetActive(false);
            initialsPanel.SetActive(false);
            initialsInput.gameObject.SetActive(false);
            initialsOkButton.gameObject.SetActive(false);
            foreach (Image row in rowHighlights) row.enabled = false;
            editing = tableReady = finished = impostor = false;
            impostorDialog.SetActive(false);
            impostorShip.gameObject.SetActive(false);
            impostorCaption.text = "";

            StartCoroutine(Play());
        }

        IEnumerable<TMP_Text> AllTexts()
        {
            yield return titleText; yield return subtitleText;
            yield return scoreLabel; yield return scoreValue; yield return livesLabel; yield return livesValue;
            yield return coinsLabel; yield return coinsValue; yield return totalLabel; yield return totalValue;
            yield return newHighScore; yield return tableTitle; yield return initialsHint;
            yield return initialsOkButton.label;
            foreach (TMP_Text t in rankTexts) yield return t;
            foreach (TMP_Text t in nameTexts) yield return t;
            foreach (TMP_Text t in scoreTexts) yield return t;
        }

        IEnumerator Type(TMP_Text text, string value, float speed = -1f)
        {
            yield return Typewriter.Type(text, value, speed > 0f ? speed : charTime);
            yield return new WaitForSecondsRealtime(pauseBetweenLines);
        }

        IEnumerator Play()
        {
            yield return new WaitForSecondsRealtime(0.3f);

            yield return Type(titleText, "CONGRATULATIONS!", titleCharTime);
            SetBlink(titleText, true);
            yield return Type(subtitleText, "EARTH IS SAVED\nALL 5 WAVES CLEARED");

            // Подсчёт: подпись печатается, число «накручивается»
            yield return Type(scoreLabel, "SCORE");
            yield return CountUp(scoreValue, gm.BaseScore, 0.9f);
            yield return Type(livesLabel, $"LIVES <color=#48D46C>{gm.Lives} x {gm.bonusPerLife}</color>");
            yield return CountUp(livesValue, gm.LivesBonus, 0.5f);
            yield return Type(coinsLabel, $"COINS <color=#48D46C>{gm.Coins} x {gm.bonusPerCoin}</color>");
            yield return CountUp(coinsValue, gm.CoinsBonus, 0.5f);
            yield return Type(totalLabel, "TOTAL");
            yield return CountUp(totalValue, gm.Score, 0.7f);

            if (gm.Score > gm.PreviousBest)
            {
                newHighScore.gameObject.SetActive(true);
                yield return Type(newHighScore, "NEW HIGH SCORE!");
                SetBlink(newHighScore, true);
            }

            // Таблица TOP 5, строка за строкой
            table = HighScoreTable.Load();
            tableRoot.SetActive(true);
            yield return Type(tableTitle, "- TOP 5 -");
            yield return ArcadeUnlockedDialog(); // прохождение открывает аркаду всем, какое бы имя ни ввели
            for (int i = 0; i < nameTexts.Length; i++)
            {
                rowHighlights[i].enabled = i == gm.TableIndex;
                bool filled = i < table.Count;
                yield return Type(rankTexts[i], Ranks[Mathf.Min(i, Ranks.Length - 1)]);
                yield return Type(nameTexts[i], filled ? Colored(table[i].initials) : "---");
                yield return Type(scoreTexts[i], filled ? table[i].score.ToString("00000") : "00000");
            }
            tableReady = true;

            // Ввод имени - единственное действие на экране. Если в топ не попали, имя всё равно
            // спрашиваем (оно запомнится на следующие игры), но в таблицу не пишем
            initialsPanel.SetActive(true);
            yield return Type(initialsHint, "ENTER YOUR NAME");
            initialsInput.gameObject.SetActive(true);
            initialsInput.text = ""; // поле пустое: OK неактивна, пока игрок что-то не впишет
            initialsOkButton.gameObject.SetActive(true);
            initialsOkButton.SetInteractable(false);
            yield return Type(initialsOkButton.label, "OK");
            editing = true;
            // Фокус в поле: на телефоне выезжает клавиатура
            initialsInput.Select();
            initialsInput.ActivateInputField();
            RefreshTable();
        }

        IEnumerator CountUp(TMP_Text text, int target, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                text.text = Mathf.RoundToInt(target * (t / duration)).ToString("00000");
                yield return null;
            }
            text.text = target.ToString("00000");
            yield return new WaitForSecondsRealtime(pauseBetweenLines);
        }

        void Update()
        {
            // Мигает курсор «_» в строке рекорда
            if (editing) RefreshTable();
        }

        // Что сейчас набрано: до MaxInitials символов
        string TypedInitials()
        {
            string typed = initialsInput != null ? initialsInput.text.Trim().ToUpperInvariant() : "";
            if (typed.Length > MaxInitials) typed = typed.Substring(0, MaxInitials);
            return typed;
        }

        // IMPOSTOR в таблице всегда красный
        static string Colored(string name) =>
            name == ImpostorName ? $"<color={ImpostorColor}>{name}</color>" : name;

        IEnumerator Impostor(string typed)
        {
            impostor = true;
            Achievements.Award(AchievementId.Impostor, true); // плашка - при следующем заходе в меню, чтобы не ломать сцену
            initialsInput.DeactivateInputField();
            initialsInput.interactable = false;
            initialsOkButton.interactable = false; // кнопка не гаснет, просто больше не нажимается

            // 1. Над полем ввода красным NAME IS TAKEN на 3 секунды
            string hint = initialsHint.text;
            Color hintColor = initialsHint.color;
            initialsHint.text = "NAME IS TAKEN";
            initialsHint.color = ImpostorRed;
            yield return new WaitForSecondsRealtime(nameTakenTime);
            initialsHint.text = hint;
            initialsHint.color = hintColor;

            // 2. Экран глючит: CRT рывками искажает картинку, экран дёргается
            editing = false;
            initialsPanel.SetActive(false);
            if (gm.TableIndex >= 0 && gm.TableIndex < nameTexts.Length)
                nameTexts[gm.TableIndex].text = typed;
            var root = (RectTransform)transform;
            for (float t = 0f; t < glitchTime; t += Time.unscaledDeltaTime)
            {
                CrtBoost.Set(Random.value < 0.3f ? Random.Range(3f, 7f) : Random.Range(0.5f, 2f));
                root.anchoredPosition = new Vector2(Random.Range(-30f, 30f), Random.Range(-12f, 12f));
                yield return null;
            }
            root.anchoredPosition = Vector2.zero;
            CrtBoost.Set(1.5f); // так и остаётся «поплывшим» до конца

            // 3. «???: You are not Gekata.»
            impostorDialog.SetActive(true);
            yield return TypeDialog(impostorMessage);
            yield return new WaitForSecondsRealtime(1.5f);
            impostorDialog.SetActive(false);

            // 4. В строке игрока красным печатается IMPOSTOR, а сама строка из таблицы удаляется
            if (gm.TableIndex >= 0 && gm.TableIndex < nameTexts.Length)
            {
                // Медленно, по букве; каждая буква - маленький толчок экрана
                TMP_Text row = nameTexts[gm.TableIndex];
                for (int k = 1; k <= ImpostorName.Length; k++)
                {
                    row.text = $"<color={ImpostorColor}>{ImpostorName.Substring(0, k)}</color>" + (k < ImpostorName.Length ? "_" : "");
                    SpinvUiSound.TypeTick(ImpostorName[k - 1]);
                    const float letterTime = 0.5f, thumpTime = 0.12f;
                    for (float t = 0f; t < thumpTime; t += Time.unscaledDeltaTime)
                    {
                        float s = 14f * (1f - t / thumpTime);
                        root.anchoredPosition = new Vector2(Random.Range(-s, s), Random.Range(-s, s) * 0.6f);
                        yield return null;
                    }
                    root.anchoredPosition = Vector2.zero;
                    yield return new WaitForSecondsRealtime(letterTime - thumpTime);
                }
                // IMPOSTOR только на экране: в таблицу лидеров самозванец не попадает (личный рекорд остаётся)
                HighScoreTable.Remove(gm.TableIndex);
                PlayerPrefs.Save();
                yield return new WaitForSecondsRealtime(1f);
            }

            // 5. Корабль игрока рассыпается в пыль, «YOU WERE NEVER HERE»
            yield return ShipToDust();
            for (int k = 1; k <= impostorCaptionText.Length; k++)
            {
                impostorCaption.text = impostorCaptionText.Substring(0, k) + (k < impostorCaptionText.Length ? "_" : "");
                SpinvUiSound.TypeTick(impostorCaptionText[k - 1]);
                yield return new WaitForSecondsRealtime(0.07f);
            }
            yield return new WaitForSecondsRealtime(0.6f);

            // 6. Выключение автомата и выход
            if (quitEffect != null) quitEffect.Play(QuitGame);
            else QuitGame();
        }

        // В окне диалога: «???:» цветом говорящего по буквам, пауза, затем реплика с курсором «_»
        IEnumerator TypeDialog(string message)
        {
            const string speaker = "<color=#48D46C>???:</color> ";
            for (int k = 1; k <= 4; k++)
            {
                impostorLine.text = "<color=#48D46C>" + "???:".Substring(0, k) + "</color>";
                SpinvUiSound.TypeTick("???:"[k - 1]);
                yield return new WaitForSecondsRealtime(0.11f);
            }
            yield return new WaitForSecondsRealtime(0.35f);
            for (int k = 1; k <= message.Length; k++)
            {
                impostorLine.text = speaker + message.Substring(0, k) + (k < message.Length ? "_" : "");
                SpinvUiSound.TypeTick(message[k - 1]);
                yield return new WaitForSecondsRealtime(0.095f);
            }
        }

        // В начале печати таблицы: открывается аркада, «???: Arcade mode unlocked.», по тапу печать продолжается
        IEnumerator ArcadeUnlockedDialog()
        {
            ArcadeMode.Unlock();
            yield return new WaitForSecondsRealtime(0.3f);

            impostorDialog.SetActive(true);
            yield return TypeDialog(arcadeMessage);

            // Если палец уже на экране, сначала его надо отпустить - ждём нового касания (или Space/Enter)
            bool wasPressed = true;
            while (true)
            {
                bool pressed = GameInput.PointerHeld(out _);
                if ((pressed && !wasPressed) || ConfirmKeyPressed()) break;
                wasPressed = pressed;
                if (dialogTapHint != null)
                    dialogTapHint.text = Mathf.Repeat(Time.unscaledTime, 0.7f) < 0.35f ? "TAP" : "";
                yield return null;
            }
            SpinvUiSound.Click(); // TAP - щелчок, как у кнопки
            if (dialogTapHint != null) dialogTapHint.text = "";
            impostorDialog.SetActive(false);
        }

        static bool ConfirmKeyPressed()
        {
    #if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
    #else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
    #endif
        }

        // Корабль появляется (мигнув), вздрагивает и рассыпается: картинка режется на сетку кусочков,
        // они по очереди слева направо улетают ветром вверх-вправо и тают
        IEnumerator ShipToDust()
        {
            var ship = impostorShip;
            ship.gameObject.SetActive(true);
            for (int i = 0; i < 6; i++)
            {
                ship.enabled = i % 2 == 1;
                yield return new WaitForSecondsRealtime(0.07f);
            }
            ship.enabled = true;
            yield return new WaitForSecondsRealtime(0.4f);

            var shipRect = ship.rectTransform;
            Vector2 home = shipRect.anchoredPosition;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float s = 12f * (1f - t / 0.3f);
                shipRect.anchoredPosition = home + new Vector2(Random.Range(-s, s), Random.Range(-s, s) * 0.5f);
                yield return null;
            }
            shipRect.anchoredPosition = home;

            // Сетка кусочков с той же картинкой: каждый показывает свой участок текстуры
            Sprite sprite = ship.sprite;
            Texture tex = sprite.texture;
            Rect texRect = sprite.textureRect;
            Vector2 size = shipRect.rect.size;
            int cols = Mathf.Max(1, dustColumns), rows = Mathf.Max(1, dustRows);
            var pieces = new List<(RectTransform rect, RawImage image, Vector2 start, Vector2 drift, float delay)>();
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    var go = new GameObject("Dust", typeof(RectTransform), typeof(RawImage));
                    go.transform.SetParent(shipRect, false);
                    var rect = (RectTransform)go.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(size.x / cols, size.y / rows);
                    Vector2 start = new Vector2((x + 0.5f) / cols * size.x - size.x / 2f, (y + 0.5f) / rows * size.y - size.y / 2f);
                    rect.anchoredPosition = start;
                    var image = go.GetComponent<RawImage>();
                    image.texture = tex;
                    image.raycastTarget = false;
                    image.uvRect = new Rect(
                        (texRect.x + texRect.width * x / cols) / tex.width,
                        (texRect.y + texRect.height * y / rows) / tex.height,
                        texRect.width / cols / tex.width,
                        texRect.height / rows / tex.height);
                    Vector2 drift = new Vector2(Random.Range(480f, 960f), Random.Range(160f, 480f));
                    float delay = 0.2f + (float)x / cols * 0.9f + Random.Range(0f, 0.3f);
                    pieces.Add((rect, image, start, drift, delay));
                }
            }
            ship.enabled = false; // дальше видны только кусочки

            const float flyTime = 1f;
            float total = 0.2f + 0.9f + 0.3f + flyTime;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                foreach (var p in pieces)
                {
                    float k = Mathf.Clamp01((t - p.delay) / flyTime);
                    float e = k * k; // разгоняются, как от порыва ветра
                    p.rect.anchoredPosition = p.start + p.drift * e;
                    p.image.color = new Color(1f, 1f, 1f, 1f - k);
                }
                yield return null;
            }
            foreach (var p in pieces) Destroy(p.rect.gameObject);
        }

        // Обновляет только строку игрока: остальные уже напечатаны
        void RefreshTable()
        {
            if (!tableReady || gm.TableIndex < 0 || gm.TableIndex >= nameTexts.Length) return;
            TMP_Text name = nameTexts[gm.TableIndex];

            if (!editing)
            {
                name.text = gm.TableIndex < table.Count ? Colored(table[gm.TableIndex].initials) : "---";
                return;
            }

            // Пока вводят: набранное и мигающий «_» на месте следующей буквы.
            // Пока ничего не набрано - знаки вопроса
            bool cursorVisible = Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.3f;
            string typed = TypedInitials();
            if (typed.Length == 0)
            {
                name.text = HighScoreTable.UnnamedPlayer;
                return;
            }
            string cursor = typed.Length < MaxInitials ? (cursorVisible ? "_" : "<alpha=#00>_</alpha>") : "";
            name.text = typed + cursor;
        }

        // Сохраняет имя (OK, Enter / «Готово» на клавиатуре), выключает автомат и закрывает игру
        public void FinishInitials()
        {
            if (!editing || finished) return;
            string typed = TypedInitials();
            if (typed.Length == 0) return; // пустое имя не принимаем (и Enter тоже)
            if (HighScoreTable.IsReservedName(typed))
            {
                // Попытка выдать себя за автора
                finished = true;
                StartCoroutine(Impostor(typed));
                return;
            }
            editing = false;
            finished = true;
            initialsInput.DeactivateInputField();
            initialsPanel.SetActive(false);

            // Результат уже в таблице с прошлым именем - меняем имя и сохраняем
            gm.RenameRecord(typed);
            PlayerPrefs.Save();
            table = HighScoreTable.Load();
            RefreshTable();

            if (quitEffect != null) quitEffect.Play(QuitGame);
            else QuitGame();
        }

        static void QuitGame()
        {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
            Application.Quit();
    #endif
        }

        static void SetBlink(TMP_Text text, bool on)
        {
            var blink = text.GetComponent<BlinkGraphic>();
            if (blink != null) blink.enabled = on;
        }
    }
}
