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
    public KanjiRecipe recipe;      // レシピ合体の場合（消去の場合はnull）
    public bool isElimination;      // 同種消去か
    public int eliminationScore;    // 消去時のスコア

    /// <summary>
    /// レシピ合体用コンストラクタ
    /// </summary>
    public CombinablePair(KanjiPiece a, KanjiPiece b, KanjiRecipe r)
    {
        pieceA = a;
        pieceB = b;
        recipe = r;
        isElimination = false;
        eliminationScore = 0;
    }

    /// <summary>
    /// 同種消去用コンストラクタ
    /// </summary>
    public CombinablePair(KanjiPiece a, KanjiPiece b, int score)
    {
        pieceA = a;
        pieceB = b;
        recipe = null;
        isElimination = true;
        eliminationScore = score;
    }
}

/// <summary>
/// パズル操作フロー統括
/// 全ピースの入れ替え操作（1ターン1手）、
/// 隣接する合体可能ペアの黒線表示+振動演出、
/// クリック合体・同種消去を管理する
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

    /// <summary>
    /// 合体成功イベント（スコア加算用 - レシピ合体）
    /// </summary>
    public event Action<KanjiRecipe> OnCombineSuccess;

    /// <summary>
    /// 同種消去成功イベント（スコア加算用）
    /// </summary>
    public event Action<string, int> OnEliminationSuccess;

    /// <summary>
    /// ターン経過イベント
    /// </summary>
    public event Action<int> OnTurnChanged;

    /// <summary>
    /// デッドロック検出イベント（リセットボタン表示用）
    /// </summary>
    public event Action<bool> OnDeadlockChanged;

    private KanjiPiece selectedPiece = null;
    private bool isProcessing = false;
    private bool isDeadlocked = false;
    private Camera mainCamera;
    private int turnCount = 0;

    // 合体可能ペアのリスト
    private List<CombinablePair> combinablePairs = new List<CombinablePair>();

    // 黒線インジケータのリスト
    private List<GameObject> lineIndicators = new List<GameObject>();

    // 線描画用のスプライト（ランタイムで生成）
    private Sprite lineSprite;

    // 終端漢字のキャッシュ（素材として使われない＝これ以上合体できない漢字）
    private HashSet<string> terminalKanji = new HashSet<string>();

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

        CreateLineSprite();
        BuildTerminalKanjiCache();

        board.InitializeBoard();
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

    /// <summary>
    /// 終端漢字のキャッシュを構築する
    /// レシピの結果漢字のうち、どのレシピの素材にもならないものが終端漢字
    /// </summary>
    private void BuildTerminalKanjiCache()
    {
        terminalKanji.Clear();
        if (recipes == null) return;

        // 全てのレシピ結果を収集
        HashSet<string> resultKanji = new HashSet<string>();
        HashSet<string> materialKanji = new HashSet<string>();

        foreach (var recipe in recipes)
        {
            resultKanji.Add(recipe.result);
            materialKanji.Add(recipe.materialA);
            materialKanji.Add(recipe.materialB);
        }

        // 結果漢字のうち、素材として使われないものが終端漢字
        foreach (string kanji in resultKanji)
        {
            if (!materialKanji.Contains(kanji))
            {
                terminalKanji.Add(kanji);
            }
        }

        Debug.Log($"[PuzzleManager] 終端漢字: {string.Join(", ", terminalKanji)}");
    }

    /// <summary>
    /// 指定漢字が終端漢字（これ以上合体できない）かどうか
    /// </summary>
    private bool IsTerminalKanji(string kanji)
    {
        return terminalKanji.Contains(kanji);
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
                {
                    StartCoroutine(ExecuteElimination(selectedPiece, clickedPiece, pair.Value.eliminationScore));
                }
                else
                {
                    StartCoroutine(ExecuteCombine(selectedPiece, clickedPiece, pair.Value.recipe));
                }
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

        board.SwapPieces(pieceA.row, pieceA.col, pieceB.row, pieceB.col);

        turnCount++;
        OnTurnChanged?.Invoke(turnCount);

        yield return new WaitForSeconds(0.3f);

        ScanCombinablePairs();
        isProcessing = false;
    }

    /// <summary>
    /// レシピ合体実行
    /// </summary>
    private IEnumerator ExecuteCombine(KanjiPiece firstPiece, KanjiPiece secondPiece, KanjiRecipe recipe)
    {
        isProcessing = true;
        DeselectCurrent();
        ClearAllCombinableState();
        ClearLineIndicators();

        Debug.Log($"[PuzzleManager] 合体! {recipe.materialA} + {recipe.materialB} = {recipe.result} (+{recipe.score}点)");

        board.RemovePieceAt(firstPiece.row, firstPiece.col);
        secondPiece.SetKanji(recipe.result);

        OnCombineSuccess?.Invoke(recipe);

        yield return new WaitForSeconds(0.3f);
        board.DropPieces();
        yield return new WaitForSeconds(0.3f);
        board.RefillEmptyCells();
        yield return new WaitForSeconds(0.3f);

        ScanCombinablePairs();
        isProcessing = false;
    }

    /// <summary>
    /// 同種消去実行（終端漢字の同じペアを消す）
    /// </summary>
    private IEnumerator ExecuteElimination(KanjiPiece firstPiece, KanjiPiece secondPiece, int score)
    {
        isProcessing = true;
        DeselectCurrent();
        ClearAllCombinableState();
        ClearLineIndicators();

        string kanji = firstPiece.Kanji;
        Debug.Log($"[PuzzleManager] 消去! {kanji} + {kanji} → 消滅 (+{score}点)");

        board.RemovePieceAt(firstPiece.row, firstPiece.col);
        board.RemovePieceAt(secondPiece.row, secondPiece.col);

        OnEliminationSuccess?.Invoke(kanji, score);

        yield return new WaitForSeconds(0.3f);
        board.DropPieces();
        yield return new WaitForSeconds(0.3f);
        board.RefillEmptyCells();
        yield return new WaitForSeconds(0.3f);

        ScanCombinablePairs();
        isProcessing = false;
    }

    /// <summary>
    /// 盤面を全リセットする（デッドロック解消用）
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

        yield return new WaitForSeconds(0.2f);

        board.FillBoard();

        yield return new WaitForSeconds(0.3f);

        // デッドロック解除
        isDeadlocked = false;
        OnDeadlockChanged?.Invoke(false);

        ScanCombinablePairs();
        isProcessing = false;
    }

    // ============================================================
    // 合体可能ペアのスキャンとインジケータ表示
    // ============================================================

    /// <summary>
    /// 盤面全体をスキャンし、合体可能なペアを検出して黒線と振動を表示する
    /// デッドロック判定も行う
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
                    CheckAndAddPair(piece, board.Grid[r, c + 1], true);
                }

                // 上隣チェック
                if (r + 1 < PuzzleBoard.SIZE && board.Grid[r + 1, c] != null)
                {
                    CheckAndAddPair(piece, board.Grid[r + 1, c], false);
                }
            }
        }

        // 隣接でなくても盤面全体で合体可能なペアがあるかチェック（入れ替えで隣接にできる可能性）
        bool hasAnyPossibleAction = combinablePairs.Count > 0 || HasAnyPossibleMatch();
        bool newDeadlocked = !hasAnyPossibleAction;

        if (newDeadlocked != isDeadlocked)
        {
            isDeadlocked = newDeadlocked;
            OnDeadlockChanged?.Invoke(isDeadlocked);

            if (isDeadlocked)
            {
                Debug.Log("[PuzzleManager] デッドロック検出！合体可能なペアがありません");
            }
        }

        if (combinablePairs.Count > 0)
        {
            Debug.Log($"[PuzzleManager] 合体可能ペア: {combinablePairs.Count}組");
        }
    }

    /// <summary>
    /// 2つの隣接ピースの合体・消去可能性をチェックし、ペアリストに追加する
    /// </summary>
    private void CheckAndAddPair(KanjiPiece piece, KanjiPiece otherPiece, bool isHorizontal)
    {
        // レシピ合体チェック
        KanjiRecipe recipe = FindMatchingRecipe(piece.Kanji, otherPiece.Kanji);
        if (recipe != null)
        {
            combinablePairs.Add(new CombinablePair(piece, otherPiece, recipe));
            piece.SetCombinable(true);
            otherPiece.SetCombinable(true);
            CreateLineIndicator(piece, otherPiece, isHorizontal);
            return;
        }

        // 同種終端漢字の消去チェック
        if (piece.Kanji == otherPiece.Kanji && IsTerminalKanji(piece.Kanji))
        {
            combinablePairs.Add(new CombinablePair(piece, otherPiece, eliminationScore));
            piece.SetCombinable(true);
            otherPiece.SetCombinable(true);
            CreateLineIndicator(piece, otherPiece, isHorizontal);
        }
    }

    /// <summary>
    /// 盤面全体で（入れ替え1回で）実現可能なマッチがあるかチェック
    /// </summary>
    private bool HasAnyPossibleMatch()
    {
        // 盤面上の全漢字を収集
        List<string> allKanji = new List<string>();
        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
            {
                if (board.Grid[r, c] != null)
                    allKanji.Add(board.Grid[r, c].Kanji);
            }
        }

        // 任意の2つの漢字ペアでレシピマッチがあるか
        for (int i = 0; i < allKanji.Count; i++)
        {
            for (int j = i + 1; j < allKanji.Count; j++)
            {
                KanjiRecipe recipe = FindMatchingRecipe(allKanji[i], allKanji[j]);
                if (recipe != null) return true;

                // 同種終端漢字チェック
                if (allKanji[i] == allKanji[j] && IsTerminalKanji(allKanji[i]))
                    return true;
            }
        }

        return false;
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

    private KanjiRecipe FindMatchingRecipe(string a, string b)
    {
        foreach (var recipe in recipes)
        {
            if (recipe.Matches(a, b))
                return recipe;
        }
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

        if (isHorizontal)
            lineObj.transform.localScale = new Vector3(gap, lineWidth, 1f);
        else
            lineObj.transform.localScale = new Vector3(lineWidth, gap, 1f);

        lineIndicators.Add(lineObj);
    }

    private void ClearLineIndicators()
    {
        foreach (var line in lineIndicators)
        {
            if (line != null) Destroy(line);
        }
        lineIndicators.Clear();
    }
}
