using System.Collections.Generic;
using UltimoRound.Combat;
using UnityEngine;

namespace UltimoRound.Net
{
    /// <summary>Evento da luta numerado para ser reenviado até o convidado confirmar o recebimento.</summary>
    public struct NetEventRecord
    {
        public uint Seq;
        public uint Tick;
        public byte Epoch;
        public bool IsMatch;
        public CombatEvent Combat;
        public MatchEvent Match;
    }

    /// <summary>Converte o estado da partida em bytes e de volta. Não decide nenhuma regra.</summary>
    public static class FightStateCodec
    {
        const byte NoMove = 255;

        public static void WriteMatch(NetWriter w, MatchController m)
        {
            w.Byte((byte)m.Phase);
            w.Int(m.PhaseTick);
            w.Short((short)m.Round);
            w.Short((short)m.RoundNumberShown);
            w.Int(m.TimerTicks);
            w.SByte((sbyte)m.MatchWinner);
            w.SByte((sbyte)m.RoundWinner);
            w.Byte((byte)m.LastReason);
            w.Short((short)m.DrawsThisMatch);
            w.Byte((byte)m.Wins[0]);
            w.Byte((byte)m.Wins[1]);
            var s = m.Sim;
            w.Int(s.Tick);
            w.Short((short)s.Hitstop);
            w.Short((short)s.FreezeTicks);
            w.SByte((sbyte)s.FreezeOwner);
            w.Bool(s.AttacksLocked);
            w.Bool(s.DamageLocked);
            WriteFighter(w, s[0]);
            WriteFighter(w, s[1]);
        }

        public static void ReadMatch(NetReader r, MatchController m)
        {
            var phase = (MatchPhase)r.Byte();
            int phaseTick = r.Int();
            int round = r.Short();
            int shown = r.Short();
            int timer = r.Int();
            int matchWinner = r.SByte();
            int roundWinner = r.SByte();
            var reason = (RoundEndReason)r.Byte();
            int draws = r.Short();
            int w0 = r.Byte();
            int w1 = r.Byte();
            m.NetRestore(phase, phaseTick, round, shown, timer, matchWinner, roundWinner, reason, draws, w0, w1);
            int tick = r.Int();
            int hitstop = r.Short();
            int freeze = r.Short();
            int owner = r.SByte();
            bool attacks = r.Bool();
            bool damage = r.Bool();
            m.Sim.NetRestore(tick, hitstop, freeze, owner, attacks, damage);
            ReadFighter(r, m.Sim[0]);
            ReadFighter(r, m.Sim[1]);
        }

        static void WriteFighter(NetWriter w, FighterState f)
        {
            w.Float(f.Pos.x);
            w.Float(f.Pos.y);
            w.Float(f.Vel.x);
            w.Float(f.Vel.y);
            w.SByte((sbyte)f.Facing);
            w.Bool(f.Airborne);
            w.Short((short)f.Health);
            w.Short((short)f.Energy);
            w.Byte((byte)f.State);
            w.Short((short)f.StateTick);
            w.Byte(f.Move != null ? (byte)f.Move.Slot : NoMove);
            w.SByte((sbyte)f.MoveFacing);
            w.UInt(f.HitMask);
            w.Bool(f.MoveContact);
            w.Bool(f.MoveHit);
            w.Short((short)f.StunTicks);
            w.Bool(f.Crouching);
            w.Bool(f.AirAttackUsed);
            w.SByte((sbyte)f.JumpDir);
            w.Short((short)f.LandingTicks);
            w.Short((short)f.ComboHits);
            w.Short((short)f.ComboDamage);
            w.Short((short)f.JuggleHits);
            w.Bool(f.JuggleImmune);
            w.Bool(f.BounceUsed);
            w.Short((short)f.ThrowProtection);
            w.Short((short)f.InvulnTicks);
        }

        static void ReadFighter(NetReader r, FighterState f)
        {
            f.PrevPos = f.Pos;
            f.Pos = new Vector2(r.Float(), r.Float());
            f.Vel = new Vector2(r.Float(), r.Float());
            f.Facing = r.SByte();
            f.Airborne = r.Bool();
            f.Health = r.Short();
            f.Energy = r.Short();
            f.State = (FighterStateId)r.Byte();
            f.StateTick = r.Short();
            byte move = r.Byte();
            f.Move = move == NoMove ? null : f.Def.GetMove((MoveSlot)move);
            f.MoveFacing = r.SByte();
            f.HitMask = r.UInt();
            f.MoveContact = r.Bool();
            f.MoveHit = r.Bool();
            f.StunTicks = r.Short();
            f.Crouching = r.Bool();
            f.AirAttackUsed = r.Bool();
            f.JumpDir = r.SByte();
            f.LandingTicks = r.Short();
            f.ComboHits = r.Short();
            f.ComboDamage = r.Short();
            f.JuggleHits = r.Short();
            f.JuggleImmune = r.Bool();
            f.BounceUsed = r.Bool();
            f.ThrowProtection = r.Short();
            f.InvulnTicks = r.Short();
        }

