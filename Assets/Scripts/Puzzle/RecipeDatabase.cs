using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// レシピマッチング結果
/// </summary>
public struct RecipeMatch
{
    public RecipeData recipe;
    public List<Vector2Int> matchedCells; // マッチしたセルの座標リスト

    public RecipeMatch(RecipeData recipe, List<Vector2Int> cells)
    {
        this.recipe = recipe;
        this.matchedCells = cells;
    }
}

/// <summary>
/// 合成レシピのデータベース
/// 盤面をスキャンし、レシピに合致するパターンを検出する
/// </summary>
public class RecipeDatabase : MonoBehaviour
{
    [Header("レシピ一覧")]
    [SerializeField] private RecipeData[] recipes;

    /// <summary>
    /// 全レシピを取得する
    /// </summary>
    public RecipeData[] Recipes => recipes;

    /// <summary>
    /// 盤面全体をスキャンし、マッチした全レシピを返す
    /// 優先度の高いレシピから先に返す
    /// </summary>
    public List<RecipeMatch> CheckRecipes(Piece[,] board)
    {
        List<RecipeMatch> matches = new List<RecipeMatch>();

        if (recipes == null || recipes.Length == 0)
            return matches;

        // 優先度でソートしたレシピリストを作成
        RecipeData[] sorted = (RecipeData[])recipes.Clone();
        System.Array.Sort(sorted, (a, b) => b.priority.CompareTo(a.priority));

        // 使用済みセルを追跡
        HashSet<Vector2Int> usedCells = new HashSet<Vector2Int>();

        foreach (var recipe in sorted)
        {
            if (!recipe.IsValid()) continue;

            // 盤面上の全セルを基準点として試行
            for (int r = 0; r < PuzzleBoard.ROWS; r++)
            {
                for (int c = 0; c < PuzzleBoard.COLS; c++)
                {
                    List<Vector2Int> matchedCells = TryMatchAt(board, recipe, r, c, usedCells);
                    if (matchedCells != null)
                    {
                        matches.Add(new RecipeMatch(recipe, matchedCells));

                        // マッチしたセルを使用済みとしてマーク
                        foreach (var cell in matchedCells)
                        {
                            usedCells.Add(cell);
                        }
                    }
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// 指定位置を基準点としてレシピのパターンマッチを試みる
    /// </summary>
    private List<Vector2Int> TryMatchAt(Piece[,] board, RecipeData recipe, int baseRow, int baseCol, HashSet<Vector2Int> usedCells)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        for (int i = 0; i < recipe.offsets.Length; i++)
        {
            int checkRow = baseRow + recipe.offsets[i].y;
            int checkCol = baseCol + recipe.offsets[i].x;

            // 範囲外チェック
            if (checkRow < 0 || checkRow >= PuzzleBoard.ROWS ||
                checkCol < 0 || checkCol >= PuzzleBoard.COLS)
            {
                return null;
            }

            Vector2Int cellPos = new Vector2Int(checkRow, checkCol);

            // 既に使用済みのセルはスキップ
            if (usedCells.Contains(cellPos))
                return null;

            // ピースが存在するかチェック
            Piece piece = board[checkRow, checkCol];
            if (piece == null)
                return null;

            // ピースの種類が合致するかチェック
            if (piece.Type != recipe.requiredPieces[i])
                return null;

            cells.Add(cellPos);
        }

        return cells;
    }
}
