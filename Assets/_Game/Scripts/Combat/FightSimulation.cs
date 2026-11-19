using System.Collections.Generic;
using UnityEngine;

namespace UltimoRound.Combat
{
    /// <summary>
    /// Simulação de combate 1x1 em passo fixo (60 ticks/s). Não depende de Time, Animator, física da Unity nem de dispositivos.
    /// Ordem de um tick: entrada → (congelamento/hitstop) → orientação → agarrões → estados → integração → pushbox/limites
    /// → golpes (avaliados simultaneamente) → agarrões novos → fim de combos.
    /// </summary>
    public sealed class FightSimulation
    {
        public readonly CombatConfig Config;
        public readonly FighterState[] Fighters = new FighterState[2];
        public readonly List<CombatEvent> Events = new List<CombatEvent>(64);

        public int Tick { get; private set; }
        public int Hitstop { get; private set; }
        public int FreezeTicks { get; private set; }
        public int FreezeOwner { get; private set; } = -1;

        /// <summary>Impede início de novos golpes (fim de round, apresentação).</summary>
        public bool AttacksLocked;
        /// <summary>Impede aplicação de dano (após vencedor determinado).</summary>
        public bool DamageLocked;
        /// <summary>Treino com vida infinita: o dano nunca zera a vida (sem KO).</summary>
        public bool PreventKO;

        int ClampDamage(FighterState d, int dmg) => PreventKO ? Mathf.Min(dmg, Mathf.Max(0, d.Health - 1)) : dmg;

        const int PushbackDuration = 8;

        struct StrikeResult
        {
            public bool Valid;
            public bool Blocked;
            public int HitIndex;
        }

        public FightSimulation(CombatConfig config, FighterDefinition a, FighterDefinition b, int paletteA = 0, int paletteB = 1)
        {
            Config = config;
            Fighters[0] = new FighterState(0, a, paletteA, config);
            Fighters[1] = new FighterState(1, b, paletteB, config);
            ResetRound();
        }

        public FighterState this[int i] => Fighters[i];

        /// <summary>Rede local: copia na réplica do convidado o estado decidido pelo anfitrião.</summary>
        public void NetRestore(int tick, int hitstop, int freezeTicks, int freezeOwner, bool attacksLocked, bool damageLocked)
        {
            Tick = tick;
            Hitstop = hitstop;
            FreezeTicks = freezeTicks;
            FreezeOwner = freezeOwner;
            AttacksLocked = attacksLocked;
            DamageLocked = damageLocked;
        }

        /// <summary>Reinicia posições, vida, energia, estados, buffers e flags transitórias.</summary>
        public void ResetRound()
        {
            float half = Config.StartDistance * 0.5f;
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                f.Pos = new Vector2(i == 0 ? -half : half, 0f);
                f.PrevPos = f.Pos;
                f.Vel = Vector2.zero;
                f.Facing = i == 0 ? 1 : -1;
                f.MoveFacing = f.Facing;
                f.Airborne = false;
                f.Health = f.Def.MaxHealth;
                f.Energy = 0;
                f.Move = null;
                f.HitMask = 0;
                f.MoveContact = f.MoveHit = false;
                f.StunTicks = 0;
                f.Crouching = false;
                f.AirAttackUsed = false;
                f.JumpDir = 0;
                f.LandingTicks = 0;
                ResetCombo(f);
                f.PushbackVel = 0;
                f.PushbackTicks = 0;
                f.ThrowProtection = 0;
                f.InvulnTicks = 0;
                f.WasHitThisTick = false;
                f.Commands.Clear();
                f.Enter(FighterStateId.Idle);
            }
            Hitstop = 0;
            FreezeTicks = 0;
            FreezeOwner = -1;
            AttacksLocked = false;
            DamageLocked = false;
        }

        public void ClearInputBuffers()
        {
            Fighters[0].Commands.Clear();
            Fighters[1].Commands.Clear();
        }

        static void ResetCombo(FighterState f)
        {
            f.ComboHits = 0;
            f.ComboDamage = 0;
            f.JuggleHits = 0;
            f.JuggleImmune = false;
            f.BounceUsed = false;
        }

        void Emit(CombatEventType type, FighterState from, FighterState to, int damage = 0, MoveDefinition move = null)
        {
            Events.Add(new CombatEvent
            {
                Type = type,
                Tick = Tick,
                Fighter = from != null ? from.Index : -1,
                Target = to != null ? to.Index : -1,
                Damage = damage,
                ComboHits = to != null ? to.ComboHits : 0,
                Position = to != null ? to.Pos : (from != null ? from.Pos : Vector2.zero),
                Slot = move != null ? move.Slot : MoveSlot.None,
                Level = move != null ? move.Level : AttackLevel.Mid,
                Sfx = move != null ? move.HitSfx : SfxKind.LightHit,
                Heavy = move != null && (move.Damage >= 80 || move.Knockdown != KnockdownType.None),
            });
        }

        // ------------------------------------------------------------------ TICK

