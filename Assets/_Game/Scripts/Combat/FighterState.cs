using UnityEngine;

namespace UltimoRound.Combat
{
    /// <summary>Estado mutável de um lutador em combate. Uma instância por lado, mesmo em confronto espelhado.</summary>
    public sealed class FighterState
    {
        public readonly int Index;
        public readonly FighterDefinition Def;
        public readonly int PaletteIndex;
        public readonly CommandReader Commands;

        public Vector2 Pos;
        public Vector2 PrevPos;
        public Vector2 Vel;
        public int Facing = 1;
        public bool Airborne;

        public int Health;
        public int Energy;

        public FighterStateId State;
        public int StateTick;

        // Golpe em execução
        public MoveDefinition Move;
        public int MoveTick => StateTick;
        public int MoveFacing = 1;
        public uint HitMask;
        public bool MoveContact;
        public bool MoveHit;

        public int StunTicks;
        public bool Crouching;
        public bool AirAttackUsed;
        public int JumpDir;
        public int LandingTicks;

        // Combo sofrido
        public int ComboHits;
        public int ComboDamage;
        public int JuggleHits;
        public bool JuggleImmune;
        public bool BounceUsed;

        public float PushbackVel;
        public int PushbackTicks;

        public int ThrowProtection;
        public int InvulnTicks;
        public bool WasHitThisTick;

        public FighterState(int index, FighterDefinition def, int paletteIndex, CombatConfig cfg)
        {
            Index = index;
            Def = def;
            PaletteIndex = paletteIndex;
            Commands = new CommandReader(cfg.InputBufferTicks, cfg.DashWindowTicks);
        }

        public float HealthRatio => Def.MaxHealth > 0 ? Mathf.Clamp01((float)Health / Def.MaxHealth) : 0f;

        public bool IsActionable =>
            State == FighterStateId.Idle || State == FighterStateId.Walk ||
            State == FighterStateId.Crouch || State == FighterStateId.Guard;

        public bool InComboState =>
            State == FighterStateId.Hitstun || State == FighterStateId.Launched || State == FighterStateId.RopeBounce;

        public bool IsDown =>
            State == FighterStateId.Knockdown || State == FighterStateId.GetUp || State == FighterStateId.KO;

        public bool IsBlockingPosture => State == FighterStateId.Guard || State == FighterStateId.Blockstun;

        public void Enter(FighterStateId s)
        {
            State = s;
            StateTick = 0;
        }

        public float PushTop
        {
            get
            {
                if (IsDown) return Pos.y + 0.45f;
                if (Airborne) return Pos.y + Def.PushHeight * 0.85f;
                return Pos.y + (Crouching ? Def.CrouchPushHeight : Def.PushHeight);
            }
        }

        /// <summary>No ar as pernas recolhidas liberam a base do pushbox (permite cruzar por cima).</summary>
        public float PushBottom => Airborne ? Pos.y + 0.9f : Pos.y;

        public Box[] CurrentHurtboxes
        {
            get
            {
                if (Airborne) return Def.AirHurtboxes;
                return Crouching ? Def.CrouchHurtboxes : Def.StandHurtboxes;
            }
        }

        /// <summary>Hash simples do estado para comparar execuções (mesmo ambiente).</summary>
        public int StateHash()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + Mathf.RoundToInt(Pos.x * 1000f);
                h = h * 31 + Mathf.RoundToInt(Pos.y * 1000f);
                h = h * 31 + Health;
                h = h * 31 + Energy;
                h = h * 31 + (int)State;
                h = h * 31 + StateTick;
                h = h * 31 + Facing;
                h = h * 31 + ComboHits;
                return h;
            }
        }
    }
}
