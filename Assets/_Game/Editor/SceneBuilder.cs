using System.IO;
using UltimoRound.Core;
using UltimoRound.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace UltimoRound.EditorTools
{
    /// <summary>
    /// Monta as cenas do jogo de forma repetível. Só recria objetos marcados como "[Gerado]";
    /// qualquer outro objeto adicionado manualmente à cena é preservado.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenesRoot = "Assets/_Game/Scenes";
        public const string MenuScenePath = ScenesRoot + "/MainMenu.unity";
        public const string FightScenePath = ScenesRoot + "/Fight.unity";
        const string Generated = "[Gerado] ";
        const string ArenaMaterials = "Assets/_Game/Art/Materials/Arena";
        const string VolumePath = "Assets/_Game/Art/PostFX_Luta.asset";

        static bool _overwriteMaterials;

        [MenuItem("Último Round/Montar cenas")]
        public static void BuildAllMenu() => BuildAll(false);

        [MenuItem("Último Round/Montar cenas (recriar materiais da arena)")]
        public static void BuildAllRecolor() => BuildAll(true);

        public static string BuildAll(bool overwriteMaterials)
        {
            _overwriteMaterials = overwriteMaterials;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "cancelado";
            var db = GameDataGenerator.Generate(false);
            Directory.CreateDirectory(ScenesRoot);
            Directory.CreateDirectory(ArenaMaterials);
            var profile = EnsureVolumeProfile();
            BuildFight(db, profile);
            BuildMenu(db, profile);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(FightScenePath, true),
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            Debug.Log("[Cenas] MainMenu e Fight montadas; Build Settings atualizado.");
            return "ok";
        }

        static Scene OpenOrCreate(string path)
        {
            if (File.Exists(path)) return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(s, path);
            return s;
        }

        static void ClearGenerated(Scene s)
        {
            foreach (var go in s.GetRootGameObjects())
                if (go.name.StartsWith(Generated)) Object.DestroyImmediate(go);
        }

        static Material ArenaMaterial(string name, Color color, float emission, bool transparent)
        {
            string path = $"{ArenaMaterials}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !_overwriteMaterials) return mat;
            var shader = Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader) { name = name };
            else mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            if (!transparent) mat.SetFloat("_Smoothness", 0.25f);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                mat.SetColor("_EmissionColor", color * emission);
            }
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            if (name.StartsWith("Publico")) mat.enableInstancing = true;
            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        static VolumeProfile EnsureVolumeProfile()
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, VolumePath);
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.6f);
            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.28f);
            vig.smoothness.Override(0.45f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.contrast.Override(12f);
            ca.saturation.Override(8f);
            ca.postExposure.Override(0.15f);
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        static void AddVolume(VolumeProfile profile)
        {
            var go = new GameObject(Generated + "PosProcessamento");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
        }

        static Camera AddCamera(string name, Vector3 pos, Vector3 euler, float fov)
        {
            var go = new GameObject(Generated + name);
            go.tag = "MainCamera";
            go.transform.position = pos;
            go.transform.eulerAngles = euler;
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.035f, 0.06f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 80f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            go.AddComponent<AudioListener>();
            return cam;
        }

        static GameObject BuildArena()
        {
            var holder = new GameObject(Generated + "Arena");
            var font = TMPro.TMP_Settings.defaultFontAsset;
            ArenaBuilder.Build(holder.transform, ArenaMaterial, null, font);
            ArenaBuilder.ApplyEnvironment();
            AssetIntegrator.PlaceScenery(holder.transform);
            return holder;
        }

        static void BuildFight(GameDatabase db, VolumeProfile profile)
        {
            var scene = OpenOrCreate(FightScenePath);
            ClearGenerated(scene);
            var arena = BuildArena();
            AddVolume(profile);
            var cam = AddCamera("CameraLuta", new Vector3(0, 1.45f, -9f), new Vector3(3.5f, 0, 0), 26f);
            var fc = cam.gameObject.AddComponent<FightCamera>();

            var ctrlGo = new GameObject(Generated + "Luta");
            var ctrl = ctrlGo.AddComponent<FightSceneController>();
            ctrl.Database = db;
            ctrl.Camera = fc;
            ctrl.Ropes = arena.GetComponentInChildren<RingRopes>();
            ctrl.Crowd = arena.GetComponentInChildren<CrowdRenderer>();
            ctrl.ArenaScreen = arena.GetComponentInChildren<ArenaScreen>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildMenu(GameDatabase db, VolumeProfile profile)
        {
            var scene = OpenOrCreate(MenuScenePath);
            ClearGenerated(scene);
            BuildArena();
            AddVolume(profile);
            var cam = AddCamera("CameraMenu", new Vector3(0, 1.55f, -7.6f), new Vector3(6f, 0, 0), 30f);

            var ctrlGo = new GameObject(Generated + "Menu");
            var ctrl = ctrlGo.AddComponent<MainMenuController>();
            ctrl.Database = db;
            ctrl.MenuCamera = cam;
            ctrl.PreviewAnchors = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var a = new GameObject("Previa" + (i + 1)).transform;
                a.SetParent(ctrlGo.transform, false);
                a.position = new Vector3(i == 0 ? -0.95f : 0.95f, 0f, 0f);
                ctrl.PreviewAnchors[i] = a;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
