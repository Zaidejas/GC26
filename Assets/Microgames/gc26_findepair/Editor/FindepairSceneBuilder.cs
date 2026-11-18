using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GC26.Findepair.Editor
{
    /// <summary>
    /// GC26 → Findepair → Rebuild Scene and Assets: sets the PNG import settings, creates the CardData
    /// assets and writes Scenes/gc26_findepair_Main.unity with all art linked to FindepairGame.
    /// Safe to run again after replacing pictures in Art/.
    /// Batch mode: Unity.exe -batchmode -projectPath . -executeMethod GC26.Findepair.Editor.FindepairSceneBuilder.Build -quit
    /// </summary>
    public static class FindepairSceneBuilder
    {
        const string Root = "Assets/Microgames/" + FindepairGame.Id;
        const string ScenePath = Root + "/Scenes/" + FindepairGame.Id + "_Main.unity";

        static readonly (string id, string name, string file, string color)[] Cards =
        {
            ("EmoCat", "Emo kitty", "emo_cat", "#FF2FB3"),
            ("EmoBear", "Emo bear", "emo_bear", "#FF7AD9"),
            ("Polaroid", "Polaroid", "polaroid", "#FF2FB3"),
            ("Mp3Player", "MP3 player", "mp3_player", "#2DE2E6"),
            ("EnergyCan", "Energy drink", "energy_drink", "#9B5CFF"),
            ("BowSkull", "Bow skull", "bow_skull", "#FCEBFF"),
        };

        [MenuItem("GC26/Findepair/Rebuild Scene and Assets")]
        public static void Build()
        {
            ImportArt();
            MakeCardData();
            int pairs = MakeScene();
            Debug.Log($"[{FindepairGame.Id}] built {ScenePath} with {pairs} pairs");
        }

        /// <summary>GC26 → Findepair → Play: opens the game scene and enters Play Mode.</summary>
        [MenuItem("GC26/Findepair/Play")]
        public static void OpenAndPlay()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        // ---------- Art import ----------

        static void ImportArt()
        {
            foreach (var path in Directory.GetFiles(Root + "/Art", "*.png", SearchOption.AllDirectories))
            {
                var p = path.Replace('\\', '/');
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.spritePixelsPerUnit = 100;

                string folder = Path.GetFileName(Path.GetDirectoryName(p));
                string file = Path.GetFileNameWithoutExtension(p);
                int max = folder == "Backgrounds" ? 2048 : folder == "Cards" ? 1024 : 256;
                ti.maxTextureSize = max;
                ti.textureCompression = folder == "UI" ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                ti.spriteBorder = file switch
                {
                    "rounded" => new Vector4(56, 56, 56, 56),
                    "frame" => new Vector4(56, 56, 56, 56),
                    "glow" => new Vector4(88, 88, 88, 88),
                    _ => Vector4.zero,
                };
                if (folder == "Cards" || folder == "Backgrounds")
                {
                    ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                    {
                        name = "Android",
                        overridden = true,
                        maxTextureSize = max,
                        format = TextureImporterFormat.ASTC_6x6,
                    });
                }
                ti.SaveAndReimport();
            }
        }

        static Sprite Art(string relative)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Art/{relative}.png");
            if (s == null) Debug.LogError($"[{FindepairGame.Id}] missing sprite Art/{relative}.png");
            return s;
        }

        // ---------- Card data ----------

        const string CardDir = Root + "/Data/Cards";

        static void MakeCardData()
        {
            Directory.CreateDirectory(CardDir);
            foreach (var c in Cards)
            {
                string path = $"{CardDir}/{c.id}.asset";
                var data = AssetDatabase.LoadAssetAtPath<CardData>(path);
                bool isNew = data == null;
                if (isNew) data = ScriptableObject.CreateInstance<CardData>();
                data.id = c.id;
                data.displayName = c.name;
                data.front = Art("Cards/" + c.file);
                data.glow = FindepairUIKit.Hex(c.color);
                if (isNew) AssetDatabase.CreateAsset(data, path);
                else EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- Scene ----------

        static int MakeScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            // NewScene unloads unused assets, so the CardData assets are loaded only after it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Findepair");
            var game = go.AddComponent<FindepairGame>();

            var so = new SerializedObject(game);
            var list = so.FindProperty("art.cards");
            list.arraySize = Cards.Length;
            for (int i = 0; i < Cards.Length; i++)
            {
                var data = AssetDatabase.LoadAssetAtPath<CardData>($"{CardDir}/{Cards[i].id}.asset");
                if (data == null) Debug.LogError($"[{FindepairGame.Id}] missing card data {Cards[i].id}");
                list.GetArrayElementAtIndex(i).objectReferenceValue = data;
            }
            void Set(string field, string sprite) => so.FindProperty("art." + field).objectReferenceValue = Art(sprite);
            Set("cardBack", "Cards/card_back");
            Set("menuWall", "Backgrounds/menu_wall");
            Set("boardCork", "Backgrounds/board_cork");
            Set("sparkle", "FX/sparkle");
            Set("flash", "FX/flash");
            Set("heart", "FX/heart");
            Set("star", "FX/star");
            Set("bolt", "FX/bolt");
            Set("rounded", "UI/rounded");
            Set("frame", "UI/frame");
            Set("glow", "UI/glow");
            Set("circle", "UI/circle");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            return list.arraySize;
        }
    }
}
