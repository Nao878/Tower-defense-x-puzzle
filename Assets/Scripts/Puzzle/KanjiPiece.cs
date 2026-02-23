using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// 盤面上の漢字ピースのコンポーネント
/// 表示、グリッド座標管理、移動、ドラッグ視覚フィードバック
/// </summary>
public class KanjiPiece : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private TextMeshPro kanjiText;
    [SerializeField] private SpriteRenderer background;

    [Header("グリッド座標")]
    public int row;
    public int col;

    public string Kanji { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsCombinable { get; private set; }
    public bool IsDragging { get; private set; }

    private Color normalBgColor = new Color(1f, 0.95f, 0.85f);
    private Color selectedBgColor = new Color(1f, 0.75f, 0.3f);
    private Color combinableBgColor = new Color(1f, 0.88f, 0.7f);
    private Color draggingBgColor = new Color(1f, 0.85f, 0.4f);

    private Vector3 baseLocalPosition;
    private Coroutine shakeCoroutine;
    private int originalSortingOrder;

    public void Initialize(string kanji, int r, int c)
    {
        Kanji = kanji;
        row = r;
        col = c;
        IsCombinable = false;
        IsDragging = false;
        UpdateVisual();
    }

    public void SetKanji(string kanji)
    {
        Kanji = kanji;
        if (kanjiText != null) kanjiText.text = kanji;
    }

    public void UpdateVisual()
    {
        if (kanjiText != null) kanjiText.text = Kanji;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        UpdateBackgroundColor();
        transform.localScale = selected ? Vector3.one * 1.15f : Vector3.one;
    }

    public void SetCombinable(bool combinable)
    {
        IsCombinable = combinable;
        UpdateBackgroundColor();
        if (combinable) StartShake();
        else StopShake();
    }

    /// <summary>
    /// ドラッグ状態の切り替え（半透明化＋sortingOrder引き上げ）
    /// </summary>
    public void SetDragging(bool dragging)
    {
        IsDragging = dragging;

        if (dragging)
        {
            originalSortingOrder = background != null ? background.sortingOrder : 0;
            if (background != null) background.sortingOrder = 100;
            if (kanjiText != null) kanjiText.sortingOrder = 101;
            transform.localScale = Vector3.one * 1.2f;

            // 半透明に
            if (background != null)
            {
                Color c = draggingBgColor;
                c.a = 0.85f;
                background.color = c;
            }
        }
        else
        {
            if (background != null) background.sortingOrder = originalSortingOrder;
            if (kanjiText != null) kanjiText.sortingOrder = originalSortingOrder + 1;
            transform.localScale = Vector3.one;
            UpdateBackgroundColor();
        }
    }

    /// <summary>
    /// 合体成功時のスケールポップ演出
    /// </summary>
    public void PlayMergePopEffect()
    {
        StopAllCoroutines();
        shakeCoroutine = null;
        StartCoroutine(MergePopCoroutine());
    }

    private IEnumerator MergePopCoroutine()
    {
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.4f;
            transform.localScale = Vector3.one * scale;
            yield return null;
        }

        transform.localScale = Vector3.one;
    }

    private void UpdateBackgroundColor()
    {
        if (background == null) return;

        if (IsDragging)
            background.color = draggingBgColor;
        else if (IsSelected)
            background.color = selectedBgColor;
        else if (IsCombinable)
            background.color = combinableBgColor;
        else
            background.color = normalBgColor;
    }

    private void StartShake()
    {
        if (shakeCoroutine != null) return;
        baseLocalPosition = transform.localPosition;
        shakeCoroutine = StartCoroutine(ShakeCoroutine());
    }

    private void StopShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
            transform.localPosition = baseLocalPosition;
        }
    }

    private IEnumerator ShakeCoroutine()
    {
        float shakeAmount = 0.03f;
        float shakeSpeed = 8f;

        while (true)
        {
            float offsetX = Mathf.Sin(Time.time * shakeSpeed) * shakeAmount;
            float offsetY = Mathf.Cos(Time.time * shakeSpeed * 1.3f) * shakeAmount * 0.5f;
            transform.localPosition = baseLocalPosition + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }
    }

    public void MoveTo(Vector3 targetPos, float duration = 0.2f)
    {
        StopShake();
        StopAllCoroutines();
        shakeCoroutine = null;
        StartCoroutine(MoveCoroutine(targetPos, duration));
    }

    public void SetPositionImmediate(Vector3 pos)
    {
        StopShake();
        StopAllCoroutines();
        shakeCoroutine = null;
        transform.position = pos;
        baseLocalPosition = transform.localPosition;
    }

    private IEnumerator MoveCoroutine(Vector3 target, float duration)
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
        baseLocalPosition = transform.localPosition;

        if (IsCombinable) StartShake();
    }

    private void OnDestroy()
    {
        StopShake();
    }
}
