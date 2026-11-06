using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Аркадный экран победы после финального босса. Всё печатается по очереди:
// шапка, заголовок, подсчёт очков (числа «накручиваются»), NEW HIGH SCORE,
// таблица TOP 5, ввод имени (до 8 символов с клавиатуры), кнопки MENU и RESTART.
// Всё на реальном времени: игра стоит на Time.timeScale = 0
public class VictoryScreen : MonoBehaviour
{
    [Header("Шапка")]
    public TMP_Text oneUpLabel;
    public TMP_Text topScoreText;
    public TMP_Text hiLabel;
    public TMP_Text topBestText;
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

    [Header("Кнопки")]
    public PressableButton menuButton;
    public PressableButton restartButton;

    [Header("Скорость печати")]
    public float titleCharTime = 0.06f;
    public float charTime = 0.03f;
    public float pauseBetweenLines = 0.08f;

    const int MaxInitials = 8; // имя в таблице рекордов: до 8 символов
    static readonly string[] Ranks = { "1ST", "2ND", "3RD", "4TH", "5TH" };

    GameManager gm;
    bool editing;
    bool tableReady;
    List<HighScoreTable.Entry> table;

    void Awake()
    {
        initialsInput.characterLimit = MaxInitials;
        // Только латиница и цифры, сразу заглавными
        initialsInput.onValidateInput = (text, index, c) =>
            c < 128 && char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
        initialsInput.onValueChanged.AddListener(_ => RefreshTable());
        initialsInput.onSubmit.AddListener(_ => FinishInitials());
        initialsOkButton.onPress.AddListener(FinishInitials);
    }

    public void Show(GameManager manager)
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
        menuButton.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(false);
        menuButton.interactable = restartButton.interactable = false;
        editing = tableReady = false;

        StartCoroutine(Play());
    }

    IEnumerable<TMP_Text> AllTexts()
    {
        yield return oneUpLabel; yield return topScoreText; yield return hiLabel; yield return topBestText;
        yield return titleText; yield return subtitleText;
        yield return scoreLabel; yield return scoreValue; yield return livesLabel; yield return livesValue;
        yield return coinsLabel; yield return coinsValue; yield return totalLabel; yield return totalValue;
        yield return newHighScore; yield return tableTitle; yield return initialsHint;
        yield return menuButton.label; yield return restartButton.label; yield return initialsOkButton.label;
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

        // Шапка автомата
        yield return Type(oneUpLabel, "1UP");
        yield return Type(topScoreText, "00000");
        yield return Type(hiLabel, "HI");
        yield return Type(topBestText, gm.PreviousBest.ToString("00000"));

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

        topScoreText.text = gm.Score.ToString("00000");
        if (gm.Score > gm.PreviousBest)
        {
            topBestText.text = gm.Score.ToString("00000");
            newHighScore.gameObject.SetActive(true);
            yield return Type(newHighScore, "NEW HIGH SCORE!");
            SetBlink(newHighScore, true);
        }

        // Таблица TOP 5, строка за строкой
        table = HighScoreTable.Load();
        tableRoot.SetActive(true);
        yield return Type(tableTitle, "- TOP 5 -");
        for (int i = 0; i < nameTexts.Length; i++)
        {
            rowHighlights[i].enabled = i == gm.TableIndex;
            bool filled = i < table.Count;
            yield return Type(rankTexts[i], Ranks[Mathf.Min(i, Ranks.Length - 1)]);
            yield return Type(nameTexts[i], filled ? table[i].initials : "---");
            yield return Type(scoreTexts[i], filled ? table[i].score.ToString("00000") : "00000");
        }
        tableReady = true;

        // Ввод имени, если попали в топ
        if (gm.TableIndex >= 0)
        {
            initialsPanel.SetActive(true);
            yield return Type(initialsHint, "ENTER YOUR NAME");
            initialsInput.gameObject.SetActive(true);
            initialsInput.text = HighScoreTable.LastInitials;
            initialsOkButton.gameObject.SetActive(true);
            yield return Type(initialsOkButton.label, "OK");
            editing = true;
            // Фокус в поле: на телефоне выезжает клавиатура
            initialsInput.Select();
            initialsInput.ActivateInputField();
            RefreshTable();
        }

        menuButton.gameObject.SetActive(true);
        yield return Type(menuButton.label, "MENU");
        restartButton.gameObject.SetActive(true);
        yield return Type(restartButton.label, "RESTART");
        menuButton.interactable = restartButton.interactable = true;
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

    // Обновляет только строку игрока: остальные уже напечатаны
    void RefreshTable()
    {
        if (!tableReady || gm.TableIndex < 0 || gm.TableIndex >= nameTexts.Length) return;
        TMP_Text name = nameTexts[gm.TableIndex];

        if (!editing)
        {
            name.text = gm.TableIndex < table.Count ? table[gm.TableIndex].initials : "---";
            return;
        }

        // Пока вводят: набранное и мигающий «_» на месте следующей буквы
        bool cursorVisible = Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.3f;
        string typed = TypedInitials();
        string cursor = typed.Length < MaxInitials ? (cursorVisible ? "_" : "<alpha=#00>_</alpha>") : "";
        name.text = typed + cursor;
    }

    // Сохраняет имя. Вызывается OK, Enter / «Готово» на клавиатуре
    // и кнопками MENU/RESTART, если ввод не закончили
    public void FinishInitials()
    {
        if (!editing) return;
        editing = false;

        string typed = TypedInitials();
        if (typed.Length == 0) typed = HighScoreTable.LastInitials;
        initialsInput.DeactivateInputField();
        initialsPanel.SetActive(false);

        gm.RenameRecord(typed);
        table = HighScoreTable.Load();
        RefreshTable();
    }

    static void SetBlink(TMP_Text text, bool on)
    {
        var blink = text.GetComponent<BlinkGraphic>();
        if (blink != null) blink.enabled = on;
    }
}
