using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ゲーム全体の管理
/// スコア管理、合体確認フローのステート管理、UI更新
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("参照")]
    [SerializeField] private PuzzleManager puzzleManager;

    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI combineInfoText;
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

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
        // イベント接続
        if (puzzleManager != null)
        {
            puzzleManager.OnCombineConfirmRequested += HandleCombineConfirmRequest;
            puzzleManager.OnCombineSuccess += HandleCombineSuccess;
        }

        // ボタンイベント
        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(OnConfirmNo);

        // UI初期化
        HideConfirmPanel();
        UpdateScoreUI();
    }

    /// <summary>
    /// 合体確認リクエストを受け取った時の処理
    /// </summary>
    private void HandleCombineConfirmRequest(CombineMatch match)
    {
        ShowConfirmPanel(match);
    }

    /// <summary>
    /// 合体成功時の処理（スコア加算）
    /// </summary>
    private void HandleCombineSuccess(KanjiRecipe recipe)
    {
        currentScore += recipe.score;
        UpdateScoreUI();
        HideConfirmPanel();
        Debug.Log($"[GameManager] スコア加算: +{recipe.score} (合計: {currentScore})");
    }

    /// <summary>
    /// 確認パネルを表示する
    /// </summary>
    private void ShowConfirmPanel(CombineMatch match)
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }

        if (combineInfoText != null)
        {
            combineInfoText.text = $"{match.recipe.materialA} + {match.recipe.materialB} = {match.recipe.result}\n(+{match.recipe.score}点)";
        }
    }

    /// <summary>
    /// 確認パネルを非表示にする
    /// </summary>
    private void HideConfirmPanel()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 「はい」ボタンが押された
    /// </summary>
    private void OnConfirmYes()
    {
        HideConfirmPanel();
        if (puzzleManager != null)
        {
            puzzleManager.ConfirmCombine();
        }
    }

    /// <summary>
    /// 「いいえ」ボタンが押された
    /// </summary>
    private void OnConfirmNo()
    {
        HideConfirmPanel();
        if (puzzleManager != null)
        {
            puzzleManager.CancelCombine();
        }
    }

    /// <summary>
    /// スコアUIを更新する
    /// </summary>
    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"スコア: {currentScore}";
        }
        OnScoreChanged?.Invoke(currentScore);
    }

    private void OnDestroy()
    {
        if (puzzleManager != null)
        {
            puzzleManager.OnCombineConfirmRequested -= HandleCombineConfirmRequest;
            puzzleManager.OnCombineSuccess -= HandleCombineSuccess;
        }
    }
}
