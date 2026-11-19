using UnityEngine;

namespace UltimoRound.Combat
{
    /// <summary>
    /// Dados imutáveis de um golpe. Ticks a 60 Hz.
    /// Convenção: tick 0..startup-1 = preparação; startup..startup+active-1 = ativo; depois recuperação.
    /// O "primeiro quadro ativo" na notação usual corresponde a startup+1.
    /// </summary>
    [CreateAssetMenu(menuName = "Último Round/Golpe", fileName = "Move")]
    public sealed class MoveDefinition : ScriptableObject
    {
        [Header("Identidade")]
        public string Id = "move";
        public string DisplayName = "Golpe";
        public MoveSlot Slot = MoveSlot.Punch;
        [Tooltip("Notação exibida na lista de golpes")]
        public string Notation = "Soco";
        [TextArea] public string Description = "";

        [Header("Acerto")]
        public AttackLevel Level = AttackLevel.Mid;
        public int Damage = 40;
        [Tooltip("Ticks antes do primeiro tick ativo")]
        public int Startup = 5;
        public int Active = 3;
        public int Recovery = 10;
        public int Hitstun = 16;
        public int Blockstun = 11;
        public int Hitstop = 7;
        public Box Hitbox = new Box(0.7f, 1.35f, 0.6f, 0.3f);
        [Tooltip("Hits múltiplos explícitos: cada hit tem índice próprio e só se aplica uma vez")]
        public int Hits = 1;
        public int HitInterval = 0;
        public float PushbackHit = 0.6f;
        public float PushbackBlock = 0.8f;
        public KnockdownType Knockdown = KnockdownType.None;
        public Vector2 LaunchVelocity = new Vector2(2.5f, 7f);
        [Tooltip("Golpe elegível para rebote nas cordas (máx. 1 por combo)")]
        public bool RopeBounce;
        [Tooltip("Durante ativo/recuperação o membro estendido também é hurtbox (permite punir golpes errados)")]
        public bool ExtendHurtbox = true;

        [Header("Energia")]
        public int EnergyCost;
        public int EnergyGain = 6;

        [Header("Cancelamentos")]
        [Tooltip("Golpes para os quais este pode ser cancelado após contato (acerto ou defesa)")]
        public MoveSlot[] CancelInto = new MoveSlot[0];
        public bool CancelOnBlock = true;
        [Tooltip("Ticks após o fim da janela ativa em que o cancelamento ainda é aceito")]
        public int CancelWindowExtra = 6;

        [Header("Movimento do golpe")]
        public float ForwardVelocity;
        public int MoveStartTick;
        public int MoveEndTick;
        public bool RequiresAir;
        [Tooltip("Ticks extras de aterrissagem se for golpe aéreo")]
        public int LandingRecovery = 4;

        [Header("Invulnerabilidade")]
        public InvulnType Invuln = InvulnType.None;
        public int InvulnStart;
        public int InvulnEnd;

        [Header("Agarrão")]
        [Tooltip("Alcance horizontal entre bordas dos pushboxes")]
        public float ThrowRange = 0.45f;
        public bool ThrowSwapsSides;

        [Header("Apresentação")]
        public AnimPose Pose = AnimPose.Jab;
        public SfxKind HitSfx = SfxKind.LightHit;
        public bool IsSpecial;

        public int TotalTicks => Startup + Active + Recovery;
        public bool IsThrow => Level == AttackLevel.Throw;

        public bool IsActiveTick(int moveTick) => moveTick >= Startup && moveTick < Startup + Active;

        /// <summary>Índice do hit atual (multi-hit) ou -1 se não estiver ativo.</summary>
        public int HitIndexAt(int moveTick)
        {
            if (!IsActiveTick(moveTick)) return -1;
            if (Hits <= 1 || HitInterval <= 0) return 0;
            int idx = (moveTick - Startup) / HitInterval;
            return idx < Hits ? idx : -1;
        }

        public bool IsInvulnerable(int moveTick, InvulnType kind) =>
            (Invuln & kind) != 0 && moveTick >= InvulnStart && moveTick <= InvulnEnd;

        public bool CanCancelInto(MoveSlot slot)
        {
            if (CancelInto == null) return false;
            for (int i = 0; i < CancelInto.Length; i++)
                if (CancelInto[i] == slot) return true;
            return false;
        }

        public string LevelLabel()
        {
            switch (Level)
            {
                case AttackLevel.High: return "Alto";
                case AttackLevel.Mid: return "Médio";
                case AttackLevel.Low: return "Baixo";
                case AttackLevel.Overhead: return "Overhead";
                default: return "Agarrão";
            }
        }
    }
}
