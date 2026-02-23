using UnityEngine;

/// <summary>
/// 漢字合体レシピのScriptableObject
/// 素材A + 素材B = 完成漢字 + スコア
/// ワイルドカード対応
/// </summary>
[CreateAssetMenu(fileName = "NewKanjiRecipe", menuName = "KanjiPuzzle/Kanji Recipe")]
public class KanjiRecipe : ScriptableObject
{
    [Header("素材")]
    [Tooltip("素材の漢字A")]
    public string materialA;

    [Tooltip("素材の漢字B")]
    public string materialB;

    [Header("結果")]
    [Tooltip("合体後の漢字")]
    public string result;

    [Tooltip("合体時に得られるスコア")]
    public int score = 100;

    [Header("ワイルドカード")]
    [Tooltip("素材Aがワイルドカード（部首）かどうか")]
    public bool isWildcardA = false;

    [Tooltip("素材Bがワイルドカード（部首）かどうか")]
    public bool isWildcardB = false;

    /// <summary>
    /// 指定した2つの漢字がこのレシピにマッチするか判定する
    /// 順序は問わない（A+B でも B+A でもOK）
    /// </summary>
    public bool Matches(string a, string b)
    {
        return (a == materialA && b == materialB)
            || (a == materialB && b == materialA);
    }
}
