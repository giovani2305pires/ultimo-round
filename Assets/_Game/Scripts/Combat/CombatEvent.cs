using UnityEngine;

namespace UltimoRound.Combat
{
    public enum CombatEventType
    {
        MoveStart,
        Hit,
        Block,
        GrabStart,
        ThrowHit,
        ThrowTech,
        Knockdown,
        GetUp,
        KO,
        RopeBounce,
        Land,
        Jump,
        Dash,
        SignatureFlash,
        SignatureDenied,
        ComboEnd,
    }

    /// <summary>Resultado da simulação consumido pela apresentação (áudio, VFX, HUD). Não altera a simulação.</summary>
    public struct CombatEvent
    {
        public CombatEventType Type;
        public int Tick;
        public int Fighter;   // quem originou (atacante)
        public int Target;    // quem recebeu (-1 se n/a)
        public int Damage;
        public int ComboHits;
        public Vector2 Position;
        public MoveSlot Slot;
        public AttackLevel Level;
        public SfxKind Sfx;
        public bool Heavy;

        public override string ToString() => $"[{Tick}] {Type} f{Fighter}->{Target} dmg={Damage} combo={ComboHits} {Slot}";
    }
}
