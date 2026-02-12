using System;
using UnityEngine;
using TMPro;

/// <summary>
/// ゲーム全体の管理
/// スコア・ターン管理とUI更新を行う
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
            puzzleManager.OnTurnChanged += HandleTurnChanged;
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

        Debug.Log($"[GameManager] スコア加算: +{recipe.score} (合計: {currentScore})");
    }

    /// <summary>
    /// ターン変更時の処理
    /// </summary>
    private void HandleTurnChanged(int turn)
    {
        UpdateTurnUI(turn);
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
            puzzleManager.OnTurnChanged -= HandleTurnChanged;
        }
    }
}
