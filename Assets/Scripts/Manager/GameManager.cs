using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ゲーム全体の管理
/// スコア・ターン管理、リセットボタン表示制御、UI更新を行う
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("参照")]
    [SerializeField] private PuzzleManager puzzleManager;

    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI lastCombineText;
    [SerializeField] private Button resetButton;

    /// <summary>
    /// スコア変更イベント
    /// </summary>
    public event Action<int> OnScoreChanged;

    private int currentScore = 0;

    /// <summary>
    /// 現在のスコア
    /// </summary>
    public int CurrentScore => currentScore;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (puzzleManager != null)
        {
            puzzleManager.OnCombineSuccess += HandleCombineSuccess;
            puzzleManager.OnEliminationSuccess += HandleEliminationSuccess;
            puzzleManager.OnTurnChanged += HandleTurnChanged;
            puzzleManager.OnDeadlockChanged += HandleDeadlockChanged;
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetButtonClicked);
            resetButton.gameObject.SetActive(false);
        }

        UpdateScoreUI();
        UpdateTurnUI(0);
    }

    /// <summary>
    /// 合体成功時の処理（スコア加算）
    /// </summary>
    private void HandleCombineSuccess(KanjiRecipe recipe)
    {
        currentScore += recipe.score;
        UpdateScoreUI();

        if (lastCombineText != null)
        {
            lastCombineText.text = $"{recipe.materialA} + {recipe.materialB} = {recipe.result}  (+{recipe.score}点)";
        }
    }

    /// <summary>
    /// 同種消去成功時の処理（スコア加算）
    /// </summary>
    private void HandleEliminationSuccess(string kanji, int score)
    {
        currentScore += score;
        UpdateScoreUI();

        if (lastCombineText != null)
        {
            lastCombineText.text = $"{kanji} + {kanji} → 消滅！ (+{score}点)";
        }
    }

    /// <summary>
    /// ターン変更時の処理
    /// </summary>
    private void HandleTurnChanged(int turn)
    {
        UpdateTurnUI(turn);
    }

    /// <summary>
    /// デッドロック状態変更時の処理
    /// </summary>
    private void HandleDeadlockChanged(bool isDeadlocked)
    {
        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(isDeadlocked);
        }

        if (isDeadlocked && lastCombineText != null)
        {
            lastCombineText.text = "合体できるペアがありません！";
        }
    }

    /// <summary>
    /// リセットボタンが押された
    /// </summary>
    private void OnResetButtonClicked()
    {
        if (puzzleManager != null)
        {
            puzzleManager.ResetBoard();
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"スコア: {currentScore}";
        }
        OnScoreChanged?.Invoke(currentScore);
    }

    private void UpdateTurnUI(int turn)
    {
        if (turnText != null)
        {
            turnText.text = $"ターン: {turn}";
        }
    }

    private void OnDestroy()
    {
        if (puzzleManager != null)
        {
            puzzleManager.OnCombineSuccess -= HandleCombineSuccess;
            puzzleManager.OnEliminationSuccess -= HandleEliminationSuccess;
            puzzleManager.OnTurnChanged -= HandleTurnChanged;
            puzzleManager.OnDeadlockChanged -= HandleDeadlockChanged;
        }
    }
}
