using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UltimoRound.Audio;
using UltimoRound.Combat;
using UltimoRound.Core;
using UltimoRound.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltimoRound.EditorTools
{
    /// <summary>
    /// Liga os pacotes da Asset Store ao jogo sem editar os originais: configura clipes (raiz fixa, porque o
    /// deslocamento é da simulação), cria cópias URP dos materiais, gera prefabs normalizados dos lutadores e
    /// preenche os dados de visual, som e efeitos. Repetível.
    /// </summary>
    public static class AssetIntegrator
    {
        const string PrefabRoot = "Assets/_Game/Prefabs/Lutadores";
        const string VisualRoot = "Assets/_Game/Data/Visuais";
        const string MaterialRoot = "Assets/_Game/Art/Materials/Convertidos";
        const string SoundBankPath = "Assets/_Game/Data/SoundBank.asset";
        public const string ReportPath = "Logs/UltimoRound_Integracao.txt";

        static readonly string[] AnimationFolders =
        {
            "Assets/FightingMotionsVolume1/FBX", "Assets/ExplosiveLLC", "Assets/Kevin Iglesias",
            "Assets/Survivalist/StarterAssets/ThirdPersonController/Character/Animations",
        };

        static readonly StringBuilder Report = new StringBuilder();

        // ---------------------------------------------------------------- MENU

        [MenuItem("Último Round/Assets/1. Configurar clipes (raiz fixa)")]
        public static void ConfigureClipsMenu() => ConfigureClips();

        [MenuItem("Último Round/Assets/2. Gerar lutadores, sons e efeitos")]
        public static void GenerateMenu() => Generate();

        // ---------------------------------------------------------------- CLIPES

        public static int ConfigureClips()
        {
            int changed = 0;
            foreach (var folder in AnimationFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter imp) || !imp.importAnimation) continue;
                    var clips = imp.clipAnimations;
                    if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
                    if (clips == null || clips.Length == 0) continue;
                    bool dirty = false;
                    foreach (var c in clips)
                    {
                        if (c.lockRootRotation && c.lockRootHeightY && c.lockRootPositionXZ && c.keepOriginalPositionY && c.keepOriginalPositionXZ && c.keepOriginalOrientation) continue;
                        c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
                        c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
                        dirty = true;
                    }
                    if (!dirty) continue;
                    imp.clipAnimations = clips;
                    imp.SaveAndReimport();
                    changed++;
                }
            }
            Debug.Log($"[Assets] Clipes configurados: {changed} arquivos.");
            return changed;
        }

        static readonly Dictionary<string, string> ModelIndex = new Dictionary<string, string>();

        static void BuildIndex()
        {
            ModelIndex.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Model"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/")) continue;
                ModelIndex[Path.GetFileNameWithoutExtension(path).Replace(".anim", "")] = path;
            }
        }

        static AnimationClip Clip(string model, string clipName = null)
        {
            if (!ModelIndex.TryGetValue(model, out var path))
            {
                Report.AppendLine("CLIPE AUSENTE: " + model);
                return null;
            }
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview") && !c.name.Contains("[RM]")).ToArray();
            string wanted = clipName ?? model;
            var clip = clips.FirstOrDefault(c => c.name == wanted) ?? clips.FirstOrDefault();
            if (clip == null) Report.AppendLine("SEM CLIPE EM: " + path);
            return clip;
        }

        // ---------------------------------------------------------------- MATERIAIS

        static bool IsUrp(Material m) => m != null && m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline");

        /// <summary>Cópia URP Lit do material (o original do pacote fica intacto).</summary>
        public static Material UrpCopy(Material src)
        {
            if (src == null || IsUrp(src)) return src;
            Directory.CreateDirectory(MaterialRoot);
            string safe = string.Concat(src.name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
            string srcPath = AssetDatabase.GetAssetPath(src);
            string suffix = string.IsNullOrEmpty(srcPath) ? "" : "_" + AssetDatabase.AssetPathToGUID(srcPath).Substring(0, 6);
            string path = $"{MaterialRoot}/{safe}{suffix}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = safe };
            if (src.HasProperty("_MainTex")) mat.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
            if (src.HasProperty("_Color")) mat.SetColor("_BaseColor", src.GetColor("_Color"));
            if (src.HasProperty("_BumpMap") && src.GetTexture("_BumpMap") != null)
            {
                mat.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
                mat.EnableKeyword("_NORMALMAP");
            }
            if (src.HasProperty("_MetallicGlossMap") && src.GetTexture("_MetallicGlossMap") != null)
            {
                mat.SetTexture("_MetallicGlossMap", src.GetTexture("_MetallicGlossMap"));
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (src.HasProperty("_Metallic")) mat.SetFloat("_Metallic", src.GetFloat("_Metallic"));
            mat.SetFloat("_Smoothness", src.HasProperty("_Glossiness") ? Mathf.Min(src.GetFloat("_Glossiness"), 0.55f) : 0.3f);
            if (src.HasProperty("_EmissionMap") && src.GetTexture("_EmissionMap") != null)
            {
                mat.SetTexture("_EmissionMap", src.GetTexture("_EmissionMap"));
                if (src.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
            }
            float mode = src.HasProperty("_Mode") ? src.GetFloat("_Mode") : 0f;
            if (mode == 1f)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (mode >= 2f)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static void ConvertRenderers(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = UrpCopy(mats[i]);
                r.sharedMaterials = mats;
            }
        }

        static void StripComponents(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            // Juntas primeiro: elas dependem do Rigidbody e impediriam a remoção.
            foreach (var j in go.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j);
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c is MonoBehaviour || c is Collider || c is Rigidbody || c is CharacterController || c is AudioSource || c is Light || c is Camera)
                    Object.DestroyImmediate(c);
            }
        }

        // ---------------------------------------------------------------- LUTADORES

        static Avatar FindAvatar(GameObject source)
        {
            var a = source.GetComponentInChildren<Animator>(true);
            if (a != null && a.avatar != null && a.avatar.isHuman) return a.avatar;
            foreach (var r in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                var av = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(r.sharedMesh)).OfType<Avatar>().FirstOrDefault(x => x.isHuman);
                if (av != null) return av;
            }
            return null;
        }

        static GameObject BuildCostumePrefab(string name, string sourcePath, float height)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) { Report.AppendLine("MODELO AUSENTE: " + sourcePath); return null; }
            var avatar = FindAvatar(source);
            if (avatar == null) { Report.AppendLine($"SEM AVATAR HUMANOID: {name} ({sourcePath})"); return null; }

            var go = Object.Instantiate(source);
            go.name = name;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            StripComponents(go);
            foreach (var extra in go.GetComponentsInChildren<Animator>(true))
                if (extra.gameObject != go) Object.DestroyImmediate(extra);
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            ConvertRenderers(go);

            // Altura de combate igual em qualquer pacote (os volumes da simulação não mudam).
            var renderers = go.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                float s = b.size.y > 0.01f ? height / b.size.y : 1f;
                go.transform.localScale *= s;
                Report.AppendLine($"{name}: altura original {b.size.y:F2} -> escala {s:F3} ({renderers.Length} malhas)");
            }
            if (name.Contains("Sobrevivente")) AttachGear(go, animator);
            Directory.CreateDirectory(PrefabRoot);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabRoot}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Fuzil nas costas e pistola na coxa: acessórios de visual, sem efeito no combate.</summary>
        static void AttachGear(GameObject go, Animator animator)
        {
            const string Weapons = "Assets/Low Poly Weapons VOL.1/Prefabs/";
            void Attach(string weapon, HumanBodyBones bone, Vector3 offset, Vector3 euler)
            {
                string path = AssetDatabase.FindAssets(weapon + " t:Prefab", new[] { "Assets/Low Poly Weapons VOL.1" })
                    .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == weapon);
                var parent = animator.GetBoneTransform(bone);
                if (path == null || parent == null) { Report.AppendLine("ACESSÓRIO AUSENTE: " + Weapons + weapon); return; }
                var item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                item.name = weapon;
                StripComponents(item);
                ConvertRenderers(item);
                item.transform.position = parent.position + go.transform.TransformVector(offset);
                item.transform.rotation = Quaternion.Euler(euler);
                item.transform.localScale = Vector3.one * go.transform.localScale.y;
                item.transform.SetParent(parent, true);
            }
            Attach("M4_8", HumanBodyBones.Chest, new Vector3(0f, 0f, -0.17f), new Vector3(-60f, 90f, 0f));
            Attach("M1911", HumanBodyBones.RightUpperLeg, new Vector3(-0.11f, -0.16f, 0f), new Vector3(90f, 0f, 0f));
        }

        /// <summary>Momento em que mão ou pé mais se afasta do quadril: é o impacto visual do golpe.</summary>
        static float FindStrikeTime(AnimationClip clip, GameObject rig)
        {
            if (clip == null || rig == null) return 0f;
            var anim = rig.GetComponent<Animator>();
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            var limbs = new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot }
                .Select(anim.GetBoneTransform).Where(t => t != null).ToArray();
            float bestTime = clip.length * 0.45f, best = -1f;
            const int samples = 40;
            for (int i = 0; i <= samples; i++)
            {
                float t = clip.length * i / samples;
                clip.SampleAnimation(rig, t);
                foreach (var l in limbs)
                {
                    Vector3 d = l.position - hips.position;
                    float reach = new Vector2(d.x, d.z).magnitude;
                    if (reach > best) { best = reach; bestTime = t; }
                }
            }
            return Mathf.Clamp(bestTime, Mathf.Min(0.05f, clip.length * 0.2f), clip.length * 0.85f);
        }

        static MoveAnimation Move(GameObject rig, MoveSlot slot, string clip, string alt = null, float crouch = 0f, float tuck = 0f)
        {
            var c = Clip(clip);
            var a = alt != null ? Clip(alt) : null;
            return new MoveAnimation
            {
                Slot = slot,
                Clip = c,
                AltClip = a,
                StrikeTime = FindStrikeTime(c, rig),
                AltStrikeTime = FindStrikeTime(a, rig),
                Crouch = crouch,
                Tuck = tuck,
            };
        }

        static FighterVisualSet SaveVisual(string id, System.Action<FighterVisualSet> fill)
        {
            Directory.CreateDirectory(VisualRoot);
            string path = $"{VisualRoot}/{id}.asset";
            var set = AssetDatabase.LoadAssetAtPath<FighterVisualSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<FighterVisualSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            set.FighterId = id;
            // Estados comuns (Fighting Motions + Starter Assets); cada lutador sobrescreve o que for seu.
            set.WalkForward = Clip("move_forward_B2");
            set.WalkBack = Clip("move_backward_B2");
            set.Idle = Clip("idle_A");
            set.HitHigh = Clip("hd_front_A");
            set.HitBody = Clip("bd_front_A");
            set.HitLow = Clip("ld_front_A");
            set.BlockHigh = Clip("hb_front_A");
            set.BlockBody = Clip("bb_front_A");
            set.BlockLow = Clip("lb_front_A");
            set.Knockdown = Clip("knockdown_A2");
            set.KnockdownLoop = Clip("knockdown_loop_A");
            set.GetUp = Clip("getup_A");
            set.KO = Clip("knockdown_A");
            set.Victory = Clip("win_A");
            set.Intro = Clip("idle_Tiramis");
            set.JumpRise = Clip("Jump--Jump", "JumpStart");
            set.JumpFall = Clip("Jump--InAir", "InAir");
            set.DashForward = Clip("Locomotion--Run_N", "Run_N");
            set.DashBack = Clip("Locomotion--Run_S", "Run_S");
            fill(set);
            EditorUtility.SetDirty(set);
            return set;
        }

        static FighterCostume Costume(string label, string file, string source, float height)
        {
            var prefab = BuildCostumePrefab(file, source, height);
            return prefab != null ? new FighterCostume { Name = label, Prefab = prefab } : null;
        }

        static FighterCostume[] Valid(params FighterCostume[] list) => list.Where(c => c != null).ToArray();

        // ---------------------------------------------------------------- GERAÇÃO

        public static string Generate()
        {
            Report.Clear();
            BuildIndex();
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameDataGenerator.DatabasePath);
            const string TT = "Assets/tt-3d/LowPolyFighterStarterPack/Character/Prefabs/";
            const string EX = "Assets/ExplosiveLLC/Fighter Pack Bundle FREE/Fighters/";

            string ExplosiveModel(string pack) => $"{EX}{pack} Fighter Mecanim Animation Pack FREE/Prefabs/{pack}.prefab";

            // Rig de referência para medir o impacto dos golpes.
            var rigSource = AssetDatabase.LoadAssetAtPath<GameObject>(TT + "fighter_man_v2.prefab");
            var rig = Object.Instantiate(rigSource);
            var rigAnim = rig.GetComponentInChildren<Animator>();
            if (rigAnim == null) rigAnim = rig.AddComponent<Animator>();
            if (rigAnim.avatar == null) rigAnim.avatar = FindAvatar(rigSource);
            if (rigAnim.gameObject != rig) rig = rigAnim.gameObject;

            var giovani = SaveVisual("giovani", v =>
            {
                v.Costumes = Valid(
                    Costume("Lutador", "Giovani_Lutador", TT + "fighter_man_v2.prefab", 1.76f),
                    Costume("Pugilista", "Giovani_Pugilista", ExplosiveModel("Male"), 1.76f),
                    Costume("Azul", "Giovani_Azul", "Assets/FightingMotionsVolume1/Prefabs/BlueGuy.prefab", 1.76f));
                v.Moves = new[]
                {
                    Move(rig, MoveSlot.Punch, "hp_straight_left_A"),
                    Move(rig, MoveSlot.Kick, "bk_rh_right_A2"),
                    Move(rig, MoveSlot.Heavy, "hp_straight_right_A2"),
                    Move(rig, MoveSlot.CrouchPunch, "hp_straight_left_Tiramis", null, 0.85f),
                    Move(rig, MoveSlot.Low, "lk_rh_right_A2", null, 0.45f),
                    Move(rig, MoveSlot.AntiAir, "hp_upper_right_A2"),
                    Move(rig, MoveSlot.Air, "hk_side_left_A2", null, 0f, 0.5f),
                    Move(rig, MoveSlot.Grab, "bp_hook_right_A2"),
                    Move(rig, MoveSlot.SpecialNeutral, "hk_rh_right_A2"),
                    Move(rig, MoveSlot.SpecialForward, "Male@Punch"),
                    Move(rig, MoveSlot.Signature, "hp_straight_left_A", "hp_straight_right_A2"),
                };
            });

            var brutus = SaveVisual("brutus", v =>
            {
                v.Costumes = Valid(
                    Costume("Lutador livre", "Brutus_Lutador", TT + "WrestlerMan_v2.prefab", 1.92f),
                    Costume("Pesado", "Brutus_Pesado", ExplosiveModel("Heavy"), 1.92f),
                    Costume("Zumbi", "Brutus_Zumbi", "Assets/Kevin Iglesias/Zombie Animations/Prefabs/Zombie.prefab", 1.92f));
                v.Idle = Clip("Heavy@Idle");
                v.WalkForward = Clip("Heavy@WalkForward");
                v.WalkBack = Clip("Heavy@WalkBackward");
                v.HitBody = Clip("Zombie@Damage01");
                v.KO = Clip("Zombie@Death01_A", "Zombie@Death01_A");
                v.Intro = Clip("Zombie@Idle01_Action01");
                v.Moves = new[]
                {
                    Move(rig, MoveSlot.Punch, "Heavy@Punch"),
                    Move(rig, MoveSlot.Kick, "bk_push_left_A"),
                    Move(rig, MoveSlot.Heavy, "bp_hook_right_A2"),
                    Move(rig, MoveSlot.ForwardHeavy, "hp_hook_left_A"),
                    Move(rig, MoveSlot.CrouchPunch, "bp_upper_left_A2", null, 0.7f),
                    Move(rig, MoveSlot.Low, "lk_rh_left_A2", null, 0.5f),
                    Move(rig, MoveSlot.AntiAir, "hp_upper_right_A2"),
                    Move(rig, MoveSlot.Air, "bk_push_left_A", null, 0f, 0.5f),
                    Move(rig, MoveSlot.Grab, "Zombie@Attack01"),
                    Move(rig, MoveSlot.SpecialNeutral, "Zombie@Attack01"),
                    Move(rig, MoveSlot.SpecialForward, "bp_hook_right_A"),
                    Move(rig, MoveSlot.Signature, "hp_upper_right_A2", null, 0.2f),
                };
            });

            var faisca = SaveVisual("faisca", v =>
            {
                v.Costumes = Valid(
                    Costume("Lutadora", "Faisca_Lutadora", TT + "fighterwoman_v2.prefab", 1.66f),
                    Costume("Atleta", "Faisca_Atleta", ExplosiveModel("Female"), 1.66f),
                    Costume("Lutadora livre", "Faisca_LutadoraLivre", TT + "WrestlerWoman_v2.prefab", 1.66f));
                v.Idle = Clip("Female@Idle");
                v.WalkForward = Clip("Female@WalkForward");
                v.WalkBack = Clip("Female@WalkBackward");
                v.Moves = new[]
                {
                    Move(rig, MoveSlot.Punch, "hp_straight_left_Tiramis"),
                    Move(rig, MoveSlot.Kick, "lk_rh_left_A2"),
                    Move(rig, MoveSlot.Heavy, "hp_hook_left_Tiramis"),
                    Move(rig, MoveSlot.CrouchPunch, "hp_straight_left_A", null, 0.85f),
                    Move(rig, MoveSlot.Low, "lk_rh_right_Tiramis", null, 0.5f),
                    Move(rig, MoveSlot.AntiAir, "hk_side_left_A2"),
                    Move(rig, MoveSlot.Air, "lk_rh_left_A2", null, 0f, 0.6f),
                    Move(rig, MoveSlot.Grab, "bp_upper_left_A2"),
                    Move(rig, MoveSlot.SpecialNeutral, "Female@Punch", "hp_straight_left_Tiramis"),
                    Move(rig, MoveSlot.SpecialForward, "lk_rh_right_A2", null, 0.8f),
                    Move(rig, MoveSlot.Signature, "hp_straight_left_Tiramis", "hp_hook_left_Tiramis"),
                };
            });

            var vertice = SaveVisual("vertice", v =>
            {
                v.Costumes = Valid(
                    Costume("Kung fu", "Vertice_KungFu", TT + "kungfu_man_v2.prefab", 1.82f),
                    Costume("Berserker", "Vertice_Berserker", ExplosiveModel("Berserker"), 1.82f),
                    Costume("Vermelho", "Vertice_Vermelho", "Assets/FightingMotionsVolume1/Prefabs/RedGuy.prefab", 1.82f),
                    Costume("Sobrevivente", "Vertice_Sobrevivente", "Assets/Survivalist/Prefab/Survivalist (1).prefab", 1.82f));
                v.Idle = Clip("Berserker@Idle");
                v.WalkForward = Clip("Berserker@WalkForward");
                v.WalkBack = Clip("Berserker@WalkBackward");
                v.Moves = new[]
                {
                    Move(rig, MoveSlot.Punch, "Berserker@Punch"),
                    Move(rig, MoveSlot.Kick, "bk_rh_right_A2"),
                    Move(rig, MoveSlot.Heavy, "hk_rh_right_A2"),
                    Move(rig, MoveSlot.ForwardHeavy, "hk_side_left_A2"),
                    Move(rig, MoveSlot.CrouchPunch, "lk_rh_left_A2", null, 0.6f),
                    Move(rig, MoveSlot.Low, "lk_rh_right_A", null, 0.5f),
                    Move(rig, MoveSlot.AntiAir, "hk_rh_right_A"),
                    Move(rig, MoveSlot.Air, "hk_side_left_A", null, 0f, 0.5f),
                    Move(rig, MoveSlot.Grab, "bp_upper_left_A"),
                    Move(rig, MoveSlot.SpecialNeutral, "hk_side_left_A2"),
                    Move(rig, MoveSlot.SpecialForward, "bk_rh_right_A"),
                    Move(rig, MoveSlot.Signature, "hk_rh_right_A2", "bk_rh_right_A2"),
                };
            });
            Object.DestroyImmediate(rig.transform.root.gameObject);

            db.Visuals = new List<FighterVisualSet> { giovani, brutus, faisca, vertice };
            db.Sounds = BuildSoundBank();
            db.VfxPrefabs = BuildVfxList();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            File.WriteAllText(ReportPath, Report.ToString());
            Debug.Log("[Assets] Integração concluída.\n" + Report);
            return Report.ToString();
        }

        // ---------------------------------------------------------------- SONS

        static SoundBank BuildSoundBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(SoundBankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, SoundBankPath);
            }
            var all = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Deadly Kombat Free version" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            AudioClip[] Of(params string[] prefixes) =>
                all.Where(c => prefixes.Any(p => c.name.StartsWith(p, System.StringComparison.OrdinalIgnoreCase))).ToArray();
            bank.LightHits = Of("body_hit_small", "face_hit_small");
            bank.HeavyHits = Of("body_hit_large", "face_hit_large");
            bank.FinisherHits = Of("body_hit_finisher", "face_hit_finisher", "wood_bat_finisher", "guts_and_gore");
            bank.LightBlocks = Of("block_small", "block_medium");
            bank.HeavyBlocks = Of("block_large");
            bank.ShortWhooshes = Of("punch_short_whoosh", "kick_short_whoosh");
            bank.LongWhooshes = Of("punch_long_whoosh", "kick_long_whoosh");
            bank.SpecialImpacts = Of("fire_punch_0", "metal_punch_0", "blade_hit");
            bank.SignatureImpacts = Of("fire_punch_finisher", "metal_punch_finisher");
            bank.ThrowImpacts = Of("bone_breaking");
            bank.Acrobatics = Of("somersalt");
            int used = bank.LightHits.Length + bank.HeavyHits.Length + bank.FinisherHits.Length + bank.LightBlocks.Length + bank.HeavyBlocks.Length +
                       bank.ShortWhooshes.Length + bank.LongWhooshes.Length + bank.SpecialImpacts.Length + bank.SignatureImpacts.Length + bank.ThrowImpacts.Length + bank.Acrobatics.Length;
            Report.AppendLine($"Sons: {used} de {all.Count} usados no banco.");
            EditorUtility.SetDirty(bank);
            return bank;
        }

        // ---------------------------------------------------------------- CENÁRIO

        const float Ground = -1.45f;
        const float TableTop = 0.56f;

        static readonly (string prefab, Vector3 pos, float rotY)[] Scenery =
        {
            // Chão de areia da base.
            ("Tile", new Vector3(0f, Ground, 19f), 0f),
            ("Tile", new Vector3(-30f, Ground, 19f), 0f),
            ("Tile", new Vector3(30f, Ground, 19f), 0f),
            ("Tile", new Vector3(0f, Ground, 49f), 0f),
            ("Tile", new Vector3(-30f, Ground, 49f), 0f),
            ("Tile", new Vector3(30f, Ground, 49f), 0f),
            // Prédios ao fundo.
            ("Hangar2", new Vector3(0f, Ground, 44f), 0f),
            ("Hangar1", new Vector3(-34f, Ground, 38f), 0f),
            ("Hangar4", new Vector3(34f, Ground, 38f), 0f),
            ("Garage1", new Vector3(0f, Ground, 26f), 180f),
            ("Tower1", new Vector3(-18f, Ground, 15f), 0f),
            ("Tower1", new Vector3(18f, Ground, 15f), 180f),
            // Pilhas de contêineres atrás do público.
            ("Container1", new Vector3(-10.5f, Ground, 15.5f), 90f),
            ("Container3", new Vector3(10.5f, Ground, 15.5f), 90f),
            ("Container2", new Vector3(-11.5f, Ground + 3.6f, 15.5f), 90f),
            ("Container4", new Vector3(11.5f, Ground + 3.6f, 15.5f), 90f),
            ("Container3", new Vector3(-24f, Ground, 22f), 0f),
            ("Container1", new Vector3(24f, Ground, 24f), 0f),
            // Postes de luz.
            ("Lamp1", new Vector3(-15f, Ground, 13f), 0f),
            ("Lamp1", new Vector3(15f, Ground, 13f), 0f),
            // Barreiras com barris atrás do ringue.
            ("Block1", new Vector3(-3.2f, Ground, 4.05f), 90f),
            ("Block1", new Vector3(3.2f, Ground, 4.05f), 90f),
            ("Barrel1", new Vector3(-2.6f, Ground + 0.8f, 4.05f), 0f),
            ("Barrel2", new Vector3(-3.6f, Ground + 0.8f, 4.05f), 40f),
            ("Barrel3", new Vector3(2.6f, Ground + 0.8f, 4.05f), 15f),
            ("Barrel4", new Vector3(3.6f, Ground + 0.8f, 4.05f), 80f),
            // Mesas com armas: só decoração, o jogo continua sendo de luta sem armas.
            ("Block2", new Vector3(-6.2f, Ground, 3.9f), 90f),
            ("Block2", new Vector3(6.2f, Ground, 3.9f), 90f),
            ("AK74", new Vector3(-7.2f, TableTop, 3.75f), 90f),
            ("M4_8", new Vector3(-6.5f, TableTop, 4.05f), 90f),
            ("RPG7", new Vector3(-5.5f, TableTop, 3.8f), 90f),
            ("Uzi", new Vector3(-6.3f, TableTop, 3.6f), 90f),
            ("RGD-5", new Vector3(-4.95f, TableTop, 3.6f), 0f),
            ("M2_50cal", new Vector3(5.6f, TableTop, 3.9f), 90f),
            ("M107", new Vector3(7.05f, TableTop, 3.7f), 90f),
            ("Bennelli_M4", new Vector3(7.2f, TableTop, 4.1f), 90f),
            ("M249", new Vector3(6.3f, TableTop, 4.2f), 90f),
            ("M1911", new Vector3(6.35f, TableTop, 3.6f), 90f),
        };

        /// <summary>Coloca os objetos dos pacotes de cenário (com cópias URP dos materiais), apoiados pela base.</summary>
        public static void PlaceScenery(Transform parent)
        {
            var root = new GameObject("CenarioBaseMilitar").transform;
            root.SetParent(parent, false);
            var index = new Dictionary<string, string>();
            foreach (var folder in new[] { "Assets/Tiny Teacup Studio", "Assets/Low Poly Weapons VOL.1" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    index[Path.GetFileNameWithoutExtension(path)] = path;
                }
            }
            var items = new List<(string prefab, Vector3 pos, float rotY)>(Scenery);
            // Cerca contínua atrás dos contêineres, com a entrada aberta na garagem.
            for (float x = -28.5f; x <= 28.5f; x += 3f)
                if (Mathf.Abs(x) > 6.5f) items.Add(("Fence", new Vector3(x, Ground, 18f), 90f));
            foreach (var (name, pos, rotY) in items)
            {
                if (!index.TryGetValue(name, out var path)) continue;
                var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), root);
                go.name = name;
                go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
                go.transform.localPosition = Vector3.zero;
                if (name.StartsWith("Barrel")) go.transform.localScale *= 0.6f; // no pacote têm 1,6 m de altura
                StripComponents(go);
                ConvertRenderers(go);
                var rs = go.GetComponentsInChildren<Renderer>(false);
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
                }
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            }
        }

        // ---------------------------------------------------------------- EFEITOS

        static GameObject[] BuildVfxList()
        {
            const string Root = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/";
            GameObject Load(string name)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(Root + name + ".prefab");
                if (go == null) Report.AppendLine("EFEITO AUSENTE: " + name);
                return go;
            }
            int count = System.Enum.GetValues(typeof(VfxKind)).Length;
            var result = new GameObject[count];
            result[(int)VfxKind.Hit] = Load("vfx_Sparks_01");
            result[(int)VfxKind.HeavyHit] = Load("vfx_Impact_01");
            result[(int)VfxKind.Block] = Load("vfx_Shield_01");
            result[(int)VfxKind.Dust] = Load("vfx_Smoke_01");
            result[(int)VfxKind.Signature] = Load("vfx_Electricity_01");
            result[(int)VfxKind.Tech] = Load("vfx_Shockwave_01");
            result[(int)VfxKind.KO] = Load("vfx_Explosion_02");
            result[(int)VfxKind.Special] = Load("vfx_Explosion_01");
            result[(int)VfxKind.Lightning] = Load("vfx_Lightning_01");
            result[(int)VfxKind.Flames] = Load("vfx_Flames_01");
            result[(int)VfxKind.Tornado] = Load("vfx_Tornado_01");
            result[(int)VfxKind.Implosion] = Load("vfx_Implosion_01");
            result[(int)VfxKind.Heal] = Load("vfx_Heal_01");
            return result;
        }
    }
}
