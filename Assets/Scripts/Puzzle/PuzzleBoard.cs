using UnityEngine;

/// <summary>
/// パズル盤面のデータ管理
/// 6列×5行のグリッドにPieceを配置・操作する
/// </summary>
public class PuzzleBoard : MonoBehaviour
{
    public const int COLS = 6;
    public const int ROWS = 5;

    [Header("設定")]
    [SerializeField] private float cellSize = 1.0f;
    [SerializeField] private GameObject piecePrefab;
    [SerializeField] private PieceData[] availablePieces;

    /// <summary>
    /// 盤面のグリッド配列 [row, col]
    /// </summary>
    public Piece[,] Grid { get; private set; }

    /// <summary>
    /// セルサイズ
    /// </summary>
    public float CellSize => cellSize;

    /// <summary>
    /// ピースのプレハブ
    /// </summary>
    public GameObject PiecePrefab => piecePrefab;

    /// <summary>
    /// 利用可能なピースデータ
    /// </summary>
    public PieceData[] AvailablePieces => availablePieces;

    /// <summary>
    /// 盤面の左下のワールド座標
    /// </summary>
    public Vector3 BoardOrigin => transform.position;

    /// <summary>
    /// 盤面を初期化する
    /// </summary>
    public void InitializeBoard()
    {
        Grid = new Piece[ROWS, COLS];
        ClearBoard();
        FillBoard();
    }

    /// <summary>
    /// 盤面上の全ピースを削除する
    /// </summary>
    public void ClearBoard()
    {
        if (Grid == null) return;

        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                if (Grid[r, c] != null)
                {
                    Destroy(Grid[r, c].gameObject);
                    Grid[r, c] = null;
                }
            }
        }
    }

    /// <summary>
    /// 空のセルにランダムなピースを生成して埋める
    /// </summary>
    public void FillBoard()
    {
        if (availablePieces == null || availablePieces.Length == 0)
        {
            Debug.LogWarning("PuzzleBoard: availablePiecesが設定されていません");
            return;
        }

        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                if (Grid[r, c] == null)
                {
                    SpawnPieceAt(r, c);
                }
            }
        }
    }

    /// <summary>
    /// 指定位置にランダムなピースを生成する
    /// </summary>
    public Piece SpawnPieceAt(int row, int col)
    {
        if (piecePrefab == null)
        {
            Debug.LogError("PuzzleBoard: piecePrefabが設定されていません");
            return null;
        }

        PieceData randomData = availablePieces[Random.Range(0, availablePieces.Length)];
        Vector3 worldPos = GridToWorldPosition(row, col);

        GameObject go = Instantiate(piecePrefab, worldPos, Quaternion.identity, transform);
        go.name = $"Piece_{row}_{col}";

        Piece piece = go.GetComponent<Piece>();
        if (piece == null)
            piece = go.AddComponent<Piece>();

        piece.Initialize(randomData, row, col);
        Grid[row, col] = piece;

        return piece;
    }

    /// <summary>
    /// 2つのピースを入れ替える
    /// </summary>
    public void SwapPieces(int r1, int c1, int r2, int c2)
    {
        Piece temp = Grid[r1, c1];
        Grid[r1, c1] = Grid[r2, c2];
        Grid[r2, c2] = temp;

        // 座標情報を更新
        if (Grid[r1, c1] != null)
        {
            Grid[r1, c1].row = r1;
            Grid[r1, c1].col = c1;
        }
        if (Grid[r2, c2] != null)
        {
            Grid[r2, c2].row = r2;
            Grid[r2, c2].col = c2;
        }
    }

    /// <summary>
    /// 指定位置のピースを削除する
    /// </summary>
    public void RemovePieceAt(int row, int col)
    {
        if (Grid[row, col] != null)
        {
            Destroy(Grid[row, col].gameObject);
            Grid[row, col] = null;
        }
    }

    /// <summary>
    /// 空いたセルの上にあるピースを落下させる
    /// </summary>
    public void DropPieces()
    {
        for (int c = 0; c < COLS; c++)
        {
            int emptyRow = -1;

            // 下から上にスキャン
            for (int r = 0; r < ROWS; r++)
            {
                if (Grid[r, c] == null)
                {
                    if (emptyRow < 0)
                        emptyRow = r;
                }
                else if (emptyRow >= 0)
                {
                    // ピースを落下させる
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

    /// <summary>
    /// グリッド座標をワールド座標に変換する
    /// 行0が一番下、列0が一番左
    /// </summary>
    public Vector3 GridToWorldPosition(int row, int col)
    {
        float x = BoardOrigin.x + col * cellSize + cellSize * 0.5f;
        float y = BoardOrigin.y + row * cellSize + cellSize * 0.5f;
        return new Vector3(x, y, 0f);
    }

    /// <summary>
    /// ワールド座標をグリッド座標に変換する
    /// </summary>
    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        int col = Mathf.FloorToInt((worldPos.x - BoardOrigin.x) / cellSize);
        int row = Mathf.FloorToInt((worldPos.y - BoardOrigin.y) / cellSize);
        return new Vector2Int(row, col);
    }

    /// <summary>
    /// 座標がグリッド内かどうかを判定する
    /// </summary>
    public bool IsValidPosition(int row, int col)
    {
        return row >= 0 && row < ROWS && col >= 0 && col < COLS;
    }
}
