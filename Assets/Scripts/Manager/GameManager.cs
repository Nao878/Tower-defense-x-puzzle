using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// ゲーム全体の管理
/// リアルタイム・タイムアタック制：60秒カウントダウン、合体で+3秒回復
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

    [Header("ハイスコア")]
    [SerializeField] private TextMeshProUGUI highScoreText;

    private const string HIGH_SCORE_KEY = "KanjiPuzzle_HighScore";

    private int score = 0;
    private int highScore = 0;
    private float remainingTime;
    private bool isGameOver = false;
    private bool isStalemateState = false;
    private Coroutine flashCoroutine;
    private Coroutine popupCoroutine;
    private Color timeNormalColor = new Color(0.2f, 0.15f, 0.1f);
    private Color timeWarningColor = new Color(0.9f, 0.15f, 0.1f);

    public int Score => score;
    public float RemainingTime => remainingTime;
    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        remainingTime = initialTime;

        // ハイスコア読込
        highScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);

        if (puzzleManager != null)
        {
            puzzleManager.OnCombineSuccess += HandleCombineSuccess;
            puzzleManager.OnEliminationSuccess += HandleEliminationSuccess;
            puzzleManager.OnStalemateChanged += HandleStalemateChanged;
        }

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetButtonClicked);

        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmYes);

        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryButtonClicked);

        if (confirmResetPanel != null)
            confirmResetPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (timeBonusPopup != null)
            timeBonusPopup.gameObject.SetActive(false);

        UpdateScoreUI();
        UpdateHighScoreUI();
        UpdateTimeUI();
    }

    private void Update()
    {
        if (isGameOver) return;

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

        UpdateScoreUI();
    }

    private void HandleEliminationSuccess(string kanji, int elimScore)
    {
        score += elimScore;
        AddTime(mergeTimeBonus);

        if (lastCombineText != null)
            lastCombineText.text = $"{kanji} + {kanji} → 消滅！ (+{elimScore}点)";

        UpdateScoreUI();
    }

    private void HandleStalemateChanged(bool isStalemate)
    {
        isStalemateState = isStalemate;

        if (isStalemate)
        {
            if (lastCombineText != null)
                lastCombineText.text = "手詰まり！ リセットボタンを押してください";

            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashResetButton());

            if (resetButtonLabel != null)
                resetButtonLabel.text = "手詰まり！\nリセット";
        }
        else
        {
            if (flashCoroutine != null) { StopCoroutine(flashCoroutine); flashCoroutine = null; }

            if (resetButtonImage != null)
                resetButtonImage.color = new Color(0.5f, 0.5f, 0.55f);

            if (resetButtonLabel != null)
                resetButtonLabel.text = "リセット";
        }
    }

    // ============================================================
    // 時間管理
    // ============================================================

    /// <summary>
    /// 時間を加算する（合体ボーナス）
    /// </summary>
    private void AddTime(float seconds)
    {
        if (isGameOver) return;

        remainingTime += seconds;
        ShowTimeBonusPopup($"+{seconds:F0}sec");
    }

    /// <summary>
    /// 時間を減算する（リセットペナルティ）
    /// </summary>
    private void SubtractTime(float seconds)
    {
        if (isGameOver) return;

        remainingTime = Mathf.Max(0f, remainingTime - seconds);
        ShowTimeBonusPopup($"-{seconds:F0}sec");

        if (remainingTime <= 0f)
        {
            TriggerGameOver();
        }
    }

    // ============================================================
    // ゲームオーバー
    // ============================================================

    private void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        // PuzzleManagerの入力を無効化
        if (puzzleManager != null)
            puzzleManager.SetGameOver(true);

        // リセットボタンを無効化
        if (resetButton != null)
            resetButton.interactable = false;

        // ハイスコア更新
        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt(HIGH_SCORE_KEY, highScore);
            PlayerPrefs.Save();
        }
        UpdateHighScoreUI();

        // ゲームオーバーパネル表示
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            string highScoreNote = score >= highScore ? "\nNEW RECORD!" : "";
            if (gameOverScoreText != null)
                gameOverScoreText.text = $"TIME UP!\n\n最終スコア: {score}{highScoreNote}";
        }

        Debug.Log($"[GameManager] ゲームオーバー！ 最終スコア: {score}");
    }

    /// <summary>
    /// リトライ：ゲーム全体をリセットして再開
    /// </summary>
    private void OnRetryButtonClicked()
    {
        // 状態リセット
        score = 0;
        remainingTime = initialTime;
        isGameOver = false;
        isStalemateState = false;

        // UI非表示
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // リセットボタン再有効化
        if (resetButton != null)
            resetButton.interactable = true;

        if (resetButtonImage != null)
            resetButtonImage.color = new Color(0.5f, 0.5f, 0.55f);

        if (resetButtonLabel != null)
            resetButtonLabel.text = "リセット";

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        // PuzzleManagerを再開
        if (puzzleManager != null)
        {
            puzzleManager.SetGameOver(false);
            puzzleManager.ResetBoard();
        }

        if (lastCombineText != null)
            lastCombineText.text = "";

        UpdateScoreUI();
        UpdateHighScoreUI();
        UpdateTimeUI();
    }

    // ============================================================
    // リセットボタン
    // ============================================================

    private void OnResetButtonClicked()
    {
        if (isGameOver) return;

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
        if (scoreText != null)
            scoreText.text = $"スコア: {score}";
    }

    private void UpdateHighScoreUI()
    {
        if (highScoreText != null)
            highScoreText.text = $"Best: {highScore}";
    }

    private void UpdateTimeUI()
    {
        if (timeText != null)
        {
            timeText.text = $"Time: {remainingTime:F2}";

            // 残り10秒以下で赤文字
            timeText.color = remainingTime <= 10f ? timeWarningColor : timeNormalColor;

            // 残り10秒以下でスケール演出
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

    /// <summary>
    /// +Xsec / -Xsec ポップアップ演出
    /// </summary>
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

            // 上に浮かんで消える
            timeBonusPopup.rectTransform.anchoredPosition =
                startPos + new Vector3(0, 40f * t, 0);

            // フェードアウト
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
            Color colorA = new Color(0.9f, 0.2f, 0.15f);
            Color colorB = new Color(0.6f, 0.1f, 0.1f);

            if (resetButtonImage != null)
                resetButtonImage.color = Color.Lerp(colorA, colorB, lerp);

            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (puzzleManager != null)
        {
            puzzleManager.OnCombineSuccess -= HandleCombineSuccess;
            puzzleManager.OnEliminationSuccess -= HandleEliminationSuccess;
            puzzleManager.OnStalemateChanged -= HandleStalemateChanged;
        }
    }
}
