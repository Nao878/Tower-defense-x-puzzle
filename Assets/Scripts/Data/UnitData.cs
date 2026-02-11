using UnityEngine;

/// <summary>
/// 合成で完成するユニットのステータス定義（ScriptableObject）
/// 射程・攻撃速度・攻撃力などのパラメータを管理する
/// </summary>
[CreateAssetMenu(fileName = "NewUnitData", menuName = "PuzzleTD/Unit Data")]
public class UnitData : ScriptableObject
{
    [Header("基本情報")]
    public string unitName;
    public Sprite icon;
    public GameObject prefab;

    [Header("戦闘パラメータ")]
    [Tooltip("攻撃可能な射程距離")]
    public float range = 3f;

    [Tooltip("攻撃間隔（秒）")]
    public float attackSpeed = 1f;

    [Tooltip("一回の攻撃で与えるダメージ")]
    public float damage = 10f;
}
