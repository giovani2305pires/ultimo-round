using System.IO;
using UltimoRound.Combat;
using UltimoRound.Core;
using UltimoRound.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltimoRound.EditorTools
{
    /// <summary>
    /// Gera os ScriptableObjects de dados a partir de <see cref="DefaultRoster"/>.
    /// "Criar ausentes" nunca sobrescreve ajustes manuais; "Regenerar" copia os valores padrão
    /// sobre os assets existentes preservando GUIDs (referências continuam válidas).
    /// </summary>
    public static class GameDataGenerator
    {
        public const string DataRoot = "Assets/_Game/Data";
        public const string DatabasePath = DataRoot + "/GameDatabase.asset";
        public const string ConfigPath = DataRoot + "/CombatConfig.asset";
        public const string MaterialsRoot = "Assets/_Game/Art/Materials";

        [MenuItem("Último Round/Dados/Criar dados ausentes")]
        public static void CreateMissing() => Generate(false);

        [MenuItem("Último Round/Dados/Regenerar dados padrão (sobrescreve)")]
        public static void RegenerateAll()
        {
            if (Application.isBatchMode || EditorUtility.DisplayDialog("Regenerar dados",
                    "Sobrescrever golpes, lutadores e configuração com os valores padrão do código?", "Sobrescrever", "Cancelar"))
                Generate(true);
        }

        public static GameDatabase Generate(bool overwrite)
        {
            Directory.CreateDirectory(DataRoot + "/Fighters");
            Directory.CreateDirectory(DataRoot + "/Moves");
            Directory.CreateDirectory(MaterialsRoot);

            var config = SaveOrUpdate(DefaultRoster.CreateConfig(), ConfigPath, overwrite);
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            db.Combat = config;
            EnsureMaterials(db);

            var fighters = DefaultRoster.CreateAll();
            for (int i = 0; i < fighters.Count; i++)
            {
                var src = fighters[i];
                string fighterPath = $"{DataRoot}/Fighters/{src.Id}.asset";
                bool fighterExists = AssetDatabase.LoadAssetAtPath<FighterDefinition>(fighterPath) != null;
                if (fighterExists && !overwrite)
                {
                    var existing = AssetDatabase.LoadAssetAtPath<FighterDefinition>(fighterPath);
                    if (!db.Fighters.Contains(existing)) db.Fighters.Add(existing);
                    continue;
                }

                Directory.CreateDirectory($"{DataRoot}/Moves/{src.Id}");
                foreach (var slot in FighterDefinition.AllSlots)
                {
                    var m = src.GetMove(slot);
                    if (m == null) continue;
                    var saved = SaveOrUpdate(m, $"{DataRoot}/Moves/{src.Id}/{m.Id}.asset", true);
                    Assign(src, slot, saved);
                }
                var fighter = SaveOrUpdate(src, fighterPath, true);
                if (!db.Fighters.Contains(fighter)) db.Fighters.Add(fighter);
            }

            // Ordem do elenco segue DefaultRoster.FighterIds.
            db.Fighters.RemoveAll(f => f == null);
            db.Fighters.Sort((a, b) => System.Array.IndexOf(DefaultRoster.FighterIds, a.Id).CompareTo(System.Array.IndexOf(DefaultRoster.FighterIds, b.Id)));
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Dados] GameDatabase com {db.Fighters.Count} lutadores (sobrescrever={overwrite}).");
            return db;
        }

        static void Assign(FighterDefinition f, MoveSlot slot, MoveDefinition m)
        {
            switch (slot)
            {
                case MoveSlot.Punch: f.Punch = m; break;
                case MoveSlot.Kick: f.Kick = m; break;
                case MoveSlot.Heavy: f.Heavy = m; break;
                case MoveSlot.CrouchPunch: f.CrouchPunch = m; break;
                case MoveSlot.Low: f.Low = m; break;
                case MoveSlot.AntiAir: f.AntiAir = m; break;
                case MoveSlot.Air: f.Air = m; break;
                case MoveSlot.Grab: f.Grab = m; break;
                case MoveSlot.SpecialNeutral: f.SpecialNeutral = m; break;
                case MoveSlot.SpecialForward: f.SpecialForward = m; break;
                case MoveSlot.Signature: f.Signature = m; break;
                case MoveSlot.ForwardHeavy: f.ForwardHeavy = m; break;
            }
        }

        static T SaveOrUpdate<T>(T fresh, string path, bool overwrite) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            if (overwrite)
            {
                string keepName = existing.name;
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = keepName;
                EditorUtility.SetDirty(existing);
            }
            return existing;
        }

        static void EnsureMaterials(GameDatabase db)
        {
            db.LitMaterial = EnsureMaterial("M_Lit", "Universal Render Pipeline/Lit", m =>
            {
                m.SetFloat("_Smoothness", 0.35f);
            });
            db.TransparentMaterial = EnsureMaterial("M_Transparent", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetColor("_BaseColor", new Color(1, 1, 1, 0.3f));
            });
            db.ParticleMaterial = EnsureMaterial("M_Particle", "Universal Render Pipeline/Particles/Unlit", m =>
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f); // aditivo
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.One);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.EnableKeyword("_BLENDMODE_ADD");
                m.renderQueue = (int)RenderQueue.Transparent;
            });
            db.UnlitMaterial = EnsureMaterial("M_Unlit", "Universal Render Pipeline/Unlit", null);
        }

        static Material EnsureMaterial(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{MaterialsRoot}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[Dados] Shader não encontrado: " + shaderName);
                return null;
            }
            mat = new Material(shader) { name = name };
            setup?.Invoke(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
