using System.Collections.Generic;
using UltimoRound.Audio;
using UltimoRound.Combat;
using UltimoRound.Presentation;
using UnityEngine;

namespace UltimoRound.Core
{
    /// <summary>Ponto único de referência aos dados do jogo (evita buscas por nome em runtime).</summary>
    [CreateAssetMenu(menuName = "Último Round/Banco de Dados", fileName = "GameDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        public CombatConfig Combat;
        public List<FighterDefinition> Fighters = new List<FighterDefinition>();

        [Header("Materiais base (URP)")]
        public Material LitMaterial;
        public Material TransparentMaterial;
        public Material ParticleMaterial;
        public Material UnlitMaterial;

        [Header("Assets de apresentação")]
        public List<FighterVisualSet> Visuals = new List<FighterVisualSet>();
        public SoundBank Sounds;
        [Tooltip("Efeitos por VfxKind (nulo = efeito simples por código)")]
        public GameObject[] VfxPrefabs = new GameObject[0];

        public FighterVisualSet FindVisuals(FighterDefinition f)
        {
            if (f == null) return null;
            for (int i = 0; i < Visuals.Count; i++)
                if (Visuals[i] != null && Visuals[i].FighterId == f.Id) return Visuals[i];
            return null;
        }

        /// <summary>Opções de visual selecionáveis (modelos, ou paletas se não houver modelos).</summary>
        public int VariantCount(FighterDefinition f)
        {
            var v = FindVisuals(f);
            if (v != null && v.Costumes.Length > 0) return v.Costumes.Length;
            return f != null && f.Palettes != null ? Mathf.Max(1, f.Palettes.Length) : 1;
        }

        public string VariantName(FighterDefinition f, int index)
        {
            var v = FindVisuals(f);
            if (v != null && v.Costumes.Length > 0) return v.Costume(index).Name;
            return f.GetPalette(index).Name;
        }

        public FighterDefinition FindFighter(string id)
        {
            for (int i = 0; i < Fighters.Count; i++)
                if (Fighters[i] != null && Fighters[i].Id == id) return Fighters[i];
            return null;
        }

        public int IndexOf(FighterDefinition f) => Fighters.IndexOf(f);
    }
}
