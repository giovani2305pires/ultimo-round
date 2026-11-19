using System.Collections.Generic;
using NUnit.Framework;
using UltimoRound.Combat;
using UltimoRound.Data;
using UnityEngine;

namespace UltimoRound.Tests
{
    /// <summary>Validação dos dados do elenco e smoke tests automatizados de todos os pares.</summary>
    public class RosterTests
    {
        static IEnumerable<string> Ids()
        {
            foreach (var id in DefaultRoster.FighterIds)
                if (DefaultRoster.Create(id) != null) yield return id;
        }

        [Test]
        public void Elenco_TodosOsSlotsObrigatorios()
        {
            foreach (var id in Ids())
            {
                var f = DefaultRoster.Create(id);
                foreach (var slot in FighterDefinition.AllSlots)
                {
                    if (slot == MoveSlot.ForwardHeavy) continue;
                    var m = f.GetMove(slot);
                    Assert.IsNotNull(m, $"{id} sem {slot}");
                    Assert.AreEqual(slot, m.Slot, $"{id}.{slot} com slot incorreto");
                    Assert.GreaterOrEqual(m.Startup, 1, $"{id}.{slot}");
                    Assert.GreaterOrEqual(m.Active, 1, $"{id}.{slot}");
                    Assert.GreaterOrEqual(m.Recovery, 1, $"{id}.{slot}");
                }
                Assert.GreaterOrEqual(f.Combos.Length, 2, id);
                Assert.GreaterOrEqual(f.Palettes.Length, 2, id);
                Assert.IsFalse(string.IsNullOrEmpty(f.Weakness), id);
            }
        }

        [Test]
        public void Elenco_GrafoDeCancelamentoSemCiclos()
        {
            foreach (var id in Ids())
            {
                var f = DefaultRoster.Create(id);
                foreach (var slot in FighterDefinition.AllSlots)
                {
                    var visited = new HashSet<MoveSlot>();
                    Assert.IsFalse(HasCycle(f, slot, visited, new HashSet<MoveSlot>()), $"{id}: ciclo a partir de {slot}");
                }
            }
        }

        static bool HasCycle(FighterDefinition f, MoveSlot s, HashSet<MoveSlot> done, HashSet<MoveSlot> stack)
        {
            var m = f.GetMove(s);
            if (m == null || done.Contains(s)) return false;
            if (!stack.Add(s)) return true;
            foreach (var next in m.CancelInto)
                if (stack.Contains(next) || HasCycle(f, next, done, stack)) return true;
            stack.Remove(s);
            done.Add(s);
            return false;
        }

        [Test]
        public void Elenco_AtaquesFortesPuniveisQuandoDefendidos()
        {
            foreach (var id in Ids())
            {
                var f = DefaultRoster.Create(id);
                foreach (var m in new[] { f.Heavy, f.SpecialNeutral, f.SpecialForward })
                {
                    if (m == null || m.IsThrow) continue;
                    int advantage = m.Blockstun - (m.Active - 1 + m.Recovery);
                    Assert.Less(advantage, -4, $"{id}.{m.Slot} deveria ser punível na defesa (vantagem {advantage})");
                }
            }
        }

        // Combos sugeridos ---------------------------------------------------

        internal static InputFrame InputFor(MoveSlot slot, int facing)
        {
            switch (slot)
            {
                case MoveSlot.Punch: return TestFight.Press(Buttons.Punch);
                case MoveSlot.Kick: return TestFight.Press(Buttons.Kick);
                case MoveSlot.Heavy: return TestFight.Press(Buttons.Heavy);
                case MoveSlot.ForwardHeavy: return TestFight.Press(Buttons.Heavy, facing);
                case MoveSlot.CrouchPunch: return TestFight.Press(Buttons.Punch, 0, -1);
                case MoveSlot.Low: return TestFight.Press(Buttons.Kick, 0, -1);
                case MoveSlot.AntiAir: return TestFight.Press(Buttons.Heavy, 0, -1);
                case MoveSlot.SpecialNeutral: return TestFight.Press(Buttons.Special);
                case MoveSlot.SpecialForward: return TestFight.Press(Buttons.Special, facing);
                case MoveSlot.Signature: return TestFight.Press(Buttons.Signature);
                case MoveSlot.Grab: return TestFight.Press(Buttons.Grab);
                default: return InputFrame.Neutral;
            }
        }

        /// <summary>Executa a sequência apertando o próximo golpe logo após o contato do anterior.</summary>
        internal static int PerformCombo(TestFight t, MoveSlot[] seq)
        {
            int maxHits = 0;
            var first = InputFor(seq[0], t.A.Facing);
            int holdY = first.Y;
            t.Step(first, InputFrame.Neutral);
            int idx = 1;
            for (int i = 0; i < 240; i++)
            {
                var input = InputFrame.Neutral;
                if (idx < seq.Length && t.A.State == FighterStateId.Attack && t.A.Move != null &&
                    t.A.Move.Slot == seq[idx - 1] && t.A.MoveContact)
                {
                    input = InputFor(seq[idx], t.A.Facing);
                    holdY = input.Y;
                    idx++;
                }
                // O jogador mantém a direção (ex.: baixo) do último comando até o próximo.
                var keep = input.Pressed == Buttons.None ? new InputFrame(0, holdY, Buttons.None, Buttons.None) : input;
                t.Step(keep, InputFrame.Neutral);
                maxHits = Mathf.Max(maxHits, t.B.ComboHits);
            }
            // Golpes que derrubam encerram o combo no mesmo tick: use também o evento de fim de combo.
            foreach (var e in t.AllEvents)
                if (e.Type == CombatEventType.ComboEnd && e.Target == 1) maxHits = Mathf.Max(maxHits, e.ComboHits);
            return maxHits;
        }

