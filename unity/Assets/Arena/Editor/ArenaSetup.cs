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
        string summary = ConfigureProjectNoDialog();
        EditorUtility.DisplayDialog("Arena VR", summary +
            "\n\nPróximo passo:\nMeta > Tools > Project Setup Tool → 'Fix All' e depois 'Apply All'.\n\nDepois: Arena > 2 - Criar cena de teste.", "OK");
    }

    // Sem diálogos: pode ser chamado pelo Unity CLI (unity command eval) sem travar o editor.
    public static string ConfigureProjectNoDialog()
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
        bool inputChanged = SetInputSystemOnly();

        int ok = SetHandTrackingSupport();
        int xr = EnableOpenXRFeatures();

        AssetDatabase.SaveAssets();
        string summary = $"Projeto configurado para o Quest. Hand tracking: {(ok > 0 ? "ok" : "configure manualmente")}. Recursos OpenXR ativados: {xr}." +
                         (inputChanged ? " Active Input Handling mudou para Input System: REINICIE o editor." : "");
        Debug.Log("[Arena] " + summary + " Agora rode Meta > Tools > Project Setup Tool e clique em 'Fix All' e 'Apply All'.");
        return summary;
    }

    [MenuItem("Arena/2 - Criar cena de teste multimodal", priority = 2)]
    public static void CreateTestScene()
    {
        int flags = CreateTestSceneNoDialog();
        if (flags < 0)
        {
            EditorUtility.DisplayDialog("Arena VR", "Não encontrei o prefab OVRCameraRig. Confira se o 'Meta XR Core SDK' foi instalado no Package Manager.", "OK");
            return;
        }
        EditorUtility.DisplayDialog("Arena VR",
            $"Cena de teste criada.\n\nOpções 'mãos + controles' ativadas automaticamente: {flags}.\n" +
            (flags == 0 ? "Ative manualmente: selecione OVRCameraRig > OVRManager > 'Concurrent hands and controllers support'.\n\n" : "\n") +
            "Para testar: conecte o Quest pelo cabo e use File > Build And Run.", "OK");
    }

    // Sem diálogos. Retorna quantas opções "mãos + controles" foram ligadas, ou -1 se faltar o OVRCameraRig.
    public static int CreateTestSceneNoDialog()
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
        if (rigPrefabGuid == null) return -1;
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
        return flags;
    }

    // "Both" (legado + Input System) não é suportado no Android. 0 = legado, 1 = Input System, 2 = ambos.
    // Retorna true se mudou (a mudança só vale depois de reiniciar o editor).
    static bool SetInputSystemOnly()
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
        if (asset == null) return false;
        var so = new SerializedObject(asset);
        var prop = so.FindProperty("activeInputHandler");
        if (prop == null || prop.intValue == 1) return false;
        prop.intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // ---------- Partes específicas do SDK (via reflexão) ----------

    static int SetHandTrackingSupport()
    {
        try
        {
            var cfgType = FindType("OVRProjectConfig");
            if (cfgType == null) return 0;
            // SDK 207+: propriedade CachedProjectConfig; versões antigas: GetProjectConfig()
            object cfg = cfgType.GetProperty("CachedProjectConfig", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (cfg == null)
            {
                var get = cfgType.GetMethod("GetProjectConfig", BindingFlags.Public | BindingFlags.Static);
                cfg = get?.Invoke(null, get.GetParameters().Length == 0 ? null : new object[] { false });
            }
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

    // Recursos OpenXR que o teste multimodal precisa (nome completo do tipo).
    static readonly string[] OpenXRFeatureTypes =
    {
        "Meta.XR.MetaXRFeature",                                              // Meta XR Core SDK sobre OpenXR
        "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature",    // Meta Quest Support (só Android)
        "UnityEngine.XR.Hands.OpenXR.HandTracking",                            // XR Hands: juntas da mão
        "UnityEngine.XR.Hands.OpenXR.MetaHandTrackingAim",                     // XR Hands: pinça/mira
        "UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile",
        "UnityEngine.XR.OpenXR.Features.Interactions.MetaQuestTouchPlusControllerProfile",
    };

    // Liga o loader OpenXR (XR Plug-in Management) e os recursos acima.
    // Android = build no Quest; Standalone = Play no editor via Quest Link.
    static int EnableOpenXRFeatures()
    {
        int n = 0;
        foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
        {
            try
            {
                EnableOpenXRLoader(group);
                var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (settings == null) continue;
                foreach (var f in settings.GetFeatures())
                {
                    if (!OpenXRFeatureTypes.Contains(f.GetType().FullName) || f.enabled) continue;
                    f.enabled = true;
                    EditorUtility.SetDirty(f);
                    n++;
                }
                EditorUtility.SetDirty(settings);
            }
            catch (Exception e) { Debug.LogWarning($"[Arena] OpenXR ({group}): " + e.Message); }
        }
        return n;
    }

    static void EnableOpenXRLoader(BuildTargetGroup group)
    {
        EditorBuildSettings.TryGetConfigObject(UnityEngine.XR.Management.XRGeneralSettings.k_SettingsKey,
            out UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget perTarget);
        if (perTarget == null)
        {
            perTarget = ScriptableObject.CreateInstance<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>();
            System.IO.Directory.CreateDirectory("Assets/XR");
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(UnityEngine.XR.Management.XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
        if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
        var general = perTarget.SettingsForBuildTarget(group);
        UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(
            general.AssignedSettings, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
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
