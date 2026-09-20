using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    // The project has no imported TextMeshPro essentials, so the font asset and the
    // TMP_Settings that TMP looks for in Resources are generated here instead.
    public static class TmpSetup
    {
        public const string FontAssetPath = "Assets/Fonts/Roboto-Regular SDF.asset";
        const string SourceFontPath = "Assets/Fonts/Roboto-Regular.ttf";
        const string SettingsPath = "Assets/Resources/TMP Settings.asset";

        // Split in two because TMP_FontAsset.CreateFontAsset reads TMP_Settings
        // internally: the settings asset has to exist, and be loadable from Resources,
        // before any font asset can be built. Run these as separate Editor launches.
        [MenuItem("Office Imposter/Set Up TextMeshPro (2 - Font)")]
        public static void CreateFont()
        {
            if (TMP_Settings.instance == null)
            {
                Debug.LogError("[TmpSetup] TMP_Settings still not loadable; run step 1 first.");
                return;
            }

            TMP_FontAsset fontAsset = EnsureFontAsset();
            EnsureSettings(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TmpSetup] font={(fontAsset != null ? fontAsset.name : "NULL")} " +
                      $"defaultFont={(TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset.name : "NULL")}");
        }

        // TMP's runtime shaders live only inside the package's own essentials archive,
        // so Shader.Find fails until it is imported. This is Unity's first-party content
        // already on disk with the installed package, not a downloaded asset.
        public static void ImportEssentials()
        {
            var stale = AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsPath);
            if (stale != null)
            {
                AssetDatabase.DeleteAsset(SettingsPath);
                Debug.Log("[TmpSetup] removed hand-made settings; the package ships its own");
            }

            string package = FindEssentialsPackage();
            if (package == null)
            {
                Debug.LogError("[TmpSetup] TMP Essential Resources.unitypackage not found");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[TmpSetup] importing {package}");
            AssetDatabase.importPackageCompleted += _ => Finish(0, "import completed");
            AssetDatabase.importPackageFailed += (_, error) => Finish(1, $"import failed: {error}");
            AssetDatabase.importPackageCancelled += _ => Finish(1, "import cancelled");
            AssetDatabase.ImportPackage(package, false);
        }

        static void Finish(int code, string message)
        {
            Debug.Log($"[TmpSetup] {message}");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorApplication.Exit(code);
        }

        static string FindEssentialsPackage()
        {
            if (!Directory.Exists("Library/PackageCache")) return null;

            foreach (string file in Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage",
                         SearchOption.AllDirectories))
            {
                return file.Replace('\\', '/');
            }
            return null;
        }

        public static void CreateFontFromCommandLine() => Run(CreateFont);

        static void Run(System.Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TmpSetup] Failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        static TMP_FontAsset EnsureFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[TmpSetup] Source font missing at {SourceFontPath}");
                return null;
            }

            // Dynamic atlas: glyphs are rasterised on demand, so umlauts work without
            // pre-baking a character set.
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(source);
            if (fontAsset == null)
            {
                Debug.LogError("[TmpSetup] CreateFontAsset returned null");
                return null;
            }

            fontAsset.name = "Roboto-Regular SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
            {
                fontAsset.atlasTextures[0].name = "Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = "Roboto-Regular SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        // The essentials import ships its own TMP_Settings; find it rather than
        // creating a second one that Resources.Load could pick between.
        static void EnsureSettings(TMP_FontAsset fontAsset)
        {
            string[] guids = AssetDatabase.FindAssets("t:TMP_Settings");
            if (guids.Length == 0)
            {
                Debug.LogError("[TmpSetup] No TMP_Settings asset in project");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(path);
            if (settings == null) return;

            var so = new SerializedObject(settings);
            SetObject(so, "m_defaultFontAsset", fontAsset);
            SetString(so, "m_defaultFontAssetPath", "Fonts & Materials/");
            SetFloat(so, "m_defaultFontSize", 24f);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(settings);
            Debug.Log($"[TmpSetup] settings at {path}");
        }

        static void SetObject(SerializedObject so, string path, Object value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property != null) property.objectReferenceValue = value;
        }

        static void SetString(SerializedObject so, string path, string value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property != null) property.stringValue = value;
        }

        static void SetFloat(SerializedObject so, string path, float value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property != null) property.floatValue = value;
        }

        static void SetBool(SerializedObject so, string path, bool value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property != null) property.boolValue = value;
        }
    }
}
