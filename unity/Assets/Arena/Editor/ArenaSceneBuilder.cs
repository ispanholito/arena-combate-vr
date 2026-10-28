using System.Linq;
using Arena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Menu "Arena > 3": monta a cena jogável da arena (VR no Quest e modo desktop no editor).
public static class ArenaSceneBuilder
{
    public const string ScenePath = "Assets/Arena/Scenes/Arena.unity";

    [MenuItem("Arena/3 - Criar cena da arena (jogável)", priority = 3)]
    public static void CreateArenaScene()
    {
        string r = CreateArenaSceneNoDialog();
        EditorUtility.DisplayDialog("Arena VR", r + "\n\nAperte Play: sem headset entra no modo desktop (WASD + mouse, clique para golpear).\nNo Quest: File > Build And Run.", "OK");
    }

    public static string CreateArenaSceneNoDialog()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Luz e clima dark fantasy
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.14f, 0.2f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.07f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 10f; RenderSettings.fogEndDistance = 38f;
        var sun = new GameObject("Luz da Lua").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 0.25f; sun.color = new Color(0.6f, 0.65f, 0.9f);
        sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        var world = new GameObject("Arena").transform;
        var stone = SaveMat("Pedra", new Color(0.32f, 0.3f, 0.34f));
        var wallMat = SaveMat("Parede", new Color(0.27f, 0.25f, 0.3f));
        var pillarMat = SaveMat("Pilar", new Color(0.22f, 0.2f, 0.25f));
        var wood = SaveMat("Madeira", new Color(0.3f, 0.19f, 0.1f));
        var fire = SaveMat("Fogo", new Color(1f, 0.48f, 0.1f), new Color(1.5f, 0.6f, 0.1f));

