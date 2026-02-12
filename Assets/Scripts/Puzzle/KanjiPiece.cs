using UnityEngine;
using TMPro;

/// <summary>
/// 盤面上の漢字ピースのコンポーネント
/// 漢字テキストの表示、グリッド座標管理、移動アニメーションを担当
/// </summary>
public class KanjiPiece : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private TextMeshPro kanjiText;
    [SerializeField] private SpriteRenderer background;

    [Header("グリッド座標")]
    public int row;
    public int col;

    /// <summary>
    /// この漢字ピースの文字
    /// </summary>
    public string Kanji { get; private set; }

    /// <summary>
    /// 選択中かどうか
    /// </summary>
    public bool IsSelected { get; private set; }

    private Color normalBgColor = new Color(1f, 0.95f, 0.85f);
    private Color selectedBgColor = new Color(1f, 0.75f, 0.3f);

    /// <summary>
    /// 漢字ピースを初期化する
    /// </summary>
    public void Initialize(string kanji, int r, int c)
    {
        Kanji = kanji;
        row = r;
        col = c;
        UpdateVisual();
    }

    /// <summary>
    /// 漢字テキストを変更する
    /// </summary>
    public void SetKanji(string kanji)
    {
        Kanji = kanji;
        if (kanjiText != null)
        {
            kanjiText.text = kanji;
        }
    }

    /// <summary>
    /// ビジュアルを更新する
    /// </summary>
    public void UpdateVisual()
    {
        if (kanjiText != null)
        {
            kanjiText.text = Kanji;
        }
        SetSelected(false);
    }

    /// <summary>
    /// 選択状態を切り替える
    /// </summary>
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (background != null)
        {
            background.color = selected ? selectedBgColor : normalBgColor;
        }

        // 選択時に少し拡大
        transform.localScale = selected ? Vector3.one * 1.15f : Vector3.one;
    }

    /// <summary>
    /// ワールド座標を滑らかに移動（アニメーション）
    /// </summary>
    public void MoveTo(Vector3 targetPos, float duration = 0.2f)
    {
        StopAllCoroutines();
        StartCoroutine(MoveCoroutine(targetPos, duration));
    }

    /// <summary>
    /// 即座に位置を設定する
    /// </summary>
    public void SetPositionImmediate(Vector3 pos)
    {
        transform.position = pos;
    }

    private System.Collections.IEnumerator MoveCoroutine(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - (1f - t) * (1f - t); // EaseOutQuad
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        transform.position = target;
    }
}
