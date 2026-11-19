using UltimoRound.Combat;
using UltimoRound.Core;
using UnityEngine;

namespace UltimoRound.AI
{
    /// <summary>
    /// CPU por utilidade simples. Usa somente estado observável do oponente (posição, estado, golpe visível),
    /// percebido com atraso de reação; nunca lê a entrada do jogador nem recebe dano extra.
    /// Produz o mesmo <see cref="InputFrame"/> que um jogador humano.
    /// </summary>
    public sealed class CpuBrain
    {
        public struct Profile
        {
            public int ReactionTicks;
            public int DecisionInterval;
            public float BlockChance;
            public float AntiAirChance;
            public float PunishChance;
            public float ComboChance;
            public float ErrorChance;
            public float Aggression;
            public float GrabChance;
        }

        public static Profile For(CpuDifficulty d)
        {
            switch (d)
            {
                case CpuDifficulty.Easy:
                    return new Profile { ReactionTicks = 26, DecisionInterval = 18, BlockChance = 0.25f, AntiAirChance = 0.15f, PunishChance = 0.2f, ComboChance = 0.25f, ErrorChance = 0.3f, Aggression = 0.35f, GrabChance = 0.08f };
                case CpuDifficulty.Hard:
                    return new Profile { ReactionTicks = 10, DecisionInterval = 8, BlockChance = 0.78f, AntiAirChance = 0.7f, PunishChance = 0.8f, ComboChance = 0.85f, ErrorChance = 0.05f, Aggression = 0.6f, GrabChance = 0.22f };
                default:
                    return new Profile { ReactionTicks = 16, DecisionInterval = 12, BlockChance = 0.55f, AntiAirChance = 0.45f, PunishChance = 0.55f, ComboChance = 0.6f, ErrorChance = 0.12f, Aggression = 0.5f, GrabChance = 0.15f };
            }
        }

        struct Snap
        {
            public Vector2 Pos;
            public FighterStateId State;
            public bool Airborne;
            public bool Crouching;
            public MoveDefinition Move;
            public int MoveTick;
        }

        enum Plan { Wait, Approach, Retreat, Block, Press, Jump, Dash }

        readonly Profile _p;
        readonly System.Random _rng;
        readonly Snap[] _history = new Snap[64];
        int _head;
        int _count;
        Plan _plan;
        int _planTicks;
        int _cooldown;
        Buttons _pressButton;
        int _pressX, _pressY;
        bool _blockCrouch;
        int _jumpDir;

        public CpuBrain(CpuDifficulty difficulty, int seed)
        {
            _p = For(difficulty);
            _rng = new System.Random(seed);
        }

        float Rand() => (float)_rng.NextDouble();

        public void Reset()
        {
            _count = 0;
            _head = 0;
            _plan = Plan.Wait;
            _planTicks = 0;
            _cooldown = 20;
        }

        void Record(FighterState o)
        {
            _history[_head] = new Snap { Pos = o.Pos, State = o.State, Airborne = o.Airborne, Crouching = o.Crouching, Move = o.Move, MoveTick = o.MoveTick };
            _head = (_head + 1) % _history.Length;
            if (_count < _history.Length) _count++;
        }

        Snap Perceived()
        {
            int delay = Mathf.Min(_p.ReactionTicks, _count - 1);
            int idx = (_head - 1 - delay + _history.Length * 2) % _history.Length;
            return _history[idx];
        }

        static float Reach(MoveDefinition m)
        {
            if (m == null) return 0f;
            if (m.IsThrow) return m.ThrowRange + 0.65f;
            float travel = m.ForwardVelocity * Mathf.Max(0, Mathf.Min(m.MoveEndTick, m.Startup + m.Active) - m.MoveStartTick) / 60f;
            return m.Hitbox.X + m.Hitbox.W * 0.5f + 0.32f + travel;
        }

        /// <param name="allowAct">Falso durante pausa, apresentação ou fim de round.</param>
        public InputFrame Tick(FightSimulation sim, int selfIndex, bool allowAct)
        {
            var self = sim[selfIndex];
            var opp = sim[1 - selfIndex];
            Record(opp);
            if (!allowAct) { _plan = Plan.Wait; return InputFrame.Neutral; }

            var seen = Perceived();
            int fwd = self.Facing;
            float dist = Mathf.Abs(seen.Pos.x - self.Pos.x);
            if (_cooldown > 0) _cooldown--;

            // Continuação de combo: só após contato confirmado do próprio golpe.
            if (self.State == FighterStateId.Attack && self.Move != null && self.MoveHit && self.Move.CancelInto.Length > 0)
            {
                var next = NextComboStep(self);
                if (next != MoveSlot.None && Rand() < _p.ComboChance * 0.25f)
                    return PressFor(next, fwd);
            }

            if (self.State == FighterStateId.Blockstun || self.State == FighterStateId.Guard)
            {
                if (_plan == Plan.Block && _planTicks > 0) { _planTicks--; return Hold(0, _blockCrouch ? -1 : 0, Buttons.Block); }
            }
            if (self.State == FighterStateId.Grabbed && Rand() < _p.BlockChance * 0.35f)
                return new InputFrame(0, 0, Buttons.Grab, Buttons.Grab);

            if (!self.IsActionable)
            {
                // Mantém defesa se estiver em blockstun; senão não força nada.
                if (self.State == FighterStateId.Blockstun) return Hold(0, _blockCrouch ? -1 : 0, Buttons.Block);
                return InputFrame.Neutral;
            }

            // Plano em andamento
            if (_planTicks > 0)
            {
                _planTicks--;
                return ExecutePlan(fwd);
            }

            Decide(self, seen, dist, fwd, sim);
            return ExecutePlan(fwd);
        }

