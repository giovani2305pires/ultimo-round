using System;
using System.Collections.Generic;
using NUnit.Framework;
using UltimoRound.Combat;
using UltimoRound.Core;
using UltimoRound.Data;
using UltimoRound.Input;
using static UltimoRound.Tests.TestFight;

namespace UltimoRound.Tests
{
    public class MatchAndSystemTests
    {
        static MatchController NewMatch(bool training = false)
        {
            var cfg = DefaultRoster.CreateConfig();
            return new MatchController(cfg, DefaultRoster.Create("giovani"), DefaultRoster.Create("brutus"), 0, 0, training);
        }

        static void Run(MatchController m, int ticks)
        {
            for (int i = 0; i < ticks; i++) m.Tick(InputFrame.Neutral, InputFrame.Neutral);
        }

        static void ToFighting(MatchController m)
        {
            int guard = 0;
            while (m.Phase != MatchPhase.Fighting && guard++ < 1000) m.Tick(InputFrame.Neutral, InputFrame.Neutral);
            Assert.AreEqual(MatchPhase.Fighting, m.Phase);
        }

        static int Count(MatchController m, MatchEventType t)
        {
            int n = 0;
            foreach (var e in m.Events) if (e.Type == t) n++;
            return n;
        }

        // 7 ---------------------------------------------------------------
        [Test]
        public void KO_VenceRound_EMelhorDeTresEncerraUmaVez()
        {
            var m = NewMatch();
            for (int r = 0; r < 2; r++)
            {
                ToFighting(m);
                m.Sim[1].Health = 0;
                m.Tick(InputFrame.Neutral, InputFrame.Neutral);
                Assert.AreEqual(MatchPhase.RoundOver, m.Phase);
                // Dano adicional / novo KO durante o encerramento não gera eventos duplicados.
                m.Sim[0].Health = 0;
                Run(m, 5);
                Run(m, m.Config.RoundOverTicks);
            }
            Assert.AreEqual(2, m.Wins[0]);
            Assert.AreEqual(0, m.Wins[1]);
            Assert.AreEqual(MatchPhase.MatchOver, m.Phase);
            Assert.AreEqual(0, m.MatchWinner);
            Run(m, 600);
            Assert.AreEqual(1, Count(m, MatchEventType.MatchWon));
            Assert.AreEqual(2, Count(m, MatchEventType.KO));
            Assert.AreEqual(0, Count(m, MatchEventType.DoubleKO));
        }

        [Test]
        public void TempoEsgotado_VenceQuemTemMaiorPorcentagem()
        {
            var m = NewMatch();
            ToFighting(m);
            // Brutus tem mais vida máxima: compara porcentagem, não valor absoluto.
            m.Sim[0].Health = 600;  // 60% de 1000
            m.Sim[1].Health = 650;  // ~56% de 1150
            Run(m, m.Config.RoundSeconds * 60 + 5);
            Assert.AreEqual(1, Count(m, MatchEventType.TimeUp));
            Assert.AreEqual(1, m.Wins[0]);
            Assert.AreEqual(RoundEndReason.TimeUp, m.LastReason);
        }

        [Test]
        public void TempoEsgotado_EmpateExato_RepeteRoundSemPontuar()
        {
            var m = NewMatch();
            ToFighting(m);
            Assert.AreEqual(1, m.RoundNumberShown);
            Run(m, m.Config.RoundSeconds * 60 + 5);
            Assert.AreEqual(1, Count(m, MatchEventType.RoundDraw));
            Assert.AreEqual(0, m.Wins[0] + m.Wins[1]);
            Run(m, m.Config.RoundOverTicks + 2);
            Assert.AreEqual(MatchPhase.PreRound, m.Phase);
            Assert.AreEqual(1, m.RoundNumberShown, "round repetido mantém o número");
            Assert.AreEqual(m.Sim[0].Def.MaxHealth, m.Sim[0].Health);
            Assert.AreEqual(0, m.Sim[0].Energy);
        }

