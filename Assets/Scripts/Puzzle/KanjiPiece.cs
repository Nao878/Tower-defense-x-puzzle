using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// 盤面上の漢字ピースのコンポーネント
/// 漢字テキストの表示、グリッド座標管理、移動アニメーション、振動演出を担当
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

    /// <summary>
    /// 合体可能ペアの一部か（振動用）
    /// </summary>
    public bool IsCombinable { get; private set; }

    private Color normalBgColor = new Color(1f, 0.95f, 0.85f);
    private Color selectedBgColor = new Color(1f, 0.75f, 0.3f);
    private Color combinableBgColor = new Color(1f, 0.88f, 0.7f);

    private Vector3 baseLocalPosition;
    private Coroutine shakeCoroutine;

    /// <summary>
    /// 漢字ピースを初期化する
    /// </summary>
    public void Initialize(string kanji, int r, int c)
    {
        Kanji = kanji;
        row = r;
        col = c;
        IsCombinable = false;
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
        UpdateBackgroundColor();

        // 選択時に少し拡大
        transform.localScale = selected ? Vector3.one * 1.15f : Vector3.one;
    }

    /// <summary>
    /// 合体可能フラグを設定し、振動を開始/停止する
    /// </summary>
    public void SetCombinable(bool combinable)
    {
        IsCombinable = combinable;
        UpdateBackgroundColor();

        if (combinable)
        {
            StartShake();
        }
        else
        {
            StopShake();
        }
    }

    private void UpdateBackgroundColor()
    {
        if (background == null) return;

        if (IsSelected)
        {
            background.color = selectedBgColor;
        }
        else if (IsCombinable)
        {
            background.color = combinableBgColor;
        }
        else
        {
            background.color = normalBgColor;
        }
    }

    /// <summary>
    /// 振動アニメーションを開始する
    /// </summary>
    private void StartShake()
    {
        if (shakeCoroutine != null) return;
        baseLocalPosition = transform.localPosition;
        shakeCoroutine = StartCoroutine(ShakeCoroutine());
    }

    /// <summary>
    /// 振動アニメーションを停止し、元の位置に戻す
    /// </summary>
    private void StopShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
            // 振動オフセットをリセットして基準位置に戻す
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
            // 基準位置からのオフセットとして振動を適用
            transform.localPosition = baseLocalPosition + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }
    }

    /// <summary>
    /// ワールド座標を滑らかに移動（アニメーション）
    /// </summary>
    public void MoveTo(Vector3 targetPos, float duration = 0.2f)
    {
        StopShake();
        StopAllCoroutines();
        shakeCoroutine = null;
        StartCoroutine(MoveCoroutine(targetPos, duration));
    }

    /// <summary>
    /// 即座に位置を設定する
    /// </summary>
    public void SetPositionImmediate(Vector3 pos)
    {
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
        // 移動完了後、基準ローカル位置を更新
        baseLocalPosition = transform.localPosition;

        // 合体可能なら再度振動開始
        if (IsCombinable)
        {
            StartShake();
        }
    }

    private void OnDestroy()
    {
        StopShake();
    }
}
