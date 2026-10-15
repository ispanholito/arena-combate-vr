using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu "Arena" no topo do Unity: configura o projeto para Quest e cria a cena de teste.
// Partes específicas do SDK da Meta usam reflexão para o projeto compilar mesmo se a API mudar.
public static class ArenaSetup
{
    const string ScenePath = "Assets/Arena/Scenes/TesteMultimodal.unity";

    [MenuItem("Arena/1 - Configurar projeto para Quest", priority = 1)]
    public static void ConfigureProject()
    {
        var android = NamedBuildTarget.Android;
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.companyName = "Pablo";
        PlayerSettings.productName = "Arena VR";
        PlayerSettings.SetApplicationIdentifier(android, "com.pablo.arenavr");
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.colorSpace = ColorSpace.Linear;

        int ok = SetHandTrackingSupport();
        int xr = EnableOpenXRFeatures();

        AssetDatabase.SaveAssets();
        Debug.Log($"[Arena] Projeto configurado para Android/Quest. Hand tracking: {(ok > 0 ? "ok" : "configure manualmente")}. Recursos OpenXR ativados: {xr}. " +
                  "Agora rode Meta > Tools > Project Setup Tool e clique em 'Fix All' e 'Apply All'.");
        EditorUtility.DisplayDialog("Arena VR",
            "Projeto configurado para o Quest.\n\nPróximo passo:\nMeta > Tools > Project Setup Tool → 'Fix All' e depois 'Apply All'.\n\nDepois: Arena > 2 - Criar cena de teste.", "OK");
    }

    [MenuItem("Arena/2 - Criar cena de teste multimodal", priority = 2)]
    public static void CreateTestScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Remove a câmera padrão (o OVRCameraRig traz a dele)
        var mainCam = GameObject.Find("Main Camera");
        if (mainCam != null) UnityEngine.Object.DestroyImmediate(mainCam);

        // Chão
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Chao";
        floor.transform.localScale = new Vector3(2, 1, 2);

        // Alvos de referência
        for (int i = -1; i <= 1; i++)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            t.name = "Alvo";
            t.transform.position = new Vector3(i * 1.2f, 1f, 3f);
        }

        // OVRCameraRig do SDK da Meta
        var rigPrefabGuid = AssetDatabase.FindAssets("OVRCameraRig t:Prefab").FirstOrDefault();
        if (rigPrefabGuid == null)
        {
            EditorUtility.DisplayDialog("Arena VR", "Não encontrei o prefab OVRCameraRig. Confira se o 'Meta XR Core SDK' foi instalado no Package Manager.", "OK");
            return;
        }
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(rigPrefabGuid));
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
        rig.transform.position = Vector3.zero;

        int flags = EnableConcurrentHandsAndControllers(rig);

        // Script de teste
        var test = new GameObject("TesteMultimodal");
        AddComponentByName(test, "ArenaMultimodalTest");

        System.IO.Directory.CreateDirectory("Assets/Arena/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        Debug.Log($"[Arena] Cena criada em {ScenePath}. Opções de mãos+controles ativadas no OVRManager: {flags}.");
        EditorUtility.DisplayDialog("Arena VR",
            $"Cena de teste criada.\n\nOpções 'mãos + controles' ativadas automaticamente: {flags}.\n" +
            (flags == 0 ? "Ative manualmente: selecione OVRCameraRig > OVRManager > 'Concurrent hands and controllers support'.\n\n" : "\n") +
            "Para testar: conecte o Quest pelo cabo e use File > Build And Run.", "OK");
    }

    // ---------- Partes específicas do SDK (via reflexão) ----------

    static int SetHandTrackingSupport()
    {
        try
        {
            var cfgType = FindType("OVRProjectConfig");
            if (cfgType == null) return 0;
            var get = cfgType.GetMethod("GetProjectConfig", BindingFlags.Public | BindingFlags.Static);
            var cfg = get?.Invoke(null, get.GetParameters().Length == 0 ? null : new object[] { false });
            if (cfg == null) return 0;
            int n = 0;
            var field = cfgType.GetField("handTrackingSupport");
            if (field != null)
            {
                var value = Enum.Parse(field.FieldType, "ControllersAndHands");
                field.SetValue(cfg, value);
                n++;
            }
            var commit = cfgType.GetMethod("CommitProjectConfig", BindingFlags.Public | BindingFlags.Static);
            commit?.Invoke(null, new[] { cfg });
            return n;
        }
        catch (Exception e) { Debug.LogWarning("[Arena] Hand tracking support: " + e.Message); return 0; }
    }

    static int EnableConcurrentHandsAndControllers(GameObject rig)
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
                if ((name.Contains("simultaneous") || name.Contains("concurrent")) && name.Contains("hand"))
                {
                    it.boolValue = true;
                    n++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return n;
    }

    static int EnableOpenXRFeatures()
    {
        int n = 0;
        try
        {
            var settingsType = FindType("UnityEngine.XR.OpenXR.OpenXRSettings");
            var getSettings = settingsType?.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.Static);
            var settings = getSettings?.Invoke(null, new object[] { BuildTargetGroup.Android });
            if (settings == null) return 0;
            var features = settingsType.GetMethod("GetFeatures", Type.EmptyTypes)?.Invoke(settings, null) as Array;
            if (features == null) return 0;
            foreach (var f in features)
            {
                var t = f.GetType();
                string label = (t.GetField("nameUi")?.GetValue(f) as string) ?? t.Name;
                string l = label.ToLowerInvariant();
                if (l.Contains("hand tracking") || l.Contains("meta xr") || l.Contains("meta quest"))
                {
                    t.GetProperty("enabled")?.SetValue(f, true);
                    n++;
                }
            }
        }
        catch (Exception e) { Debug.LogWarning("[Arena] OpenXR features: " + e.Message); }
        return n;
    }

    static void AddComponentByName(GameObject go, string typeName)
    {
        var t = FindType(typeName);
        if (t != null) go.AddComponent(t);
        else Debug.LogWarning($"[Arena] Script {typeName} não encontrado (erro de compilação?)");
    }

    static Type FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = null;
            try { t = asm.GetType(name) ?? asm.GetTypes().FirstOrDefault(x => x.Name == name || x.FullName == name); } catch { }
            if (t != null) return t;
        }
        return null;
    }
}