        [Test]
        public void DuploKO_EmpateSemPontuar()
        {
            var m = NewMatch();
            ToFighting(m);
            m.Sim[0].Health = 0;
            m.Sim[1].Health = 0;
            m.Tick(InputFrame.Neutral, InputFrame.Neutral);
            Assert.AreEqual(1, Count(m, MatchEventType.DoubleKO));
            Assert.AreEqual(1, Count(m, MatchEventType.RoundDraw));
            Assert.AreEqual(0, m.Wins[0] + m.Wins[1]);
        }

        [Test]
        public void Cronometro_PausaDuranteHitstop()
        {
            var m = NewMatch();
            ToFighting(m);
            m.Sim[0].Pos = new UnityEngine.Vector2(0, 0);
            m.Sim[1].Pos = new UnityEngine.Vector2(1.0f, 0);
            int before = m.TimerTicks;
            m.Tick(Press(Buttons.Punch), InputFrame.Neutral);
            int ticks = 1;
            while (m.Sim.Hitstop == 0 && ticks < 30) { m.Tick(InputFrame.Neutral, InputFrame.Neutral); ticks++; }
            int hs = m.Sim.Hitstop;
            Assert.Greater(hs, 0);
            int atHit = m.TimerTicks;
            Run(m, hs);
            Assert.AreEqual(atHit, m.TimerTicks, "cronômetro não avança durante o hitstop");
            Assert.AreEqual(before - ticks, atHit);
        }

        [Test]
        public void AposVencedor_NaoHaDanoNemNovosGolpes()
        {
            var m = NewMatch();
            ToFighting(m);
            m.Sim[1].Health = 0;
            m.Tick(InputFrame.Neutral, InputFrame.Neutral);
            Assert.IsTrue(m.Sim.AttacksLocked);
            Assert.IsTrue(m.Sim.DamageLocked);
            int hp = m.Sim[0].Health;
            m.Tick(Press(Buttons.Punch), Press(Buttons.Punch));
            Run(m, 40);
            Assert.AreEqual(hp, m.Sim[0].Health);
        }

        // 8 ---------------------------------------------------------------
        [Test]
        public void Reset_LimpaEstado()
        {
            var t = new TestFight("giovani", "brutus");
            t.Place(0f, 1.1f);
            t.PressA(Buttons.Punch, thenTicks: 30);
            t.A.Energy = 77;
            t.B.BounceUsed = true;
            t.B.ComboHits = 5;
            t.A.Enter(FighterStateId.Knockdown);
            t.Sim.Step(Press(Buttons.Kick), InputFrame.Neutral); // pressionamento pendente no buffer
            Assert.IsTrue(t.A.Commands.Buffered(Buttons.Kick));
            t.Sim.ResetRound();
            Assert.AreEqual(t.A.Def.MaxHealth, t.A.Health);
            Assert.AreEqual(t.B.Def.MaxHealth, t.B.Health);
            Assert.AreEqual(0, t.A.Energy);
            Assert.AreEqual(0, t.B.ComboHits);
            Assert.IsFalse(t.B.BounceUsed);
            Assert.AreEqual(FighterStateId.Idle, t.A.State);
            Assert.AreEqual(0, t.Sim.Hitstop);
            t.Sim.Events.Clear();
            t.AllEvents.Clear();
            t.Idle(5);
            Assert.AreEqual(0, t.Count(CombatEventType.MoveStart), "buffer limpo no reset");
        }

        [Test]
        public void Revanche_StartMatchZeraPlacar()
        {
            var m = NewMatch();
            ToFighting(m);
            m.Sim[1].Health = 0;
            Run(m, 3);
            Assert.AreEqual(1, m.Wins[0]);
            m.StartMatch();
            Assert.AreEqual(0, m.Wins[0]);
            Assert.AreEqual(1, m.RoundNumberShown);
            Assert.AreEqual(-1, m.MatchWinner);
            Assert.AreEqual(MatchPhase.PreRound, m.Phase);
        }

        // 11 --------------------------------------------------------------
        [Test]
        public void Pausa_NaoInjetaComandosAoRetomar()
        {
            var acc = new InputAccumulator();
            acc.Sample(0, 0, Buttons.None, Buttons.None);
            acc.ConsumeTick();
            // Durante a pausa o jogador aperta e segura Soco.
            acc.Sample(0, 0, Buttons.Punch, Buttons.Punch);
            acc.ResumeWithHeld(Buttons.Punch);
            var f = acc.ConsumeTick();
            Assert.AreEqual(Buttons.None, f.Pressed);
            Assert.AreEqual(Buttons.Punch, f.Held);
            acc.Sample(0, 0, Buttons.Punch, Buttons.None);
            Assert.AreEqual(Buttons.None, acc.ConsumeTick().Pressed, "segurar não repete o golpe");
        }

