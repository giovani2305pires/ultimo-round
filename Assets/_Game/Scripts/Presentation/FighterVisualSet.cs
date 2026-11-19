using System;
using UltimoRound.Combat;
using UnityEngine;

namespace UltimoRound.Presentation
{
    [Serializable]
    public sealed class FighterCostume
    {
        public string Name;
        [Tooltip("Prefab gerado: modelo Humanoid, materiais URP e altura normalizada")]
        public GameObject Prefab;
    }

    [Serializable]
    public sealed class MoveAnimation
    {
        public MoveSlot Slot;
        public AnimationClip Clip;
        [Tooltip("Segundo clipe para alternar em golpes multi-hit (opcional)")]
        public AnimationClip AltClip;
        [Tooltip("Momento do impacto no clipe (s), alinhado ao primeiro tick ativo")]
        public float StrikeTime;
        public float AltStrikeTime;
        [Range(0, 1)] public float Crouch;
        [Range(0, 1)] public float Tuck;
    }

    /// <summary>
    /// Modelos e clipes de um lutador. Só apresentação: frame data, volumes e dano continuam na simulação.
    /// </summary>
    [CreateAssetMenu(menuName = "Último Round/Visual do Lutador", fileName = "Visual")]
    public sealed class FighterVisualSet : ScriptableObject
    {
        public string FighterId;
        public FighterCostume[] Costumes = new FighterCostume[0];

        [Header("Estados")]
        public AnimationClip Idle;
        public AnimationClip WalkForward;
        public AnimationClip WalkBack;
        public AnimationClip HitHigh;
        public AnimationClip HitBody;
        public AnimationClip HitLow;
        public AnimationClip BlockHigh;
        public AnimationClip BlockBody;
        public AnimationClip BlockLow;
        public AnimationClip Knockdown;
        public AnimationClip KnockdownLoop;
        public AnimationClip GetUp;
        public AnimationClip KO;
        public AnimationClip Victory;
        public AnimationClip Intro;
        public AnimationClip JumpRise;
        public AnimationClip JumpFall;
        public AnimationClip DashForward;
        public AnimationClip DashBack;

        public MoveAnimation[] Moves = new MoveAnimation[0];

        public MoveAnimation Find(MoveSlot slot)
        {
            for (int i = 0; i < Moves.Length; i++)
                if (Moves[i].Slot == slot) return Moves[i];
            return null;
        }

        public FighterCostume Costume(int index)
        {
            if (Costumes == null || Costumes.Length == 0) return null;
            return Costumes[Mathf.Abs(index) % Costumes.Length];
        }
    }
}
