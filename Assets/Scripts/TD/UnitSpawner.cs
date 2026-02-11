using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// パズルからの通知を受けてユニットをTDフィールドに生成する
/// </summary>
public class UnitSpawner : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PuzzleManager puzzleManager;

    [Header("配置設定")]
    [Tooltip("ユニット配置可能エリアの左下座標")]
    [SerializeField] private Vector2 placementAreaMin = new Vector2(-5f, 2f);

    [Tooltip("ユニット配置可能エリアの右上座標")]
    [SerializeField] private Vector2 placementAreaMax = new Vector2(5f, 6f);

    [Tooltip("ユニット間の最低距離")]
    [SerializeField] private float minSpacing = 1.5f;

    [Header("デフォルトプレハブ")]
    [Tooltip("UnitDataにprefabが未設定の場合に使うデフォルトプレハブ")]
    [SerializeField] private GameObject defaultUnitPrefab;

    private List<Vector3> occupiedPositions = new List<Vector3>();

    private void OnEnable()
    {
        if (puzzleManager != null)
        {
            puzzleManager.OnUnitSynthesized += SpawnUnit;
        }
    }

    private void OnDisable()
    {
        if (puzzleManager != null)
        {
            puzzleManager.OnUnitSynthesized -= SpawnUnit;
        }
    }

    /// <summary>
    /// ユニットをフィールドに生成する
    /// </summary>
    public void SpawnUnit(UnitData unitData)
    {
        if (unitData == null)
        {
            Debug.LogError("[UnitSpawner] UnitDataがnullです");
            return;
        }

        // プレハブを選択
        GameObject prefab = unitData.prefab != null ? unitData.prefab : defaultUnitPrefab;
        if (prefab == null)
        {
            Debug.LogError("[UnitSpawner] ユニットプレハブが見つかりません");
            return;
        }

        // 配置位置を決定
        Vector3 spawnPos = FindPlacementPosition();

        // ユニットを生成
        GameObject unitGO = Instantiate(prefab, spawnPos, Quaternion.identity, transform);

        UnitObject unitObj = unitGO.GetComponent<UnitObject>();
        if (unitObj == null)
            unitObj = unitGO.AddComponent<UnitObject>();

        unitObj.Initialize(unitData);
        occupiedPositions.Add(spawnPos);

        Debug.Log($"[UnitSpawner] {unitData.unitName} を ({spawnPos.x:F1}, {spawnPos.y:F1}) に配置");
    }

    /// <summary>
    /// 配置可能な位置を探す
    /// 既存ユニットと重ならない位置を返す
    /// </summary>
    private Vector3 FindPlacementPosition()
    {
        int maxAttempts = 30;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(placementAreaMin.x, placementAreaMax.x),
                Random.Range(placementAreaMin.y, placementAreaMax.y),
                0f
            );

            bool valid = true;
            foreach (var occupied in occupiedPositions)
            {
                if (Vector3.Distance(candidate, occupied) < minSpacing)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                return candidate;
        }

        // 見つからなければランダムな位置を返す
        return new Vector3(
            Random.Range(placementAreaMin.x, placementAreaMax.x),
            Random.Range(placementAreaMin.y, placementAreaMax.y),
            0f
        );
    }

    /// <summary>
    /// デバッグ用：配置可能エリアをGizmoで表示
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
        Vector3 center = new Vector3(
            (placementAreaMin.x + placementAreaMax.x) / 2f,
            (placementAreaMin.y + placementAreaMax.y) / 2f,
            0f
        );
        Vector3 size = new Vector3(
            placementAreaMax.x - placementAreaMin.x,
            placementAreaMax.y - placementAreaMin.y,
            0.1f
        );
        Gizmos.DrawCube(center, size);
        Gizmos.DrawWireCube(center, size);
    }
}
