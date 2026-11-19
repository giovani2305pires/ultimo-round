using System;
using UnityEngine;

namespace UltimoRound.Combat
{
    public enum GuardStyle
    {
        Kickboxer,
        Brawler,
        Agile,
        KickSpecialist,
    }

    [Serializable]
    public struct FighterPalette
    {
        public string Name;
        public Color Primary;
        public Color Secondary;
        public Color Skin;
        public Color Accent;
        public Color Hair;
    }

    [Serializable]
    public struct BodyShape
    {
        [Tooltip("Escala geral de altura")] public float Height;
        [Tooltip("Largura de ombros/tronco")] public float Bulk;
        [Tooltip("Espessura dos membros")] public float Limb;
        [Tooltip("Proporção de pernas")] public float LegLength;

        public static BodyShape Default => new BodyShape { Height = 1f, Bulk = 1f, Limb = 1f, LegLength = 1f };
    }

    [Serializable]
    public sealed class ComboRecipe
    {
        public string Name;
        public string Notation;
        [TextArea] public string Notes;
        public MoveSlot[] Sequence = new MoveSlot[0];
    }

    /// <summary>Dados imutáveis de um lutador. O estado de cada instância em luta fica em <see cref="FighterState"/>.</summary>
    [CreateAssetMenu(menuName = "Último Round/Lutador", fileName = "Fighter")]
    public sealed class FighterDefinition : ScriptableObject
    {
        [Header("Identidade")]
        public string Id = "fighter";
        public string DisplayName = "Lutador";
        public string Epithet = "";
        public string Style = "";
        [TextArea] public string Summary = "";
        [TextArea] public string Weakness = "";
        [Tooltip("Disponível na seleção de personagens")]
        public bool Selectable = true;
        [Range(1, 5)] public int StatReach = 3;
        [Range(1, 5)] public int StatSpeed = 3;
        [Range(1, 5)] public int StatPower = 3;

        [Header("Atributos")]
        public int MaxHealth = 1000;
        public float WalkSpeed = 2.6f;
        public float BackWalkSpeed = 2.0f;
        public float DashSpeed = 7.5f;
        public int DashTicks = 14;
        public float BackDashSpeed = 6.5f;
        public int BackDashTicks = 16;
        public int BackDashInvulnTicks = 5;
        public float JumpVelocity = 9.6f;
        public float JumpForwardSpeed = 3.0f;
        public float Gravity = 30f;
        public int PreJumpTicks = 3;
        public int LandingTicks = 3;

        [Header("Volumes (metros, relativos aos pés)")]
        public float PushHalfWidth = 0.32f;
        public float PushHeight = 1.7f;
        public float CrouchPushHeight = 1.1f;
        public Box[] StandHurtboxes = { new Box(0.05f, 0.85f, 0.6f, 1.7f), new Box(0.12f, 1.62f, 0.32f, 0.32f) };
        public Box[] CrouchHurtboxes = { new Box(0.05f, 0.55f, 0.7f, 1.1f) };
        public Box[] AirHurtboxes = { new Box(0f, 0.85f, 0.6f, 1.3f) };

        [Header("Golpes")]
        public MoveDefinition Punch;
        public MoveDefinition Kick;
        public MoveDefinition Heavy;
        public MoveDefinition CrouchPunch;
        public MoveDefinition Low;
        public MoveDefinition AntiAir;
        public MoveDefinition Air;
        public MoveDefinition Grab;
        public MoveDefinition SpecialNeutral;
        public MoveDefinition SpecialForward;
        public MoveDefinition Signature;
        [Tooltip("Opcional: Frente + Forte")]
        public MoveDefinition ForwardHeavy;

        public ComboRecipe[] Combos = new ComboRecipe[0];

        [Header("Apresentação")]
        public GuardStyle Guard = GuardStyle.Kickboxer;
        public BodyShape Body = BodyShape.Default;
        public FighterPalette[] Palettes = new FighterPalette[2];
        [Tooltip("Ritmo da animação de guarda (ciclos/s)")]
        public float IdleBounce = 1.6f;

        public MoveDefinition GetMove(MoveSlot slot)
        {
            switch (slot)
            {
                case MoveSlot.Punch: return Punch;
                case MoveSlot.Kick: return Kick;
                case MoveSlot.Heavy: return Heavy;
                case MoveSlot.CrouchPunch: return CrouchPunch;
                case MoveSlot.Low: return Low;
                case MoveSlot.AntiAir: return AntiAir;
                case MoveSlot.Air: return Air;
                case MoveSlot.Grab: return Grab;
                case MoveSlot.SpecialNeutral: return SpecialNeutral;
                case MoveSlot.SpecialForward: return SpecialForward;
                case MoveSlot.Signature: return Signature;
                case MoveSlot.ForwardHeavy: return ForwardHeavy;
                default: return null;
            }
        }

        public static readonly MoveSlot[] AllSlots =
        {
            MoveSlot.Punch, MoveSlot.Kick, MoveSlot.Heavy, MoveSlot.ForwardHeavy, MoveSlot.CrouchPunch, MoveSlot.Low,
            MoveSlot.AntiAir, MoveSlot.Air, MoveSlot.Grab, MoveSlot.SpecialNeutral, MoveSlot.SpecialForward, MoveSlot.Signature,
        };

        public FighterPalette GetPalette(int index)
        {
            if (Palettes == null || Palettes.Length == 0)
                return new FighterPalette { Primary = Color.gray, Secondary = Color.white, Skin = new Color(0.8f, 0.6f, 0.5f), Accent = Color.white, Hair = Color.black };
            return Palettes[Mathf.Clamp(index, 0, Palettes.Length - 1)];
        }
    }
}
