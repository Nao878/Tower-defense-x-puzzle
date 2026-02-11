using UnityEngine;

/// <summary>
/// 合成パターンの種類
/// </summary>
public enum PatternType
{
    Vertical2,    // 縦2マス
    Horizontal2,  // 横2マス
    LShape,       // L字型
    TShape,       // T字型
    Square2x2     // 2x2ブロック
}

/// <summary>
/// 合成レシピの定義（ScriptableObject）
/// 特定のパーツの配置パターンと、完成するユニットを紐づける
/// </summary>
[CreateAssetMenu(fileName = "NewRecipeData", menuName = "PuzzleTD/Recipe Data")]
public class RecipeData : ScriptableObject
{
    [Header("レシピ情報")]
    public string recipeName;

    [Tooltip("レシピの優先度（高い値が優先）")]
    public int priority = 0;

    [Header("必要パターン")]
    [Tooltip("配置パターンの種類")]
    public PatternType patternType;

    [Tooltip("パターン内で必要なピースの種類（offsetsと同じ順番）")]
    public PieceType[] requiredPieces;

    [Tooltip("基準セル(0,0)からの相対位置。requiredPiecesと同じ順番で対応")]
    public Vector2Int[] offsets;

    [Header("合成結果")]
    [Tooltip("このレシピで完成するユニット")]
    public UnitData resultUnit;

    /// <summary>
    /// レシピの妥当性をチェック
    /// </summary>
    public bool IsValid()
    {
        return requiredPieces != null
            && offsets != null
            && requiredPieces.Length == offsets.Length
            && requiredPieces.Length > 0
            && resultUnit != null;
    }
}
