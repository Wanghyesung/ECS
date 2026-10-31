using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Unity 직렬화와 AssetDatabase를 통해서만 테스트 씬을 저장/이름 변경한다.
internal static class BenchmarkStaticSceneBuilder
{
    private const string REQUEST_PATH = "Library/BenchmarkStaticScene.request";
    private const string SCENE_FOLDER = "Assets/00_Scene/3D/TestScene/GridTestScene0/";
    private static readonly string[] s_arrSuffix = { "Brute", "", "Job", "Unified", "Final" };
    private static readonly string[] s_arrOldNames = { "BruteForceTestScene", "GridTestScene0", "GridTestScene1", "GridTestScene2", "GridTestScene3" };
    private static readonly string[] s_arrNames = { "BruteForceTestScene", "BoxGridTestScene", "BoxGridBurstJobTestScene", "UnifiedGridBurstJobTestScene", "UnifiedGridScheduledJobTestScene" };

    [InitializeOnLoadMethod]
    private static void RegisterRequest()
    {
        if (File.Exists(REQUEST_PATH)) EditorApplication.update += RunRequest;
    }

    private static void RunRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= RunRequest;
        string strRequest = File.ReadAllText(REQUEST_PATH).Trim();
        File.Delete(REQUEST_PATH);
        try { if (strRequest == "background") ApplyBackgroundToAll(); else BakeAll(); }
        catch (Exception refException)
        {
            File.WriteAllText("Library/BenchmarkStaticScene.result", refException.ToString());
            Debug.LogException(refException);
        }
    }

    [MenuItem("Tools/Collider Benchmark/Bake Static Test Scenes")]
    private static void BakeAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("BLOCKED: unsaved scene changes");
        string strPrevious = SceneManager.GetActiveScene().path;
        var refReport = new StringBuilder();
        var arrObstacles = new[] {
            new BenchmarkMeshVisual(Load("Assets/3D/12_MainResource/Prefabs/BonusContent/Asteroid Lava Red.prefab")),
            new BenchmarkMeshVisual(Load("Assets/3D/12_MainResource/Prefabs/BonusContent/Asteroid Lava Blue.prefab")) };
        var arrMonsters = new[] {
            new BenchmarkMeshVisual(Load("Assets/3D/03_Monster/PreFab/BaseMonster.prefab")),
            new BenchmarkMeshVisual(Load("Assets/3D/03_Monster/PreFab/BaseMonster1.prefab")) };
        for (int i = 0; i < s_arrSuffix.Length; i++)
        {
            string strOld = SCENE_FOLDER + s_arrOldNames[i] + ".unity";
            string strPath = SCENE_FOLDER + s_arrNames[i] + ".unity";
            if (strOld != strPath && AssetDatabase.LoadAssetAtPath<SceneAsset>(strOld) != null)
            {
                string strError = AssetDatabase.MoveAsset(strOld, strPath);
                if (strError.Length != 0) throw new InvalidOperationException(strError);
                var arrBuildScenes = EditorBuildSettings.scenes;
                foreach (var refBuildScene in arrBuildScenes)
                    if (refBuildScene.path == strOld) refBuildScene.path = strPath;
                EditorBuildSettings.scenes = arrBuildScenes;
                if (strPrevious == strOld) strPrevious = strPath;
            }
            Scene tScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Type refManagerType = typeof(LegacyBruteForceSpawner).Assembly.GetType("LegacyBruteForceColliderManager" + s_arrSuffix[i], true);
            Type refSpawnerType = typeof(LegacyBruteForceSpawner).Assembly.GetType("LegacyBruteForceSpawner" + s_arrSuffix[i], true);
            Type refBoxType = typeof(LegacyBruteForceSpawner).Assembly.GetType("LegacyBruteForceBoxCollider" + s_arrSuffix[i], true);
            Type refCircleType = typeof(LegacyBruteForceSpawner).Assembly.GetType("LegacyBruteForceCircleCollider" + s_arrSuffix[i], true);
            Component refManager = new GameObject("Collidermgr").AddComponent(refManagerType);
            Transform refBoxes = new GameObject("boxs").transform;
            Transform refMonsters = new GameObject("monsters").transform;
            Transform refBullets = new GameObject("Bullets").transform;
            var arrBoxes = new Component[70];
            var arrCircles = new Component[20];
            var refRandom = new System.Random(12345);
            for (int j = 0; j < 70; j++)
            {
                GameObject refObject = new GameObject("Obstacle_" + j);
                refObject.transform.SetParent(refBoxes, false);
                refObject.layer = LayerMask.NameToLayer("PhysXBenchmark");
                refObject.transform.position = Point(refRandom);
                refObject.transform.rotation = Quaternion.Euler(Next(refRandom, 0, 360), Next(refRandom, 0, 360), Next(refRandom, 0, 360));
                Vector3 vHalf = new Vector3(Next(refRandom, 1, 4), Next(refRandom, 1, 4), Next(refRandom, 1, 4));
                arrBoxes[j] = refObject.AddComponent(refBoxType);
                var refData = new SerializedObject(arrBoxes[j]);
                refData.FindProperty("m_vHalfExtent").vector3Value = vHalf;
                if (i == 0) refData.FindProperty("m_iSceneLayer").intValue = 1;
                refData.ApplyModifiedPropertiesWithoutUndo();
                arrObstacles[j % 2].AttachBox(refObject.transform, vHalf);
            }
            for (int j = 0; j < 20; j++)
            {
                GameObject refObject = new GameObject("Monster_" + j);
                refObject.transform.SetParent(refMonsters, false);
                refObject.layer = LayerMask.NameToLayer("PhysXMonsterDemo");
                refObject.transform.position = Point(refRandom);
                arrCircles[j] = refObject.AddComponent(refCircleType);
                var refData = new SerializedObject(arrCircles[j]);
                refData.FindProperty("m_fRadius").floatValue = 6f;
                if (i == 0) refData.FindProperty("m_iSceneLayer").intValue = 2;
                refData.ApplyModifiedPropertiesWithoutUndo();
                arrMonsters[j % 2].AttachSphere(refObject.transform, 6f);
            }
            GameObject refMap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            refMap.name = "MapRange";
            UnityEngine.Object.DestroyImmediate(refMap.GetComponent<UnityEngine.Collider>());
            refMap.transform.localScale = Vector3.one * 200f;
            refMap.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/3D/04_Map/ForceField/M_ForceFieldBoundary.mat");
            BenchmarkMapRange refRange = refMap.AddComponent<BenchmarkMapRange>();
            Component refSpawner = new GameObject("BulletSpawner").AddComponent(refSpawnerType);
            var refSerialized = new SerializedObject(refSpawner);
            refSerialized.FindProperty("m_refColliderManager").objectReferenceValue = refManager;
            SetArray(refSerialized.FindProperty("m_arrBoxes"), arrBoxes);
            SetArray(refSerialized.FindProperty("m_arrMonsters"), arrCircles);
            refSerialized.FindProperty("m_refMapRange").objectReferenceValue = refRange;
            refSerialized.FindProperty("m_refBulletRoot").objectReferenceValue = refBullets;
            refSerialized.FindProperty("m_refBulletVisualPrefab").objectReferenceValue = Load("Assets/3D/02_Player/Weapon/Bullet/BaseBullet.prefab");
            refSerialized.ApplyModifiedPropertiesWithoutUndo();
            var refCamera = new GameObject("Benchmark Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            refCamera.tag = "MainCamera";
            refCamera.transform.position = new Vector3(0, 90, -230);
            refCamera.transform.LookAt(Vector3.zero);
            refCamera.fieldOfView = 60;
            refCamera.farClipPlane = 600;
            refCamera.clearFlags = CameraClearFlags.Skybox;
            refCamera.backgroundColor = new Color(0.015f, 0.02f, 0.035f);
            var refLight = new GameObject("Benchmark Light", typeof(Light)).GetComponent<Light>();
            refLight.type = LightType.Directional;
            refLight.intensity = 1.2f;
            refLight.transform.rotation = Quaternion.Euler(45, -30, 0);
            ApplyBackground();
            if (GizmoUtility.TryGetGizmoInfo(refSpawnerType, out _)) GizmoUtility.SetGizmoEnabled(refSpawnerType, true, false);
            if (!EditorSceneManager.SaveScene(tScene, strPath)) throw new IOException("Cannot save " + strPath);
            // 저장한 실제 씬을 다시 읽어 직렬화 참조와 개수를 확인한다.
            EditorSceneManager.OpenScene(strPath, OpenSceneMode.Single);
            Component refSaved = UnityEngine.Object.FindObjectOfType(refSpawnerType) as Component;
            var refCheck = new SerializedObject(refSaved);
            foreach (string strField in new[] { "m_refColliderManager", "m_refMapRange", "m_refBulletRoot", "m_refBulletVisualPrefab" })
                if (refCheck.FindProperty(strField).objectReferenceValue == null) throw new InvalidOperationException("Missing " + strField);
            ValidateArray(refCheck.FindProperty("m_arrBoxes"), 70);
            ValidateArray(refCheck.FindProperty("m_arrMonsters"), 20);
            if (((Transform)refCheck.FindProperty("m_refBulletRoot").objectReferenceValue).childCount != 0) throw new InvalidOperationException("Bullets must start empty");
            refReport.AppendLine(s_arrNames[i] + ": manager=1 boxes=70 monsters=20 bullets=0 (spawn=4000) references=OK");
        }
        GridBenchmarkSceneSetup.VerifyHitResponses();
        foreach (SceneView refView in SceneView.sceneViews) refView.drawGizmos = true;
        if (!string.IsNullOrEmpty(strPrevious)) EditorSceneManager.OpenScene(strPrevious, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        refReport.AppendLine("SUCCESS: five scenes saved and renamed; five hit response checks passed; no Play Mode run.");
        File.WriteAllText("Library/BenchmarkStaticScene.result", refReport.ToString());
    }

    private const string BACKGROUND_FOLDER = "Assets/3D/99_ColliderBenchmark/Background/";

    // main BattleScene의 Skybox4 텍스처/면 배치와 환경 설정을 재사용한다.
    private static void ApplyBackground()
    {
        string strMaterialPath = BACKGROUND_FOLDER + "Skybox4.mat";
        Material refMaterial = AssetDatabase.LoadAssetAtPath<Material>(strMaterialPath);
        if (refMaterial == null)
        {
            refMaterial = new Material(Shader.Find("Skybox/6 Sided"));
            refMaterial.name = "Skybox4";
            string[] arrFaces = { "_LeftTex", "_RightTex", "_UpTex", "_DownTex", "_FrontTex", "_BackTex" };
            string[] arrAxes = { "X+", "X-", "Y+", "Y-", "Z+", "Z-" };
            for (int i = 0; i < arrFaces.Length; i++)
            {
                string strPath = BACKGROUND_FOLDER + "skybox4" + arrAxes[i] + ".png";
                var refImporter = (TextureImporter)AssetImporter.GetAtPath(strPath);
                refImporter.textureType = TextureImporterType.Default;
                refImporter.sRGBTexture = true;
                refImporter.mipmapEnabled = true;
                refImporter.wrapMode = TextureWrapMode.Clamp;
                refImporter.filterMode = FilterMode.Bilinear;
                refImporter.anisoLevel = 1;
                refImporter.maxTextureSize = 1024;
                refImporter.textureCompression = TextureImporterCompression.Compressed;
                refImporter.SaveAndReimport();
                refMaterial.SetTexture(arrFaces[i], AssetDatabase.LoadAssetAtPath<Texture2D>(strPath));
            }
            refMaterial.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            AssetDatabase.CreateAsset(refMaterial, strMaterialPath);
        }
        RenderSettings.skybox = refMaterial;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientSkyColor = new Color(0.212f, 0.227f, 0.259f);
        RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f);
        RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.55f, 0.6f, 0.58f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0007f;
        RenderSettings.fogStartDistance = 0f;
        RenderSettings.fogEndDistance = 300f;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.reflectionBounces = 1;
    }

    [MenuItem("Tools/Collider Benchmark/Restore Main Background")]
    private static void ApplyBackgroundToAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("BLOCKED: unsaved scene changes");
        string strPrevious = SceneManager.GetActiveScene().path;
        var refReport = new StringBuilder();
        foreach (string strName in s_arrNames)
        {
            string strPath = SCENE_FOLDER + strName + ".unity";
            Scene tScene = EditorSceneManager.OpenScene(strPath, OpenSceneMode.Single);
            ApplyBackground();
            foreach (GameObject refRoot in tScene.GetRootGameObjects())
                foreach (Camera refCamera in refRoot.GetComponentsInChildren<Camera>(true))
                    refCamera.clearFlags = CameraClearFlags.Skybox;
            EditorSceneManager.MarkSceneDirty(tScene);
            if (!EditorSceneManager.SaveScene(tScene)) throw new IOException(strPath);
            EditorSceneManager.OpenScene(strPath, OpenSceneMode.Single);
            if (RenderSettings.skybox == null || RenderSettings.skybox.name != "Skybox4" || Camera.main.clearFlags != CameraClearFlags.Skybox)
                throw new InvalidOperationException("Background verification failed: " + strName);
            refReport.AppendLine(strName + ": Skybox4 + Skybox camera + main fog/ambient settings OK");
        }
        if (!string.IsNullOrEmpty(strPrevious)) EditorSceneManager.OpenScene(strPrevious, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        refReport.AppendLine("SUCCESS: background restored to all five scenes; no Play Mode run.");
        File.WriteAllText("Library/BenchmarkStaticScene.result", refReport.ToString());
    }

    private static GameObject Load(string _strPath) => AssetDatabase.LoadAssetAtPath<GameObject>(_strPath) ?? throw new InvalidOperationException(_strPath);
    private static float Next(System.Random _refRandom, float _fMin, float _fMax) => Mathf.Lerp(_fMin, _fMax, (float)_refRandom.NextDouble());
    private static Vector3 Point(System.Random _refRandom)
    {
        for (int i = 0; i < 1000; i++)
        {
            Vector3 vPoint = new Vector3(Next(_refRandom, -1, 1), Next(_refRandom, -1, 1), Next(_refRandom, -1, 1));
            if (vPoint.sqrMagnitude <= 1f) return vPoint * 100f;
        }
        return Vector3.zero;
    }
    private static void SetArray(SerializedProperty _refProperty, Component[] _arrComponents)
    {
        _refProperty.arraySize = _arrComponents.Length;
        for (int i = 0; i < _arrComponents.Length; i++) _refProperty.GetArrayElementAtIndex(i).objectReferenceValue = _arrComponents[i];
    }
    private static void ValidateArray(SerializedProperty _refProperty, int _iCount)
    {
        if (_refProperty.arraySize != _iCount) throw new InvalidOperationException("Wrong target count");
        for (int i = 0; i < _iCount; i++)
        {
            var refComponent = _refProperty.GetArrayElementAtIndex(i).objectReferenceValue as Component;
            if (refComponent == null || refComponent.GetComponentsInChildren<MeshRenderer>().Length == 0) throw new InvalidOperationException("Missing target or visual");
        }
    }
}