        public const int MaxEventBytes = 40;

        public static void WriteEvent(NetWriter w, in NetEventRecord e)
        {
            w.UInt(e.Seq);
            w.UInt(e.Tick);
            w.Byte(e.Epoch);
            w.Bool(e.IsMatch);
            if (e.IsMatch)
            {
                w.Byte((byte)e.Match.Type);
                w.Short((short)e.Match.Round);
                w.SByte((sbyte)e.Match.Winner);
                return;
            }
            var c = e.Combat;
            w.Byte((byte)c.Type);
            w.Int(c.Tick);
            w.SByte((sbyte)c.Fighter);
            w.SByte((sbyte)c.Target);
            w.Short((short)c.Damage);
            w.Short((short)c.ComboHits);
            w.Float(c.Position.x);
            w.Float(c.Position.y);
            w.Byte((byte)c.Slot);
            w.Byte((byte)c.Level);
            w.Byte((byte)c.Sfx);
            w.Bool(c.Heavy);
        }

        public static NetEventRecord ReadEvent(NetReader r)
        {
            var e = new NetEventRecord { Seq = r.UInt(), Tick = r.UInt(), Epoch = r.Byte(), IsMatch = r.Bool() };
            if (e.IsMatch)
            {
                e.Match = new MatchEvent { Type = (MatchEventType)r.Byte(), Round = r.Short(), Winner = r.SByte() };
                return e;
            }
            e.Combat = new CombatEvent
            {
                Type = (CombatEventType)r.Byte(),
                Tick = r.Int(),
                Fighter = r.SByte(),
                Target = r.SByte(),
                Damage = r.Short(),
                ComboHits = r.Short(),
                Position = new Vector2(r.Float(), r.Float()),
                Slot = (MoveSlot)r.Byte(),
                Level = (AttackLevel)r.Byte(),
                Sfx = (SfxKind)r.Byte(),
                Heavy = r.Bool(),
            };
            return e;
        }

        public static void WriteInput(NetWriter w, in InputFrame f)
        {
            w.SByte(f.X);
            w.SByte(f.Y);
            w.Byte((byte)f.Held);
            w.Byte((byte)f.Pressed);
        }

        public static InputFrame ReadInput(NetReader r)
        {
            int x = r.SByte();
            int y = r.SByte();
            return new InputFrame(x, y, (Buttons)r.Byte(), (Buttons)r.Byte());
        }
    }

    /// <summary>Lado do anfitrião: fila de comandos do convidado e envio do estado e dos eventos a cada tick.</summary>
    public sealed class NetFightHost
    {
        const int MaxQueuedInputs = 6;
        const int TargetQueuedInputs = 2;

        readonly List<NetEventRecord> _events = new List<NetEventRecord>(64);
        readonly Queue<InputFrame> _inputs = new Queue<InputFrame>(16);
        uint _nextEventSeq = 1;
        uint _lastInputSeq;
        InputFrame _lastInput;

        public uint NetTick { get; private set; }
        public byte Epoch { get; private set; }
        public int MissingInputs { get; private set; }
        public int MergedInputs { get; private set; }
        public int PendingEvents => _events.Count;

        public void BeginTick() => NetTick++;

        /// <summary>Revanche: o convidado reinicia a tela quando vê o novo número.</summary>
        public void NewEpoch()
        {
            Epoch++;
            NetTick++;
        }

        /// <summary>Copia os eventos deste tick antes de a apresentação consumi-los.</summary>
        public void CollectEvents(MatchController m)
        {
            foreach (var e in m.Sim.Events)
                _events.Add(new NetEventRecord { Seq = _nextEventSeq++, Tick = NetTick, Epoch = Epoch, Combat = e });
            foreach (var e in m.Events)
                _events.Add(new NetEventRecord { Seq = _nextEventSeq++, Tick = NetTick, Epoch = Epoch, IsMatch = true, Match = e });
        }

