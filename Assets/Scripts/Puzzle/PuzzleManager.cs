using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// マッチ情報
/// </summary>
public struct MatchInfo
{
    public List<Vector2Int> positions;
    public KanjiRecipe recipe;       // レシピマッチの場合
    public string resultKanji;       // 結果漢字
    public int score;
    public bool isRecipeMatch;       // レシピマッチか同種並びか
    public bool isElimination;       // 終端漢字消去
}

/// <summary>
/// パズルマネージャー（パズドラ風）
/// ドラッグ操作・押し退け・連鎖コンボシステム
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    [Header("ボード参照")]
    [SerializeField] private PuzzleBoard board;

    [Header("レシピ")]
    [SerializeField] private KanjiRecipe[] recipes;

    [Header("消去スコア")]
    [SerializeField] private int eliminationScore = 300;

    [Header("ドラッグ設定")]
    [SerializeField] private float dragTimeLimit = 5f;

    public event Action<KanjiRecipe> OnCombineSuccess;
    public event Action<string, int> OnEliminationSuccess;
    public event Action<int> OnComboChanged;
    public event Action<bool> OnStalemateChanged;

    private Camera mainCamera;
    private bool isProcessing = false;
    private bool isGameOver = false;
    private bool isDragging = false;
    private KanjiPiece draggedPiece;
    private float dragStartTime;
    private int currentCombo = 0;

    private HashSet<string> terminalKanji = new HashSet<string>();

    public KanjiRecipe[] Recipes => recipes;
    public bool IsStalemate { get; private set; }

    public void SetGameOver(bool gameOver) { isGameOver = gameOver; }

    private void Start()
    {
        mainCamera = Camera.main;
        if (board == null) board = GetComponent<PuzzleBoard>();

        BuildTerminalKanjiCache();
        board.InitializeBoard();
    }

    private void BuildTerminalKanjiCache()
    {
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

    // ============================================================
    // Update: ドラッグ入力処理
    // ============================================================

    private void Update()
    {
        if (isProcessing || isGameOver) return;

        if (Pointer.current == null) return;

        bool pressed = Pointer.current.press.isPressed;
        bool justPressed = Pointer.current.press.wasPressedThisFrame;
        bool justReleased = Pointer.current.press.wasReleasedThisFrame;

        if (justPressed && !isDragging)
        {
            TryStartDrag();
        }
        else if (isDragging && pressed)
        {
            UpdateDrag();

            // 5秒タイムリミット
            if (Time.time - dragStartTime > dragTimeLimit)
            {
                EndDrag();
            }
        }
        else if (isDragging && justReleased)
        {
            EndDrag();
        }
    }

    // ============================================================
    // ドラッグ操作
    // ============================================================

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

        // ドラッグ中のピースを指に追従
        draggedPiece.transform.position = worldPos;

        // 現在のセルを計算
        Vector2Int gridPos = board.WorldToGridPosition(worldPos);

        if (!board.IsValidPosition(gridPos.x, gridPos.y)) return;

        int newRow = gridPos.x;
        int newCol = gridPos.y;

        // 別のセルに入った場合、押し退け（スワップ）
        if (newRow != draggedPiece.row || newCol != draggedPiece.col)
        {
            int oldRow = draggedPiece.row;
            int oldCol = draggedPiece.col;

            board.SwapPiecesImmediate(oldRow, oldCol, newRow, newCol);
        }
    }

    private void EndDrag()
    {
        if (draggedPiece != null)
        {
            draggedPiece.SetDragging(false);

            // ドラッグ終了時、正しい位置にスナップ
            Vector3 correctPos = board.GridToWorldPosition(draggedPiece.row, draggedPiece.col);
            draggedPiece.SetPositionImmediate(correctPos);

            draggedPiece = null;
        }

        isDragging = false;

        // ドラッグ終了後、連鎖判定を開始
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

            // マッチを処理（消去・合体）
            yield return StartCoroutine(ProcessMatches(matches));

            // 重力落下
            yield return new WaitForSeconds(0.15f);
            board.DropPieces();

            // 補充
            yield return new WaitForSeconds(0.2f);
            board.RefillEmptyCells();
        }

        // 連鎖終了
        if (currentCombo > 0)
        {
            OnComboChanged?.Invoke(0); // 0=連鎖終了
        }

        // 手詰まり判定
        CheckStalemate();

        isProcessing = false;
    }

    private IEnumerator ProcessMatches(List<MatchInfo> matches)
    {
        // 重複しない消去位置を集める
        HashSet<Vector2Int> toRemove = new HashSet<Vector2Int>();

        foreach (var match in matches)
        {
            if (match.isRecipeMatch && match.recipe != null)
            {
                // レシピマッチ：素材を消去して結果をスポーン
                Vector2Int posA = match.positions[0];
                Vector2Int posB = match.positions[1];

                string resultKanji = match.recipe.result;
                int score = match.recipe.score;

                // コンボボーナス
                int comboScore = score + (currentCombo - 1) * 50;

                OnCombineSuccess?.Invoke(match.recipe);

                // 素材を消去
                if (board.Grid[posA.x, posA.y] != null)
                    board.Grid[posA.x, posA.y].PlayMergePopEffect();

                yield return new WaitForSeconds(0.15f);

                board.RemovePieceAt(posA.x, posA.y);
                board.RemovePieceAt(posB.x, posB.y);

                // 結果漢字をposAの位置にスポーン
                KanjiPiece resultPiece = board.SpawnPieceAt(posA.x, posA.y);
                if (resultPiece != null)
                {
                    resultPiece.SetKanji(resultKanji);
                    resultPiece.PlayMergePopEffect();
                }
            }
            else if (match.isElimination)
            {
                // 同種消去
                int elimScore = match.score + (currentCombo - 1) * 50;
                OnEliminationSuccess?.Invoke(match.resultKanji, elimScore);

                foreach (var pos in match.positions)
                {
                    toRemove.Add(pos);
                }
            }
            else if (match.positions.Count >= 3)
            {
                // 3つ揃え：同じ漢字3つ並び
                int tripleScore = 200 + (currentCombo - 1) * 50;
                OnEliminationSuccess?.Invoke(match.resultKanji, tripleScore);

                // 特殊ルール：木+木+木=森 等
                if (match.resultKanji != null)
                {
                    Vector2Int firstPos = match.positions[0];
                    foreach (var pos in match.positions)
                    {
                        toRemove.Add(pos);
                    }

                    yield return new WaitForSeconds(0.15f);

                    foreach (var pos in toRemove)
                    {
                        board.RemovePieceAt(pos.x, pos.y);
                    }
                    toRemove.Clear();

                    // 結果漢字を生成
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

                foreach (var pos in match.positions)
                {
                    toRemove.Add(pos);
                }
            }
        }

        // 残りの消去対象をまとめて消す
        if (toRemove.Count > 0)
        {
            yield return new WaitForSeconds(0.15f);

            foreach (var pos in toRemove)
            {
                board.RemovePieceAt(pos.x, pos.y);
            }
        }
    }

    // ============================================================
    // マッチ検索
    // ============================================================

    private List<MatchInfo> FindAllMatches()
    {
        List<MatchInfo> matches = new List<MatchInfo>();
        HashSet<string> usedPositions = new HashSet<string>();

        // 1. 隣接2ピースのレシピマッチを検索
        FindRecipeMatches(matches, usedPositions);

        // 2. 同一漢字3つ並び（横方向）
        FindHorizontalTriples(matches, usedPositions);

        // 3. 同一漢字3つ並び（縦方向）
        FindVerticalTriples(matches, usedPositions);

        // 4. 終端漢字の同種2つ隣接消去
        FindTerminalPairEliminations(matches, usedPositions);

        return matches;
    }

    private void FindRecipeMatches(List<MatchInfo> matches, HashSet<string> usedPositions)
    {
        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
            {
                // 右隣
                if (c + 1 < PuzzleBoard.SIZE)
                    TryRecipeMatch(r, c, r, c + 1, matches, usedPositions);

                // 上隣
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

                string resultKanji = GetTripleResult(a.Kanji);

                matches.Add(new MatchInfo
                {
                    positions = new List<Vector2Int>
                    {
                        new Vector2Int(r, c),
                        new Vector2Int(r, c + 1),
                        new Vector2Int(r, c + 2)
                    },
                    recipe = null,
                    resultKanji = resultKanji,
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

                string resultKanji = GetTripleResult(a.Kanji);

                matches.Add(new MatchInfo
                {
                    positions = new List<Vector2Int>
                    {
                        new Vector2Int(r, c),
                        new Vector2Int(r + 1, c),
                        new Vector2Int(r + 2, c)
                    },
                    recipe = null,
                    resultKanji = resultKanji,
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

    /// <summary>
    /// 同じ漢字3つ並び時の結果漢字を返す
    /// </summary>
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
            case "月": return "鑫";
            default: return null; // 特殊結果なし→消去のみ
        }
    }

    private void FindTerminalPairEliminations(List<MatchInfo> matches, HashSet<string> usedPositions)
    {
        for (int r = 0; r < PuzzleBoard.SIZE; r++)
        {
            for (int c = 0; c < PuzzleBoard.SIZE; c++)
            {
                KanjiPiece piece = board.Grid[r, c];
                if (piece == null) continue;
                if (!IsTerminalKanji(piece.Kanji)) continue;

                string key = $"{r},{c}";
                if (usedPositions.Contains(key)) continue;

                // 右隣
                if (c + 1 < PuzzleBoard.SIZE)
                {
                    KanjiPiece other = board.Grid[r, c + 1];
                    string otherKey = $"{r},{c + 1}";
                    if (other != null && other.Kanji == piece.Kanji && !usedPositions.Contains(otherKey))
                    {
                        matches.Add(new MatchInfo
                        {
                            positions = new List<Vector2Int> { new Vector2Int(r, c), new Vector2Int(r, c + 1) },
                            recipe = null,
                            resultKanji = piece.Kanji,
                            score = eliminationScore,
                            isRecipeMatch = false,
                            isElimination = true
                        });
                        usedPositions.Add(key);
                        usedPositions.Add(otherKey);
                        continue;
                    }
                }

                // 上隣
                if (r + 1 < PuzzleBoard.SIZE)
                {
                    KanjiPiece other = board.Grid[r + 1, c];
                    string otherKey = $"{r + 1},{c}";
                    if (other != null && other.Kanji == piece.Kanji && !usedPositions.Contains(otherKey))
                    {
                        matches.Add(new MatchInfo
                        {
                            positions = new List<Vector2Int> { new Vector2Int(r, c), new Vector2Int(r + 1, c) },
                            recipe = null,
                            resultKanji = piece.Kanji,
                            score = eliminationScore,
                            isRecipeMatch = false,
                            isElimination = true
                        });
                        usedPositions.Add(key);
                        usedPositions.Add(otherKey);
                    }
                }
            }
        }
    }

    private bool IsTerminalKanji(string kanji)
    {
        return terminalKanji.Contains(kanji);
    }

    private KanjiRecipe FindMatchingRecipe(string a, string b)
    {
        if (recipes == null) return null;
        foreach (var recipe in recipes)
        {
            if (recipe.Matches(a, b)) return recipe;
        }
        return null;
    }

    // ============================================================
    // 手詰まり判定
    // ============================================================

    private void CheckStalemate()
    {
        bool hasMatch = HasAnyPossibleMatch();
        bool wasStalemate = IsStalemate;
        IsStalemate = !hasMatch;

        if (IsStalemate != wasStalemate)
        {
            OnStalemateChanged?.Invoke(IsStalemate);
        }
    }

    private bool HasAnyPossibleMatch()
    {
        // 現在の盤面でマッチがあるか
        List<MatchInfo> currentMatches = FindAllMatches();
        if (currentMatches.Count > 0) return true;

        // スワップでマッチが作れるか
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

        // 仮スワップ
        board.Grid[r1, c1] = b;
        board.Grid[r2, c2] = a;

        List<MatchInfo> matches = FindAllMatches();

        // 元に戻す
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

        IsStalemate = false;
        OnStalemateChanged?.Invoke(false);

        // リセット後にも連鎖判定
        yield return StartCoroutine(ResolveChains());

        isProcessing = false;
    }
}
