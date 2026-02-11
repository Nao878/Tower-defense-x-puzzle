using UnityEngine;

/// <summary>
/// 盤面上の各パーツ（ドロップ）のコンポーネント
/// グリッド座標とビジュアルを管理する
/// </summary>
public class Piece : MonoBehaviour
{
    [Header("データ")]
    [SerializeField] private PieceData pieceData;

    [Header("グリッド座標")]
    public int row;
    public int col;

    private SpriteRenderer spriteRenderer;

    /// <summary>
    /// パーツの種類データ
    /// </summary>
    public PieceData PieceData => pieceData;

    /// <summary>
    /// パーツの種類
    /// </summary>
    public PieceType Type => pieceData != null ? pieceData.pieceType : PieceType.Base;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// PieceDataを設定し、ビジュアルを更新する
    /// </summary>
    public void Initialize(PieceData data, int r, int c)
    {
        pieceData = data;
        row = r;
        col = c;
        UpdateVisual();
    }

    /// <summary>
    /// ビジュアルをPieceDataに合わせて更新する
    /// </summary>
    public void UpdateVisual()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (pieceData != null)
        {
            if (pieceData.icon != null)
            {
                spriteRenderer.sprite = pieceData.icon;
                spriteRenderer.color = Color.white;
            }
            else
            {
                // アイコン未設定の場合は色で代替表示
                spriteRenderer.color = pieceData.color;
            }
        }
    }

    /// <summary>
    /// ワールド座標を滑らかに移動させる（アニメーション用）
    /// </summary>
    public void MoveTo(Vector3 targetPos, float duration = 0.15f)
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
            // EaseOutQuadで滑らかな移動
            t = 1f - (1f - t) * (1f - t);
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        transform.position = target;
    }
}