        public void Step(in InputFrame in0, in InputFrame in1)
        {
            Tick++;
            bool frozen = Hitstop > 0 || FreezeTicks > 0;
            Fighters[0].Commands.Feed(in0, Fighters[0].Facing, frozen);
            Fighters[1].Commands.Feed(in1, Fighters[1].Facing, frozen);

            if (FreezeTicks > 0)
            {
                FreezeTicks--;
                if (FreezeTicks == 0) FreezeOwner = -1;
                return;
            }
            if (Hitstop > 0)
            {
                Hitstop--;
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                f.PrevPos = f.Pos;
                f.WasHitThisTick = false;
                f.StateTick++;
                if (f.ThrowProtection > 0) f.ThrowProtection--;
                if (f.InvulnTicks > 0) f.InvulnTicks--;
            }

            UpdateFacing(Fighters[0], Fighters[1]);
            UpdateFacing(Fighters[1], Fighters[0]);

            UpdateGrabPair(Fighters[0], Fighters[1]);
            UpdateGrabPair(Fighters[1], Fighters[0]);

            UpdateFighter(Fighters[0], Fighters[1]);
            UpdateFighter(Fighters[1], Fighters[0]);

            Integrate(Fighters[0]);
            Integrate(Fighters[1]);

            ResolvePush();
            ResolveStrikes();
            ResolveThrows();
            CheckComboEnds();
        }

        // ------------------------------------------------------------------ ORIENTAÇÃO

        static bool CanTurn(FighterState f) =>
            !f.Airborne && (f.IsActionable || f.State == FighterStateId.Landing || f.State == FighterStateId.PreJump ||
                            (f.State == FighterStateId.GetUp && f.StateTick == 1));

        void UpdateFacing(FighterState f, FighterState o)
        {
            if (!CanTurn(f)) return;
            float dx = o.Pos.x - f.Pos.x;
            if (Mathf.Abs(dx) > 0.02f) f.Facing = dx > 0 ? 1 : -1;
        }

        // ------------------------------------------------------------------ ESTADOS

        void UpdateFighter(FighterState f, FighterState o)
        {
            var cfg = Config;
            var def = f.Def;
            switch (f.State)
            {
                case FighterStateId.Idle:
                case FighterStateId.Walk:
                case FighterStateId.Crouch:
                case FighterStateId.Guard:
                    HandleNeutral(f);
                    break;

                case FighterStateId.Dash:
                {
                    float t = f.StateTick / (float)Mathf.Max(1, def.DashTicks);
                    f.Vel.x = f.Facing * def.DashSpeed * (1f - 0.6f * t);
                    if (f.StateTick >= def.DashTicks) ToNeutral(f);
                    break;
                }
                case FighterStateId.BackDash:
                {
                    float t = f.StateTick / (float)Mathf.Max(1, def.BackDashTicks);
                    f.Vel.x = -f.Facing * def.BackDashSpeed * (1f - 0.6f * t);
                    if (f.StateTick >= def.BackDashTicks) ToNeutral(f);
                    break;
                }
                case FighterStateId.PreJump:
                    f.Vel.x = 0;
                    if (f.StateTick >= def.PreJumpTicks)
                    {
                        f.Airborne = true;
                        f.Crouching = false;
                        f.AirAttackUsed = false;
                        f.Vel = new Vector2(f.JumpDir * def.JumpForwardSpeed, def.JumpVelocity);
                        f.Enter(FighterStateId.Air);
                        Emit(CombatEventType.Jump, f, null);
                    }
                    break;

                case FighterStateId.Air:
                    if (!AttacksLocked && !f.AirAttackUsed && f.Pos.y > 0.25f)
                    {
                        var c = f.Commands;
                        Buttons b = c.Buffered(Buttons.Heavy) ? Buttons.Heavy :
                                    c.Buffered(Buttons.Kick) ? Buttons.Kick :
                                    c.Buffered(Buttons.Punch) ? Buttons.Punch : Buttons.None;
                        if (b != Buttons.None && def.Air != null)
                        {
                            c.Consume(b);
                            f.AirAttackUsed = true;
                            StartMove(f, def.Air);
                        }
                    }
                    break;

                case FighterStateId.Landing:
                    f.Vel.x = 0;
                    if (f.StateTick >= f.LandingTicks) ToNeutral(f);
                    break;

                case FighterStateId.Attack:
                    UpdateAttack(f);
                    break;

                case FighterStateId.Blockstun:
                    f.Crouching = f.Commands.Current.Y < 0;
                    if (f.StateTick >= f.StunTicks)
                    {
                        f.ThrowProtection = cfg.ThrowProtectionTicks;
                        ToNeutral(f);
                        HandleNeutral(f);
                    }
                    break;

                case FighterStateId.Hitstun:
                    if (f.StateTick >= f.StunTicks)
                    {
                        f.ThrowProtection = cfg.ThrowProtectionTicks;
                        ToNeutral(f);
                    }
                    break;

                case FighterStateId.ThrowTech:
                    if (f.StateTick >= cfg.ThrowTechRecovery) ToNeutral(f);
                    break;

                case FighterStateId.Knockdown:
                    f.Vel.x = 0;
                    if (f.StateTick >= cfg.KnockdownTicks)
                    {
                        f.Enter(FighterStateId.GetUp);
                        Emit(CombatEventType.GetUp, f, null);
                    }
                    break;

                case FighterStateId.GetUp:
                    if (f.StateTick >= cfg.GetUpTicks)
                    {
                        f.ThrowProtection = cfg.ThrowProtectionTicks;
                        ToNeutral(f);
                    }
                    break;

                case FighterStateId.Launched:
                case FighterStateId.RopeBounce:
                case FighterStateId.Grabbing:
                case FighterStateId.Grabbed:
                case FighterStateId.KO:
                case FighterStateId.Victory:
                    break;
            }
        }

