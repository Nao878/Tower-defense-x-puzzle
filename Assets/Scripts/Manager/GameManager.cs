using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// ゲーム全体の管理
/// スコア（基本+ターンボーナス）・ターン管理、リセットボタン・確認ダイアログ制御
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("参照")]
    [SerializeField] private PuzzleManager puzzleManager;

    [Header("スコア設定")]
    [SerializeField] private int maxBonus = 5000;
    [SerializeField] private int decayRate = 50;

    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private TextMeshProUGUI lastCombineText;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI resetButtonLabel;
    [SerializeField] private Image resetButtonImage;

    [Header("確認ダイアログ")]
    [SerializeField] private GameObject confirmResetPanel;
    [SerializeField] private TextMeshProUGUI confirmResetText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    public event Action<int> OnScoreChanged;

    private int baseScore = 0;
    private int currentTurn = 0;
    private bool isStalemateState = false;
    private Coroutine flashCoroutine;

    public int BaseScore => baseScore;
    public int TurnBonus => Mathf.Max(0, maxBonus - currentTurn * decayRate);
    public int TotalScore => baseScore + TurnBonus;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (puzzleManager != null)
        {
            puzzleManager.OnCombineSuccess += HandleCombineSuccess;
            puzzleManager.OnEliminationSuccess += HandleEliminationSuccess;
            puzzleManager.OnTurnChanged += HandleTurnChanged;
            puzzleManager.OnStalemateChanged += HandleStalemateChanged;
        }

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetButtonClicked);

        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmYes);

        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (confirmResetPanel != null)
            confirmResetPanel.SetActive(false);

        UpdateAllUI();
    }

    private void HandleCombineSuccess(KanjiRecipe recipe)
    {
        baseScore += recipe.score;
        if (lastCombineText != null)
            lastCombineText.text = $"{recipe.materialA} + {recipe.materialB} = {recipe.result}  (+{recipe.score}点)";
        UpdateAllUI();
    }

    private void HandleEliminationSuccess(string kanji, int score)
    {
        baseScore += score;
        if (lastCombineText != null)
            lastCombineText.text = $"{kanji} + {kanji} → 消滅！ (+{score}点)";
        UpdateAllUI();
    }

    private void HandleTurnChanged(int turn)
    {
        currentTurn = turn;
        UpdateAllUI();
    }

    private void HandleStalemateChanged(bool isStalemate)
    {
        isStalemateState = isStalemate;

        if (isStalemate)
        {
            if (lastCombineText != null)
                lastCombineText.text = "手詰まり！ リセットボタンを押してください";

            // リセットボタンを赤く点滅
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

    /// <summary>
    /// リセットボタン赤点滅コルーチン
    /// </summary>
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

    private void OnResetButtonClicked()
    {
        if (confirmResetPanel == null)
        {
            // 確認パネルがなければ直接リセット
            puzzleManager?.ResetBoard();
            return;
        }

        int penalty = puzzleManager != null ? puzzleManager.ResetPenaltyTurns : 5;
        if (confirmResetText != null)
            confirmResetText.text = $"盤面を入れ替えますか？\n（ペナルティ: +{penalty}ターン）";

        confirmResetPanel.SetActive(true);
    }

    private void OnConfirmYes()
    {
        if (confirmResetPanel != null)
            confirmResetPanel.SetActive(false);

        puzzleManager?.ResetBoard();
    }

    private void OnConfirmNo()
    {
        if (confirmResetPanel != null)
            confirmResetPanel.SetActive(false);
    }

    private void UpdateAllUI()
    {
        if (scoreText != null) scoreText.text = $"基本スコア: {baseScore}";
        if (turnText != null) turnText.text = $"ターン: {currentTurn}";
        if (totalScoreText != null)
            totalScoreText.text = $"トータル: {TotalScore}  (ボーナス: {TurnBonus})";
        OnScoreChanged?.Invoke(TotalScore);
    }

    private void OnDestroy()
    {
        if (puzzleManager != null)
        {
            puzzleManager.OnCombineSuccess -= HandleCombineSuccess;
            puzzleManager.OnEliminationSuccess -= HandleEliminationSuccess;
            puzzleManager.OnTurnChanged -= HandleTurnChanged;
            puzzleManager.OnStalemateChanged -= HandleStalemateChanged;
        }
    }
}
