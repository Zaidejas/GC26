using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Подключает CRT-эффект к 2D-рендеру. Настройки: Assets/Settings/CRT.mat.
// Отключить: Assets/Settings/Renderer2D.asset, галочка у Full Screen Pass "CRT"
public static class SpaceInvadersCrt
{
    const string ShaderName = "SpaceInvaders/CRT";
    const string MaterialPath = "Assets/Settings/CRT.mat";
    const string RendererPath = "Assets/Settings/Renderer2D.asset";
    const string FeatureName = "CRT";

    [MenuItem("Tools/Space Invaders/Apply CRT Effect")]
    public static void Apply()
    {
        Material material = LoadOrCreateMaterial();
        if (material == null) return;

        var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
        if (renderer == null)
        {
            Debug.LogError("Space Invaders: renderer not found at " + RendererPath);
            return;
        }

        foreach (ScriptableRendererFeature existing in renderer.rendererFeatures)
        {
            if (existing != null && existing.name == FeatureName) return;
        }

        var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        feature.name = FeatureName;
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
        feature.fetchColorBuffer = true;
        feature.requirements = ScriptableRenderPassInput.Color;
        feature.passMaterial = material;
        feature.passIndex = 0;

        AssetDatabase.AddObjectToAsset(feature, renderer);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

        // Список эффектов и его карта хранятся в рендере рядом и должны совпадать
        var serialized = new SerializedObject(renderer);
        SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
        SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        Debug.Log("Space Invaders: CRT effect added to " + RendererPath);
    }

    static Material LoadOrCreateMaterial()
    {
        // Существующий материал не трогаем: в нём могут быть подобранные настройки
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("Space Invaders: shader not found: " + ShaderName);
            return null;
        }

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }
}
