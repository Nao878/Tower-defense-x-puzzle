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
    public bool isElimination;
    public int eliminationScore;

    public CombinablePair(KanjiPiece a, KanjiPiece b, KanjiRecipe r)
    {
        pieceA = a; pieceB = b; recipe = r;
        isElimination = false; eliminationScore = 0;
    }

    public CombinablePair(KanjiPiece a, KanjiPiece b, int score)
    {
        pieceA = a; pieceB = b; recipe = null;
        isElimination = true; eliminationScore = score;
    }
}

/// <summary>
/// パズル操作フロー統括
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PuzzleBoard board;
    [SerializeField] private KanjiRecipe[] recipes;

    [Header("線の設定")]
    [SerializeField] private float lineWidth = 0.06f;

    [Header("消去スコア")]
    [SerializeField] private int eliminationScore = 300;

    [Header("リセットペナルティ")]
    [SerializeField] private int resetPenaltyTurns = 5;

    public event Action<KanjiRecipe> OnCombineSuccess;
    public event Action<string, int> OnEliminationSuccess;
    public event Action<int> OnTurnChanged;
    public event Action<bool> OnStalemateChanged;

    private KanjiPiece selectedPiece = null;
    private bool isProcessing = false;
    private bool isStalemate = false;
    private Camera mainCamera;
    private int turnCount = 0;

    private List<CombinablePair> combinablePairs = new List<CombinablePair>();
    private List<GameObject> lineIndicators = new List<GameObject>();
    private Sprite lineSprite;
    private HashSet<string> terminalKanji = new HashSet<string>();

    public KanjiRecipe[] Recipes => recipes;
    public int TurnCount => turnCount;
    public bool IsStalemate => isStalemate;
    public int ResetPenaltyTurns => resetPenaltyTurns;

    private void Start()
    {
        mainCamera = Camera.main;
        if (board == null) board = GetComponent<PuzzleBoard>();

        CreateLineSprite();
        BuildTerminalKanjiCache();
        board.InitializeBoard();
        StartCoroutine(DelayedScan());
    }

    private void CreateLineSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        lineSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    private void BuildTerminalKanjiCache()
    {
        terminalKanji.Clear();
        if (recipes == null) return;

        HashSet<string> resultKanji = new HashSet<string>();
        HashSet<string> materialKanji = new HashSet<string>();

        foreach (var recipe in recipes)
        {
            resultKanji.Add(recipe.result);
            materialKanji.Add(recipe.materialA);
            materialKanji.Add(recipe.materialB);
        }

        foreach (string kanji in resultKanji)
        {
            if (!materialKanji.Contains(kanji))
                terminalKanji.Add(kanji);
        }
    }

    private bool IsTerminalKanji(string kanji)
    {
        return terminalKanji.Contains(kanji);
    }

    private IEnumerator DelayedScan()
    {
        yield return new WaitForSeconds(0.1f);
        ScanAndCheckStalemate();
    }

    private void Update()
    {
        if (isProcessing) return;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            HandleClick();
    }

    private void HandleClick()
    {
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        worldPos.z = 0f;

        Vector2Int gridPos = board.WorldToGridPosition(worldPos);
        if (!board.IsValidPosition(gridPos.x, gridPos.y)) { DeselectCurrent(); return; }

        KanjiPiece clickedPiece = board.Grid[gridPos.x, gridPos.y];
        if (clickedPiece == null) { DeselectCurrent(); return; }

        if (selectedPiece == null)
        {
            SelectPiece(clickedPiece);
        }
        else if (clickedPiece == selectedPiece)
        {
            DeselectCurrent();
        }
        else if (board.AreAdjacent(selectedPiece.row, selectedPiece.col, clickedPiece.row, clickedPiece.col))
        {
            CombinablePair? pair = FindPairBetween(selectedPiece, clickedPiece);
            if (pair.HasValue)
            {
                if (pair.Value.isElimination)
                    StartCoroutine(ExecuteElimination(selectedPiece, clickedPiece, pair.Value.eliminationScore));
                else
                    StartCoroutine(ExecuteCombine(selectedPiece, clickedPiece, pair.Value.recipe));
            }
            else
            {
                StartCoroutine(ExecuteSwap(selectedPiece, clickedPiece));
            }
        }
        else
        {
            DeselectCurrent();
            SelectPiece(clickedPiece);
        }
    }

    private void SelectPiece(KanjiPiece piece) { selectedPiece = piece; piece.SetSelected(true); }
    private void DeselectCurrent()
    {
        if (selectedPiece != null) { selectedPiece.SetSelected(false); selectedPiece = null; }
    }

    private IEnumerator ExecuteSwap(KanjiPiece pieceA, KanjiPiece pieceB)
    {
        isProcessing = true;
        DeselectCurrent();
        board.SwapPieces(pieceA.row, pieceA.col, pieceB.row, pieceB.col);
        turnCount++;
        OnTurnChanged?.Invoke(turnCount);
        yield return new WaitForSeconds(0.3f);
        ScanAndCheckStalemate();
        isProcessing = false;
    }

    private IEnumerator ExecuteCombine(KanjiPiece firstPiece, KanjiPiece secondPiece, KanjiRecipe recipe)
    {
        isProcessing = true;
        DeselectCurrent();
        ClearAllCombinableState();
        ClearLineIndicators();

        board.RemovePieceAt(firstPiece.row, firstPiece.col);
        secondPiece.SetKanji(recipe.result);
        OnCombineSuccess?.Invoke(recipe);

        yield return new WaitForSeconds(0.3f);
        board.DropPieces();
        yield return new WaitForSeconds(0.3f);
        board.RefillEmptyCells();
        yield return new WaitForSeconds(0.3f);
        ScanAndCheckStalemate();
        isProcessing = false;
    }

    private IEnumerator ExecuteElimination(KanjiPiece firstPiece, KanjiPiece secondPiece, int score)
    {
        isProcessing = true;
        DeselectCurrent();
        ClearAllCombinableState();
        ClearLineIndicators();

        board.RemovePieceAt(firstPiece.row, firstPiece.col);
        board.RemovePieceAt(secondPiece.row, secondPiece.col);
        OnEliminationSuccess?.Invoke(firstPiece.Kanji, score);

        yield return new WaitForSeconds(0.3f);
        board.DropPieces();
        yield return new WaitForSeconds(0.3f);
        board.RefillEmptyCells();
        yield return new WaitForSeconds(0.3f);
        ScanAndCheckStalemate();
        isProcessing = false;
    }

    /// <summary>
    /// 盤面リセット（ペナルティ付き）
    /// </summary>
    public void ResetBoard()
    {
        StartCoroutine(ExecuteReset());
    }

    private IEnumerator ExecuteReset()
    {
        isProcessing = true;
        ClearLineIndicators();
        ClearAllCombinableState();
        board.ClearBoard();

        // ペナルティ: ターン加算
        turnCount += resetPenaltyTurns;
        OnTurnChanged?.Invoke(turnCount);

        yield return new WaitForSeconds(0.2f);
        board.FillBoard();
        yield return new WaitForSeconds(0.3f);

        isStalemate = false;
        OnStalemateChanged?.Invoke(false);
        ScanAndCheckStalemate();
        isProcessing = false;
    }

    // ============================================================
    // スキャン & 詰み判定
    // ============================================================

    /// <summary>
    /// 合体可能ペアのスキャンと詰み判定を一括実行
    /// </summary>
    private void ScanAndCheckStalemate()
    {
        ScanCombinablePairs();
        CheckStalemate();
    }

    /// <summary>
    /// 盤面全体をスキャンし、隣接する合体可能ペアを検出
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

                if (c + 1 < PuzzleBoard.SIZE && board.Grid[r, c + 1] != null)
                    CheckAndAddPair(piece, board.Grid[r, c + 1], true);

                if (r + 1 < PuzzleBoard.SIZE && board.Grid[r + 1, c] != null)
                    CheckAndAddPair(piece, board.Grid[r + 1, c], false);
            }
        }
    }

    /// <summary>
    /// 詰み判定：1回のswapで合体可能になる箇所があるかを全探索
    /// </summary>
    private void CheckStalemate()
    {
        // 現在すでに合体可能ペアがあれば詰みではない
        if (combinablePairs.Count > 0)
        {
            SetStalemate(false);
            return;
        }

        // 全隣接ペアのswapをシミュレーションして合体可能性をチェック
        bool canMatch = SimulateAnySwapMatch();
        SetStalemate(!canMatch);
    }

    private void SetStalemate(bool stalemate)
    {
        if (stalemate != isStalemate)
        {
            isStalemate = stalemate;
            OnStalemateChanged?.Invoke(isStalemate);
            if (isStalemate)
                Debug.Log("[PuzzleManager] 手詰まり検出！入れ替えても合体できるペアがありません");
        }
    }

    /// <summary>
    /// 全隣接ペアのswapをシミュレーションし、1つでも合体可能な状況が生まれるか判定
    /// </summary>
    private bool SimulateAnySwapMatch()
    {
        // 全隣接ペアを列挙
        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
            {
                // 右隣とのswap
                if (c + 1 < PuzzleBoard.SIZE)
                {
                    if (SimulateSwapAndCheck(r, c, r, c + 1))
                        return true;
                }
                // 上隣とのswap
                if (r + 1 < PuzzleBoard.SIZE)
                {
                    if (SimulateSwapAndCheck(r, c, r + 1, c))
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 指定2セルをswapした状態で合体可能ペアがあるかチェック
    /// </summary>
    private bool SimulateSwapAndCheck(int r1, int c1, int r2, int c2)
    {
        KanjiPiece p1 = board.Grid[r1, c1];
        KanjiPiece p2 = board.Grid[r2, c2];
        if (p1 == null || p2 == null) return false;

        // 仮swap（グリッドデータのみ）
        board.Grid[r1, c1] = p2;
        board.Grid[r2, c2] = p1;

        bool found = HasAdjacentMatch();

        // 元に戻す
        board.Grid[r1, c1] = p1;
        board.Grid[r2, c2] = p2;

        return found;
    }

    /// <summary>
    /// 現在のグリッド状態で隣接する合体/消去可能ペアがあるかチェック
    /// </summary>
    private bool HasAdjacentMatch()
    {
        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
            {
                KanjiPiece piece = board.Grid[r, c];
                if (piece == null) continue;

                // 右隣
                if (c + 1 < PuzzleBoard.SIZE && board.Grid[r, c + 1] != null)
                {
                    string a = piece.Kanji, b = board.Grid[r, c + 1].Kanji;
                    if (FindMatchingRecipe(a, b) != null) return true;
                    if (a == b && IsTerminalKanji(a)) return true;
                }
                // 上隣
                if (r + 1 < PuzzleBoard.SIZE && board.Grid[r + 1, c] != null)
                {
                    string a = piece.Kanji, b = board.Grid[r + 1, c].Kanji;
                    if (FindMatchingRecipe(a, b) != null) return true;
                    if (a == b && IsTerminalKanji(a)) return true;
                }
            }
        }
        return false;
    }

    private void CheckAndAddPair(KanjiPiece piece, KanjiPiece otherPiece, bool isHorizontal)
    {
        KanjiRecipe recipe = FindMatchingRecipe(piece.Kanji, otherPiece.Kanji);
        if (recipe != null)
        {
            combinablePairs.Add(new CombinablePair(piece, otherPiece, recipe));
            piece.SetCombinable(true);
            otherPiece.SetCombinable(true);
            CreateLineIndicator(piece, otherPiece, isHorizontal);
            return;
        }

        if (piece.Kanji == otherPiece.Kanji && IsTerminalKanji(piece.Kanji))
        {
            combinablePairs.Add(new CombinablePair(piece, otherPiece, eliminationScore));
            piece.SetCombinable(true);
            otherPiece.SetCombinable(true);
            CreateLineIndicator(piece, otherPiece, isHorizontal);
        }
    }

    private void ClearAllCombinableState()
    {
        if (board.Grid == null) return;
        for (int r = 0; r < PuzzleBoard.SIZE; r++)
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
                if (board.Grid[r, c] != null) board.Grid[r, c].SetCombinable(false);
    }

    private CombinablePair? FindPairBetween(KanjiPiece a, KanjiPiece b)
    {
        foreach (var pair in combinablePairs)
            if ((pair.pieceA == a && pair.pieceB == b) || (pair.pieceA == b && pair.pieceB == a))
                return pair;
        return null;
    }

    private KanjiRecipe FindMatchingRecipe(string a, string b)
    {
        foreach (var recipe in recipes)
            if (recipe.Matches(a, b)) return recipe;
        return null;
    }

    private void CreateLineIndicator(KanjiPiece a, KanjiPiece b, bool isHorizontal)
    {
        if (lineSprite == null) return;
        Vector3 posA = board.GridToWorldPosition(a.row, a.col);
        Vector3 posB = board.GridToWorldPosition(b.row, b.col);
        Vector3 midPoint = (posA + posB) / 2f;
        midPoint.z = -1f;

        GameObject lineObj = new GameObject("CombineLine");
        lineObj.transform.SetParent(transform);
        lineObj.transform.position = midPoint;

        SpriteRenderer sr = lineObj.AddComponent<SpriteRenderer>();
        sr.sprite = lineSprite;
        sr.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        sr.sortingOrder = 10;

        float gap = board.CellSize * 0.3f;
        lineObj.transform.localScale = isHorizontal
            ? new Vector3(gap, lineWidth, 1f)
            : new Vector3(lineWidth, gap, 1f);

        lineIndicators.Add(lineObj);
    }

    private void ClearLineIndicators()
    {
        foreach (var line in lineIndicators)
            if (line != null) Destroy(line);
        lineIndicators.Clear();
    }
}
