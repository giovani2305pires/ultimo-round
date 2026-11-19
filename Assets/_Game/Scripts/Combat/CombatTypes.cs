using System;
using UnityEngine;

namespace UltimoRound.Combat
{
    /// <summary>Botões lógicos de combate. Independentes de dispositivo.</summary>
    [Flags]
    public enum Buttons : ushort
    {
        None = 0,
        Punch = 1 << 0,
        Kick = 1 << 1,
        Heavy = 1 << 2,
        Block = 1 << 3,
        Special = 1 << 4,
        Grab = 1 << 5,
        Signature = 1 << 6,
    }

    /// <summary>
    /// Comando de um tick. Direção em coordenadas absolutas da arena (X: -1 esquerda, +1 direita; Y: -1 baixo, +1 cima).
    /// <see cref="Pressed"/> contém os botões pressionados desde o tick anterior (acumulados entre quadros de renderização).
    /// </summary>
    [Serializable]
    public struct InputFrame : IEquatable<InputFrame>
    {
        public sbyte X;
        public sbyte Y;
        public Buttons Held;
        public Buttons Pressed;

        public static readonly InputFrame Neutral = default;

        public InputFrame(int x, int y, Buttons held, Buttons pressed)
        {
            X = (sbyte)Mathf.Clamp(x, -1, 1);
            Y = (sbyte)Mathf.Clamp(y, -1, 1);
            Held = held;
            Pressed = pressed;
        }

        public bool IsHeld(Buttons b) => (Held & b) != 0;
        public bool WasPressed(Buttons b) => (Pressed & b) != 0;

        public bool Equals(InputFrame o) => X == o.X && Y == o.Y && Held == o.Held && Pressed == o.Pressed;
        public override bool Equals(object obj) => obj is InputFrame f && Equals(f);
        public override int GetHashCode() => (X + 2) | ((Y + 2) << 3) | ((int)Held << 6) | ((int)Pressed << 16);
    }

    /// <summary>Retângulo em coordenadas de combate (metros). Offset relativo aos pés do lutador; X positivo = frente.</summary>
    [Serializable]
    public struct Box
    {
        public float X;
        public float Y;
        public float W;
        public float H;

        public Box(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }

        public bool IsEmpty => W <= 0f || H <= 0f;

        /// <summary>Converte para coordenadas do mundo de combate, espelhando no eixo X conforme a orientação.</summary>
        public Rect ToWorld(Vector2 origin, int facing)
        {
            float cx = origin.x + X * facing;
            float cy = origin.y + Y;
            return new Rect(cx - W * 0.5f, cy - H * 0.5f, W, H);
        }
    }

    public enum AttackLevel
    {
        High,      // defensável em pé/agachado; pode passar sobre agachado pelos volumes
        Mid,       // defensável em pé ou agachado
        Low,       // exige defesa agachada
        Overhead,  // exige defesa em pé
        Throw,     // agarrão: indefensável, sujeito a escape
    }

    public enum MoveSlot
    {
        None = 0,
        Punch,
        Kick,
        Heavy,
        CrouchPunch,
        Low,
        AntiAir,
        Air,
        Grab,
        SpecialNeutral,
        SpecialForward,
        Signature,
        ForwardHeavy,
    }

    public enum KnockdownType
    {
        None,       // reação em pé (hitstun)
        Knockdown,  // derruba
        Sweep,      // rasteira: derruba no lugar
        Launch,     // lança para o ar (permite juggle limitado)
    }

    [Flags]
    public enum InvulnType
    {
        None = 0,
        Air = 1 << 0,    // ignora ataques aéreos (antiaéreo)
        Strike = 1 << 1, // ignora golpes
        Throw = 1 << 2,  // ignora agarrões
    }

    public enum FighterStateId
    {
        Idle,
        Walk,
        Crouch,
        Guard,
        Dash,
        BackDash,
        PreJump,
        Air,
        Landing,
        Attack,
        Blockstun,
        Hitstun,
        Launched,
        RopeBounce,
        Grabbing,
        Grabbed,
        ThrowTech,
        Knockdown,
        GetUp,
        KO,
        Victory,
    }

    /// <summary>Chave de pose para a apresentação procedural. Não interfere na simulação.</summary>
    public enum AnimPose
    {
        Jab,
        Straight,
        Hook,
        Uppercut,
        BodyKick,
        HighKick,
        LowKick,
        Sweep,
        CrouchJab,
        AirKick,
        AirPunch,
        Grab,
        DashPunch,
        SpinKick,
        Headbutt,
        Stomp,
        Shoulder,
        Flurry,
        Axe,
        Signature,
        Overhead,
    }

    public enum SfxKind
    {
        LightHit,
        HeavyHit,
        Block,
        Grab,
        Dash,
        Special,
        KO,
        Whoosh,
        Bell,
        Bounce,
        Tech,
        Signature,
        Land,
        Jump,
        // Novos valores sempre no fim: os golpes salvos guardam o número do enum.
        Finisher,
        HeavyBlock,
        SpecialImpact,
        SignatureImpact,
        ThrowImpact,
    }
}