        void ToNeutral(FighterState f)
        {
            f.Move = null;
            f.Vel.x = 0;
            var input = f.Commands.Current;
            if (input.IsHeld(Buttons.Block) && !AttacksLocked)
            {
                f.Crouching = input.Y < 0;
                f.Enter(FighterStateId.Guard);
            }
            else if (input.Y < 0)
            {
                f.Crouching = true;
                f.Enter(FighterStateId.Crouch);
            }
            else
            {
                f.Crouching = false;
                f.Enter(FighterStateId.Idle);
            }
        }

        void SetNeutralState(FighterState f, FighterStateId s)
        {
            if (f.State != s) f.Enter(s);
        }

        void HandleNeutral(FighterState f)
        {
            var def = f.Def;
            var c = f.Commands;
            var input = c.Current;
            int relX = c.RelX;

            if (AttacksLocked)
            {
                f.Vel.x = 0;
                f.Crouching = false;
                SetNeutralState(f, FighterStateId.Idle);
                return;
            }

            if (TryStartFromBuffer(f, null)) return;

            if (c.DashForward && input.Y == 0)
            {
                c.ConsumeDash();
                f.Crouching = false;
                f.Enter(FighterStateId.Dash);
                Emit(CombatEventType.Dash, f, null);
                return;
            }
            if (c.DashBack && input.Y == 0)
            {
                c.ConsumeDash();
                f.Crouching = false;
                f.InvulnTicks = def.BackDashInvulnTicks;
                f.Enter(FighterStateId.BackDash);
                Emit(CombatEventType.Dash, f, null);
                return;
            }

            if (input.Y > 0)
            {
                f.JumpDir = input.X;
                f.Crouching = false;
                f.Vel.x = 0;
                f.Enter(FighterStateId.PreJump);
                return;
            }

            if (input.IsHeld(Buttons.Block))
            {
                f.Vel.x = 0;
                f.Crouching = input.Y < 0;
                SetNeutralState(f, FighterStateId.Guard);
                return;
            }

            if (input.Y < 0)
            {
                f.Vel.x = 0;
                f.Crouching = true;
                SetNeutralState(f, FighterStateId.Crouch);
                return;
            }

            f.Crouching = false;
            if (relX > 0)
            {
                f.Vel.x = f.Facing * def.WalkSpeed;
                SetNeutralState(f, FighterStateId.Walk);
            }
            else if (relX < 0)
            {
                f.Vel.x = -f.Facing * def.BackWalkSpeed;
                SetNeutralState(f, FighterStateId.Walk);
            }
            else
            {
                f.Vel.x = 0;
                SetNeutralState(f, FighterStateId.Idle);
            }
        }

        /// <summary>
        /// Escolhe o golpe a partir do buffer. Prioridade para comandos simultâneos:
        /// Assinatura &gt; Especial &gt; Agarrão &gt; Forte &gt; Chute &gt; Soco.
        /// Se <paramref name="cancelFrom"/> não for nulo, só aceita golpes listados no cancelamento.
        /// </summary>
        bool TryStartFromBuffer(FighterState f, MoveDefinition cancelFrom)
        {
            var def = f.Def;
            var c = f.Commands;
            // A direção considerada é a do momento do aperto (intenção do jogador), não a do tick de execução.

            if (c.Buffered(Buttons.Signature))
            {
                var m = def.Signature;
                if (m != null && Allowed(cancelFrom, m))
                {
                    c.Consume(Buttons.Signature);
                    if (f.Energy >= Config.MaxEnergy) return StartMove(f, m);
                    Emit(CombatEventType.SignatureDenied, f, null);
                    if (cancelFrom == null) return false;
                }
            }
            if (c.Buffered(Buttons.Special))
            {
                var m = c.PressRelX(Buttons.Special) > 0 ? def.SpecialForward : def.SpecialNeutral;
                if (m != null && Allowed(cancelFrom, m) && f.Energy >= m.EnergyCost)
                {
                    c.Consume(Buttons.Special);
                    return StartMove(f, m);
                }
            }
            if (c.Buffered(Buttons.Grab) && cancelFrom == null)
            {
                var m = def.Grab;
                if (m != null)
                {
                    c.Consume(Buttons.Grab);
                    return StartMove(f, m);
                }
            }
            if (c.Buffered(Buttons.Heavy))
            {
                var m = c.PressY(Buttons.Heavy) < 0 ? def.AntiAir
                    : (c.PressRelX(Buttons.Heavy) > 0 && def.ForwardHeavy != null ? def.ForwardHeavy : def.Heavy);
                if (m != null && Allowed(cancelFrom, m))
                {
                    c.Consume(Buttons.Heavy);
                    return StartMove(f, m);
                }
            }
            if (c.Buffered(Buttons.Kick))
            {
                var m = c.PressY(Buttons.Kick) < 0 ? def.Low : def.Kick;
                if (m != null && Allowed(cancelFrom, m))
                {
                    c.Consume(Buttons.Kick);
                    return StartMove(f, m);
                }
            }
            if (c.Buffered(Buttons.Punch))
            {
                var m = c.PressY(Buttons.Punch) < 0 ? def.CrouchPunch : def.Punch;
                if (m != null && Allowed(cancelFrom, m))
                {
                    c.Consume(Buttons.Punch);
                    return StartMove(f, m);
                }
            }
            return false;
        }