        public void WriteSnapshot(NetWriter w, MatchController m)
        {
            NetProtocol.WriteHeader(w, NetMessage.Snapshot);
            w.UInt(NetTick);
            w.Byte(Epoch);
            int stateLengthAt = w.Length;
            w.Short(0);
            FightStateCodec.WriteMatch(w, m);
            int stateLength = w.Length - stateLengthAt - 2;
            w.Data[stateLengthAt] = (byte)stateLength;
            w.Data[stateLengthAt + 1] = (byte)(stateLength >> 8);

            int countAt = w.Length;
            w.Byte(0);
            int count = 0;
            for (int i = 0; i < _events.Count && count < 255; i++)
            {
                if (w.Remaining < FightStateCodec.MaxEventBytes) break;
                FightStateCodec.WriteEvent(w, _events[i]);
                count++;
            }
            w.Data[countAt] = (byte)count;
        }

        public void ReadInput(NetReader r)
        {
            uint newest = r.UInt();
            uint ackedEvents = r.UInt();
            int count = r.Byte();
            if (!r.Ok || count > 32) return;
            for (int i = 0; i < count; i++)
            {
                var f = FightStateCodec.ReadInput(r);
                if (!r.Ok) return;
                uint seq = newest - (uint)(count - 1 - i);
                if (seq <= _lastInputSeq) continue;
                _lastInputSeq = seq;
                _inputs.Enqueue(f);
            }
            int acked = 0;
            while (acked < _events.Count && _events[acked].Seq <= ackedEvents) acked++;
            if (acked > 0) _events.RemoveRange(0, acked);
        }

        /// <summary>Um comando por tick. Atraso acumulado é descartado juntando os toques, para nenhum se perder.</summary>
        public InputFrame NextRemoteInput()
        {
            if (_inputs.Count == 0)
            {
                MissingInputs++;
                return new InputFrame(_lastInput.X, _lastInput.Y, _lastInput.Held, Buttons.None);
            }
            var f = _inputs.Dequeue();
            if (_inputs.Count > MaxQueuedInputs)
            {
                var pressed = f.Pressed;
                while (_inputs.Count > TargetQueuedInputs)
                {
                    f = _inputs.Dequeue();
                    pressed |= f.Pressed;
                    MergedInputs++;
                }
                f = new InputFrame(f.X, f.Y, f.Held, pressed);
            }
            _lastInput = f;
            return f;
        }
    }

    /// <summary>Lado do convidado: guarda os estados recebidos e os aplica a uma cópia da partida, sem simular.</summary>
    public sealed class NetFightGuest
    {
        const int MaxBuffered = 32;
        const int MaxBufferTarget = 6;
        const int StableTicksToShrink = 600;

        struct Buffered
        {
            public uint Tick;
            public byte Epoch;
            public byte[] State;
            public int Length;
        }

        readonly List<Buffered> _buffer = new List<Buffered>(MaxBuffered);
        readonly Stack<byte[]> _pool = new Stack<byte[]>();
        readonly List<NetEventRecord> _pending = new List<NetEventRecord>(32);
        readonly NetReader _stateReader = new NetReader();
        readonly InputFrame[] _recentInputs = new InputFrame[8];
        uint _inputSeq;
        uint _lastEventSeq;
        uint _lastAppliedTick;
        uint _newestTick;
        uint _playbackTick;
        bool _started;

        /// <summary>
        /// Estados guardados antes de exibir: absorve a variação de chegada dos pacotes (Wi-Fi).
        /// Cresce quando falta estado e encolhe depois de 10 s estáveis, para atrasar só o necessário.
        /// </summary>
        public int BufferTarget = 2;
        int _stableTicks;

        public int BufferedCount => _buffer.Count;
        public uint LastAppliedTick => _lastAppliedTick;
        public int Holds { get; private set; }
        public int Skipped { get; private set; }
        public int LostSnapshots { get; private set; }
        public int MaxBufferSeen { get; private set; }
        public bool HasSnapshot => _buffer.Count > 0;
        public byte NewestEpoch => _buffer.Count > 0 ? _buffer[_buffer.Count - 1].Epoch : (byte)0;

        /// <summary>Revanche: descarta o que sobrou da partida anterior.</summary>
        public void PurgeOldEpoch()
        {
            byte newest = NewestEpoch;
            for (int i = _buffer.Count - 1; i >= 0; i--)
                if (_buffer[i].Epoch != newest) Recycle(i);
            _pending.RemoveAll(e => e.Epoch != newest);
        }

