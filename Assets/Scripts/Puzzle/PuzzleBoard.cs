using UnityEngine;

/// <summary>
/// 4x4パズル盤面のデータ管理
/// ピースの生成・入れ替え・重力落下・補充を担当
/// </summary>
public class PuzzleBoard : MonoBehaviour
{
    public const int SIZE = 4;

    [Header("設定")]
    [SerializeField] private float cellSize = 1.1f;
    [SerializeField] private float cellSpacing = 0.1f;
    [SerializeField] private GameObject piecePrefab;

    [Header("漢字プール")]
    [Tooltip("ランダム生成に使用する漢字のリスト")]
    [SerializeField] private string[] kanjiPool = { "木", "火", "日", "月", "人" };

    /// <summary>
    /// 盤面のグリッド配列 [row, col]
    /// row=0が一番下、row=SIZE-1が一番上
    /// </summary>
    public KanjiPiece[,] Grid { get; private set; }

    public float CellSize => cellSize;
    public float CellSpacing => cellSpacing;
    public GameObject PiecePrefab => piecePrefab;
    public string[] KanjiPool => kanjiPool;

    /// <summary>
    /// 1セル分のトータルサイズ（セル＋スペーシング）
    /// </summary>
    public float TotalCellSize => cellSize + cellSpacing;

    public void InitializeBoard()
    {
        Grid = new KanjiPiece[SIZE, SIZE];
        ClearBoard();
        FillBoard();
    }

    public void ClearBoard()
    {
        if (Grid == null) return;

        for (int r = 0; r < SIZE; r++)
        {
            for (int c = 0; c < SIZE; c++)
            {
                if (Grid[r, c] != null)
                {
                    Destroy(Grid[r, c].gameObject);
                    Grid[r, c] = null;
                }
            }
        }
    }

    public void FillBoard()
    {
        if (kanjiPool == null || kanjiPool.Length == 0)
        {
            Debug.LogWarning("PuzzleBoard: kanjiPoolが設定されていません");
            return;
        }

        for (int r = 0; r < SIZE; r++)
        {
            for (int c = 0; c < SIZE; c++)
            {
                if (Grid[r, c] == null)
                {
                    SpawnPieceAt(r, c);
                }
            }
        }
    }

    public KanjiPiece SpawnPieceAt(int row, int col, bool animateFromTop = false)
    {
        if (piecePrefab == null)
        {
            Debug.LogError("PuzzleBoard: piecePrefabが設定されていません");
            return null;
        }

        string randomKanji = kanjiPool[Random.Range(0, kanjiPool.Length)];
        Vector3 targetPos = GridToWorldPosition(row, col);

        Vector3 spawnPos = animateFromTop
            ? GridToWorldPosition(SIZE + 1, col)
            : targetPos;

        GameObject go = Instantiate(piecePrefab, spawnPos, Quaternion.identity, transform);
        go.name = $"Kanji_{row}_{col}";

        KanjiPiece piece = go.GetComponent<KanjiPiece>();
        if (piece == null)
            piece = go.AddComponent<KanjiPiece>();

        piece.Initialize(randomKanji, row, col);
        Grid[row, col] = piece;

        if (animateFromTop)
        {
            piece.MoveTo(targetPos, 0.3f);
        }

        return piece;
    }

    /// <summary>
    /// 2つのピースを即座に入れ替える（ドラッグ用）
    /// </summary>
    public void SwapPiecesImmediate(int r1, int c1, int r2, int c2)
    {
        KanjiPiece temp = Grid[r1, c1];
        Grid[r1, c1] = Grid[r2, c2];
        Grid[r2, c2] = temp;

        if (Grid[r1, c1] != null)
        {
            Grid[r1, c1].row = r1;
            Grid[r1, c1].col = c1;
            Grid[r1, c1].SetPositionImmediate(GridToWorldPosition(r1, c1));
        }
        if (Grid[r2, c2] != null)
        {
            Grid[r2, c2].row = r2;
            Grid[r2, c2].col = c2;
            // ドラッグ中のピースは位置固定しない（指に追従）
        }
    }

    /// <summary>
    /// 2つのピースをアニメーション付きで入れ替える
    /// </summary>
    public void SwapPieces(int r1, int c1, int r2, int c2)
    {
        KanjiPiece temp = Grid[r1, c1];
        Grid[r1, c1] = Grid[r2, c2];
        Grid[r2, c2] = temp;

        if (Grid[r1, c1] != null)
        {
            Grid[r1, c1].row = r1;
            Grid[r1, c1].col = c1;
            Grid[r1, c1].MoveTo(GridToWorldPosition(r1, c1));
        }
        if (Grid[r2, c2] != null)
        {
            Grid[r2, c2].row = r2;
            Grid[r2, c2].col = c2;
            Grid[r2, c2].MoveTo(GridToWorldPosition(r2, c2));
        }
    }

    public void RemovePieceAt(int row, int col)
    {
        if (Grid[row, col] != null)
        {
            Destroy(Grid[row, col].gameObject);
            Grid[row, col] = null;
        }
    }

    /// <summary>
    /// 空きセルに対して上のピースを落下させる（重力処理）
    /// </summary>
    public void DropPieces()
    {
        for (int c = 0; c < SIZE; c++)
        {
            int emptyRow = -1;

            for (int r = 0; r < SIZE; r++)
            {
                if (Grid[r, c] == null)
                {
                    if (emptyRow < 0) emptyRow = r;
                }
                else if (emptyRow >= 0)
                {
                    Grid[emptyRow, c] = Grid[r, c];
                    Grid[r, c] = null;

                    Grid[emptyRow, c].row = emptyRow;
                    Grid[emptyRow, c].col = c;
                    Grid[emptyRow, c].MoveTo(GridToWorldPosition(emptyRow, c));

                    emptyRow++;
                }
            }
        }
    }

    public void RefillEmptyCells()
    {
        for (int c = 0; c < SIZE; c++)
        {
            for (int r = 0; r < SIZE; r++)
            {
                if (Grid[r, c] == null)
                {
                    SpawnPieceAt(r, c, animateFromTop: true);
                }
            }
        }
    }

    public Vector3 GridToWorldPosition(int row, int col)
    {
        float totalCellSize = cellSize + cellSpacing;
        float offsetX = -(SIZE - 1) * totalCellSize / 2f;
        float offsetY = -(SIZE - 1) * totalCellSize / 2f;

        float x = transform.position.x + offsetX + col * totalCellSize;
        float y = transform.position.y + offsetY + row * totalCellSize;
        return new Vector3(x, y, 0f);
    }

    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        float totalCellSize = cellSize + cellSpacing;
        float offsetX = -(SIZE - 1) * totalCellSize / 2f;
        float offsetY = -(SIZE - 1) * totalCellSize / 2f;

        int col = Mathf.RoundToInt((worldPos.x - transform.position.x - offsetX) / totalCellSize);
        int row = Mathf.RoundToInt((worldPos.y - transform.position.y - offsetY) / totalCellSize);
        return new Vector2Int(row, col);
    }

    public bool IsValidPosition(int row, int col)
    {
        return row >= 0 && row < SIZE && col >= 0 && col < SIZE;
    }

    public bool AreAdjacent(int r1, int c1, int r2, int c2)
    {
        int dr = Mathf.Abs(r1 - r2);
        int dc = Mathf.Abs(c1 - c2);
        return (dr == 1 && dc == 0) || (dr == 0 && dc == 1);
    }
}