        static bool Allowed(MoveDefinition from, MoveDefinition to) => from == null || from.CanCancelInto(to.Slot);

        bool StartMove(FighterState f, MoveDefinition m)
        {
            if (m == null || AttacksLocked) return false;
            if (m.Slot == MoveSlot.Signature)
            {
                if (f.Energy < Config.MaxEnergy) return false;
                f.Energy = 0;
            }
            else if (m.EnergyCost > 0)
            {
                if (f.Energy < m.EnergyCost) return false;
                f.Energy -= m.EnergyCost;
            }

            f.Move = m;
            f.MoveFacing = f.Facing;
            f.HitMask = 0;
            f.MoveContact = false;
            f.MoveHit = false;
            f.Crouching = m.Slot == MoveSlot.CrouchPunch || m.Slot == MoveSlot.Low;
            if (!f.Airborne) f.Vel.x = 0;
            f.Enter(FighterStateId.Attack);
            Emit(CombatEventType.MoveStart, f, null, 0, m);
            if (m.Slot == MoveSlot.Signature)
            {
                FreezeTicks = Config.SignatureFreezeTicks;
                FreezeOwner = f.Index;
                Emit(CombatEventType.SignatureFlash, f, null, 0, m);
            }
            return true;
        }

        void UpdateAttack(FighterState f)
        {
            var m = f.Move;
            if (m == null) { ToNeutral(f); return; }
            int t = f.MoveTick;

            // Cancelamento explícito após contato
            if (f.MoveContact && !AttacksLocked && m.CancelInto != null && m.CancelInto.Length > 0 &&
                t >= m.Startup && t < m.Startup + m.Active + m.CancelWindowExtra &&
                (f.MoveHit || m.CancelOnBlock))
            {
                if (TryStartFromBuffer(f, m)) return;
            }

            if (!f.Airborne)
            {
                if (m.ForwardVelocity != 0f && t >= m.MoveStartTick && t < m.MoveEndTick)
                    f.Vel.x = f.MoveFacing * m.ForwardVelocity;
                else
                    f.Vel.x = 0f;
            }

            if (t >= m.TotalTicks)
            {
                if (f.Airborne)
                {
                    f.Move = null;
                    f.Enter(FighterStateId.Air);
                }
                else ToNeutral(f);
            }
        }

        // ------------------------------------------------------------------ AGARRÃO

        void UpdateGrabPair(FighterState a, FighterState d)
        {
            if (a.State != FighterStateId.Grabbing) return;
            var cfg = Config;
            int t = a.StateTick;

            if (d.State == FighterStateId.Grabbed)
            {
                d.Vel = Vector2.zero;
                a.Vel = Vector2.zero;
                if (t <= cfg.ThrowTechWindow && d.Commands.Buffered(Buttons.Grab))
                {
                    d.Commands.Consume(Buttons.Grab);
                    TechPair(a, d);
                    return;
                }
                if (t == cfg.ThrowTechWindow + 1)
                {
                    ExecuteThrow(a, d);
                    return;
                }
            }

            if (t >= cfg.ThrowHoldTicks && a.State == FighterStateId.Grabbing)
                ToNeutral(a);
        }

        void TechPair(FighterState a, FighterState b)
        {
            float dir = Mathf.Sign(b.Pos.x - a.Pos.x);
            if (dir == 0) dir = a.Facing;
            float half = Config.ThrowTechSeparation * 0.5f;
            float mid = (a.Pos.x + b.Pos.x) * 0.5f;
            a.Pos.x = mid - dir * half;
            b.Pos.x = mid + dir * half;
            ClampToArena(a);
            ClampToArena(b);
            foreach (var f in new[] { a, b })
            {
                f.Move = null;
                f.Vel = Vector2.zero;
                f.Crouching = false;
                f.Enter(FighterStateId.ThrowTech);
            }
            Emit(CombatEventType.ThrowTech, a, b);
        }

        void ExecuteThrow(FighterState a, FighterState d)
        {
            var m = a.Move;
            if (m == null || DamageLocked)
            {
                TechPair(a, d);
                return;
            }
            int dmg = ClampDamage(d, m.Damage);
            d.ComboHits++;
            d.ComboDamage += dmg;
            d.Health = Mathf.Max(0, d.Health - dmg);
            AddEnergy(a, m.EnergyGain);
            AddEnergy(d, Mathf.RoundToInt(m.EnergyGain * Config.DefenderEnergyFactor));

            if (m.ThrowSwapsSides)
            {
                float sep = a.Def.PushHalfWidth + d.Def.PushHalfWidth + 0.15f;
                float target = a.Pos.x - a.Facing * sep;
                if (Mathf.Abs(target) > Config.ArenaHalfWidth)
                {
                    // Sem espaço atrás: o arremessador avança para manter o arremesso coerente na corda.
                    target = Mathf.Clamp(target, -Config.ArenaHalfWidth, Config.ArenaHalfWidth);
                    a.Pos.x = target + a.Facing * sep;
                }
                d.Pos.x = target;
                d.Facing = a.Facing;
            }

            d.Move = null;
            d.Crouching = false;
            Hitstop = Mathf.Max(Hitstop, 8);
            Emit(CombatEventType.ThrowHit, a, d, dmg, m);
            if (d.Health <= 0) EnterKO(d, a);
            else
            {
                d.Airborne = true;
                d.Pos.y = 0.05f;
                float away = m.ThrowSwapsSides ? -a.Facing : a.Facing;
                d.Vel = new Vector2(away * 2.5f, 4.5f);
                d.JuggleImmune = true;
                d.Enter(FighterStateId.Launched);
            }
        }