        public void ReadSnapshot(NetReader r)
        {
            uint tick = r.UInt();
            byte epoch = r.Byte();
            int stateLength = (ushort)r.Short();
            if (!r.Ok || stateLength > r.Remaining) return;

            var sink = _pool.Count > 0 ? _pool.Pop() : new byte[NetProtocol.MaxPacket];
            CopyState(r, sink, stateLength);

            int count = r.Byte();
            for (int i = 0; i < count && r.Ok; i++)
            {
                var e = FightStateCodec.ReadEvent(r);
                if (!r.Ok) break;
                if (e.Seq <= _lastEventSeq) continue;
                _lastEventSeq = e.Seq;
                _pending.Add(e);
            }
            if (!r.Ok) { _pool.Push(sink); return; }

            // Fora de ordem: insere na posição certa; atrasado ou repetido é descartado.
            if (tick <= _lastAppliedTick && _started) { _pool.Push(sink); return; }
            int at = _buffer.Count;
            while (at > 0 && _buffer[at - 1].Tick > tick) at--;
            if (at > 0 && _buffer[at - 1].Tick == tick) { _pool.Push(sink); return; }
            _buffer.Insert(at, new Buffered { Tick = tick, Epoch = epoch, State = sink, Length = stateLength });
            if (tick > _newestTick) _newestTick = tick;
            while (_buffer.Count > MaxBuffered) Recycle(0);
            if (_buffer.Count > MaxBufferSeen) MaxBufferSeen = _buffer.Count;
        }

        static void CopyState(NetReader r, byte[] sink, int length)
        {
            for (int i = 0; i < length; i++) sink[i] = r.Byte();
        }

        void Recycle(int index)
        {
            _pool.Push(_buffer[index].State);
            _buffer.RemoveAt(index);
        }

        /// <summary>
        /// Um passo por tick do convidado. A reprodução segue o número do tick do anfitrião, então um pacote
        /// perdido só deixa um quadro sem novidade, sem esvaziar a fila. Retorna falso se não houve estado novo.
        /// </summary>
        public bool Step(MatchController m)
        {
            if (!_started)
            {
                if (_buffer.Count == 0 || _newestTick - _buffer[0].Tick + 1 < BufferTarget) return false;
                _started = true;
                _playbackTick = _newestTick - (uint)BufferTarget + 1;
            }
            else
            {
                _playbackTick++;
                // Atrasou demais: pula para perto do estado mais novo.
                if (_newestTick > _playbackTick + (uint)BufferTarget + 4)
                    _playbackTick = _newestTick - (uint)BufferTarget + 1;
            }

            if (_playbackTick > _newestTick)
            {
                Holds++;
                _stableTicks = 0;
                if (BufferTarget < MaxBufferTarget) BufferTarget++;
                _started = false;
                return false;
            }
            if (++_stableTicks >= StableTicksToShrink)
            {
                _stableTicks = 0;
                if (BufferTarget > 1) BufferTarget--;
            }

            // Aplica só o mais recente que já chegou à hora de ser exibido.
            if (_buffer.Count == 0 || _buffer[0].Tick > _playbackTick) return false;
            while (_buffer.Count > 1 && _buffer[1].Tick <= _playbackTick)
            {
                Skipped++;
                Recycle(0);
            }

            var b = _buffer[0];
            _stateReader.Reset(b.State, b.Length);
            FightStateCodec.ReadMatch(_stateReader, m);
            if (_lastAppliedTick != 0 && b.Tick > _lastAppliedTick + 1) LostSnapshots += (int)(b.Tick - _lastAppliedTick - 1);
            _lastAppliedTick = b.Tick;
            Recycle(0);

            int released = 0;
            foreach (var e in _pending)
            {
                if (e.Tick > _lastAppliedTick) break;
                if (e.IsMatch) m.Events.Add(e.Match);
                else m.Sim.Events.Add(e.Combat);
                released++;
            }
            if (released > 0) _pending.RemoveRange(0, released);
            return true;
        }

        /// <summary>Monta o pacote de comandos, repetindo os últimos para resistir à perda de pacotes.</summary>
        public void WriteInput(NetWriter w, in InputFrame f)
        {
            for (int i = _recentInputs.Length - 1; i > 0; i--) _recentInputs[i] = _recentInputs[i - 1];
            _recentInputs[0] = f;
            _inputSeq++;
            int count = _inputSeq < _recentInputs.Length ? (int)_inputSeq : _recentInputs.Length;
            NetProtocol.WriteHeader(w, NetMessage.Input);
            w.UInt(_inputSeq);
            w.UInt(_lastEventSeq);
            w.Byte((byte)count);
            for (int i = count - 1; i >= 0; i--) FightStateCodec.WriteInput(w, _recentInputs[i]);
        }
    }
}
