using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 合体マッチ結果
/// </summary>
public struct CombineMatch
{
    public KanjiPiece pieceA;
    public KanjiPiece pieceB;
    public KanjiRecipe recipe;

    public CombineMatch(KanjiPiece a, KanjiPiece b, KanjiRecipe r)
    {
        pieceA = a;
        pieceB = b;
        recipe = r;
    }
}

/// <summary>
/// パズル操作フロー統括
/// ピース選択→隣接入れ替え→合体判定→確認ダイアログ→合体/キャンセル→重力処理
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PuzzleBoard board;
    [SerializeField] private KanjiRecipe[] recipes;

    /// <summary>
    /// 合体確認リクエストのイベント（UIに通知）
    /// </summary>
    public event Action<CombineMatch> OnCombineConfirmRequested;

    /// <summary>
    /// 合体成功イベント（スコア加算等に使用）
    /// </summary>
    public event Action<KanjiRecipe> OnCombineSuccess;

    private KanjiPiece selectedPiece = null;
    private bool isProcessing = false;
    private bool waitingForConfirm = false;
    private CombineMatch pendingMatch;
    private Camera mainCamera;

    /// <summary>
    /// 操作可能かどうか
    /// </summary>
    public bool CanInteract => !isProcessing && !waitingForConfirm;

    /// <summary>
    /// レシピ一覧
    /// </summary>
    public KanjiRecipe[] Recipes => recipes;

    private void Start()
    {
        mainCamera = Camera.main;

        if (board == null)
            board = GetComponent<PuzzleBoard>();

        board.InitializeBoard();
    }

    private void Update()
    {
        if (!CanInteract) return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    /// <summary>
    /// クリック処理
    /// </summary>
    private void HandleClick()
    {
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f;

        Vector2Int gridPos = board.WorldToGridPosition(worldPos);

        if (!board.IsValidPosition(gridPos.x, gridPos.y))
        {
            DeselectCurrent();
            return;
        }

        KanjiPiece clickedPiece = board.Grid[gridPos.x, gridPos.y];
        if (clickedPiece == null)
        {
            DeselectCurrent();
            return;
        }

        // まだ選択していない場合 → 選択
        if (selectedPiece == null)
        {
            SelectPiece(clickedPiece);
        }
        // 同じピースを再クリック → 選択解除
        else if (clickedPiece == selectedPiece)
        {
            DeselectCurrent();
        }
        // 隣接ピースをクリック → 入れ替え
        else if (board.AreAdjacent(selectedPiece.row, selectedPiece.col, clickedPiece.row, clickedPiece.col))
        {
            StartCoroutine(PerformSwapAndCheck(selectedPiece, clickedPiece));
        }
        // 隣接でないピースをクリック → 選択変更
        else
        {
            DeselectCurrent();
            SelectPiece(clickedPiece);
        }
    }

    private void SelectPiece(KanjiPiece piece)
    {
        selectedPiece = piece;
        piece.SetSelected(true);
    }

    private void DeselectCurrent()
    {
        if (selectedPiece != null)
        {
            selectedPiece.SetSelected(false);
            selectedPiece = null;
        }
    }

    /// <summary>
    /// ピースの入れ替えと合体判定
    /// </summary>
    private IEnumerator PerformSwapAndCheck(KanjiPiece pieceA, KanjiPiece pieceB)
    {
        isProcessing = true;
        DeselectCurrent();

        int r1 = pieceA.row, c1 = pieceA.col;
        int r2 = pieceB.row, c2 = pieceB.col;

        // 入れ替え実行
        board.SwapPieces(r1, c1, r2, c2);

        yield return new WaitForSeconds(0.3f);

        // 合体判定：盤面全体の隣接ペアをチェック
        CombineMatch? match = FindCombineMatch();

        if (match.HasValue)
        {
            // 合体可能 → 確認ダイアログを表示
            pendingMatch = match.Value;
            waitingForConfirm = true;
            isProcessing = false;

            // ハイライト表示
            pendingMatch.pieceA.SetSelected(true);
            pendingMatch.pieceB.SetSelected(true);

            OnCombineConfirmRequested?.Invoke(pendingMatch);
        }
        else
        {
            // 合体なし → そのまま次の操作へ
            isProcessing = false;
        }
    }

    /// <summary>
    /// 盤面全体から合体可能なペアを探す
    /// </summary>
    private CombineMatch? FindCombineMatch()
    {
        if (recipes == null) return null;

        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
            {
                KanjiPiece piece = board.Grid[r, c];
                if (piece == null) continue;

                // 右隣チェック
                if (c + 1 < PuzzleBoard.SIZE && board.Grid[r, c + 1] != null)
                {
                    KanjiRecipe recipe = FindMatchingRecipe(piece.Kanji, board.Grid[r, c + 1].Kanji);
                    if (recipe != null)
                    {
                        return new CombineMatch(piece, board.Grid[r, c + 1], recipe);
                    }
                }

                // 上隣チェック
                if (r + 1 < PuzzleBoard.SIZE && board.Grid[r + 1, c] != null)
                {
                    KanjiRecipe recipe = FindMatchingRecipe(piece.Kanji, board.Grid[r + 1, c].Kanji);
                    if (recipe != null)
                    {
                        return new CombineMatch(piece, board.Grid[r + 1, c], recipe);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// マッチするレシピを検索する
    /// </summary>
    private KanjiRecipe FindMatchingRecipe(string a, string b)
    {
        foreach (var recipe in recipes)
        {
            if (recipe.Matches(a, b))
                return recipe;
        }
        return null;
    }

    /// <summary>
    /// 合体確認で「はい」が押された場合
    /// </summary>
    public void ConfirmCombine()
    {
        if (!waitingForConfirm) return;

        StartCoroutine(ExecuteCombine());
    }

    /// <summary>
    /// 合体確認で「いいえ」が押された場合
    /// </summary>
    public void CancelCombine()
    {
        if (!waitingForConfirm) return;

        // ハイライト解除
        if (pendingMatch.pieceA != null) pendingMatch.pieceA.SetSelected(false);
        if (pendingMatch.pieceB != null) pendingMatch.pieceB.SetSelected(false);

        waitingForConfirm = false;
    }

    /// <summary>
    /// 合体実行コルーチン
    /// </summary>
    private IEnumerator ExecuteCombine()
    {
        isProcessing = true;
        waitingForConfirm = false;

        KanjiPiece pieceA = pendingMatch.pieceA;
        KanjiPiece pieceB = pendingMatch.pieceB;
        KanjiRecipe recipe = pendingMatch.recipe;

        Debug.Log($"[PuzzleManager] 合体! {recipe.materialA} + {recipe.materialB} = {recipe.result} (+{recipe.score}点)");

        // 素材ピースを消去
        board.RemovePieceAt(pieceA.row, pieceA.col);
        board.RemovePieceAt(pieceB.row, pieceB.col);

        // スコア加算イベント発行
        OnCombineSuccess?.Invoke(recipe);

        yield return new WaitForSeconds(0.2f);

        // 重力処理
        board.DropPieces();

        yield return new WaitForSeconds(0.3f);

        // 空きセルを補充
        board.RefillEmptyCells();

        yield return new WaitForSeconds(0.3f);

        isProcessing = false;
    }
}
