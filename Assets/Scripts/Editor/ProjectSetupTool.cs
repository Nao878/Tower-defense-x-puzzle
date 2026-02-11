using UnityEngine;
using UnityEditor;

/// <summary>
/// プロジェクトのシーンセットアップを自動実行するエディタツール
/// Tools > Setup Puzzle TD メニューから実行する
/// </summary>
public class ProjectSetupTool : Editor
{
    private const string SCRIPTABLE_OBJECTS_PATH = "Assets/ScriptableObjects";
    private const string PREFABS_PATH = "Assets/Prefabs";

    [MenuItem("Tools/Setup Puzzle TD")]
    public static void SetupScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Puzzle TD セットアップ",
            "シーンをセットアップします。\n既存のオブジェクトが削除される場合があります。\n続行しますか？",
            "はい", "いいえ"))
        {
            return;
        }

        // フォルダ作成
        CreateFolders();

        // ScriptableObjectアセットの作成
        CreatePieceDataAssets();
        CreateUnitDataAssets();
        CreateRecipeDataAssets();

        // プレハブの作成
        CreatePrefabs();

        // シーンオブジェクトの構成
        SetupSceneObjects();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== Puzzle TD セットアップ完了 ===");
        EditorUtility.DisplayDialog("完了", "Puzzle TD のセットアップが完了しました！", "OK");
    }

    // ============================================================
    // フォルダ作成
    // ============================================================
    private static void CreateFolders()
    {
        CreateFolderIfNotExists("Assets", "Scripts");
        CreateFolderIfNotExists("Assets/Scripts", "Puzzle");
        CreateFolderIfNotExists("Assets/Scripts", "TD");
        CreateFolderIfNotExists("Assets/Scripts", "Data");
        CreateFolderIfNotExists("Assets/Scripts", "Manager");
        CreateFolderIfNotExists("Assets/Scripts", "Editor");
        CreateFolderIfNotExists("Assets", "ScriptableObjects");
        CreateFolderIfNotExists("Assets/ScriptableObjects", "Pieces");
        CreateFolderIfNotExists("Assets/ScriptableObjects", "Units");
        CreateFolderIfNotExists("Assets/ScriptableObjects", "Recipes");
        CreateFolderIfNotExists("Assets", "Prefabs");
        CreateFolderIfNotExists("Assets/Prefabs", "Puzzle");
        CreateFolderIfNotExists("Assets/Prefabs", "Units");
        CreateFolderIfNotExists("Assets/Prefabs", "Enemies");
        CreateFolderIfNotExists("Assets", "Materials");
    }

    private static void CreateFolderIfNotExists(string parent, string folderName)
    {
        string path = $"{parent}/{folderName}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    // ============================================================
    // ScriptableObject アセットの作成
    // ============================================================
    private static void CreatePieceDataAssets()
    {
        CreatePieceData("Base", PieceType.Base, "台座", new Color(0.6f, 0.4f, 0.2f));
        CreatePieceData("CannonTop", PieceType.CannonTop, "砲台", new Color(0.8f, 0.2f, 0.2f));
        CreatePieceData("Engine", PieceType.Engine, "エンジン", new Color(0.2f, 0.6f, 0.8f));
        CreatePieceData("Armor", PieceType.Armor, "装甲", new Color(0.5f, 0.5f, 0.5f));
        CreatePieceData("Shield", PieceType.Shield, "シールド", new Color(0.2f, 0.8f, 0.4f));
        CreatePieceData("Missile", PieceType.Missile, "ミサイル", new Color(0.9f, 0.6f, 0.1f));
    }

    private static void CreatePieceData(string fileName, PieceType type, string displayName, Color color)
    {
        string path = $"{SCRIPTABLE_OBJECTS_PATH}/Pieces/{fileName}.asset";
        if (AssetDatabase.LoadAssetAtPath<PieceData>(path) != null) return;

        PieceData data = ScriptableObject.CreateInstance<PieceData>();
        data.pieceType = type;
        data.displayName = displayName;
        data.color = color;

        AssetDatabase.CreateAsset(data, path);
    }

    private static void CreateUnitDataAssets()
    {
        CreateUnitData("BasicCannon", "基本キャノン", 3f, 1f, 15f);
        CreateUnitData("MissileLauncher", "ミサイルランチャー", 5f, 2f, 40f);
    }

    private static void CreateUnitData(string fileName, string unitName, float range, float attackSpeed, float damage)
    {
        string path = $"{SCRIPTABLE_OBJECTS_PATH}/Units/{fileName}.asset";
        if (AssetDatabase.LoadAssetAtPath<UnitData>(path) != null) return;

        UnitData data = ScriptableObject.CreateInstance<UnitData>();
        data.unitName = unitName;
        data.range = range;
        data.attackSpeed = attackSpeed;
        data.damage = damage;

        AssetDatabase.CreateAsset(data, path);
    }

    private static void CreateRecipeDataAssets()
    {
        // レシピ1: 台座 + 砲台（縦並び）→ 基本キャノン
        UnitData cannonUnit = AssetDatabase.LoadAssetAtPath<UnitData>($"{SCRIPTABLE_OBJECTS_PATH}/Units/BasicCannon.asset");
        CreateRecipeData("Recipe_BasicCannon", "基本キャノンの合成", 0, PatternType.Vertical2,
            new PieceType[] { PieceType.Base, PieceType.CannonTop },
            new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(0, 1) },
            cannonUnit);

        // レシピ2: 台座 + ミサイル（縦並び）→ ミサイルランチャー
        UnitData missileUnit = AssetDatabase.LoadAssetAtPath<UnitData>($"{SCRIPTABLE_OBJECTS_PATH}/Units/MissileLauncher.asset");
        CreateRecipeData("Recipe_MissileLauncher", "ミサイルランチャーの合成", 1, PatternType.Vertical2,
            new PieceType[] { PieceType.Base, PieceType.Missile },
            new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(0, 1) },
            missileUnit);
    }

    private static void CreateRecipeData(string fileName, string recipeName, int priority,
        PatternType patternType, PieceType[] requiredPieces, Vector2Int[] offsets, UnitData resultUnit)
    {
        string path = $"{SCRIPTABLE_OBJECTS_PATH}/Recipes/{fileName}.asset";
        if (AssetDatabase.LoadAssetAtPath<RecipeData>(path) != null) return;

        RecipeData data = ScriptableObject.CreateInstance<RecipeData>();
        data.recipeName = recipeName;
        data.priority = priority;
        data.patternType = patternType;
        data.requiredPieces = requiredPieces;
        data.offsets = offsets;
        data.resultUnit = resultUnit;

        AssetDatabase.CreateAsset(data, path);
    }

    // ============================================================
    // プレハブの作成
    // ============================================================
    private static void CreatePrefabs()
    {
        CreatePiecePrefab();
        CreateUnitPrefab();
        CreateEnemyPrefab();
    }

    private static void CreatePiecePrefab()
    {
        string path = $"{PREFABS_PATH}/Puzzle/PiecePrefab.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        GameObject go = new GameObject("PiecePrefab");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateDefaultSprite();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 1;
        go.AddComponent<Piece>();
        go.AddComponent<BoxCollider2D>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    private static void CreateUnitPrefab()
    {
        string path = $"{PREFABS_PATH}/Units/UnitPrefab.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        GameObject go = new GameObject("UnitPrefab");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateDefaultSprite();
        sr.color = Color.cyan;
        sr.sortingOrder = 2;
        go.AddComponent<UnitObject>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    private static void CreateEnemyPrefab()
    {
        string path = $"{PREFABS_PATH}/Enemies/EnemyPrefab.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        GameObject go = new GameObject("EnemyPrefab");
        go.tag = "Enemy";
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateDefaultSprite();
        sr.color = Color.red;
        sr.sortingOrder = 2;
        go.AddComponent<EnemyController>();
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    /// <summary>
    /// デフォルトの白い四角形スプライトを取得する
    /// </summary>
    private static Sprite CreateDefaultSprite()
    {
        // Unityビルトインの白テクスチャからスプライトを作成
        Texture2D tex = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        string texPath = "Assets/Materials/DefaultSquare.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
        {
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(Application.dataPath, "Materials/DefaultSquare.png"),
                tex.EncodeToPNG()
            );
            AssetDatabase.Refresh();

            // テクスチャインポート設定
            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
    }

    // ============================================================
    // シーンオブジェクトの構成
    // ============================================================
    private static void SetupSceneObjects()
    {
        // 既存のゲームオブジェクトを削除
        DestroyExistingSetup();

        // --- GameManager ---
        GameObject gameManagerGO = new GameObject("GameManager");
        GameManager gameManager = gameManagerGO.AddComponent<GameManager>();

        // --- パズル盤面 ---
        GameObject puzzleRoot = new GameObject("PuzzleSystem");
        puzzleRoot.transform.position = new Vector3(-2.5f, -4f, 0f);

        PuzzleBoard puzzleBoard = puzzleRoot.AddComponent<PuzzleBoard>();
        PuzzleManager puzzleManager = puzzleRoot.AddComponent<PuzzleManager>();
        RecipeDatabase recipeDB = puzzleRoot.AddComponent<RecipeDatabase>();

        // PuzzleBoard設定
        SerializedObject boardSO = new SerializedObject(puzzleBoard);
        boardSO.FindProperty("cellSize").floatValue = 1.0f;

        // ピースプレハブを設定
        GameObject piecePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS_PATH}/Puzzle/PiecePrefab.prefab");
        boardSO.FindProperty("piecePrefab").objectReferenceValue = piecePrefab;

        // 利用可能なピースを設定
        PieceData[] allPieces = LoadAllPieceData();
        SerializedProperty availablePiecesProperty = boardSO.FindProperty("availablePieces");
        availablePiecesProperty.arraySize = allPieces.Length;
        for (int i = 0; i < allPieces.Length; i++)
        {
            availablePiecesProperty.GetArrayElementAtIndex(i).objectReferenceValue = allPieces[i];
        }
        boardSO.ApplyModifiedProperties();

        // PuzzleManager設定
        SerializedObject pmSO = new SerializedObject(puzzleManager);
        pmSO.FindProperty("board").objectReferenceValue = puzzleBoard;
        pmSO.FindProperty("recipeDatabase").objectReferenceValue = recipeDB;
        pmSO.ApplyModifiedProperties();

        // RecipeDatabase設定
        RecipeData[] allRecipes = LoadAllRecipeData();
        SerializedObject rdbSO = new SerializedObject(recipeDB);
        SerializedProperty recipesProperty = rdbSO.FindProperty("recipes");
        recipesProperty.arraySize = allRecipes.Length;
        for (int i = 0; i < allRecipes.Length; i++)
        {
            recipesProperty.GetArrayElementAtIndex(i).objectReferenceValue = allRecipes[i];
        }
        rdbSO.ApplyModifiedProperties();

        // --- TDフィールド ---
        GameObject tdField = new GameObject("TDField");
        tdField.transform.position = new Vector3(0f, 3f, 0f);

        // UnitSpawner
        UnitSpawner unitSpawner = tdField.AddComponent<UnitSpawner>();
        SerializedObject usSO = new SerializedObject(unitSpawner);
        usSO.FindProperty("puzzleManager").objectReferenceValue = puzzleManager;
        usSO.FindProperty("placementAreaMin").vector2Value = new Vector2(-4f, 1.5f);
        usSO.FindProperty("placementAreaMax").vector2Value = new Vector2(4f, 5f);

        GameObject unitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS_PATH}/Units/UnitPrefab.prefab");
        usSO.FindProperty("defaultUnitPrefab").objectReferenceValue = unitPrefab;
        usSO.ApplyModifiedProperties();

        // --- 敵経路（Waypoints）---
        GameObject waypointsParent = new GameObject("EnemyPath");
        waypointsParent.transform.position = Vector3.zero;

        Vector3[] waypointPositions = new Vector3[]
        {
            new Vector3(6f, 5f, 0f),   // スポーンポイント（右上）
            new Vector3(3f, 5f, 0f),
            new Vector3(3f, 3f, 0f),
            new Vector3(-3f, 3f, 0f),
            new Vector3(-3f, 5f, 0f),
            new Vector3(-6f, 5f, 0f),  // ゴール（左上）
        };

        for (int i = 0; i < waypointPositions.Length; i++)
        {
            GameObject wp = new GameObject($"Waypoint_{i}");
            wp.transform.parent = waypointsParent.transform;
            wp.transform.position = waypointPositions[i];
        }

        // スポーンポイント
        GameObject spawnPoint = new GameObject("EnemySpawnPoint");
        spawnPoint.transform.position = waypointPositions[0];

        // EnemySpawner
        EnemySpawner enemySpawner = tdField.AddComponent<EnemySpawner>();
        SerializedObject esSO = new SerializedObject(enemySpawner);
        esSO.FindProperty("waypointsParent").objectReferenceValue = waypointsParent.transform;
        esSO.FindProperty("spawnPoint").objectReferenceValue = spawnPoint.transform;
        esSO.FindProperty("timeBetweenWaves").floatValue = 10f;

        // ウェーブ設定
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS_PATH}/Enemies/EnemyPrefab.prefab");
        SerializedProperty wavesProperty = esSO.FindProperty("waves");
        wavesProperty.arraySize = 2;

        // ウェーブ1
        SerializedProperty wave1 = wavesProperty.GetArrayElementAtIndex(0);
        wave1.FindPropertyRelative("waveName").stringValue = "Wave 1";
        SerializedProperty wave1Enemies = wave1.FindPropertyRelative("enemies");
        wave1Enemies.arraySize = 1;
        SerializedProperty w1e1 = wave1Enemies.GetArrayElementAtIndex(0);
        w1e1.FindPropertyRelative("enemyPrefab").objectReferenceValue = enemyPrefab;
        w1e1.FindPropertyRelative("count").intValue = 5;
        w1e1.FindPropertyRelative("hp").floatValue = 50f;
        w1e1.FindPropertyRelative("speed").floatValue = 2f;
        w1e1.FindPropertyRelative("spawnInterval").floatValue = 2f;

        // ウェーブ2
        SerializedProperty wave2 = wavesProperty.GetArrayElementAtIndex(1);
        wave2.FindPropertyRelative("waveName").stringValue = "Wave 2";
        SerializedProperty wave2Enemies = wave2.FindPropertyRelative("enemies");
        wave2Enemies.arraySize = 1;
        SerializedProperty w2e1 = wave2Enemies.GetArrayElementAtIndex(0);
        w2e1.FindPropertyRelative("enemyPrefab").objectReferenceValue = enemyPrefab;
        w2e1.FindPropertyRelative("count").intValue = 8;
        w2e1.FindPropertyRelative("hp").floatValue = 80f;
        w2e1.FindPropertyRelative("speed").floatValue = 2.5f;
        w2e1.FindPropertyRelative("spawnInterval").floatValue = 1.5f;

        esSO.ApplyModifiedProperties();

        // --- GameManager設定 ---
        SerializedObject gmSO = new SerializedObject(gameManager);
        gmSO.FindProperty("puzzleManager").objectReferenceValue = puzzleManager;
        gmSO.FindProperty("enemySpawner").objectReferenceValue = enemySpawner;
        gmSO.FindProperty("unitSpawner").objectReferenceValue = unitSpawner;
        gmSO.ApplyModifiedProperties();

        // --- カメラ設定 ---
        SetupCamera();

        // --- 背景---
        SetupBackground();

        Debug.Log("[ProjectSetupTool] シーンオブジェクトの構成完了");
    }

    private static void DestroyExistingSetup()
    {
        string[] objectNames = { "GameManager", "PuzzleSystem", "TDField", "EnemyPath", "EnemySpawnPoint", "Background" };
        foreach (string name in objectNames)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }
    }

    private static PieceData[] LoadAllPieceData()
    {
        string[] guids = AssetDatabase.FindAssets("t:PieceData", new[] { $"{SCRIPTABLE_OBJECTS_PATH}/Pieces" });
        PieceData[] pieces = new PieceData[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            pieces[i] = AssetDatabase.LoadAssetAtPath<PieceData>(path);
        }
        return pieces;
    }

    private static RecipeData[] LoadAllRecipeData()
    {
        string[] guids = AssetDatabase.FindAssets("t:RecipeData", new[] { $"{SCRIPTABLE_OBJECTS_PATH}/Recipes" });
        RecipeData[] recipes = new RecipeData[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            recipes[i] = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
        }
        return recipes;
    }

    private static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 1f, -10f);
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        }
    }

    private static void SetupBackground()
    {
        // パズルエリアの背景
        GameObject bg = new GameObject("Background");

        GameObject puzzleBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        puzzleBg.name = "PuzzleBackground";
        puzzleBg.transform.parent = bg.transform;
        puzzleBg.transform.position = new Vector3(0.5f, -1.5f, 1f);
        puzzleBg.transform.localScale = new Vector3(6.5f, 5.5f, 1f);

        Renderer puzzleBgRenderer = puzzleBg.GetComponent<Renderer>();
        Material puzzleBgMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        puzzleBgMat.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
        puzzleBgRenderer.material = puzzleBgMat;

        // Colliderが不要なので削除
        Object.DestroyImmediate(puzzleBg.GetComponent<MeshCollider>());

        // TDフィールドの背景
        GameObject tdBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tdBg.name = "TDBackground";
        tdBg.transform.parent = bg.transform;
        tdBg.transform.position = new Vector3(0f, 3.5f, 1f);
        tdBg.transform.localScale = new Vector3(12f, 5f, 1f);

        Renderer tdBgRenderer = tdBg.GetComponent<Renderer>();
        Material tdBgMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        tdBgMat.color = new Color(0.1f, 0.2f, 0.15f, 0.8f);
        tdBgRenderer.material = tdBgMat;

        Object.DestroyImmediate(tdBg.GetComponent<MeshCollider>());
    }
}
