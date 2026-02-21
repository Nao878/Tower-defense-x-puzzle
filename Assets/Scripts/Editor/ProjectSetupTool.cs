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
    // レシピアセット作成（小学校レベルの漢字のみ使用）
    // ============================================================
    private static void CreateRecipeAssets()
    {
        // 既存のレシピを削除して再生成
        string[] oldGuids = AssetDatabase.FindAssets("t:KanjiRecipe", new[] { $"{SO_PATH}/Recipes" });
        foreach (string guid in oldGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.DeleteAsset(path);
        }

        // ===== 既存レシピ（基本） =====
        CreateRecipe("Recipe_MokuMoku", "木", "木", "林", 100);
        CreateRecipe("Recipe_NichiGetsu", "日", "月", "明", 150);
        CreateRecipe("Recipe_DenRyoku", "田", "力", "男", 150);
        CreateRecipe("Recipe_JinMoku", "人", "木", "休", 150);
        CreateRecipe("Recipe_YamaYama", "山", "山", "出", 100);
        CreateRecipe("Recipe_YamaIshi", "山", "石", "岩", 150);
        CreateRecipe("Recipe_RitsuNichi", "立", "日", "音", 150);
        CreateRecipe("Recipe_KaDen", "火", "田", "畑", 150);

        // ===== 第1段階：基本素材同士の新レシピ =====
        // 人 (Person) 起点
        CreateRecipe("Recipe_JinSan", "人", "山", "仙", 150);
        CreateRecipe("Recipe_JinRitsu", "人", "立", "位", 150);
        CreateRecipe("Recipe_JinDen", "人", "田", "佃", 150);

        // 木 (Tree) 起点
        CreateRecipe("Recipe_MokuSan", "木", "山", "杣", 150);
        CreateRecipe("Recipe_MokuSeki", "木", "石", "柘", 150);
        CreateRecipe("Recipe_MokuNichi", "木", "日", "杲", 150);
        CreateRecipe("Recipe_MokuDen", "木", "田", "果", 200);

        // 月 (Moon/Flesh) 起点
        CreateRecipe("Recipe_GetsuRyoku", "月", "力", "肋", 150);
        CreateRecipe("Recipe_GetsuDen", "月", "田", "胃", 200);
        CreateRecipe("Recipe_GetsuGetsu", "月", "月", "朋", 100);

        // 日 (Sun) 起点
        CreateRecipe("Recipe_NichiRitsu", "日", "立", "昱", 150);
        CreateRecipe("Recipe_NichiNichi", "日", "日", "昌", 100);

        // 火 (Fire) 起点
        CreateRecipe("Recipe_KaKa", "火", "火", "炎", 100);

        // ===== 第2段階：進化合体（3ピース分の価値） =====
        CreateRecipe("Recipe_RinMoku", "林", "木", "森", 300);
        CreateRecipe("Recipe_ShouNichi", "昌", "日", "晶", 300);
        CreateRecipe("Recipe_EnMoku", "炎", "木", "焚", 300);
        CreateRecipe("Recipe_OnNichi", "音", "日", "暗", 300);
        CreateRecipe("Recipe_GetsuShutsu", "月", "出", "朏", 300);
        CreateRecipe("Recipe_MokuShou", "木", "昌", "椙", 300);
    }

    private static KanjiRecipe CreateRecipe(string fileName, string a, string b, string result, int score)
    {
        string path = $"{SO_PATH}/Recipes/{fileName}.asset";

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

        // 既存プレハブがあれば削除して再生成
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

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

        // プレハブとして保存
        PrefabUtility.SaveAsPrefabAsset(root, path);
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

        TMP_FontAsset fontSDF = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_SDF_PATH);

        SetupCamera();
        SetupPuzzleSystem(fontSDF);
        SetupUI(fontSDF);

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

        if (cam == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();

            // URP対応
            camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

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

        GameObject piecePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_PATH}/Puzzle/KanjiPiecePrefab.prefab");
        boardSO.FindProperty("piecePrefab").objectReferenceValue = piecePrefab;

        // 漢字プール（小学校レベルの漢字のみ、10種類）
        SerializedProperty kanjiPoolProp = boardSO.FindProperty("kanjiPool");
        string[] kanjiPool = { "木", "火", "日", "月", "人", "田", "力", "山", "石", "立" };
        kanjiPoolProp.arraySize = kanjiPool.Length;
        for (int i = 0; i < kanjiPool.Length; i++)
        {
            kanjiPoolProp.GetArrayElementAtIndex(i).stringValue = kanjiPool[i];
        }
        boardSO.ApplyModifiedProperties();

        // PuzzleManager設定
        SerializedObject pmSO = new SerializedObject(pm);
        pmSO.FindProperty("board").objectReferenceValue = board;

        KanjiRecipe[] recipes = LoadAllRecipes();
        SerializedProperty recipesProp = pmSO.FindProperty("recipes");
        recipesProp.arraySize = recipes.Length;
        for (int i = 0; i < recipes.Length; i++)
        {
            recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
        }
        pmSO.ApplyModifiedProperties();
    }

    private static void SetupUI(TMP_FontAsset fontSDF)
    {
        // EventSystem（新Input System対応）
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
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

        // タイトル
        CreateTMPText(canvasGO, "TitleText", "漢字合体パズル",
            new Vector2(0, 440), new Vector2(600, 80),
            48, fontSDF, new Color(0.2f, 0.15f, 0.1f), TextAlignmentOptions.Center);

        // タイマー表示
        GameObject timeText = CreateTMPText(canvasGO, "TimeText", "Time: 60.00",
            new Vector2(0, 380), new Vector2(400, 50),
            36, fontSDF, new Color(0.2f, 0.15f, 0.1f), TextAlignmentOptions.Center);

        // ハイスコア表示（左上）
        GameObject highScoreTextGO = CreateTMPText(canvasGO, "HighScoreText", "Best: 0",
            new Vector2(0, 0), new Vector2(250, 40),
            24, fontSDF, new Color(0.6f, 0.45f, 0.0f), TextAlignmentOptions.Left);

        RectTransform hsRT = highScoreTextGO.GetComponent<RectTransform>();
        hsRT.anchorMin = new Vector2(0, 1);
        hsRT.anchorMax = new Vector2(0, 1);
        hsRT.pivot = new Vector2(0, 1);
        hsRT.anchoredPosition = new Vector2(20, -20);

        // 時間ボーナスポップアップ（タイマーの横）
        GameObject timeBonusPopup = CreateTMPText(canvasGO, "TimeBonusPopup", "+3sec",
            new Vector2(220, 380), new Vector2(200, 40),
            28, fontSDF, new Color(0.1f, 0.7f, 0.2f), TextAlignmentOptions.Left);
        timeBonusPopup.SetActive(false);

        // スコア表示
        GameObject scoreText = CreateTMPText(canvasGO, "ScoreText", "スコア: 0",
            new Vector2(0, 340), new Vector2(400, 40),
            30, fontSDF, new Color(0.3f, 0.2f, 0.1f), TextAlignmentOptions.Center);

        // 最後の合体情報
        GameObject lastCombineText = CreateTMPText(canvasGO, "LastCombineText", "",
            new Vector2(0, 300), new Vector2(550, 40),
            24, fontSDF, new Color(0.5f, 0.3f, 0.1f), TextAlignmentOptions.Center);

        // 操作説明
        CreateTMPText(canvasGO, "InstructionText",
            "ピースをクリックして選択 → 隣のピースをクリックで入れ替え\n振動しているペアをクリックで合体すると時間回復！",
            new Vector2(0, -400), new Vector2(700, 80),
            22, fontSDF, new Color(0.4f, 0.35f, 0.3f), TextAlignmentOptions.Center);

        // ===== 常設リセットボタン（下部） =====
        GameObject resetBtnGO = CreateButton(canvasGO, "ResetButton", "リセット",
            new Vector2(0, -320), new Vector2(160, 60),
            fontSDF, new Color(0.5f, 0.5f, 0.55f), Color.white);

        Image resetBtnImage = resetBtnGO.GetComponent<Image>();
        TextMeshProUGUI resetBtnLabel = resetBtnGO.GetComponentInChildren<TextMeshProUGUI>();

        // ===== 確認ダイアログパネル =====
        GameObject confirmPanel = new GameObject("ConfirmResetPanel");
        confirmPanel.transform.SetParent(canvasGO.transform, false);

        RectTransform cpRT = confirmPanel.AddComponent<RectTransform>();
        cpRT.anchorMin = Vector2.zero;
        cpRT.anchorMax = Vector2.one;
        cpRT.sizeDelta = Vector2.zero;

        Image cpBg = confirmPanel.AddComponent<Image>();
        cpBg.color = new Color(0, 0, 0, 0.5f);

        GameObject dialogBox = new GameObject("DialogBox");
        dialogBox.transform.SetParent(confirmPanel.transform, false);

        RectTransform dbRT = dialogBox.AddComponent<RectTransform>();
        dbRT.anchoredPosition = Vector2.zero;
        dbRT.sizeDelta = new Vector2(500, 250);

        Image dbBg = dialogBox.AddComponent<Image>();
        dbBg.color = new Color(0.95f, 0.92f, 0.88f);

        GameObject confirmTextGO = CreateTMPText(dialogBox, "ConfirmText",
            "盤面を入れ替えますか？\n（ペナルティ: -10秒）",
            new Vector2(0, 30), new Vector2(450, 130),
            26, fontSDF, new Color(0.2f, 0.15f, 0.1f), TextAlignmentOptions.Center);

        GameObject yesBtnGO = CreateButton(dialogBox, "YesButton", "はい",
            new Vector2(-90, -80), new Vector2(140, 50),
            fontSDF, new Color(0.2f, 0.6f, 0.3f), Color.white);

        GameObject noBtnGO = CreateButton(dialogBox, "NoButton", "いいえ",
            new Vector2(90, -80), new Vector2(140, 50),
            fontSDF, new Color(0.6f, 0.3f, 0.2f), Color.white);

        confirmPanel.SetActive(false);

        // ===== ゲームオーバーパネル =====
        GameObject gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(canvasGO.transform, false);

        RectTransform goRT = gameOverPanel.AddComponent<RectTransform>();
        goRT.anchorMin = Vector2.zero;
        goRT.anchorMax = Vector2.one;
        goRT.sizeDelta = Vector2.zero;

        Image goBg = gameOverPanel.AddComponent<Image>();
        goBg.color = new Color(0, 0, 0, 0.7f);

        GameObject goBox = new GameObject("GameOverBox");
        goBox.transform.SetParent(gameOverPanel.transform, false);

        RectTransform goBoxRT = goBox.AddComponent<RectTransform>();
        goBoxRT.anchoredPosition = Vector2.zero;
        goBoxRT.sizeDelta = new Vector2(600, 300);

        Image goBoxBg = goBox.AddComponent<Image>();
        goBoxBg.color = new Color(0.95f, 0.92f, 0.88f);

        GameObject gameOverScoreText = CreateTMPText(goBox, "GameOverScoreText",
            "TIME UP!\n\n最終スコア: 0",
            new Vector2(0, 30), new Vector2(500, 200),
            36, fontSDF, new Color(0.8f, 0.2f, 0.1f), TextAlignmentOptions.Center);

        // リトライボタン
        GameObject retryBtnGO = CreateButton(goBox, "RetryButton", "リトライ",
            new Vector2(0, -100), new Vector2(200, 60),
            fontSDF, new Color(0.2f, 0.5f, 0.8f), Color.white);

        gameOverPanel.SetActive(false);

        // ===== GameManager =====
        GameObject gmGO = new GameObject("GameManager");
        GameManager gm = gmGO.AddComponent<GameManager>();

        PuzzleManager pm = Object.FindFirstObjectByType<PuzzleManager>();

        SerializedObject gmSO = new SerializedObject(gm);
        gmSO.FindProperty("puzzleManager").objectReferenceValue = pm;
        gmSO.FindProperty("scoreText").objectReferenceValue = scoreText.GetComponent<TextMeshProUGUI>();
        gmSO.FindProperty("timeText").objectReferenceValue = timeText.GetComponent<TextMeshProUGUI>();
        gmSO.FindProperty("timeBonusPopup").objectReferenceValue = timeBonusPopup.GetComponent<TextMeshProUGUI>();
        gmSO.FindProperty("lastCombineText").objectReferenceValue = lastCombineText.GetComponent<TextMeshProUGUI>();
        gmSO.FindProperty("resetButton").objectReferenceValue = resetBtnGO.GetComponent<Button>();
        gmSO.FindProperty("resetButtonLabel").objectReferenceValue = resetBtnLabel;
        gmSO.FindProperty("resetButtonImage").objectReferenceValue = resetBtnImage;
        gmSO.FindProperty("confirmResetPanel").objectReferenceValue = confirmPanel;
        gmSO.FindProperty("confirmResetText").objectReferenceValue = confirmTextGO.GetComponent<TextMeshProUGUI>();
        gmSO.FindProperty("confirmYesButton").objectReferenceValue = yesBtnGO.GetComponent<Button>();
        gmSO.FindProperty("confirmNoButton").objectReferenceValue = noBtnGO.GetComponent<Button>();
        gmSO.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        gmSO.FindProperty("gameOverScoreText").objectReferenceValue = gameOverScoreText.GetComponent<TextMeshProUGUI>();
        gmSO.FindProperty("retryButton").objectReferenceValue = retryBtnGO.GetComponent<Button>();
        gmSO.FindProperty("highScoreText").objectReferenceValue = highScoreTextGO.GetComponent<TextMeshProUGUI>();
        gmSO.ApplyModifiedProperties();
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
        ColorBlock colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.1f;
        colors.pressedColor = bgColor * 0.85f;
        btn.colors = colors;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        if (font != null) tmp.font = font;

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