        // ------------------------------------------------------------------ FÍSICA

        void Integrate(FighterState f)
        {
            float dt = CombatConfig.TickDt;
            float pushV = 0f;
            if (f.PushbackTicks > 0)
            {
                pushV = f.PushbackVel;
                f.PushbackTicks--;
                if (f.PushbackTicks == 0) f.PushbackVel = 0f;
            }

            if (f.Airborne)
            {
                // Rebote nas cordas: trajetória horizontal até a corda (sem gravidade), com limite de segurança.
                if (f.State == FighterStateId.RopeBounce)
                {
                    f.Vel.y = 0f;
                    if (f.StateTick > 45) f.Enter(FighterStateId.Launched);
                }
                else f.Vel.y -= f.Def.Gravity * dt;
                f.Pos += new Vector2(f.Vel.x + pushV, f.Vel.y) * dt;

                if (f.State == FighterStateId.RopeBounce)
                {
                    float limit = Config.ArenaHalfWidth;
                    if (Mathf.Abs(f.Pos.x) >= limit - 0.001f && Mathf.Sign(f.Vel.x) == Mathf.Sign(f.Pos.x))
                    {
                        float side = Mathf.Sign(f.Pos.x);
                        f.Pos.x = side * limit;
                        f.Vel = new Vector2(-side * Config.RopeBounceBackSpeed, Config.RopeBounceUpSpeed);
                        f.Enter(FighterStateId.Launched);
                        Emit(CombatEventType.RopeBounce, null, f);
                    }
                }

                if (f.Pos.y <= 0f && f.Vel.y <= 0f) Land(f);
            }
            else
            {
                f.Pos.x += (f.Vel.x + pushV) * dt;
            }
            ClampToArena(f);
        }

        void Land(FighterState f)
        {
            f.Pos.y = 0f;
            f.Vel.y = 0f;
            f.Airborne = false;
            switch (f.State)
            {
                case FighterStateId.Air:
                    f.Vel.x = 0;
                    f.LandingTicks = f.Def.LandingTicks;
                    f.Enter(FighterStateId.Landing);
                    Emit(CombatEventType.Land, f, null);
                    break;
                case FighterStateId.Attack:
                    f.Vel.x = 0;
                    f.LandingTicks = f.Move != null ? f.Move.LandingRecovery : f.Def.LandingTicks;
                    f.Move = null;
                    f.Enter(FighterStateId.Landing);
                    Emit(CombatEventType.Land, f, null);
                    break;
                case FighterStateId.Launched:
                case FighterStateId.RopeBounce:
                    f.Vel = Vector2.zero;
                    f.Enter(FighterStateId.Knockdown);
                    Emit(CombatEventType.Knockdown, null, f);
                    break;
                case FighterStateId.KO:
                    f.Vel = Vector2.zero;
                    Emit(CombatEventType.Land, f, null);
                    break;
                case FighterStateId.Hitstun:
                    f.Vel = Vector2.zero;
                    f.Enter(FighterStateId.Knockdown);
                    Emit(CombatEventType.Knockdown, null, f);
                    break;
                default:
                    f.Vel.x = 0;
                    break;
            }
        }

        void ClampToArena(FighterState f)
        {
            float limit = Config.ArenaHalfWidth;
            if (f.Pos.x < -limit) f.Pos.x = -limit;
            else if (f.Pos.x > limit) f.Pos.x = limit;
        }

        bool AtWall(FighterState f, float dir) => dir > 0 ? f.Pos.x >= Config.ArenaHalfWidth - 0.02f : f.Pos.x <= -Config.ArenaHalfWidth + 0.02f;