        // Arena 26×26 m (2026-10-28, mais espaço; era 18×18). H = meia largura.
        const float H = 13f;
        Box(world, "Chao", new Vector3(0, -0.05f, 0), new Vector3(H * 2, 0.1f, H * 2), stone);
        Box(world, "Parede N", new Vector3(0, 2.5f, -H - 0.1f), new Vector3(H * 2 + 0.4f, 5, 0.2f), wallMat);
        Box(world, "Parede S", new Vector3(0, 2.5f, H + 0.1f), new Vector3(H * 2 + 0.4f, 5, 0.2f), wallMat);
        Box(world, "Parede L", new Vector3(H + 0.1f, 2.5f, 0), new Vector3(0.2f, 5, H * 2 + 0.4f), wallMat);
        Box(world, "Parede O", new Vector3(-H - 0.1f, 2.5f, 0), new Vector3(0.2f, 5, H * 2 + 0.4f), wallMat);
        // Pilares: 4 nos cantos internos + 4 no meio dos lados (cobertura e âncora para a teia).
        foreach (var p in new[] { new Vector3(-8, 0, -8), new Vector3(8, 0, -8), new Vector3(-8, 0, 8), new Vector3(8, 0, 8),
                                  new Vector3(0, 0, -9), new Vector3(-9, 0, 0), new Vector3(9, 0, 0), new Vector3(0, 0, 10.5f) })
        {
            var pil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pil.name = "Pilar"; pil.transform.SetParent(world, false);
            pil.transform.position = p + Vector3.up * 2.5f; pil.transform.localScale = new Vector3(1.1f, 2.5f, 1.1f);
            pil.GetComponent<Renderer>().sharedMaterial = pillarMat;
        }
        foreach (var p in new[] { new Vector3(-H + 0.2f, 2.6f, -6), new Vector3(H - 0.2f, 2.6f, -6), new Vector3(-H + 0.2f, 2.6f, 6), new Vector3(H - 0.2f, 2.6f, 6), new Vector3(0, 2.6f, -H + 0.2f), new Vector3(-6, 2.6f, H - 0.2f), new Vector3(6, 2.6f, H - 0.2f) })
        {
            var t = new GameObject("Tocha").transform; t.SetParent(world, false); t.position = p;
            var l = t.gameObject.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.54f, 0.23f); l.range = 9f; l.intensity = 2.2f;
            var f = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(f.GetComponent<Collider>());
            f.transform.SetParent(t, false); f.transform.localScale = Vector3.one * 0.2f; f.GetComponent<Renderer>().sharedMaterial = fire;
        }

        // Fogueira (checkpoint / cura)
        var bonfire = new GameObject("Fogueira").transform; bonfire.SetParent(world, false); bonfire.position = new Vector3(0, 0, 4.5f);
        for (int i = 0; i < 3; i++)
        {
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(log.GetComponent<Collider>());
            log.transform.SetParent(bonfire, false); log.transform.localPosition = new Vector3(0, 0.08f, 0);
            log.transform.localRotation = Quaternion.Euler(90, i * 60, 0); log.transform.localScale = new Vector3(0.1f, 0.35f, 0.1f);
            log.GetComponent<Renderer>().sharedMaterial = wood;
        }
        var flame = GameObject.CreatePrimitive(PrimitiveType.Capsule); Object.DestroyImmediate(flame.GetComponent<Collider>());
        flame.transform.SetParent(bonfire, false); flame.transform.localPosition = new Vector3(0, 0.4f, 0); flame.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        flame.GetComponent<Renderer>().sharedMaterial = fire;
        var fl = new GameObject("Luz").AddComponent<Light>(); fl.transform.SetParent(bonfire, false); fl.transform.localPosition = new Vector3(0, 0.9f, 0);
        fl.type = LightType.Point; fl.color = new Color(1f, 0.48f, 0.16f); fl.range = 8f; fl.intensity = 3f;

        var spawns = new GameObject("PontosDeSpawn").transform;
        var spawnPts = new[] { new Vector3(-11.5f, 0, -11.5f), new Vector3(11.5f, 0, -11.5f), new Vector3(-11.5f, 0, 0), new Vector3(11.5f, 0, 0), new Vector3(0, 0, -11.5f), new Vector3(-11.5f, 0, 11.5f), new Vector3(11.5f, 0, 11.5f) }
            .Select((p, i) => { var t = new GameObject("Spawn" + i).transform; t.SetParent(spawns, false); t.position = p; return t; }).ToArray();
        var bossSpawn = new GameObject("SpawnChefe").transform; bossSpawn.SetParent(spawns, false); bossSpawn.position = new Vector3(0, 0, -10f);

        // Jogador: OVRCameraRig + componentes da arena
        var guid = AssetDatabase.FindAssets("OVRCameraRig t:Prefab").FirstOrDefault();
        if (guid == null) return "ERRO: prefab OVRCameraRig não encontrado (Meta XR Core SDK instalado?)";
        var rigGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
        rigGo.name = "Jogador (OVRCameraRig)";
        var rig = rigGo.GetComponent<OVRCameraRig>();
        int flags = EnableHandsAndControllers(rigGo);
        var player = rigGo.AddComponent<ArenaPlayer>();
        player.rigRoot = rigGo.transform;
        player.trackingSpace = rig.transform.Find("TrackingSpace");
        player.head = player.trackingSpace.Find("CenterEyeAnchor");
        player.rightHand = player.trackingSpace.Find("RightHandAnchor");
        player.leftHand = player.trackingSpace.Find("LeftHandAnchor");
        rigGo.AddComponent<SkillCaster>();
        rigGo.AddComponent<WeaponAbilityCaster>();
        rigGo.AddComponent<LeftHandPowers>();
        rigGo.AddComponent<PlayerShield>();
        rigGo.AddComponent<StartMenu>();
        rigGo.AddComponent<TwoHandGrip>();
        rigGo.AddComponent<ArenaHud>();
        rigGo.AddComponent<SkillTreeMenu>();
        var cam = player.head.GetComponent<Camera>();
        if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor; cam.nearClipPlane = 0.03f; }

        var game = new GameObject("ArenaGame").AddComponent<ArenaGame>();
        game.player = player;
        game.bonfire = bonfire;
        game.spawnPoints = spawnPts;
        game.bossSpawn = bossSpawn;
        game.enemyRoot = new GameObject("Inimigos").transform;

        int shaders = EnsureAlwaysIncludedShaders("Standard", "Sprites/Default");

        System.IO.Directory.CreateDirectory("Assets/Arena/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return $"Cena da arena criada em {ScenePath} (primeira do build). Mãos+controles ativados: {flags}. Shaders incluídos: +{shaders}.";
    }

    // Os materiais criados em código usam Shader.Find; sem isto o build pode remover esses shaders.
    static int EnsureAlwaysIncludedShaders(params string[] names)
    {
        var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
        if (gs == null) return 0;
        var so = new SerializedObject(gs);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        int added = 0;
        foreach (var n in names)
        {
            var sh = Shader.Find(n);
            if (sh == null) continue;
            bool present = false;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) present = true;
            if (present) continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
            added++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return added;
    }

    static int EnableHandsAndControllers(GameObject rig)
    {
        int n = 0;
        foreach (var comp in rig.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp.GetType().Name != "OVRManager") continue;
            var so = new SerializedObject(comp);
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.Boolean) continue;
                string name = it.name.ToLowerInvariant();
                if ((name.Contains("simultaneous") || name.Contains("concurrent")) && name.Contains("hand")) { it.boolValue = true; n++; }
            }
            var origin = so.FindProperty("_trackingOriginType");
            if (origin != null && origin.propertyType == SerializedPropertyType.Enum) origin.enumValueIndex = 1; // FloorLevel
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return n;
    }

    static void Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = name; b.transform.SetParent(parent, false);
        b.transform.position = pos; b.transform.localScale = scale;
        b.GetComponent<Renderer>().sharedMaterial = m;
    }

    static Material SaveMat(string name, Color c, Color? emission = null)
    {
        System.IO.Directory.CreateDirectory("Assets/Arena/Materials");
        string path = $"Assets/Arena/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = c;
        m.SetFloat("_Glossiness", 0.1f);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
        EditorUtility.SetDirty(m);
        return m;
    }
}
