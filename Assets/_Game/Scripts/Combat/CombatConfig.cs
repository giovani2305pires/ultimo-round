using UnityEngine;

namespace UltimoRound.Combat
{
    /// <summary>Regras compartilhadas de combate e partida. Valores iniciais são hipóteses de balanceamento.</summary>
    [CreateAssetMenu(menuName = "Último Round/Config de Combate", fileName = "CombatConfig")]
    public sealed class CombatConfig : ScriptableObject
    {
        public const int TickRate = 60;
        public const float TickDt = 1f / TickRate;

        [Header("Entrada")]
        public int InputBufferTicks = 6;
        public int DashWindowTicks = 12;

        [Header("Arena")]
        [Tooltip("Limite lateral do centro do lutador (cordas)")]
        public float ArenaHalfWidth = 4.2f;
        [Tooltip("Distância máxima entre lutadores (enquadramento da câmera)")]
        public float MaxSeparation = 6.4f;
        public float StartDistance = 2.6f;

        [Header("Energia")]
        public int MaxEnergy = 100;
        [Range(0, 1)] public float BlockEnergyFactor = 0.5f;
        [Range(0, 1)] public float DefenderEnergyFactor = 0.35f;

        [Header("Combo")]
        [Tooltip("Hits sem redução de dano")] public int ScalingStartHit = 2;
        [Range(0, 1)] public float ScalingStep = 0.1f;
        [Range(0, 1)] public float ScalingFloor = 0.3f;
        public int MaxJuggleHits = 3;
        public int MaxComboHits = 12;
        [Tooltip("A partir deste hit o hitstun passa a diminuir")] public int HitstunDecayStart = 5;
        public int HitstunDecayPerHit = 1;
        public int MinHitstun = 8;
        [Range(0, 1)] public float SpecialChipFactor = 0.12f;

        [Header("Chão e agarrão")]
        public int KnockdownTicks = 40;
        public int GetUpTicks = 22;
        public int ThrowTechWindow = 10;
        public int ThrowHoldTicks = 26;
        public int ThrowTechRecovery = 16;
        public float ThrowTechSeparation = 1.3f;
        [Tooltip("Ticks sem poder ser agarrado após sair de stun/levantar")]
        public int ThrowProtectionTicks = 6;

        [Header("Rebote nas cordas")]
        public float RopeBounceOutSpeed = 12f;
        public float RopeBounceBackSpeed = 3.5f;
        public float RopeBounceUpSpeed = 6.5f;

        [Header("Assinatura")]
        public int SignatureFreezeTicks = 24;

        [Header("Partida")]
        public int RoundsToWin = 2;
        public int RoundSeconds = 60;
        public int PreRoundTicks = 110;
        public int RoundOverTicks = 170;
    }
}
