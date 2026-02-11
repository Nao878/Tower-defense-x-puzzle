using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// パズルの操作統括マネージャー
/// パズドラ風ドラッグ操作、合成判定、パーツ消費・補充を管理する
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PuzzleBoard board;
    [SerializeField] private RecipeDatabase recipeDatabase;

    [Header("操作設定")]
    [Tooltip("ドラッグ中のピースの拡大倍率")]
    [SerializeField] private float dragScale = 1.2f;

    [Tooltip("ドラッグ中のピースのソート順（前面に表示）")]
    [SerializeField] private int dragSortingOrder = 10;

    /// <summary>
    /// ユニット合成成功時のイベント
    /// </summary>
    public event Action<UnitData> OnUnitSynthesized;

    // ドラッグ状態
    private bool isDragging = false;
    private Piece draggedPiece = null;
    private int originalSortingOrder;
    private Vector3 originalScale;
    private Camera mainCamera;
    private bool isProcessing = false;

    private void Start()
    {
        mainCamera = Camera.main;

        if (board == null)
            board = GetComponentInChildren<PuzzleBoard>();
        if (recipeDatabase == null)
            recipeDatabase = GetComponent<RecipeDatabase>();

        // 盤面を初期化
        if (board != null)
        {
            board.InitializeBoard();
        }
    }

    private void Update()
    {
        if (isProcessing) return;

        HandleInput();
    }

    /// <summary>
    /// マウス/タッチ入力の処理
    /// </summary>
    private void HandleInput()
    {
        // ドラッグ開始
        if (Input.GetMouseButtonDown(0))
        {
            TryStartDrag();
        }
        // ドラッグ中
        else if (Input.GetMouseButton(0) && isDragging)
        {
            UpdateDrag();
        }
        // ドラッグ終了
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }
    }

    /// <summary>
    /// ドラッグの開始を試みる
    /// </summary>
    private void TryStartDrag()
    {
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f;

        Vector2Int gridPos = board.WorldToGridPosition(worldPos);

        if (!board.IsValidPosition(gridPos.x, gridPos.y))
            return;

        Piece piece = board.Grid[gridPos.x, gridPos.y];
        if (piece == null)
            return;

        // ドラッグ開始
        isDragging = true;
        draggedPiece = piece;

        // ビジュアルを前面に
        SpriteRenderer sr = draggedPiece.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            originalSortingOrder = sr.sortingOrder;
            sr.sortingOrder = dragSortingOrder;
        }

        originalScale = draggedPiece.transform.localScale;
        draggedPiece.transform.localScale = originalScale * dragScale;
    }

    /// <summary>
    /// ドラッグ中の更新
    /// ドラッグ中のピースとそれが通過したセルのピースを入れ替える
    /// </summary>
    private void UpdateDrag()
    {
        if (draggedPiece == null) return;

        Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f;

        // ドラッグ中のピースをマウスに追従
        draggedPiece.transform.position = worldPos;

        // 現在のマウス位置のグリッド座標を求める
        Vector2Int gridPos = board.WorldToGridPosition(worldPos);

        if (!board.IsValidPosition(gridPos.x, gridPos.y))
            return;

        // ドラッグ中のピースが他のセルに入った場合、入れ替え
        if (gridPos.x != draggedPiece.row || gridPos.y != draggedPiece.col)
        {
            Piece otherPiece = board.Grid[gridPos.x, gridPos.y];
            if (otherPiece != null && otherPiece != draggedPiece)
            {
                // 入れ替え先のピースを元の位置に移動
                int oldRow = draggedPiece.row;
                int oldCol = draggedPiece.col;

                board.SwapPieces(oldRow, oldCol, gridPos.x, gridPos.y);

                // 入れ替わったピースをアニメーション移動
                otherPiece.MoveTo(board.GridToWorldPosition(oldRow, oldCol));
            }
        }
    }

    /// <summary>
    /// ドラッグの終了
    /// ピースを所定位置に戻し、合成判定を実行する
    /// </summary>
    private void EndDrag()
    {
        if (draggedPiece == null) return;

        // ビジュアルを元に戻す
        SpriteRenderer sr = draggedPiece.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = originalSortingOrder;
        }
        draggedPiece.transform.localScale = originalScale;

        // ピースを正しいグリッド位置に配置
        draggedPiece.SetPositionImmediate(
            board.GridToWorldPosition(draggedPiece.row, draggedPiece.col)
        );

        isDragging = false;
        draggedPiece = null;

        // 合成判定を実行
        StartCoroutine(ProcessSynthesis());
    }

    /// <summary>
    /// 合成判定と処理のコルーチン
    /// </summary>
    private IEnumerator ProcessSynthesis()
    {
        isProcessing = true;

        bool foundMatch;
        do
        {
            foundMatch = false;

            // レシピチェック
            List<RecipeMatch> matches = recipeDatabase.CheckRecipes(board.Grid);

            if (matches.Count > 0)
            {
                foundMatch = true;

                foreach (var match in matches)
                {
                    // マッチしたセルのピースを消費
                    foreach (var cell in match.matchedCells)
                    {
                        board.RemovePieceAt(cell.x, cell.y);
                    }

                    // 合成成功イベントを発行
                    Debug.Log($"[PuzzleManager] 合成成功: {match.recipe.recipeName} -> {match.recipe.resultUnit.unitName}");
                    OnUnitSynthesized?.Invoke(match.recipe.resultUnit);
                }

                // 少し待ってからアニメーション
                yield return new WaitForSeconds(0.2f);

                // ピースを落下させる
                board.DropPieces();

                yield return new WaitForSeconds(0.2f);

                // 空いたスペースを補充する
                board.FillBoard();

                yield return new WaitForSeconds(0.3f);
            }

        } while (foundMatch); // 連鎖的にマッチがある限り繰り返す

        isProcessing = false;
    }
}
