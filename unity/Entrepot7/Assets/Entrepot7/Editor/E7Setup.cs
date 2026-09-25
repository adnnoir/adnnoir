using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace E7.EditorTools
{
    /// <summary>
    /// Préparation automatique du projet à la première ouverture :
    /// scène principale, couleurs linéaires, nom du jeu, système d'entrées compatible.
    /// Le jeu se construit tout seul au lancement (voir Game.cs) : la scène peut rester vide.
    /// </summary>
    [InitializeOnLoad]
    public static class E7Setup
    {
        const string ScenePath = "Assets/Entrepot7/Scenes/Main.unity";

        static E7Setup() { EditorApplication.delayCall += Setup; }

        static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            if (PlayerSettings.productName != "Entrepot 7") { PlayerSettings.productName = "Entrepot 7"; PlayerSettings.companyName = "adnnoir"; }
            EnsureLegacyInput();
            if (!File.Exists(ScenePath)) CreateScene();
            var active = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && active.rootCount <= 2) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Entrepôt 7/Créer ou ouvrir la scène principale")]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var note = new GameObject("LIS-MOI : le jeu se construit tout seul en appuyant sur Play");
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        [MenuItem("Entrepôt 7/Ouvrir le dossier de sauvegarde")]
        static void OpenSave() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        [MenuItem("Entrepôt 7/Réimporter les textures")]
        static void Reimport() => AssetDatabase.ImportAsset("Assets/Entrepot7/Resources/E7/Textures", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

        /// <summary>
        /// Le jeu utilise l'ancien système d'entrées (Input Manager). Si le projet n'accepte que le nouveau,
        /// on passe sur « les deux » (il faut alors redémarrer Unity une fois).
        /// </summary>
        static void EnsureLegacyInput()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue == 1)
            {
                prop.intValue = 2;
                so.ApplyModifiedProperties();
                EditorUtility.DisplayDialog("Entrepôt 7", "Le système d'entrées a été réglé sur « Both » (ancien + nouveau). Redémarre Unity une fois pour que le changement soit pris en compte.", "OK");
            }
        }
    }

    /// <summary>Réglages d'import des textures exportées de la version web.</summary>
    public class E7TextureImport : AssetPostprocessor
    {
        static readonly string[] Linear = { "gunWear", "brush", "concreteRough" };
        static readonly string[] Alpha = { "flash", "dot", "cone", "hole", "blood", "blob", "holo" };

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/E7/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            if (n.EndsWith("_n") || n.Contains("_n_")) { ti.textureType = TextureImporterType.NormalMap; return; }
            ti.textureType = TextureImporterType.Default;
            if (System.Array.IndexOf(Linear, n) >= 0) ti.sRGBTexture = false;
            if (System.Array.IndexOf(Alpha, n) >= 0) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; }
            ti.anisoLevel = 4;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/E7/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            ai.defaultSampleSettings = s;
        }
    }
}