        void ResolvePush()
        {
            var a = Fighters[0];
            var b = Fighters[1];
            float limit = Config.ArenaHalfWidth;

            bool verticalOverlap = a.PushBottom < b.PushTop && b.PushBottom < a.PushTop;
            float minDist = a.Def.PushHalfWidth + b.Def.PushHalfWidth;
            float dx = b.Pos.x - a.Pos.x;
            if (verticalOverlap && Mathf.Abs(dx) < minDist)
            {
                float dir;
                if (Mathf.Abs(dx) > 0.0001f) dir = Mathf.Sign(dx);
                else if (Mathf.Abs(a.PrevPos.x - b.PrevPos.x) > 0.0001f) dir = Mathf.Sign(b.PrevPos.x - a.PrevPos.x);
                else dir = a.Facing;

                // Personagem aterrissando sobre outro é empurrado para o lado de onde veio.
                float overlap = minDist - Mathf.Abs(dx);
                float pa = overlap * 0.5f, pb = overlap * 0.5f;
                if (a.Airborne && !b.Airborne) { pa = overlap; pb = 0; }
                else if (b.Airborne && !a.Airborne) { pa = 0; pb = overlap; }

                a.Pos.x -= dir * pa;
                b.Pos.x += dir * pb;

                // Se um lado bateu na corda, o outro absorve o restante: sem atravessar nem prender.
                if (a.Pos.x < -limit) { b.Pos.x += -limit - a.Pos.x; a.Pos.x = -limit; }
                if (a.Pos.x > limit) { b.Pos.x -= a.Pos.x - limit; a.Pos.x = limit; }
                if (b.Pos.x < -limit) { a.Pos.x += -limit - b.Pos.x; b.Pos.x = -limit; }
                if (b.Pos.x > limit) { a.Pos.x -= b.Pos.x - limit; b.Pos.x = limit; }
                ClampToArena(a);
                ClampToArena(b);
            }

            // Distância máxima (enquadramento): devolve quem se afastou neste tick.
            float dist = Mathf.Abs(b.Pos.x - a.Pos.x);
            if (dist > Config.MaxSeparation)
            {
                float excess = dist - Config.MaxSeparation;
                float sideA = Mathf.Sign(a.Pos.x - b.Pos.x);
                float awayA = Mathf.Max(0f, (a.Pos.x - a.PrevPos.x) * sideA);
                float awayB = Mathf.Max(0f, (b.Pos.x - b.PrevPos.x) * -sideA);
                float total = awayA + awayB;
                float shareA = total > 0.0001f ? awayA / total : 0.5f;
                a.Pos.x -= sideA * excess * shareA;
                b.Pos.x += sideA * excess * (1f - shareA);
                ClampToArena(a);
                ClampToArena(b);
            }
        }

        // ------------------------------------------------------------------ GOLPES

        bool IsStrikeInvulnerable(FighterState d, MoveDefinition incoming)
        {
            if (d.InvulnTicks > 0) return true;
            switch (d.State)
            {
                case FighterStateId.Knockdown:
                case FighterStateId.GetUp:
                case FighterStateId.KO:
                case FighterStateId.Victory:
                case FighterStateId.Grabbing:
                case FighterStateId.Grabbed:
                case FighterStateId.RopeBounce:
                    return true;
                case FighterStateId.Launched:
                    return d.JuggleImmune;
                case FighterStateId.Attack:
                    if (d.Move != null)
                    {
                        if (d.Move.IsInvulnerable(d.MoveTick, InvulnType.Strike)) return true;
                        if (incoming.RequiresAir && d.Move.IsInvulnerable(d.MoveTick, InvulnType.Air)) return true;
                    }
                    return false;
            }
            return false;
        }

