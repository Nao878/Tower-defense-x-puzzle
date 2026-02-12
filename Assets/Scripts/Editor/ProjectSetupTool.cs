using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 漢字合体パズルゲームのシーンセットアップを自動実行するエディタツール
/// Tools > Setup Puzzle TD メニューから実行する
/// </summary>
public class ProjectSetupTool : Editor
{
    private const string SO_PATH = "Assets/ScriptableObjects";
    private const string PREFAB_PATH = "Assets/Prefabs";
    private const string FONT_PATH = "Assets/TextMesh Pro/Fonts/AppFont.otf";
    private const string FONT_SDF_PATH = "Assets/TextMesh Pro/Fonts/AppFont SDF.asset";

    [MenuItem("Tools/Setup Puzzle TD")]
    public static void SetupScene()
    {
        if (!EditorUtility.DisplayDialog(
            "漢字合体パズル セットアップ",
            "シーンをセットアップします。\n既存のオブジェクトが削除される場合があります。\n続行しますか？",
            "はい", "いいえ"))
        {
            return;
        }

        CreateFolders();
        CreateRecipeAssets();
        CreatePrefabs();
        SetupSceneObjects();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== 漢字合体パズル セットアップ完了 ===");
        EditorUtility.DisplayDialog("完了", "漢字合体パズルのセットアップが完了しました！\nPlayでゲームを開始できます。", "OK");
    }

    // ============================================================
    // フォルダ作成
    // ============================================================
    private static void CreateFolders()
    {
        MakeFolder("Assets", "Scripts");
        MakeFolder("Assets/Scripts", "Data");
        MakeFolder("Assets/Scripts", "Puzzle");
        MakeFolder("Assets/Scripts", "Manager");
        MakeFolder("Assets/Scripts", "Editor");
        MakeFolder("Assets", "ScriptableObjects");
        MakeFolder("Assets/ScriptableObjects", "Recipes");
        MakeFolder("Assets", "Prefabs");
        MakeFolder("Assets/Prefabs", "Puzzle");
        MakeFolder("Assets", "Materials");
    }

