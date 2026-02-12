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
/// 全ピースの入れ替え操作（1ターン1手）、
/// 隣接する合体可能ペアの黒線表示+振動演出、
/// クリック合体を管理する
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PuzzleBoard board;
    [SerializeField] private KanjiRecipe[] recipes;

    [Header("線の設定")]
    [SerializeField] private float lineWidth = 0.06f;

    /// <summary>
    /// 合体成功イベント（スコア加算用）
    /// </summary>
    public event Action<KanjiRecipe> OnCombineSuccess;

    /// <summary>
    /// ターン経過イベント
    /// </summary>
    public event Action<int> OnTurnChanged;

    private KanjiPiece selectedPiece = null;
    private bool isProcessing = false;
    private Camera mainCamera;
    private int turnCount = 0;

    // 合体可能ペアのリスト
    private List<CombinablePair> combinablePairs = new List<CombinablePair>();

    // 黒線インジケータのリスト
    private List<GameObject> lineIndicators = new List<GameObject>();

    // 線描画用のスプライト（ランタイムで生成）
    private Sprite lineSprite;

    /// <summary>
    /// レシピ一覧
    /// </summary>
    public KanjiRecipe[] Recipes => recipes;

    /// <summary>
    /// 現在のターン数
    /// </summary>
    public int TurnCount => turnCount;

    private void Start()
    {
        mainCamera = Camera.main;

        if (board == null)
            board = GetComponent<PuzzleBoard>();

        // 線用のスプライトを生成
        CreateLineSprite();

        board.InitializeBoard();

        // 初期スキャン
        StartCoroutine(DelayedScan());
    }

    /// <summary>
    /// 線描画用のスプライトをランタイムで生成
    /// </summary>
    private void CreateLineSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        lineSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    private IEnumerator DelayedScan()
    {
        yield return new WaitForSeconds(0.1f);
        ScanCombinablePairs();
    }

    private void Update()
    {
        if (isProcessing) return;

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

        // まだ選択していない場合 → どのピースでも選択可能
        if (selectedPiece == null)
        {
            SelectPiece(clickedPiece);
        }
        // 同じピースを再クリック → 選択解除
        else if (clickedPiece == selectedPiece)
        {
            DeselectCurrent();
        }
        // 隣接ピースをクリック
        else if (board.AreAdjacent(selectedPiece.row, selectedPiece.col, clickedPiece.row, clickedPiece.col))
        {
            // 合体可能ペアかチェック
            CombinablePair? pair = FindPairBetween(selectedPiece, clickedPiece);
            if (pair.HasValue)
            {
                // 合体実行
                StartCoroutine(ExecuteCombine(selectedPiece, clickedPiece, pair.Value.recipe));
            }
            else
            {
                // 通常の入れ替え（1ターン消費）
                StartCoroutine(ExecuteSwap(selectedPiece, clickedPiece));
            }
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
    /// 通常の入れ替え（1ターン消費）
    /// </summary>
    private IEnumerator ExecuteSwap(KanjiPiece pieceA, KanjiPiece pieceB)
    {
        isProcessing = true;
        DeselectCurrent();

        // 入れ替え実行
        board.SwapPieces(pieceA.row, pieceA.col, pieceB.row, pieceB.col);

        // ターン加算
        turnCount++;
        OnTurnChanged?.Invoke(turnCount);
        Debug.Log($"[PuzzleManager] ターン {turnCount}: 入れ替え実行");

        yield return new WaitForSeconds(0.3f);

        // 再スキャン
        ScanCombinablePairs();

        isProcessing = false;
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
        ClearAllCombinableState();
        ClearLineIndicators();

        Debug.Log($"[PuzzleManager] 合体! {recipe.materialA} + {recipe.materialB} = {recipe.result} (+{recipe.score}点)");

        int firstRow = firstPiece.row, firstCol = firstPiece.col;

        // 最初のピースを削除（空欄になる）
        board.RemovePieceAt(firstRow, firstCol);

        // 2番目のピースの漢字を結果に変更
        secondPiece.SetKanji(recipe.result);

        // スコア加算
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

    // ============================================================
    // 合体可能ペアのスキャンとインジケータ表示
    // ============================================================

    /// <summary>
    /// 盤面全体をスキャンし、合体可能なペアを検出して黒線と振動を表示する
    /// </summary>
    public void ScanCombinablePairs()
    {
        ClearLineIndicators();
        ClearAllCombinableState();
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
                        KanjiPiece otherPiece = board.Grid[r, c + 1];
                        combinablePairs.Add(new CombinablePair(piece, otherPiece, recipe));
                        piece.SetCombinable(true);
                        otherPiece.SetCombinable(true);
                        CreateLineIndicator(piece, otherPiece, isHorizontal: true);
                    }
                }

                // 上隣チェック
                if (r + 1 < PuzzleBoard.SIZE && board.Grid[r + 1, c] != null)
                {
                    KanjiRecipe recipe = FindMatchingRecipe(piece.Kanji, board.Grid[r + 1, c].Kanji);
                    if (recipe != null)
                    {
                        KanjiPiece otherPiece = board.Grid[r + 1, c];
                        combinablePairs.Add(new CombinablePair(piece, otherPiece, recipe));
                        piece.SetCombinable(true);
                        otherPiece.SetCombinable(true);
                        CreateLineIndicator(piece, otherPiece, isHorizontal: false);
                    }
                }
            }
        }

        if (combinablePairs.Count > 0)
        {
            Debug.Log($"[PuzzleManager] 合体可能ペア: {combinablePairs.Count}組");
        }
    }

    /// <summary>
    /// 全ピースの合体可能フラグをリセットする
    /// </summary>
    private void ClearAllCombinableState()
    {
        if (board.Grid == null) return;
        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
            {
                if (board.Grid[r, c] != null)
                {
                    board.Grid[r, c].SetCombinable(false);
                }
            }
        }
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
    private void CreateLineIndicator(KanjiPiece a, KanjiPiece b, bool isHorizontal)
    {
        if (lineSprite == null) return;

        Vector3 posA = board.GridToWorldPosition(a.row, a.col);
        Vector3 posB = board.GridToWorldPosition(b.row, b.col);
        Vector3 midPoint = (posA + posB) / 2f;
        midPoint.z = -1f; // ピースの前面に表示

        GameObject lineObj = new GameObject("CombineLine");
        lineObj.transform.SetParent(transform);
        lineObj.transform.position = midPoint;

        SpriteRenderer sr = lineObj.AddComponent<SpriteRenderer>();
        sr.sprite = lineSprite;
        sr.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        sr.sortingOrder = 10;

        float gap = board.CellSize * 0.3f;

        if (isHorizontal)
        {
            lineObj.transform.localScale = new Vector3(gap, lineWidth, 1f);
        }
        else
        {
            lineObj.transform.localScale = new Vector3(lineWidth, gap, 1f);
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
}