        [Test]
        public void CombosSugeridos_ConectamACurtaDistancia()
        {
            foreach (var id in Ids())
            {
                var f = DefaultRoster.Create(id);
                foreach (var combo in f.Combos)
                {
                    foreach (var opp in Ids())
                    {
                        var t = new TestFight(DefaultRoster.Create(id), DefaultRoster.Create(opp));
                        float gap = t.A.Def.PushHalfWidth + t.B.Def.PushHalfWidth + 0.25f;
                        t.Place(0f, gap);
                        int hits = PerformCombo(t, combo.Sequence);
                        int expected = 0;
                        foreach (var s in combo.Sequence) expected += Mathf.Max(1, f.GetMove(s).Hits);
                        Assert.AreEqual(expected, hits, $"{id} '{combo.Name}' contra {opp}");
                    }
                }
            }
        }

        // Smoke test de todos os pares (inclui espelhados) ---------------------

        [Test]
        public void Smoke_TodosOsPares_EntradasAleatorias()
        {
            var ids = new List<string>(Ids());
            foreach (var a in ids)
            foreach (var b in ids)
            {
                var cfg = DefaultRoster.CreateConfig();
                var m = new MatchController(cfg, DefaultRoster.Create(a), DefaultRoster.Create(b), 0, a == b ? 1 : 0, false);
                var rng = new System.Random(a.GetHashCode() ^ (b.GetHashCode() * 31));
                for (int tick = 0; tick < 20000 && m.Phase != MatchPhase.MatchOver; tick++)
                {
                    m.Tick(RandomInput(rng), RandomInput(rng));
                    for (int i = 0; i < 2; i++)
                    {
                        var f = m.Sim[i];
                        Assert.That(f.Health, Is.InRange(0, f.Def.MaxHealth), $"{a} x {b}");
                        Assert.That(f.Energy, Is.InRange(0, cfg.MaxEnergy));
                        Assert.That(Mathf.Abs(f.Pos.x), Is.LessThanOrEqualTo(cfg.ArenaHalfWidth + 1e-3f));
                        Assert.That(f.Pos.y, Is.GreaterThanOrEqualTo(0f));
                        Assert.That(f.ComboHits, Is.LessThanOrEqualTo(cfg.MaxComboHits + cfg.MaxJuggleHits + 2), "combo infinito?");
                    }
                    var p = m.Sim[0];
                    var q = m.Sim[1];
                    if (!p.Airborne && !q.Airborne && !p.IsDown && !q.IsDown)
                        Assert.That(Mathf.Abs(p.Pos.x - q.Pos.x), Is.GreaterThanOrEqualTo(p.Def.PushHalfWidth + q.Def.PushHalfWidth - 0.02f),
                            $"{a} x {b}: corpos sobrepostos no chão (tick {tick})");
                }
            }
        }

        [Test]
        public void Cpu_TodosOsParesEDificuldades_PartidaTermina()
        {
            var ids = new List<string>(Ids());
            foreach (Core.CpuDifficulty diff in System.Enum.GetValues(typeof(Core.CpuDifficulty)))
            foreach (var a in ids)
            foreach (var b in ids)
            {
                var cfg = DefaultRoster.CreateConfig();
                var m = new MatchController(cfg, DefaultRoster.Create(a), DefaultRoster.Create(b), 0, a == b ? 1 : 0, false);
                var c0 = new AI.CpuBrain(diff, 11);
                var c1 = new AI.CpuBrain(diff, 23);
                int hits = 0, blocks = 0;
                int tick = 0;
                for (; tick < 60 * 60 * 8 && m.Phase != MatchPhase.MatchOver; tick++)
                {
                    bool allow = m.Phase == MatchPhase.Fighting;
                    var f0 = c0.Tick(m.Sim, 0, allow);
                    var f1 = c1.Tick(m.Sim, 1, allow);
                    m.Tick(f0, f1);
                    foreach (var e in m.Sim.Events)
                    {
                        if (e.Type == CombatEventType.Hit) hits++;
                        else if (e.Type == CombatEventType.Block) blocks++;
                    }
                    m.Sim.Events.Clear();
                    m.Events.Clear();
                }
                Assert.AreEqual(MatchPhase.MatchOver, m.Phase, $"{diff} {a} x {b}: partida não terminou");
                Assert.Greater(hits, 5, $"{diff} {a} x {b}: CPU quase não acerta");
                if (diff != Core.CpuDifficulty.Easy)
                    Assert.Greater(blocks, 0, $"{diff} {a} x {b}: CPU nunca defende");
            }
        }

        static InputFrame RandomInput(System.Random rng)
        {
            int x = rng.Next(3) - 1;
            int y = rng.Next(10) == 0 ? 1 : (rng.Next(5) == 0 ? -1 : 0);
            Buttons pressed = rng.Next(4) == 0 ? (Buttons)(1 << rng.Next(7)) : Buttons.None;
            Buttons held = pressed | (rng.Next(4) == 0 ? Buttons.Block : Buttons.None);
            return new InputFrame(x, y, held, pressed);
        }
    }
}