    private static void MakeFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            AssetDatabase.CreateFolder(parent, name);
    }

    // ============================================================
    // レシピアセット作成
    // ============================================================
    private static void CreateRecipeAssets()
    {
        CreateRecipe("Recipe_MokuMoku", "木", "木", "林", 100);
        CreateRecipe("Recipe_RinMoku", "林", "木", "森", 200);
        CreateRecipe("Recipe_NichiGetsu", "日", "月", "明", 150);
        CreateRecipe("Recipe_KaKa", "火", "火", "炎", 100);
        CreateRecipe("Recipe_JinJin", "人", "人", "从", 100);
    }

    private static KanjiRecipe CreateRecipe(string fileName, string a, string b, string result, int score)
    {
        string path = $"{SO_PATH}/Recipes/{fileName}.asset";
        KanjiRecipe existing = AssetDatabase.LoadAssetAtPath<KanjiRecipe>(path);
        if (existing != null) return existing;

        KanjiRecipe recipe = ScriptableObject.CreateInstance<KanjiRecipe>();
        recipe.materialA = a;
        recipe.materialB = b;
        recipe.result = result;
        recipe.score = score;
        AssetDatabase.CreateAsset(recipe, path);
        return recipe;
    }

    // ============================================================
    // プレハブ作成
    // ============================================================
    private static void CreatePrefabs()
    {
        CreateKanjiPiecePrefab();
    }

    private static void CreateKanjiPiecePrefab()
    {
        string path = $"{PREFAB_PATH}/Puzzle/KanjiPiecePrefab.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        // ルートオブジェクト
        GameObject root = new GameObject("KanjiPiecePrefab");

        // 背景スプライト
        SpriteRenderer bgRenderer = root.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = GetOrCreateSquareSprite();
        bgRenderer.color = new Color(1f, 0.95f, 0.85f);
        bgRenderer.sortingOrder = 0;
        root.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

        // BoxCollider2D（クリック検出用）
        BoxCollider2D col = root.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);

        // 漢字テキスト（子オブジェクト）
        GameObject textObj = new GameObject("KanjiText");
        textObj.transform.SetParent(root.transform);
        textObj.transform.localPosition = Vector3.zero;
        textObj.transform.localScale = Vector3.one;

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = "漢";
        tmp.fontSize = 6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.15f, 0.1f, 0.05f);
        tmp.sortingOrder = 1;

        // AppFont SDFを適用
        TMP_FontAsset fontSDF = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_SDF_PATH);
        if (fontSDF != null)
        {
            tmp.font = fontSDF;
        }

        // RectTransformの設定
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1f, 1f);

        // KanjiPieceコンポーネント追加
        KanjiPiece piece = root.AddComponent<KanjiPiece>();

        // SerializedObjectでprivateフィールドを設定
        AssetDatabase.SaveAssets();

        // プレハブとして保存
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        // プレハブ内のフィールドを設定
        SetupPiecePrefabReferences(path);
    }

    private static void SetupPiecePrefabReferences(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        KanjiPiece piece = prefab.GetComponent<KanjiPiece>();
        SpriteRenderer bg = prefab.GetComponent<SpriteRenderer>();
        TextMeshPro tmp = prefab.GetComponentInChildren<TextMeshPro>();

        if (piece != null)
        {
            SerializedObject so = new SerializedObject(piece);
            so.FindProperty("kanjiText").objectReferenceValue = tmp;
            so.FindProperty("background").objectReferenceValue = bg;
            so.ApplyModifiedProperties();
        }
    }

    private static Sprite GetOrCreateSquareSprite()
    {
        string texPath = "Assets/Materials/WhiteSquare.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
        if (existing != null) return existing;

        Texture2D tex = new Texture2D(64, 64);
        Color[] pixels = new Color[64 * 64];

        // 角丸風の四角形
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dx = Mathf.Abs(x - 31.5f);
                float dy = Mathf.Abs(y - 31.5f);
                float cornerRadius = 8f;
                float edgeDist = 27f;

                if (dx > edgeDist && dy > edgeDist)
                {
                    float dist = Mathf.Sqrt((dx - edgeDist) * (dx - edgeDist) + (dy - edgeDist) * (dy - edgeDist));
                    pixels[y * 64 + x] = dist <= cornerRadius ? Color.white : Color.clear;
                }
                else if (dx > edgeDist + cornerRadius || dy > edgeDist + cornerRadius)
                {
                    pixels[y * 64 + x] = Color.clear;
                }
                else
                {
                    pixels[y * 64 + x] = Color.white;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        string fullPath = System.IO.Path.Combine(Application.dataPath, "Materials/WhiteSquare.png");
        System.IO.File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
    }

    // ============================================================
    // シーンオブジェクト構成
    // ============================================================
    private static void SetupSceneObjects()
    {
        DestroyExisting();

        // フォントの読み込み
        Font appFont = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
        TMP_FontAsset fontSDF = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_SDF_PATH);

        // --- カメラ設定 ---
        SetupCamera();

        // --- パズルシステム ---
        SetupPuzzleSystem(fontSDF);

        // --- Canvas & UI ---
        SetupUI(appFont, fontSDF);

        Debug.Log("[ProjectSetupTool] シーンオブジェクト構成完了");
    }

    private static void DestroyExisting()
    {
        string[] names = { "GameManager", "PuzzleSystem", "Canvas", "EventSystem", "Background" };
        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    private static void SetupCamera()
    {
        Camera cam = Camera.main;

        // カメラが存在しない場合は新規作成
        if (cam == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();

            // URP対応: UniversalAdditionalCameraDataを追加
            var urpCamData = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            Debug.Log("[ProjectSetupTool] Main Cameraを新規作成しました");
        }

        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.backgroundColor = new Color(0.95f, 0.92f, 0.85f);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    private static void SetupPuzzleSystem(TMP_FontAsset fontSDF)
    {
        GameObject puzzleRoot = new GameObject("PuzzleSystem");
        puzzleRoot.transform.position = new Vector3(0f, -0.5f, 0f);

        PuzzleBoard board = puzzleRoot.AddComponent<PuzzleBoard>();
        PuzzleManager pm = puzzleRoot.AddComponent<PuzzleManager>();

        // PuzzleBoard設定
        SerializedObject boardSO = new SerializedObject(board);
        boardSO.FindProperty("cellSize").floatValue = 1.5f;
        boardSO.FindProperty("cellSpacing").floatValue = 0.15f;

        // ピースプレハブ
        GameObject piecePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_PATH}/Puzzle/KanjiPiecePrefab.prefab");
        boardSO.FindProperty("piecePrefab").objectReferenceValue = piecePrefab;

        // 漢字プール
        SerializedProperty kanjiPoolProp = boardSO.FindProperty("kanjiPool");
        string[] kanjiPool = { "木", "火", "日", "月", "人" };
        kanjiPoolProp.arraySize = kanjiPool.Length;
        for (int i = 0; i < kanjiPool.Length; i++)
        {
            kanjiPoolProp.GetArrayElementAtIndex(i).stringValue = kanjiPool[i];
        }
        boardSO.ApplyModifiedProperties();

        // PuzzleManager設定
        SerializedObject pmSO = new SerializedObject(pm);
        pmSO.FindProperty("board").objectReferenceValue = board;

        // レシピ設定
        KanjiRecipe[] recipes = LoadAllRecipes();
        SerializedProperty recipesProp = pmSO.FindProperty("recipes");
        recipesProp.arraySize = recipes.Length;
        for (int i = 0; i < recipes.Length; i++)
        {
            recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
        }
        pmSO.ApplyModifiedProperties();
    }

    private static void SetupUI(Font appFont, TMP_FontAsset fontSDF)
    {
        // EventSystem
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Canvas
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // --- タイトル ---
        CreateTMPText(canvasGO, "TitleText", "漢字合体パズル",
            new Vector2(0, 400), new Vector2(600, 80),
            48, fontSDF, new Color(0.2f, 0.15f, 0.1f), TextAlignmentOptions.Center);

        // --- スコア表示 ---
        GameObject scoreText = CreateTMPText(canvasGO, "ScoreText", "スコア: 0",
            new Vector2(0, 320), new Vector2(400, 60),
            36, fontSDF, new Color(0.3f, 0.2f, 0.1f), TextAlignmentOptions.Center);

        // --- 確認パネル ---
        GameObject confirmPanel = CreateConfirmPanel(canvasGO, fontSDF);

        // --- GameManager ---
        GameObject gmGO = new GameObject("GameManager");
        GameManager gm = gmGO.AddComponent<GameManager>();

        PuzzleManager pm = Object.FindFirstObjectByType<PuzzleManager>();
        TextMeshProUGUI scoreTMP = scoreText.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI infoTMP = confirmPanel.transform.Find("CombineInfoText")?.GetComponent<TextMeshProUGUI>();
        Button yesBtn = confirmPanel.transform.Find("YesButton")?.GetComponent<Button>();
        Button noBtn = confirmPanel.transform.Find("NoButton")?.GetComponent<Button>();

        SerializedObject gmSO = new SerializedObject(gm);
        gmSO.FindProperty("puzzleManager").objectReferenceValue = pm;
        gmSO.FindProperty("scoreText").objectReferenceValue = scoreTMP;
        gmSO.FindProperty("combineInfoText").objectReferenceValue = infoTMP;
        gmSO.FindProperty("confirmPanel").objectReferenceValue = confirmPanel;
        gmSO.FindProperty("confirmYesButton").objectReferenceValue = yesBtn;
        gmSO.FindProperty("confirmNoButton").objectReferenceValue = noBtn;
        gmSO.ApplyModifiedProperties();

        // --- 操作説明テキスト ---
        CreateTMPText(canvasGO, "InstructionText", "漢字をクリックして選択 → 隣の漢字をクリックで入れ替え",
            new Vector2(0, -420), new Vector2(700, 50),
            22, fontSDF, new Color(0.4f, 0.35f, 0.3f), TextAlignmentOptions.Center);
    }

    private static GameObject CreateConfirmPanel(GameObject canvas, TMP_FontAsset fontSDF)
    {
        // パネル背景
        GameObject panelGO = new GameObject("ConfirmPanel");
        panelGO.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchoredPosition = new Vector2(0, -250);
        panelRT.sizeDelta = new Vector2(500, 250);

        Image panelBg = panelGO.AddComponent<Image>();
        panelBg.color = new Color(0.98f, 0.95f, 0.88f, 0.95f);

        // アウトライン
        Outline panelOutline = panelGO.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.6f, 0.45f, 0.2f);
        panelOutline.effectDistance = new Vector2(2, 2);

        // 合体情報テキスト
        CreateTMPText(panelGO, "CombineInfoText", "木 + 木 = 林\n(+100点)",
            new Vector2(0, 40), new Vector2(450, 100),
            32, fontSDF, new Color(0.2f, 0.15f, 0.05f), TextAlignmentOptions.Center);

        // 「合体させますか？」テキスト
        CreateTMPText(panelGO, "QuestionText", "合体させますか？",
            new Vector2(0, -20), new Vector2(400, 40),
            24, fontSDF, new Color(0.3f, 0.25f, 0.15f), TextAlignmentOptions.Center);

        // 「はい」ボタン
        CreateButton(panelGO, "YesButton", "はい",
            new Vector2(-100, -80), new Vector2(150, 60),
            fontSDF, new Color(0.3f, 0.7f, 0.4f), Color.white);

        // 「いいえ」ボタン
        CreateButton(panelGO, "NoButton", "いいえ",
            new Vector2(100, -80), new Vector2(150, 60),
            fontSDF, new Color(0.7f, 0.35f, 0.3f), Color.white);

        panelGO.SetActive(false);
        return panelGO;
    }

    private static GameObject CreateTMPText(GameObject parent, string name, string text,
        Vector2 anchoredPos, Vector2 size, float fontSize,
        TMP_FontAsset font, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;

        if (font != null)
            tmp.font = font;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        return go;
    }

    private static GameObject CreateButton(GameObject parent, string name, string label,
        Vector2 anchoredPos, Vector2 size, TMP_FontAsset font, Color bgColor, Color textColor)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent.transform, false);

        RectTransform btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchoredPosition = anchoredPos;
        btnRT.sizeDelta = size;

        Image btnBg = btnGO.AddComponent<Image>();
        btnBg.color = bgColor;

        Button btn = btnGO.AddComponent<Button>();

        // ボタンのホバーエフェクト
        ColorBlock colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.1f;
        colors.pressedColor = bgColor * 0.85f;
        btn.colors = colors;

        // ボタン内テキスト
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;

        if (font != null)
            tmp.font = font;

        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.anchoredPosition = Vector2.zero;

        return btnGO;
    }

    private static KanjiRecipe[] LoadAllRecipes()
    {
        string[] guids = AssetDatabase.FindAssets("t:KanjiRecipe", new[] { $"{SO_PATH}/Recipes" });
        KanjiRecipe[] recipes = new KanjiRecipe[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            recipes[i] = AssetDatabase.LoadAssetAtPath<KanjiRecipe>(path);
        }
        return recipes;
    }
}
