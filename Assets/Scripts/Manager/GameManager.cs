using System;
using UnityEngine;

/// <summary>
/// ゲーム状態
/// </summary>
public enum GameState
{
    Preparing,  // 準備中
    Playing,    // プレイ中
    GameOver,   // ゲームオーバー
    Victory     // クリア
}

/// <summary>
/// ゲーム全体のフロー管理
/// HP、ウェーブ進行、ゲーム状態を統括する
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("参照")]
    [SerializeField] private PuzzleManager puzzleManager;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private UnitSpawner unitSpawner;

    [Header("ゲーム設定")]
    [Tooltip("拠点の初期耐久値")]
    [SerializeField] private float maxBaseHp = 100f;

    /// <summary>
    /// ゲーム状態変更イベント
    /// </summary>
    public event Action<GameState> OnGameStateChanged;

    /// <summary>
    /// 拠点HP変更イベント
    /// </summary>
    public event Action<float, float> OnBaseHpChanged; // (currentHp, maxHp)

    private float currentBaseHp;
    private GameState currentState = GameState.Preparing;

    /// <summary>
    /// 現在のゲーム状態
    /// </summary>
    public GameState CurrentState => currentState;

    /// <summary>
    /// 現在の拠点HP
    /// </summary>
    public float CurrentBaseHp => currentBaseHp;

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
        currentBaseHp = maxBaseHp;

        // イベント接続
        if (enemySpawner != null)
        {
            enemySpawner.OnEnemyReachedGoal += HandleEnemyReachedGoal;
            enemySpawner.OnAllWavesComplete += HandleAllWavesComplete;
        }

        // ゲーム開始
        StartGame();
    }

    /// <summary>
    /// ゲームを開始する
    /// </summary>
    public void StartGame()
    {
        currentBaseHp = maxBaseHp;
        ChangeState(GameState.Playing);

        if (enemySpawner != null)
        {
            enemySpawner.StartWaves();
        }

        Debug.Log("[GameManager] ゲーム開始！");
    }

    /// <summary>
    /// 敵がゴールに到達した時の処理
    /// </summary>
    private void HandleEnemyReachedGoal(EnemyController enemy)
    {
        currentBaseHp -= 10f; // 敵がゴール到達するたびに10ダメージ
        OnBaseHpChanged?.Invoke(currentBaseHp, maxBaseHp);

        Debug.Log($"[GameManager] 拠点がダメージを受けた！残りHP: {currentBaseHp}");

        if (currentBaseHp <= 0f)
        {
            ChangeState(GameState.GameOver);
            Debug.Log("[GameManager] ゲームオーバー！");
        }
    }

    /// <summary>
    /// 全ウェーブ終了時の処理
    /// </summary>
    private void HandleAllWavesComplete()
    {
        if (currentState == GameState.Playing)
        {
            ChangeState(GameState.Victory);
            Debug.Log("[GameManager] 勝利！");
        }
    }

    /// <summary>
    /// ゲーム状態を変更する
    /// </summary>
    private void ChangeState(GameState newState)
    {
        currentState = newState;
        OnGameStateChanged?.Invoke(newState);
    }

    private void OnDestroy()
    {
        if (enemySpawner != null)
        {
            enemySpawner.OnEnemyReachedGoal -= HandleEnemyReachedGoal;
            enemySpawner.OnAllWavesComplete -= HandleAllWavesComplete;
        }
    }
}
