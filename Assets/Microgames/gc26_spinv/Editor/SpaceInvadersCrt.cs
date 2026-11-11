using UnityEditor;
using UnityEngine;

namespace GC26.Spinv
{
    // Материал CRT-эффекта: Assets/Microgames/gc26_spinv/Settings/CRT.mat.
    // Эффект рисует компонент CrtCamera на камере игры (общий рендерер проекта не трогаем)
    public static class SpaceInvadersCrt
    {
        const string ShaderName = "SpaceInvaders/CRT";
        const string MaterialPath = SpaceInvadersBuilder.RootDir + "/Settings/CRT.mat";

        public static Material LoadOrCreateMaterial()
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
}
