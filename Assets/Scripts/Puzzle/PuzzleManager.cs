using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲームモード
/// </summary>
public enum GameMode
{
    Classic,  // 1マス入替モード
    Action    // 自由移動モード
}

/// <summary>
/// マッチ情報
/// </summary>
public struct MatchInfo
{
    public List<Vector2Int> positions;
    public KanjiRecipe recipe;
    public string resultKanji;
    public int score;
    public bool isRecipeMatch;
    public bool isElimination;
}

/// <summary>
/// パズルマネージャー（Classic/Actionデュアルモード対応）
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    [Header("ボード参照")]
    [SerializeField] private PuzzleBoard board;

    [Header("レシピ")]
    [SerializeField] private KanjiRecipe[] recipes;

    [Header("消去スコア")]
    [SerializeField] private int eliminationScore = 300;

    [Header("ドラッグ設定（Actionモード）")]
    [SerializeField] private float dragTimeLimit = 5f;

    public event Action<KanjiRecipe> OnCombineSuccess;
    public event Action<string, int> OnEliminationSuccess;
    public event Action<int> OnComboChanged;
    public event Action OnAutoShuffle;

    private Camera mainCamera;
    private bool isProcessing = false;
    private bool isGameOver = false;
    private bool isStarted = false;
    private GameMode currentMode = GameMode.Classic;

    // ドラッグ共通
    private bool isDragging = false;
    private KanjiPiece draggedPiece;
    private float dragStartTime;
    private Vector3 dragStartWorldPos;
    private int currentCombo = 0;

    private HashSet<string> terminalKanji = new HashSet<string>();

    public KanjiRecipe[] Recipes => recipes;
    public GameMode CurrentMode => currentMode;

    public void SetGameOver(bool gameOver) { isGameOver = gameOver; }

    /// <summary>
    /// 指定モードでゲームを開始
    /// </summary>
    public void StartGame(GameMode mode)
    {
        currentMode = mode;
        isStarted = true;
        isGameOver = false;

        BuildTerminalKanjiCache();
        board.InitializeBoard();

        // 初期盤面でマッチがあれば解決
        StartCoroutine(ResolveChains());
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (board == null) board = GetComponent<PuzzleBoard>();
    }

    private void BuildTerminalKanjiCache()
    {
        if (recipes == null) return;
        terminalKanji.Clear();

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

    // ============================================================
    // Update: モード別入力処理
    // ============================================================

    private void Update()
    {
        if (!isStarted || isProcessing || isGameOver) return;
        if (Pointer.current == null) return;

        bool justPressed = Pointer.current.press.wasPressedThisFrame;
        bool pressed = Pointer.current.press.isPressed;
        bool justReleased = Pointer.current.press.wasReleasedThisFrame;

        if (currentMode == GameMode.Classic)
        {
            UpdateClassicMode(justPressed, pressed, justReleased);
        }
        else
        {
            UpdateActionMode(justPressed, pressed, justReleased);
        }
    }

    // ============================================================
    // Classic Mode: 1マス入替
    // ============================================================

    private void UpdateClassicMode(bool justPressed, bool pressed, bool justReleased)
    {
        if (justPressed && !isDragging)
        {
            Vector3 worldPos = GetPointerWorldPos();
            Vector2Int gridPos = board.WorldToGridPosition(worldPos);

            if (!board.IsValidPosition(gridPos.x, gridPos.y)) return;

            KanjiPiece piece = board.Grid[gridPos.x, gridPos.y];
            if (piece == null) return;

            isDragging = true;
            draggedPiece = piece;
            dragStartWorldPos = worldPos;
            piece.SetSelected(true);
        }
        else if (isDragging && justReleased)
        {
            Vector3 worldPos = GetPointerWorldPos();
            Vector3 delta = worldPos - dragStartWorldPos;

            if (draggedPiece != null)
            {
                draggedPiece.SetSelected(false);

                // スワイプ方向を判定（最低移動距離）
                float minDist = board.TotalCellSize * 0.3f;

                if (delta.magnitude >= minDist)
                {
                    int dr = 0, dc = 0;

                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                        dc = delta.x > 0 ? 1 : -1;
                    else
                        dr = delta.y > 0 ? 1 : -1;

                    int newRow = draggedPiece.row + dr;
                    int newCol = draggedPiece.col + dc;

                    if (board.IsValidPosition(newRow, newCol))
                    {
                        // 1マス入替え
                        board.SwapPieces(draggedPiece.row, draggedPiece.col, newRow, newCol);
                        StartCoroutine(ResolveChains());
                    }
                }
            }

            draggedPiece = null;
            isDragging = false;
        }
    }

    // ============================================================
    // Action Mode: 自由移動 (スムーズスワップ)
    // ============================================================

    private void UpdateActionMode(bool justPressed, bool pressed, bool justReleased)
    {
        if (justPressed && !isDragging)
        {
            TryStartDrag();
        }
        else if (isDragging && pressed)
        {
            UpdateDrag();

            if (Time.time - dragStartTime > dragTimeLimit)
                EndDrag();
        }
        else if (isDragging && justReleased)
        {
            EndDrag();
        }
    }

    private Vector3 GetPointerWorldPos()
    {
        Vector2 screenPos = Pointer.current.position.ReadValue();
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        worldPos.z = 0f;
        return worldPos;
    }

    private void TryStartDrag()
    {
        Vector3 worldPos = GetPointerWorldPos();
        Vector2Int gridPos = board.WorldToGridPosition(worldPos);

        if (!board.IsValidPosition(gridPos.x, gridPos.y)) return;

        KanjiPiece piece = board.Grid[gridPos.x, gridPos.y];
        if (piece == null) return;

        isDragging = true;
        draggedPiece = piece;
        dragStartTime = Time.time;
        piece.SetDragging(true);
    }

    private void UpdateDrag()
    {
        if (draggedPiece == null) return;

        Vector3 worldPos = GetPointerWorldPos();
        draggedPiece.transform.position = worldPos;

        Vector2Int gridPos = board.WorldToGridPosition(worldPos);
        if (!board.IsValidPosition(gridPos.x, gridPos.y)) return;

        int newRow = gridPos.x;
        int newCol = gridPos.y;

        if (newRow != draggedPiece.row || newCol != draggedPiece.col)
        {
            int oldRow = draggedPiece.row;
            int oldCol = draggedPiece.col;

            // スムーズスワップ: 押し出されるピースをアニメーションで移動
            KanjiPiece displacedPiece = board.Grid[newRow, newCol];

            board.Grid[oldRow, oldCol] = displacedPiece;
            board.Grid[newRow, newCol] = draggedPiece;

            if (displacedPiece != null)
            {
                displacedPiece.row = oldRow;
                displacedPiece.col = oldCol;
                displacedPiece.MoveTo(board.GridToWorldPosition(oldRow, oldCol), 0.12f);
            }

            draggedPiece.row = newRow;
            draggedPiece.col = newCol;
        }
    }

    private void EndDrag()
    {
        if (draggedPiece != null)
        {
            draggedPiece.SetDragging(false);
            Vector3 correctPos = board.GridToWorldPosition(draggedPiece.row, draggedPiece.col);
            draggedPiece.SetPositionImmediate(correctPos);
            draggedPiece = null;
        }

        isDragging = false;
        StartCoroutine(ResolveChains());
    }

    // ============================================================
    // 連鎖（コンボ）システム
    // ============================================================

    private IEnumerator ResolveChains()
    {
        isProcessing = true;
        currentCombo = 0;

        while (true)
        {
            yield return new WaitForSeconds(0.2f);

            List<MatchInfo> matches = FindAllMatches();
            if (matches.Count == 0) break;

            currentCombo++;
            OnComboChanged?.Invoke(currentCombo);

            yield return StartCoroutine(ProcessMatches(matches));

            yield return new WaitForSeconds(0.15f);
            board.DropPieces();

            yield return new WaitForSeconds(0.2f);
            board.RefillEmptyCells();
        }

        if (currentCombo > 0)
            OnComboChanged?.Invoke(0);

        // 自動シャッフル（ノーペナルティ）
        int shuffleCount = 0;
        while (!HasAnyPossibleMatch() && shuffleCount < 10)
        {
            shuffleCount++;
            OnAutoShuffle?.Invoke();

            yield return new WaitForSeconds(0.3f);
            board.ClearBoard();
            yield return new WaitForSeconds(0.15f);
            board.FillBoard();
            yield return new WaitForSeconds(0.3f);

            // シャッフル後連鎖チェック
            List<MatchInfo> postMatches = FindAllMatches();
            while (postMatches.Count > 0)
            {
                currentCombo++;
                OnComboChanged?.Invoke(currentCombo);
                yield return StartCoroutine(ProcessMatches(postMatches));
                yield return new WaitForSeconds(0.15f);
                board.DropPieces();
                yield return new WaitForSeconds(0.2f);
                board.RefillEmptyCells();
                yield return new WaitForSeconds(0.2f);
                postMatches = FindAllMatches();
            }

            if (currentCombo > 0)
                OnComboChanged?.Invoke(0);
        }

        isProcessing = false;
    }

    private IEnumerator ProcessMatches(List<MatchInfo> matches)
    {
        HashSet<Vector2Int> toRemove = new HashSet<Vector2Int>();

        foreach (var match in matches)
        {
            if (match.isRecipeMatch && match.recipe != null)
            {
                Vector2Int posA = match.positions[0];
                Vector2Int posB = match.positions[1];
                int comboScore = match.recipe.score + (currentCombo - 1) * 50;

                OnCombineSuccess?.Invoke(match.recipe);

                if (board.Grid[posA.x, posA.y] != null)
                    board.Grid[posA.x, posA.y].PlayMergePopEffect();

                yield return new WaitForSeconds(0.15f);

                board.RemovePieceAt(posA.x, posA.y);
                board.RemovePieceAt(posB.x, posB.y);

                KanjiPiece resultPiece = board.SpawnPieceAt(posA.x, posA.y);
                if (resultPiece != null)
                {
                    resultPiece.SetKanji(match.recipe.result);
                    resultPiece.PlayMergePopEffect();
                }
            }
            else if (match.isElimination)
            {
                int elimScore = match.score + (currentCombo - 1) * 50;
                OnEliminationSuccess?.Invoke(match.resultKanji, elimScore);
                foreach (var pos in match.positions) toRemove.Add(pos);
            }
            else if (match.positions.Count >= 3)
            {
                int tripleScore = 200 + (currentCombo - 1) * 50;
                OnEliminationSuccess?.Invoke(match.resultKanji, tripleScore);

                if (match.resultKanji != null)
                {
                    Vector2Int firstPos = match.positions[0];
                    foreach (var pos in match.positions) toRemove.Add(pos);

                    yield return new WaitForSeconds(0.15f);
                    foreach (var pos in toRemove) board.RemovePieceAt(pos.x, pos.y);
                    toRemove.Clear();

                    if (!string.IsNullOrEmpty(match.resultKanji))
                    {
                        KanjiPiece resultPiece = board.SpawnPieceAt(firstPos.x, firstPos.y);
                        if (resultPiece != null)
                        {
                            resultPiece.SetKanji(match.resultKanji);
                            resultPiece.PlayMergePopEffect();
                        }
                    }
                    continue;
                }

                foreach (var pos in match.positions) toRemove.Add(pos);
            }
        }

        if (toRemove.Count > 0)
        {
            yield return new WaitForSeconds(0.15f);
            foreach (var pos in toRemove) board.RemovePieceAt(pos.x, pos.y);
        }
    }

    // ============================================================
    // マッチ検索
    // ============================================================

    private List<MatchInfo> FindAllMatches()
    {
        List<MatchInfo> matches = new List<MatchInfo>();
        HashSet<string> usedPositions = new HashSet<string>();

        FindRecipeMatches(matches, usedPositions);
        FindHorizontalTriples(matches, usedPositions);
        FindVerticalTriples(matches, usedPositions);
        FindTerminalPairEliminations(matches, usedPositions);

        return matches;
    }

    private void FindRecipeMatches(List<MatchInfo> matches, HashSet<string> usedPositions)
    {
        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
            {
                if (c + 1 < PuzzleBoard.SIZE)
                    TryRecipeMatch(r, c, r, c + 1, matches, usedPositions);
                if (r + 1 < PuzzleBoard.SIZE)
                    TryRecipeMatch(r, c, r + 1, c, matches, usedPositions);
            }
        }
    }

    private void TryRecipeMatch(int r1, int c1, int r2, int c2,
        List<MatchInfo> matches, HashSet<string> usedPositions)
    {
        KanjiPiece a = board.Grid[r1, c1];
        KanjiPiece b = board.Grid[r2, c2];
        if (a == null || b == null) return;

        string keyA = $"{r1},{c1}";
        string keyB = $"{r2},{c2}";
        if (usedPositions.Contains(keyA) || usedPositions.Contains(keyB)) return;

        KanjiRecipe recipe = FindMatchingRecipe(a.Kanji, b.Kanji);
        if (recipe == null) return;

        matches.Add(new MatchInfo
        {
            positions = new List<Vector2Int> { new Vector2Int(r1, c1), new Vector2Int(r2, c2) },
            recipe = recipe,
            resultKanji = recipe.result,
            score = recipe.score,
            isRecipeMatch = true,
            isElimination = false
        });

        usedPositions.Add(keyA);
        usedPositions.Add(keyB);
    }

    private void FindHorizontalTriples(List<MatchInfo> matches, HashSet<string> usedPositions)
    {
        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c <= PuzzleBoard.SIZE - 3; c++)
            {
                KanjiPiece a = board.Grid[r, c];
                KanjiPiece b = board.Grid[r, c + 1];
                KanjiPiece d = board.Grid[r, c + 2];
                if (a == null || b == null || d == null) continue;
                if (a.Kanji != b.Kanji || b.Kanji != d.Kanji) continue;

                string keyA = $"{r},{c}";
                string keyB = $"{r},{c + 1}";
                string keyC = $"{r},{c + 2}";
                if (usedPositions.Contains(keyA) || usedPositions.Contains(keyB) || usedPositions.Contains(keyC))
                    continue;

                matches.Add(new MatchInfo
                {
                    positions = new List<Vector2Int>
                    {
                        new Vector2Int(r, c), new Vector2Int(r, c + 1), new Vector2Int(r, c + 2)
                    },
                    recipe = null,
                    resultKanji = GetTripleResult(a.Kanji),
                    score = 200,
                    isRecipeMatch = false,
                    isElimination = false
                });

                usedPositions.Add(keyA);
                usedPositions.Add(keyB);
                usedPositions.Add(keyC);
            }
        }
    }

    private void FindVerticalTriples(List<MatchInfo> matches, HashSet<string> usedPositions)
    {
        for (int c = 0; c < PuzzleBoard.SIZE; c++)
        {
            for (int r = 0; r <= PuzzleBoard.SIZE - 3; r++)
            {
                KanjiPiece a = board.Grid[r, c];
                KanjiPiece b = board.Grid[r + 1, c];
                KanjiPiece d = board.Grid[r + 2, c];
                if (a == null || b == null || d == null) continue;
                if (a.Kanji != b.Kanji || b.Kanji != d.Kanji) continue;

                string keyA = $"{r},{c}";
                string keyB = $"{r + 1},{c}";
                string keyC = $"{r + 2},{c}";
                if (usedPositions.Contains(keyA) || usedPositions.Contains(keyB) || usedPositions.Contains(keyC))
                    continue;

                matches.Add(new MatchInfo
                {
                    positions = new List<Vector2Int>
                    {
                        new Vector2Int(r, c), new Vector2Int(r + 1, c), new Vector2Int(r + 2, c)
                    },
                    recipe = null,
                    resultKanji = GetTripleResult(a.Kanji),
                    score = 200,
                    isRecipeMatch = false,
                    isElimination = false
                });

                usedPositions.Add(keyA);
                usedPositions.Add(keyB);
                usedPositions.Add(keyC);
            }
        }
    }

    private string GetTripleResult(string kanji)
    {
        switch (kanji)
        {
            case "木": return "森";
            case "日": return "晶";
            case "火": return "焱";
            case "人": return "众";
            case "山": return "嵐";
            case "石": return "磊";
            default: return null;
        }
    }

    private void FindTerminalPairEliminations(List<MatchInfo> matches, HashSet<string> usedPositions)
    {
        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
            {
                KanjiPiece piece = board.Grid[r, c];
                if (piece == null || !terminalKanji.Contains(piece.Kanji)) continue;

                string key = $"{r},{c}";
                if (usedPositions.Contains(key)) continue;

                if (c + 1 < PuzzleBoard.SIZE)
                {
                    KanjiPiece other = board.Grid[r, c + 1];
                    string otherKey = $"{r},{c + 1}";
                    if (other != null && other.Kanji == piece.Kanji && !usedPositions.Contains(otherKey))
                    {
                        matches.Add(new MatchInfo
                        {
                            positions = new List<Vector2Int> { new Vector2Int(r, c), new Vector2Int(r, c + 1) },
                            recipe = null, resultKanji = piece.Kanji, score = eliminationScore,
                            isRecipeMatch = false, isElimination = true
                        });
                        usedPositions.Add(key);
                        usedPositions.Add(otherKey);
                        continue;
                    }
                }

                if (r + 1 < PuzzleBoard.SIZE)
                {
                    KanjiPiece other = board.Grid[r + 1, c];
                    string otherKey = $"{r + 1},{c}";
                    if (other != null && other.Kanji == piece.Kanji && !usedPositions.Contains(otherKey))
                    {
                        matches.Add(new MatchInfo
                        {
                            positions = new List<Vector2Int> { new Vector2Int(r, c), new Vector2Int(r + 1, c) },
                            recipe = null, resultKanji = piece.Kanji, score = eliminationScore,
                            isRecipeMatch = false, isElimination = true
                        });
                        usedPositions.Add(key);
                        usedPositions.Add(otherKey);
                    }
                }
            }
        }
    }

    private KanjiRecipe FindMatchingRecipe(string a, string b)
    {
        if (recipes == null) return null;
        foreach (var recipe in recipes)
            if (recipe.Matches(a, b)) return recipe;
        return null;
    }

    // ============================================================
    // 手詰まり判定
    // ============================================================

    private bool HasAnyPossibleMatch()
    {
        List<MatchInfo> currentMatches = FindAllMatches();
        if (currentMatches.Count > 0) return true;

        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
            {
                if (c + 1 < PuzzleBoard.SIZE && SimulateSwapAndCheck(r, c, r, c + 1))
                    return true;
                if (r + 1 < PuzzleBoard.SIZE && SimulateSwapAndCheck(r, c, r + 1, c))
                    return true;
            }
        }

        return false;
    }

    private bool SimulateSwapAndCheck(int r1, int c1, int r2, int c2)
    {
        KanjiPiece a = board.Grid[r1, c1];
        KanjiPiece b = board.Grid[r2, c2];
        if (a == null || b == null) return false;

        board.Grid[r1, c1] = b;
        board.Grid[r2, c2] = a;
        List<MatchInfo> matches = FindAllMatches();
        board.Grid[r1, c1] = a;
        board.Grid[r2, c2] = b;

        return matches.Count > 0;
    }

    // ============================================================
    // 盤面リセット
    // ============================================================

    public void ResetBoard()
    {
        StartCoroutine(ExecuteReset());
    }

    private IEnumerator ExecuteReset()
    {
        isProcessing = true;
        board.ClearBoard();

        yield return new WaitForSeconds(0.2f);
        board.FillBoard();
        yield return new WaitForSeconds(0.3f);

        yield return StartCoroutine(ResolveChains());
    }
}