        MoveSlot NextComboStep(FighterState self)
        {
            var combos = self.Def.Combos;
            if (combos == null) return MoveSlot.None;
            foreach (var c in combos)
                for (int i = 0; i < c.Sequence.Length - 1; i++)
                    if (c.Sequence[i] == self.Move.Slot && self.Move.CanCancelInto(c.Sequence[i + 1]))
                        return c.Sequence[i + 1];
            return MoveSlot.None;
        }

        void Decide(FighterState self, Snap seen, float dist, int fwd, FightSimulation sim)
        {
            var def = self.Def;
            bool oppAttacking = seen.State == FighterStateId.Attack && seen.Move != null;
            bool oppActiveSoon = oppAttacking && seen.MoveTick < seen.Move.Startup + seen.Move.Active;
            bool oppRecovering = oppAttacking && seen.MoveTick >= seen.Move.Startup + seen.Move.Active;
            bool oppDown = seen.State == FighterStateId.Knockdown || seen.State == FighterStateId.GetUp || seen.State == FighterStateId.KO;

            if (Rand() < _p.ErrorChance)
            {
                RandomAction(def, fwd);
                return;
            }

            // Defesa contra golpe percebido
            if (oppActiveSoon && dist < Reach(seen.Move) + 0.4f)
            {
                if (Rand() < _p.BlockChance)
                {
                    _plan = Plan.Block;
                    _planTicks = 18;
                    _blockCrouch = seen.Move.Level == AttackLevel.Low;
                    if (seen.Move.Level == AttackLevel.Overhead || seen.Airborne) _blockCrouch = false;
                    return;
                }
            }

            // Antiaéreo
            if (seen.Airborne && dist < 2.4f && seen.State != FighterStateId.Launched && seen.State != FighterStateId.KO)
            {
                if (dist < 1.5f && Rand() < _p.AntiAirChance && def.AntiAir != null)
                {
                    SetPress(MoveSlot.AntiAir, fwd);
                    return;
                }
                if (Rand() < _p.BlockChance)
                {
                    _plan = Plan.Block;
                    _planTicks = 20;
                    _blockCrouch = false;
                    return;
                }
            }

            // Punição de recuperação
            if (oppRecovering && Rand() < _p.PunishChance)
            {
                var best = BestInRange(def, dist, fastest: true);
                if (best != MoveSlot.None) { SetPress(best, fwd); return; }
            }

            if (oppDown)
            {
                _plan = dist < 1.6f ? Plan.Retreat : Plan.Wait;
                _planTicks = 12;
                return;
            }

            // Assinatura com energia cheia e alvo ao alcance
            if (self.Energy >= sim.Config.MaxEnergy && def.Signature != null && dist <= Reach(def.Signature) && (oppRecovering || Rand() < 0.25f))
            {
                SetPress(MoveSlot.Signature, fwd);
                return;
            }

            if (_cooldown > 0)
            {
                _plan = Plan.Wait;
                _planTicks = 4;
                return;
            }

            float r = Rand();
            if (dist > 2.3f)
            {
                if (r < 0.12f * _p.Aggression) { _plan = Plan.Jump; _jumpDir = fwd; _planTicks = 4; }
                else if (r < 0.3f) { _plan = Plan.Dash; _planTicks = 6; }
                else { _plan = Plan.Approach; _planTicks = _p.DecisionInterval; }
                return;
            }

            if (dist < 1.05f && seen.State == FighterStateId.Guard && Rand() < _p.GrabChance * 2.5f)
            {
                SetPress(MoveSlot.Grab, fwd);
                return;
            }

            if (r < _p.Aggression)
            {
                var slot = BestInRange(def, dist, fastest: false);
                if (slot != MoveSlot.None)
                {
                    SetPress(slot, fwd);
                    _cooldown = Mathf.RoundToInt(_p.DecisionInterval * 1.5f);
                    return;
                }
                _plan = Plan.Approach;
                _planTicks = _p.DecisionInterval / 2;
            }
            else if (r < _p.Aggression + 0.2f)
            {
                _plan = Plan.Block;
                _blockCrouch = Rand() < 0.4f;
                _planTicks = _p.DecisionInterval;
            }
            else if (r < _p.Aggression + 0.35f)
            {
                _plan = Plan.Retreat;
                _planTicks = _p.DecisionInterval;
            }
            else
            {
                _plan = Plan.Wait;
                _planTicks = _p.DecisionInterval / 2;
            }
        }

