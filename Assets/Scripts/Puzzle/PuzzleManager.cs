using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 合体可能ペア情報
/// </summary>
public struct CombinablePair
{
    public KanjiPiece pieceA;
    public KanjiPiece pieceB;
    public KanjiRecipe recipe;

    public CombinablePair(KanjiPiece a, KanjiPiece b, KanjiRecipe r)
    {
        pieceA = a;
        pieceB = b;
        recipe = r;
    }
}

/// <summary>
/// パズル操作フロー統括
/// 隣接する合体可能ペアの間に黒線を表示し、
/// プレイヤーが2つのピースを順にクリックすると合体を実行する
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PuzzleBoard board;
    [SerializeField] private KanjiRecipe[] recipes;

    [Header("線の設定")]
    [SerializeField] private Color lineColor = Color.black;
    [SerializeField] private float lineWidth = 0.08f;

    /// <summary>
    /// 合体成功イベント（スコア加算用）
    /// </summary>
    public event Action<KanjiRecipe> OnCombineSuccess;

    private KanjiPiece selectedPiece = null;
    private bool isProcessing = false;
    private Camera mainCamera;

    // 合体可能ペアのリスト
    private List<CombinablePair> combinablePairs = new List<CombinablePair>();

    // 黒線インジケータのリスト
    private List<GameObject> lineIndicators = new List<GameObject>();

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

        // 初期状態の合体可能ペアを検出
        StartCoroutine(DelayedScan());
    }

    private IEnumerator DelayedScan()
    {
        yield return new WaitForSeconds(0.1f);
        ScanCombinablePairs();
    }

    private void Update()
    {
        if (isProcessing) return;

        // 新Input System でクリック検出
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleClick();
        }
    }

    /// <summary>
    /// クリック処理
    /// </summary>
    private void HandleClick()
    {
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
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

        // まだ選択していない場合
        if (selectedPiece == null)
        {
            // クリックしたピースが合体可能ペアの一部かチェック
            if (IsPieceInCombinablePair(clickedPiece))
            {
                SelectPiece(clickedPiece);
            }
        }
        // 同じピースを再クリック → 選択解除
        else if (clickedPiece == selectedPiece)
        {
            DeselectCurrent();
        }
        // 別のピースをクリック → 合体できるか判定
        else
        {
            CombinablePair? pair = FindPairBetween(selectedPiece, clickedPiece);
            if (pair.HasValue)
            {
                // 合体実行: selectedPiece の位置が空欄、clickedPiece の位置に結果が出現
                StartCoroutine(ExecuteCombine(selectedPiece, clickedPiece, pair.Value.recipe));
            }
            else
            {
                // 合体できない → 選択変更
                DeselectCurrent();
                if (IsPieceInCombinablePair(clickedPiece))
                {
                    SelectPiece(clickedPiece);
                }
            }
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
    /// 盤面全体をスキャンし、合体可能なペアを検出して黒線を表示する
    /// </summary>
    public void ScanCombinablePairs()
    {
        // 既存の線を削除
        ClearLineIndicators();
        combinablePairs.Clear();

        if (recipes == null || board.Grid == null) return;

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
                        CombinablePair pair = new CombinablePair(piece, board.Grid[r, c + 1], recipe);
                        combinablePairs.Add(pair);
                        CreateLineIndicator(piece, board.Grid[r, c + 1]);
                    }
                }

                // 上隣チェック
                if (r + 1 < PuzzleBoard.SIZE && board.Grid[r + 1, c] != null)
                {
                    KanjiRecipe recipe = FindMatchingRecipe(piece.Kanji, board.Grid[r + 1, c].Kanji);
                    if (recipe != null)
                    {
                        CombinablePair pair = new CombinablePair(piece, board.Grid[r + 1, c], recipe);
                        combinablePairs.Add(pair);
                        CreateLineIndicator(piece, board.Grid[r + 1, c]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 指定ピースが合体可能ペアに含まれているか
    /// </summary>
    private bool IsPieceInCombinablePair(KanjiPiece piece)
    {
        foreach (var pair in combinablePairs)
        {
            if (pair.pieceA == piece || pair.pieceB == piece)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 2つのピース間の合体可能ペアを探す
    /// </summary>
    private CombinablePair? FindPairBetween(KanjiPiece a, KanjiPiece b)
    {
        foreach (var pair in combinablePairs)
        {
            if ((pair.pieceA == a && pair.pieceB == b) ||
                (pair.pieceA == b && pair.pieceB == a))
            {
                return pair;
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
    /// 2つのピース間に黒線インジケータを作成する
    /// </summary>
    private void CreateLineIndicator(KanjiPiece a, KanjiPiece b)
    {
        Vector3 posA = board.GridToWorldPosition(a.row, a.col);
        Vector3 posB = board.GridToWorldPosition(b.row, b.col);
        Vector3 midPoint = (posA + posB) / 2f;
        midPoint.z = -0.5f; // ピースの前面に表示

        GameObject lineObj = new GameObject("CombineLine");
        lineObj.transform.SetParent(transform);
        lineObj.transform.position = midPoint;

        SpriteRenderer sr = lineObj.AddComponent<SpriteRenderer>();
        sr.color = lineColor;
        sr.sortingOrder = 5;

        // Unityビルトインの白ピクセルスプライトを使用
        sr.sprite = board.PiecePrefab.GetComponent<SpriteRenderer>()?.sprite;

        // 線の方向とサイズを計算
        bool isHorizontal = (a.row == b.row);
        float distance = Vector3.Distance(posA, posB);
        float lineLength = distance * 0.4f;

        if (isHorizontal)
        {
            lineObj.transform.localScale = new Vector3(lineLength, lineWidth, 1f);
        }
        else
        {
            lineObj.transform.localScale = new Vector3(lineWidth, lineLength, 1f);
        }

        lineIndicators.Add(lineObj);
    }

    /// <summary>
    /// 全ての線インジケータを削除する
    /// </summary>
    private void ClearLineIndicators()
    {
        foreach (var line in lineIndicators)
        {
            if (line != null) Destroy(line);
        }
        lineIndicators.Clear();
    }

    /// <summary>
    /// 合体実行コルーチン
    /// firstPiece: 最初にクリックしたピース（空欄になる）
    /// secondPiece: 2番目にクリックしたピース（ここに結果が出現）
    /// </summary>
    private IEnumerator ExecuteCombine(KanjiPiece firstPiece, KanjiPiece secondPiece, KanjiRecipe recipe)
    {
        isProcessing = true;
        DeselectCurrent();
        ClearLineIndicators();

        int firstRow = firstPiece.row, firstCol = firstPiece.col;
        int secondRow = secondPiece.row, secondCol = secondPiece.col;

        Debug.Log($"[PuzzleManager] 合体! {recipe.materialA} + {recipe.materialB} = {recipe.result} (+{recipe.score}点)");

        // 最初のピース（空欄になる方）を削除
        board.RemovePieceAt(firstRow, firstCol);

        // 2番目のピースの漢字を結果に変更
        secondPiece.SetKanji(recipe.result);

        // スコア加算イベント
        OnCombineSuccess?.Invoke(recipe);

        yield return new WaitForSeconds(0.3f);

        // 重力処理
        board.DropPieces();

        yield return new WaitForSeconds(0.3f);

        // 空きセル補充
        board.RefillEmptyCells();

        yield return new WaitForSeconds(0.3f);

        // 再スキャン
        ScanCombinablePairs();

        isProcessing = false;
    }
}
