using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ウェーブ内の敵定義
/// </summary>
[Serializable]
public class EnemyWaveEntry
{
    [Tooltip("敵のプレハブ")]
    public GameObject enemyPrefab;

    [Tooltip("この種類の敵の数")]
    public int count = 5;

    [Tooltip("敵のHP")]
    public float hp = 50f;

    [Tooltip("敵の移動速度")]
    public float speed = 2f;

    [Tooltip("各敵のスポーン間隔（秒）")]
    public float spawnInterval = 1f;
}

/// <summary>
/// ウェーブ定義
/// </summary>
[Serializable]
public class Wave
{
    [Tooltip("ウェーブ名")]
    public string waveName = "Wave 1";

    [Tooltip("このウェーブの敵リスト")]
    public List<EnemyWaveEntry> enemies = new List<EnemyWaveEntry>();
}

/// <summary>
/// 敵のウェーブ生成を管理する
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("ウェーブ設定")]
    [SerializeField] private List<Wave> waves = new List<Wave>();

    [Tooltip("ウェーブ間の待機時間（秒）")]
    [SerializeField] private float timeBetweenWaves = 10f;

    [Header("経路設定")]
    [Tooltip("敵の移動経路（ウェイポイント）の親オブジェクト")]
    [SerializeField] private Transform waypointsParent;

    [Header("スポーン位置")]
    [SerializeField] private Transform spawnPoint;

    /// <summary>
    /// 全ウェーブ終了イベント
    /// </summary>
    public event Action OnAllWavesComplete;

    /// <summary>
    /// ウェーブ開始イベント
    /// </summary>
    public event Action<int> OnWaveStart;

    /// <summary>
    /// 敵がゴールに到達したイベント
    /// </summary>
    public event Action<EnemyController> OnEnemyReachedGoal;

    /// <summary>
    /// 敵が死亡したイベント
    /// </summary>
    public event Action<EnemyController> OnEnemyDeath;

    private Transform[] waypointArray;
    private int currentWaveIndex = 0;
    private int aliveEnemyCount = 0;
    private bool isSpawning = false;

    /// <summary>
    /// 現在のウェーブ番号
    /// </summary>
    public int CurrentWaveIndex => currentWaveIndex;

    /// <summary>
    /// 全ウェーブ数
    /// </summary>
    public int TotalWaves => waves.Count;

    /// <summary>
    /// 生存中の敵の数
    /// </summary>
    public int AliveEnemyCount => aliveEnemyCount;

    private void Awake()
    {
        // ウェイポイントを取得
        if (waypointsParent != null)
        {
            waypointArray = new Transform[waypointsParent.childCount];
            for (int i = 0; i < waypointsParent.childCount; i++)
            {
                waypointArray[i] = waypointsParent.GetChild(i);
            }
        }
    }

    /// <summary>
    /// ウェーブの生成を開始する
    /// </summary>
    public void StartWaves()
    {
        currentWaveIndex = 0;
        StartCoroutine(RunWaves());
    }

    /// <summary>
    /// 次のウェーブを開始する
    /// </summary>
    public void StartNextWave()
    {
        if (currentWaveIndex < waves.Count && !isSpawning)
        {
            StartCoroutine(SpawnWave(waves[currentWaveIndex]));
        }
    }

    private IEnumerator RunWaves()
    {
        while (currentWaveIndex < waves.Count)
        {
            yield return StartCoroutine(SpawnWave(waves[currentWaveIndex]));

            // 全敵が倒されるまで待つ
            while (aliveEnemyCount > 0)
            {
                yield return null;
            }

            currentWaveIndex++;

            if (currentWaveIndex < waves.Count)
            {
                Debug.Log($"[EnemySpawner] 次のウェーブまで {timeBetweenWaves}秒");
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        Debug.Log("[EnemySpawner] 全ウェーブ終了！");
        OnAllWavesComplete?.Invoke();
    }

    private IEnumerator SpawnWave(Wave wave)
    {
        isSpawning = true;
        Debug.Log($"[EnemySpawner] {wave.waveName} 開始");
        OnWaveStart?.Invoke(currentWaveIndex);

        foreach (var entry in wave.enemies)
        {
            if (entry.enemyPrefab == null) continue;

            for (int i = 0; i < entry.count; i++)
            {
                SpawnEnemy(entry);
                yield return new WaitForSeconds(entry.spawnInterval);
            }
        }

        isSpawning = false;
    }

    private void SpawnEnemy(EnemyWaveEntry entry)
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject enemyGO = Instantiate(entry.enemyPrefab, pos, Quaternion.identity, transform);

        EnemyController enemy = enemyGO.GetComponent<EnemyController>();
        if (enemy == null)
            enemy = enemyGO.AddComponent<EnemyController>();

        enemy.Initialize(waypointArray, entry.hp, entry.speed);

        enemy.OnDeath += HandleEnemyDeath;
        enemy.OnReachGoal += HandleEnemyReachedGoal;

        aliveEnemyCount++;
    }

    private void HandleEnemyDeath(EnemyController enemy)
    {
        aliveEnemyCount--;
        OnEnemyDeath?.Invoke(enemy);
    }

    private void HandleEnemyReachedGoal(EnemyController enemy)
    {
        aliveEnemyCount--;
        OnEnemyReachedGoal?.Invoke(enemy);
    }
}