        [Test]
        public void Acumulador_NaoPerdeToqueCurtoEntreTicks()
        {
            var acc = new InputAccumulator();
            acc.Sample(0, 0, Buttons.Kick, Buttons.Kick);  // quadro 1: aperta
            acc.Sample(0, 0, Buttons.None, Buttons.None);  // quadro 2: solta (antes do tick)
            var f = acc.ConsumeTick();
            Assert.AreEqual(Buttons.Kick, f.Pressed);
            Assert.AreEqual(Buttons.None, acc.ConsumeTick().Pressed);
        }

        [Test]
        public void Pausa_MatchNaoEvoluiSemTicks()
        {
            var m = NewMatch();
            ToFighting(m);
            int h = m.Sim.StateHash();
            int timer = m.TimerTicks;
            // Pausado: o runner simplesmente não chama Tick.
            Assert.AreEqual(h, m.Sim.StateHash());
            Assert.AreEqual(timer, m.TimerTicks);
        }

        // 12 --------------------------------------------------------------
        static List<InputFrame> Recording(int seed, int ticks)
        {
            var rng = new Random(seed);
            var list = new List<InputFrame>(ticks);
            Buttons held = Buttons.None;
            int x = 0, y = 0;
            for (int i = 0; i < ticks; i++)
            {
                if (rng.Next(8) == 0) x = rng.Next(3) - 1;
                if (rng.Next(14) == 0) y = rng.Next(3) - 1;
                Buttons pressed = Buttons.None;
                if (rng.Next(6) == 0) pressed = (Buttons)(1 << rng.Next(7));
                held = (rng.Next(3) == 0 ? Buttons.Block : Buttons.None) | pressed;
                list.Add(new InputFrame(x, y, held, pressed));
            }
            return list;
        }

        static int RunWithFps(double fps, List<InputFrame> p0, List<InputFrame> p1, bool jitter)
        {
            var m = NewMatch();
            var clock = new FixedStepClock();
            var rng = new Random(99);
            int tick = 0;
            while (tick < p0.Count)
            {
                double dt = 1.0 / fps;
                if (jitter) dt *= 0.6 + rng.NextDouble() * 0.8;
                int steps = clock.Advance(dt);
                for (int s = 0; s < steps && tick < p0.Count; s++, tick++)
                    m.Tick(p0[tick], p1[tick]);
            }
            return m.Sim.StateHash() ^ (m.TimerTicks * 31) ^ (m.Wins[0] * 7) ^ (m.Wins[1] * 13);
        }

        [Test]
        public void Replay_ResultadoEstavelEm30_60_120Fps()
        {
            var p0 = Recording(1, 3000);
            var p1 = Recording(2, 3000);
            int h60 = RunWithFps(60, p0, p1, false);
            Assert.AreEqual(h60, RunWithFps(30, p0, p1, false));
            Assert.AreEqual(h60, RunWithFps(120, p0, p1, false));
            Assert.AreEqual(h60, RunWithFps(144, p0, p1, true));
        }

        [Test]
        public void Relogio_LimitaPicosSemAcelerar()
        {
            var c = new FixedStepClock(60, 6);
            Assert.AreEqual(6, c.Advance(2.0));
            Assert.AreEqual(0, c.Advance(0.0));
            Assert.AreEqual(1, c.Advance(1.0 / 60.0));
            Assert.AreEqual(2, c.Advance(1.0 / 30.0));
        }

        // Treino ----------------------------------------------------------
        [Test]
        public void Treino_SemCronometroNemFimDeRound()
        {
            var m = NewMatch(training: true);
            Assert.AreEqual(MatchPhase.Fighting, m.Phase);
            m.Sim[1].Health = 0;
            Run(m, 5000);
            Assert.AreEqual(MatchPhase.Fighting, m.Phase);
            Assert.AreEqual(0, m.Events.Count);
        }
    }
}
