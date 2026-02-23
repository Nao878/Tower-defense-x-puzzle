using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// ゲーム全体の管理
/// タイトル画面・デュアルモード・タイムアタック制
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("参照")]
    [SerializeField] private PuzzleManager puzzleManager;

    [Header("時間設定")]
    [SerializeField] private float initialTime = 60f;
    [SerializeField] private float mergeTimeBonus = 3f;
    [SerializeField] private float resetTimePenalty = 10f;

    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI timeBonusPopup;
    [SerializeField] private TextMeshProUGUI lastCombineText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI modeLabel;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI resetButtonLabel;
    [SerializeField] private Image resetButtonImage;

    [Header("確認ダイアログ")]
    [SerializeField] private GameObject confirmResetPanel;
    [SerializeField] private TextMeshProUGUI confirmResetText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("ゲームオーバー")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverScoreText;
    [SerializeField] private Button retryButton;

    [Header("タイトル画面")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private Button classicModeButton;
    [SerializeField] private Button actionModeButton;
    [SerializeField] private TextMeshProUGUI titleClassicHighScore;
    [SerializeField] private TextMeshProUGUI titleActionHighScore;

    [Header("ハイスコア")]
    [SerializeField] private TextMeshProUGUI highScoreText;

    [Header("パーティクル")]
    [SerializeField] private ParticleSystem mergeParticle;

    private const string HIGH_SCORE_CLASSIC = "HighScore_Classic";
    private const string HIGH_SCORE_ACTION = "HighScore_Action";

    private int score = 0;
    private int highScore = 0;
    private float remainingTime;
    private bool isGameOver = false;
    private bool isPlaying = false;
    private GameMode currentMode = GameMode.Classic;
    private Coroutine flashCoroutine;
    private Coroutine popupCoroutine;
    private Coroutine comboCoroutine;
    private Color timeNormalColor = new Color(0.2f, 0.15f, 0.1f);
    private Color timeWarningColor = new Color(0.9f, 0.15f, 0.1f);

    public int Score => score;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (puzzleManager != null)
        {
            puzzleManager.OnCombineSuccess += HandleCombineSuccess;
            puzzleManager.OnEliminationSuccess += HandleEliminationSuccess;
            puzzleManager.OnComboChanged += HandleComboChanged;
            puzzleManager.OnAutoShuffle += HandleAutoShuffle;
        }

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetButtonClicked);

        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmYes);

        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryButtonClicked);

        if (classicModeButton != null)
            classicModeButton.onClick.AddListener(() => StartGameWithMode(GameMode.Classic));

        if (actionModeButton != null)
            actionModeButton.onClick.AddListener(() => StartGameWithMode(GameMode.Action));

        // 初期状態: タイトル画面表示
        ShowTitleScreen();
    }

    // ============================================================
    // タイトル画面
    // ============================================================

    private void ShowTitleScreen()
    {
        isPlaying = false;
        isGameOver = false;

        if (titlePanel != null)
            titlePanel.SetActive(true);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (confirmResetPanel != null)
            confirmResetPanel.SetActive(false);

        if (comboText != null)
            comboText.gameObject.SetActive(false);

        if (timeBonusPopup != null)
            timeBonusPopup.gameObject.SetActive(false);

        // ゲームUI非表示
        SetGameUIVisible(false);

        // タイトル画面にハイスコア表示
        int classicHigh = PlayerPrefs.GetInt(HIGH_SCORE_CLASSIC, 0);
        int actionHigh = PlayerPrefs.GetInt(HIGH_SCORE_ACTION, 0);

        if (titleClassicHighScore != null)
            titleClassicHighScore.text = $"Best: {classicHigh}";
        if (titleActionHighScore != null)
            titleActionHighScore.text = $"Best: {actionHigh}";
    }

    private void SetGameUIVisible(bool visible)
    {
        if (scoreText != null) scoreText.gameObject.SetActive(visible);
        if (timeText != null) timeText.gameObject.SetActive(visible);
        if (lastCombineText != null) lastCombineText.gameObject.SetActive(visible);
        if (resetButton != null) resetButton.gameObject.SetActive(visible);
        if (highScoreText != null) highScoreText.gameObject.SetActive(visible);
        if (modeLabel != null) modeLabel.gameObject.SetActive(visible);
    }

    private void StartGameWithMode(GameMode mode)
    {
        currentMode = mode;

        // タイトル非表示
        if (titlePanel != null)
            titlePanel.SetActive(false);

        // ゲームUI表示
        SetGameUIVisible(true);

        // 状態リセット
        score = 0;
        remainingTime = initialTime;
        isGameOver = false;
        isPlaying = true;

        string highScoreKey = mode == GameMode.Classic ? HIGH_SCORE_CLASSIC : HIGH_SCORE_ACTION;
        highScore = PlayerPrefs.GetInt(highScoreKey, 0);

        if (modeLabel != null)
            modeLabel.text = mode == GameMode.Classic ? "Classic" : "Action";

        if (lastCombineText != null)
            lastCombineText.text = "";

        UpdateScoreUI();
        UpdateHighScoreUI();
        UpdateTimeUI();

        // PuzzleManager開始
        if (puzzleManager != null)
        {
            puzzleManager.SetGameOver(false);
            puzzleManager.StartGame(mode);
        }
    }

    private void Update()
    {
        if (!isPlaying || isGameOver) return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            TriggerGameOver();
        }

        UpdateTimeUI();
    }

    // ============================================================
    // イベントハンドラ
    // ============================================================

    private void HandleCombineSuccess(KanjiRecipe recipe)
    {
        score += recipe.score;
        AddTime(mergeTimeBonus);

        if (lastCombineText != null)
            lastCombineText.text = $"{recipe.materialA} + {recipe.materialB} = {recipe.result}  (+{recipe.score}点)";

        PlayMergeParticle();
        UpdateScoreUI();
    }

    private void HandleEliminationSuccess(string kanji, int elimScore)
    {
        score += elimScore;
        AddTime(mergeTimeBonus);

        if (lastCombineText != null)
            lastCombineText.text = $"{kanji} → 消滅！ (+{elimScore}点)";

        PlayMergeParticle();
        UpdateScoreUI();
    }

    private void HandleComboChanged(int combo)
    {
        if (combo == 0)
        {
            if (comboCoroutine != null) StopCoroutine(comboCoroutine);
            if (comboText != null) comboText.gameObject.SetActive(false);
            return;
        }

        if (combo >= 2)
        {
            if (comboCoroutine != null) StopCoroutine(comboCoroutine);
            comboCoroutine = StartCoroutine(ShowComboPopup(combo));
        }
    }

    private IEnumerator ShowComboPopup(int combo)
    {
        if (comboText == null) yield break;

        comboText.gameObject.SetActive(true);
        comboText.text = $"{combo} Combo!";

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.5f;
            comboText.transform.localScale = Vector3.one * scale;
            float hue = (Time.time * 0.5f) % 1f;
            comboText.color = Color.HSVToRGB(hue, 0.8f, 1f);
            yield return null;
        }

        comboText.transform.localScale = Vector3.one;
    }

    private void HandleAutoShuffle()
    {
        if (comboCoroutine != null) StopCoroutine(comboCoroutine);
        comboCoroutine = StartCoroutine(ShowShufflePopup());
    }

    private IEnumerator ShowShufflePopup()
    {
        if (comboText == null) yield break;

        comboText.gameObject.SetActive(true);
        comboText.text = "Shuffle!";
        comboText.color = new Color(0.2f, 0.8f, 1f);

        float duration = 0.6f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.6f;
            comboText.transform.localScale = Vector3.one * scale;
            Color c = comboText.color;
            c.a = 1f - t * 0.3f;
            comboText.color = c;
            yield return null;
        }

        comboText.transform.localScale = Vector3.one;
        comboText.gameObject.SetActive(false);
    }

    // ============================================================
    // 時間管理
    // ============================================================

    private void AddTime(float seconds)
    {
        if (isGameOver) return;
        remainingTime += seconds;
        ShowTimeBonusPopup($"+{seconds:F0}sec");
    }

    private void SubtractTime(float seconds)
    {
        if (isGameOver) return;
        remainingTime = Mathf.Max(0f, remainingTime - seconds);
        ShowTimeBonusPopup($"-{seconds:F0}sec");
        if (remainingTime <= 0f) TriggerGameOver();
    }

    // ============================================================
    // パーティクル
    // ============================================================

    private void PlayMergeParticle()
    {
        if (mergeParticle != null)
        {
            mergeParticle.Stop();
            mergeParticle.Play();
        }
    }

    // ============================================================
    // ゲームオーバー
    // ============================================================

    private void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        isPlaying = false;

        if (puzzleManager != null)
            puzzleManager.SetGameOver(true);

        if (resetButton != null)
            resetButton.interactable = false;

        string highScoreKey = currentMode == GameMode.Classic ? HIGH_SCORE_CLASSIC : HIGH_SCORE_ACTION;
        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt(highScoreKey, highScore);
            PlayerPrefs.Save();
        }
        UpdateHighScoreUI();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            string modeName = currentMode == GameMode.Classic ? "Classic" : "Action";
            string highScoreNote = score >= highScore ? "\nNEW RECORD!" : "";
            if (gameOverScoreText != null)
                gameOverScoreText.text = $"TIME UP! [{modeName}]\n\n最終スコア: {score}{highScoreNote}";
        }
    }

    private void OnRetryButtonClicked()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // タイトル画面に戻る
        ShowTitleScreen();
    }

    // ============================================================
    // リセットボタン
    // ============================================================

    private void OnResetButtonClicked()
    {
        if (isGameOver || !isPlaying) return;

        if (confirmResetPanel == null)
        {
            puzzleManager?.ResetBoard();
            SubtractTime(resetTimePenalty);
            return;
        }

        if (confirmResetText != null)
            confirmResetText.text = $"盤面を入れ替えますか？\n（ペナルティ: -{resetTimePenalty:F0}秒）";

        confirmResetPanel.SetActive(true);
    }

    private void OnConfirmYes()
    {
        if (confirmResetPanel != null)
            confirmResetPanel.SetActive(false);

        puzzleManager?.ResetBoard();
        SubtractTime(resetTimePenalty);
    }

    private void OnConfirmNo()
    {
        if (confirmResetPanel != null)
            confirmResetPanel.SetActive(false);
    }

    // ============================================================
    // UI更新
    // ============================================================

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = $"スコア: {score}";
    }

    private void UpdateHighScoreUI()
    {
        if (highScoreText != null) highScoreText.text = $"Best: {highScore}";
    }

    private void UpdateTimeUI()
    {
        if (timeText != null)
        {
            timeText.text = $"Time: {remainingTime:F2}";
            timeText.color = remainingTime <= 10f ? timeWarningColor : timeNormalColor;

            if (remainingTime <= 10f)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.05f;
                timeText.transform.localScale = Vector3.one * pulse;
            }
            else
            {
                timeText.transform.localScale = Vector3.one;
            }
        }
    }

    private void ShowTimeBonusPopup(string text)
    {
        if (timeBonusPopup == null) return;
        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(TimeBonusPopupCoroutine(text));
    }

    private IEnumerator TimeBonusPopupCoroutine(string text)
    {
        timeBonusPopup.gameObject.SetActive(true);
        timeBonusPopup.text = text;

        bool isBonus = text.StartsWith("+");
        timeBonusPopup.color = isBonus
            ? new Color(0.1f, 0.7f, 0.2f)
            : new Color(0.9f, 0.2f, 0.1f);

        Vector3 startPos = timeBonusPopup.rectTransform.anchoredPosition;
        float elapsed = 0f;
        float duration = 1.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            timeBonusPopup.rectTransform.anchoredPosition =
                startPos + new Vector3(0, 40f * t, 0);
            Color c = timeBonusPopup.color;
            c.a = 1f - t;
            timeBonusPopup.color = c;
            yield return null;
        }

        timeBonusPopup.gameObject.SetActive(false);
        timeBonusPopup.rectTransform.anchoredPosition = startPos;
    }

    private IEnumerator FlashResetButton()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 3f;
            float lerp = (Mathf.Sin(t) + 1f) / 2f;
            if (resetButtonImage != null)
                resetButtonImage.color = Color.Lerp(
                    new Color(0.9f, 0.2f, 0.15f),
                    new Color(0.6f, 0.1f, 0.1f), lerp);
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (puzzleManager != null)
        {
            puzzleManager.OnCombineSuccess -= HandleCombineSuccess;
            puzzleManager.OnEliminationSuccess -= HandleEliminationSuccess;
            puzzleManager.OnComboChanged -= HandleComboChanged;
            puzzleManager.OnAutoShuffle -= HandleAutoShuffle;
        }
    }
}
