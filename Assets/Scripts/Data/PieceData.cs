using UnityEngine;

/// <summary>
/// パーツ（ドロップ）の種類を定義する列挙型
/// </summary>
public enum PieceType
{
    Base,       // 台座
    CannonTop,  // 砲台
    Engine,     // エンジン
    Armor,      // 装甲
    Shield,     // シールド
    Missile     // ミサイル
}

/// <summary>
/// パーツの定義データ（ScriptableObject）
/// 各パーツの見た目やタイプを管理する
/// </summary>
[CreateAssetMenu(fileName = "NewPieceData", menuName = "PuzzleTD/Piece Data")]
public class PieceData : ScriptableObject
{
    [Header("基本設定")]
    public PieceType pieceType;
    public string displayName;

    [Header("ビジュアル")]
    public Sprite icon;
    public Color color = Color.white;
}