        static readonly MoveSlot[] StrikeSlots =
            { MoveSlot.Punch, MoveSlot.Kick, MoveSlot.Heavy, MoveSlot.CrouchPunch, MoveSlot.Low, MoveSlot.SpecialForward, MoveSlot.SpecialNeutral, MoveSlot.ForwardHeavy };

        MoveSlot BestInRange(FighterDefinition def, float dist, bool fastest)
        {
            MoveSlot best = MoveSlot.None;
            float bestScore = float.MinValue;
            foreach (var s in StrikeSlots)
            {
                var m = def.GetMove(s);
                if (m == null || m.IsThrow) continue;
                if (dist > Reach(m)) continue;
                float score = fastest ? -m.Startup * 3f + m.Damage * 0.05f : m.Damage * 0.05f - m.Startup * 0.6f - m.Recovery * 0.2f + Rand() * 4f;
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        void RandomAction(FighterDefinition def, int fwd)
        {
            int k = _rng.Next(6);
            switch (k)
            {
                case 0: _plan = Plan.Approach; _planTicks = 10; break;
                case 1: _plan = Plan.Retreat; _planTicks = 10; break;
                case 2: _plan = Plan.Jump; _jumpDir = _rng.Next(3) - 1; _planTicks = 4; break;
                case 3: SetPress(MoveSlot.Kick, fwd); break;
                case 4: SetPress(MoveSlot.Heavy, fwd); break;
                default: _plan = Plan.Wait; _planTicks = 10; break;
            }
        }

        void SetPress(MoveSlot slot, int fwd)
        {
            var f = PressFor(slot, fwd);
            _pressButton = f.Pressed;
            _pressX = f.X;
            _pressY = f.Y;
            _plan = Plan.Press;
            _planTicks = 1;
        }

        static InputFrame PressFor(MoveSlot slot, int fwd)
        {
            switch (slot)
            {
                case MoveSlot.Punch: return new InputFrame(0, 0, Buttons.Punch, Buttons.Punch);
                case MoveSlot.Kick: return new InputFrame(0, 0, Buttons.Kick, Buttons.Kick);
                case MoveSlot.Heavy: return new InputFrame(0, 0, Buttons.Heavy, Buttons.Heavy);
                case MoveSlot.ForwardHeavy: return new InputFrame(fwd, 0, Buttons.Heavy, Buttons.Heavy);
                case MoveSlot.CrouchPunch: return new InputFrame(0, -1, Buttons.Punch, Buttons.Punch);
                case MoveSlot.Low: return new InputFrame(0, -1, Buttons.Kick, Buttons.Kick);
                case MoveSlot.AntiAir: return new InputFrame(0, -1, Buttons.Heavy, Buttons.Heavy);
                case MoveSlot.SpecialNeutral: return new InputFrame(0, 0, Buttons.Special, Buttons.Special);
                case MoveSlot.SpecialForward: return new InputFrame(fwd, 0, Buttons.Special, Buttons.Special);
                case MoveSlot.Signature: return new InputFrame(0, 0, Buttons.Signature, Buttons.Signature);
                case MoveSlot.Grab: return new InputFrame(0, 0, Buttons.Grab, Buttons.Grab);
                default: return InputFrame.Neutral;
            }
        }

        static InputFrame Hold(int x, int y, Buttons held) => new InputFrame(x, y, held, Buttons.None);

        InputFrame ExecutePlan(int fwd)
        {
            switch (_plan)
            {
                case Plan.Approach: return Hold(fwd, 0, Buttons.None);
                case Plan.Retreat: return Hold(-fwd, 0, Buttons.None);
                case Plan.Block: return Hold(0, _blockCrouch ? -1 : 0, Buttons.Block);
                case Plan.Jump: return Hold(_jumpDir, 1, Buttons.None);
                case Plan.Dash:
                    // Toque duplo para frente: frente, neutro, frente.
                    return _planTicks >= 5 || _planTicks == 3 ? Hold(fwd, 0, Buttons.None) : Hold(0, 0, Buttons.None);
                case Plan.Press:
                    _plan = Plan.Wait;
                    return new InputFrame(_pressX, _pressY, _pressButton, _pressButton);
                default: return InputFrame.Neutral;
            }
        }
    }
}