        static bool Overlaps(Rect r, FighterState d, Box[] boxes)
        {
            if (boxes == null) return false;
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].IsEmpty) continue;
                if (r.Overlaps(boxes[i].ToWorld(d.Pos, d.Facing))) return true;
            }
            return false;
        }

        /// <summary>Retorna se o hitbox atinge algum hurtbox do alvo. Para no primeiro: nunca conta duas vezes.</summary>
        public static bool HitboxTouches(FighterState a, FighterState d)
        {
            var m = a.Move;
            Rect hit = m.Hitbox.ToWorld(a.Pos, a.MoveFacing);
            if (Overlaps(hit, d, d.CurrentHurtboxes)) return true;
            if (d.State == FighterStateId.Attack && d.Move != null && d.Move.ExtendHurtbox && !d.Move.IsThrow &&
                d.MoveTick >= d.Move.Startup)
            {
                var ext = d.Move.Hitbox;
                ext.W *= 0.85f;
                ext.H *= 0.85f;
                if (hit.Overlaps(ext.ToWorld(d.Pos, d.MoveFacing))) return true;
            }
            return false;
        }

        bool CanBlock(FighterState d, MoveDefinition m)
        {
            if (d.Airborne || !d.IsBlockingPosture) return false;
            if (!d.Commands.Current.IsHeld(Buttons.Block)) return false;
            bool crouch = d.Commands.Current.Y < 0;
            switch (m.Level)
            {
                case AttackLevel.Low: return crouch;
                case AttackLevel.Overhead: return !crouch;
                case AttackLevel.Throw: return false;
                default: return true;
            }
        }

        StrikeResult CheckStrike(FighterState a, FighterState d)
        {
            var r = new StrikeResult();
            if (DamageLocked || a.State != FighterStateId.Attack || a.Move == null || a.Move.IsThrow) return r;
            int idx = a.Move.HitIndexAt(a.MoveTick);
            if (idx < 0 || (a.HitMask & (1u << idx)) != 0) return r;
            if (IsStrikeInvulnerable(d, a.Move)) return r;
            if (!HitboxTouches(a, d)) return r;
            r.Valid = true;
            r.HitIndex = idx;
            r.Blocked = CanBlock(d, a.Move);
            return r;
        }

        void ResolveStrikes()
        {
            var a = Fighters[0];
            var b = Fighters[1];
            // Avaliação simultânea: ambos verificados antes de aplicar (troca sem favorecer ninguém).
            var ra = CheckStrike(a, b);
            var rb = CheckStrike(b, a);
            if (ra.Valid) a.HitMask |= 1u << ra.HitIndex;
            if (rb.Valid) b.HitMask |= 1u << rb.HitIndex;
            var moveA = a.Move;
            var moveB = b.Move;
            if (ra.Valid) ApplyStrike(a, b, moveA, ra.Blocked, ra.HitIndex);
            if (rb.Valid) ApplyStrike(b, a, moveB, rb.Blocked, rb.HitIndex);
        }

        public float ComboScale(int hitNumber)
        {
            if (hitNumber <= Config.ScalingStartHit) return 1f;
            return Mathf.Max(Config.ScalingFloor, 1f - Config.ScalingStep * (hitNumber - Config.ScalingStartHit));
        }

        void AddEnergy(FighterState f, int amount)
        {
            if (amount <= 0) return;
            f.Energy = Mathf.Min(Config.MaxEnergy, f.Energy + amount);
        }

        void ApplyPushback(FighterState a, FighterState d, float distance)
        {
            float dir = a.MoveFacing;
            float v = distance / (PushbackDuration * CombatConfig.TickDt);
            if (AtWall(d, dir))
            {
                a.PushbackVel = -dir * v;
                a.PushbackTicks = PushbackDuration;
            }
            else
            {
                d.PushbackVel = dir * v;
                d.PushbackTicks = PushbackDuration;
            }
        }

        void ApplyStrike(FighterState a, FighterState d, MoveDefinition m, bool blocked, int hitIndex)
        {
            var cfg = Config;
            // Em golpes multi-hit, derrubada/lançamento/rebote valem só no último hit.
            bool finalHit = hitIndex >= m.Hits - 1;
            var knockdown = finalHit ? m.Knockdown : KnockdownType.None;
            bool ropeBounce = finalHit && m.RopeBounce;
            a.MoveContact = true;
            d.WasHitThisTick = true;

            if (blocked)
            {
                int chip = (m.IsSpecial || m.Slot == MoveSlot.Signature) ? Mathf.RoundToInt(m.Damage * cfg.SpecialChipFactor) : 0;
                if (chip > 0) d.Health = Mathf.Max(1, d.Health - chip);
                d.StunTicks = m.Blockstun;
                d.Move = null;
                d.Enter(FighterStateId.Blockstun);
                d.Vel.x = 0;
                ApplyPushback(a, d, m.PushbackBlock);
                AddEnergy(a, Mathf.RoundToInt(m.EnergyGain * cfg.BlockEnergyFactor));
                Hitstop = Mathf.Max(Hitstop, Mathf.Max(4, m.Hitstop - 2));
                Emit(CombatEventType.Block, a, d, chip, m);
                return;
            }

            a.MoveHit = true;
            int hitNumber = d.ComboHits + 1;
            int dmg = ClampDamage(d, Mathf.Max(1, Mathf.RoundToInt(m.Damage * ComboScale(hitNumber))));
            d.ComboHits = hitNumber;
            d.ComboDamage += dmg;
            d.Health = Mathf.Max(0, d.Health - dmg);
            AddEnergy(a, m.EnergyGain);
            AddEnergy(d, Mathf.RoundToInt(m.EnergyGain * cfg.DefenderEnergyFactor));
            Hitstop = Mathf.Max(Hitstop, m.Hitstop);
            d.Move = null;
            float dir = a.MoveFacing;

            Emit(CombatEventType.Hit, a, d, dmg, m);

            if (d.Health <= 0)
            {
                EnterKO(d, a);
                return;
            }

            bool wasAir = d.Airborne;
            if (ropeBounce && !d.BounceUsed)
            {
                d.BounceUsed = true;
                d.Airborne = true;
                d.Crouching = false;
                d.Pos.y = Mathf.Max(d.Pos.y, 0.35f);
                d.Vel = new Vector2(dir * cfg.RopeBounceOutSpeed, 0f);
                d.Enter(FighterStateId.RopeBounce);
                return;
            }

            if (wasAir) d.JuggleHits++;
            if (d.JuggleHits >= cfg.MaxJuggleHits) d.JuggleImmune = true;

            if (knockdown == KnockdownType.Sweep && !wasAir)
            {
                d.Crouching = false;
                d.Vel = Vector2.zero;
                d.Enter(FighterStateId.Knockdown);
                Emit(CombatEventType.Knockdown, a, d);
                return;
            }

            if (knockdown == KnockdownType.Launch || knockdown == KnockdownType.Knockdown || ropeBounce || wasAir)
            {
                Vector2 v = knockdown == KnockdownType.None || knockdown == KnockdownType.Sweep
                    ? new Vector2(1.6f, 4.5f)
                    : m.LaunchVelocity;
                if (knockdown == KnockdownType.Knockdown) d.JuggleImmune = true;
                d.Airborne = true;
                d.Crouching = false;
                d.Pos.y = Mathf.Max(d.Pos.y, 0.05f);
                d.Vel = new Vector2(dir * v.x, v.y);
                d.Enter(FighterStateId.Launched);
                return;
            }

            // Proteção contra infinitos: hitstun decrescente e derrubada forçada no limite do combo.
            int stun = m.Hitstun;
            if (hitNumber > cfg.HitstunDecayStart)
                stun = Mathf.Max(cfg.MinHitstun, stun - (hitNumber - cfg.HitstunDecayStart) * cfg.HitstunDecayPerHit);
            if (hitNumber >= cfg.MaxComboHits)
            {
                d.Airborne = true;
                d.Pos.y = 0.05f;
                d.Vel = new Vector2(dir * 2.5f, 5f);
                d.JuggleImmune = true;
                d.Enter(FighterStateId.Launched);
                return;
            }
            d.StunTicks = stun;
            d.Vel.x = 0;
            d.Enter(FighterStateId.Hitstun);
            ApplyPushback(a, d, m.PushbackHit);
        }

        void EnterKO(FighterState d, FighterState a)
        {
            d.Health = 0;
            d.Move = null;
            d.Crouching = false;
            d.Airborne = true;
            d.Pos.y = Mathf.Max(d.Pos.y, 0.05f);
            float dir = Mathf.Sign(d.Pos.x - a.Pos.x);
            if (dir == 0) dir = a.Facing;
            d.Vel = new Vector2(dir * 3.2f, 6f);
            d.Enter(FighterStateId.KO);
            Hitstop = Mathf.Max(Hitstop, 18);
            Emit(CombatEventType.KO, a, d);
        }

        // ------------------------------------------------------------------ AGARRÕES NOVOS

        bool IsThrowable(FighterState d)
        {
            if (d.Airborne || d.ThrowProtection > 0 || d.WasHitThisTick || d.InvulnTicks > 0) return false;
            switch (d.State)
            {
                case FighterStateId.Idle:
                case FighterStateId.Walk:
                case FighterStateId.Crouch:
                case FighterStateId.Guard:
                case FighterStateId.Dash:
                case FighterStateId.BackDash:
                case FighterStateId.PreJump:
                case FighterStateId.Landing:
                    return true;
                case FighterStateId.Attack:
                    return d.Move != null && !d.Move.IsInvulnerable(d.MoveTick, InvulnType.Throw);
                default:
                    return false;
            }
        }

        bool CheckThrow(FighterState a, FighterState d)
        {
            if (DamageLocked || a.State != FighterStateId.Attack || a.Move == null || !a.Move.IsThrow) return false;
            if (a.Airborne || a.WasHitThisTick || a.MoveContact || !a.Move.IsActiveTick(a.MoveTick)) return false;
            if (!IsThrowable(d)) return false;
            float side = Mathf.Sign(d.Pos.x - a.Pos.x);
            if (side != 0 && side != a.MoveFacing) return false;
            float gap = Mathf.Abs(d.Pos.x - a.Pos.x) - (a.Def.PushHalfWidth + d.Def.PushHalfWidth);
            return gap <= a.Move.ThrowRange;
        }

        void ResolveThrows()
        {
            var a = Fighters[0];
            var b = Fighters[1];
            bool ta = CheckThrow(a, b);
            bool tb = CheckThrow(b, a);
            if (ta && tb)
            {
                a.MoveContact = b.MoveContact = true;
                TechPair(a, b);
                return;
            }
            if (ta) BeginGrab(a, b);
            else if (tb) BeginGrab(b, a);
        }

        void BeginGrab(FighterState a, FighterState d)
        {
            a.MoveContact = true;
            a.Vel = Vector2.zero;
            d.Vel = Vector2.zero;
            d.Move = null;
            d.Crouching = false;
            d.PushbackTicks = 0;
            a.PushbackTicks = 0;
            float sep = a.Def.PushHalfWidth + d.Def.PushHalfWidth + 0.05f;
            d.Pos.x = a.Pos.x + a.MoveFacing * sep;
            ClampToArena(d);
            a.Pos.x = d.Pos.x - a.MoveFacing * sep;
            ClampToArena(a);
            a.Enter(FighterStateId.Grabbing);
            d.Enter(FighterStateId.Grabbed);
            Emit(CombatEventType.GrabStart, a, d, 0, a.Move);
        }

        // ------------------------------------------------------------------ COMBOS

        void CheckComboEnds()
        {
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                if (f.ComboHits > 0 && !f.InComboState && f.State != FighterStateId.Grabbed)
                {
                    Events.Add(new CombatEvent
                    {
                        Type = CombatEventType.ComboEnd,
                        Tick = Tick,
                        Fighter = 1 - i,
                        Target = i,
                        Damage = f.ComboDamage,
                        ComboHits = f.ComboHits,
                        Position = f.Pos,
                    });
                    ResetCombo(f);
                }
            }
        }

        // ------------------------------------------------------------------ APOIO À PARTIDA / TREINO

        public void SetVictory(int index)
        {
            var f = Fighters[index];
            if (f.State == FighterStateId.KO) return;
            f.Move = null;
            f.Vel = Vector2.zero;
            if (f.Airborne) return;
            f.Crouching = false;
            f.Enter(FighterStateId.Victory);
        }

        public void RefillHealth(int index) => Fighters[index].Health = Fighters[index].Def.MaxHealth;
        public void SetEnergy(int index, int value) => Fighters[index].Energy = Mathf.Clamp(value, 0, Config.MaxEnergy);

        public int StateHash()
        {
            unchecked
            {
                return Fighters[0].StateHash() * 397 ^ Fighters[1].StateHash() ^ Tick * 7919;
            }
        }
    }
}
