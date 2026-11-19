using NUnit.Framework;
using UltimoRound.Combat;
using UltimoRound.Data;
using UnityEngine;
using static UltimoRound.Tests.TestFight;

namespace UltimoRound.Tests
{
    public class CombatRulesTests
    {
        // 1 ---------------------------------------------------------------
        [Test]
        public void GolpeComum_AplicaDanoUmaVez_MesmoComVariasHurtboxes()
        {
            var t = new TestFight();
            t.Place(0f, 1.0f);
            var jab = t.A.Def.Punch;
            // O hitbox do jab cruza corpo e cabeça ao mesmo tempo.
            Rect hit = jab.Hitbox.ToWorld(t.A.Pos, 1);
            int overl = 0;
            foreach (var hb in t.B.Def.StandHurtboxes) if (hit.Overlaps(hb.ToWorld(t.B.Pos, -1))) overl++;
            Assert.GreaterOrEqual(overl, 2, "pré-condição: hitbox deve tocar mais de uma hurtbox");

            t.PressA(Buttons.Punch, thenTicks: 40);
            Assert.AreEqual(t.B.Def.MaxHealth - jab.Damage, t.B.Health);
            Assert.AreEqual(1, t.Count(CombatEventType.Hit));
        }

        [Test]
        public void MultiHit_UsaIndicesExplicitos()
        {
            var t = new TestFight();
            t.Place(0f, 1.0f);
            t.A.Energy = t.Config.MaxEnergy;
            t.PressA(Buttons.Signature, thenTicks: 120);
            Assert.AreEqual(t.A.Def.Signature.Hits, t.Count(CombatEventType.Hit));
        }

        // 2 ---------------------------------------------------------------
        [Test]
        public void ForaDeAlcance_NaoAcerta()
        {
            var t = new TestFight();
            t.Place(0f, 3f);
            t.PressA(Buttons.Punch, thenTicks: 30);
            Assert.AreEqual(t.B.Def.MaxHealth, t.B.Health);
            Assert.AreEqual(0, t.Count(CombatEventType.Hit));
        }

        [Test]
        public void Startup_NaoAtivaHitbox_EAcertaNoPrimeiroTickAtivo()
        {
            var t = new TestFight();
            t.Place(0f, 1.0f);
            var jab = t.A.Def.Punch;
            t.PressA(Buttons.Punch, thenTicks: jab.Startup - 1);
            Assert.AreEqual(t.B.Def.MaxHealth, t.B.Health, "não pode acertar durante a preparação");
            t.Idle(1);
            Assert.Less(t.B.Health, t.B.Def.MaxHealth, "deve acertar no primeiro tick ativo");
        }

        [Test]
        public void Recuperacao_NaoAtivaHitbox()
        {
            var t = new TestFight();
            t.Place(0f, 1.8f);
            var jab = t.A.Def.Punch;
            t.PressA(Buttons.Punch, thenTicks: jab.Startup + jab.Active);
            Assert.AreEqual(FighterStateId.Attack, t.A.State);
            Assert.IsFalse(jab.IsActiveTick(t.A.MoveTick));
            t.B.Pos.x = 1.0f; // entra no alcance durante a recuperação
            t.Idle(jab.Recovery);
            Assert.AreEqual(t.B.Def.MaxHealth, t.B.Health);
        }

        [Test]
        public void JanelaAtiva_LimitesCorretos()
        {
            var m = DefaultRoster.Create("giovani").Kick;
            Assert.IsFalse(m.IsActiveTick(m.Startup - 1));
            Assert.IsTrue(m.IsActiveTick(m.Startup));
            Assert.IsTrue(m.IsActiveTick(m.Startup + m.Active - 1));
            Assert.IsFalse(m.IsActiveTick(m.Startup + m.Active));
        }

        // 3 ---------------------------------------------------------------
        static void Exchange(TestFight t, Buttons button, int x, int y, InputFrame defender, int ticks = 50)
        {
            t.Step(new InputFrame(x, y, button, button), defender);
            for (int i = 0; i < ticks; i++) t.Step(new InputFrame(x, y, Buttons.None, Buttons.None), defender);
        }

        [Test]
        public void Baixo_ExigeDefesaAgachada()
        {
            var t = new TestFight();
            t.Place(0f, 1.1f);
            Exchange(t, Buttons.Kick, 0, -1, Hold(0, 0, Buttons.Block));
            Assert.AreEqual(1, t.Count(CombatEventType.Hit), "baixo contra defesa em pé deve acertar");

            t = new TestFight();
            t.Place(0f, 1.1f);
            Exchange(t, Buttons.Kick, 0, -1, Hold(0, -1, Buttons.Block));
            Assert.AreEqual(1, t.Count(CombatEventType.Block));
            Assert.AreEqual(0, t.Count(CombatEventType.Hit));
        }

        [Test]
        public void Overhead_ExigeDefesaEmPe()
        {
            var t = new TestFight("brutus", "giovani");
            t.Place(0f, 1.3f);
            Exchange(t, Buttons.Heavy, 1, 0, Hold(0, -1, Buttons.Block));
            Assert.AreEqual(1, t.Count(CombatEventType.Hit), "overhead contra defesa agachada deve acertar");

            t = new TestFight("brutus", "giovani");
            t.Place(0f, 1.3f);
            Exchange(t, Buttons.Heavy, 1, 0, Hold(0, 0, Buttons.Block));
            Assert.AreEqual(1, t.Count(CombatEventType.Block));
        }

        [Test]
        public void Medio_DefendidoEmPeOuAgachado()
        {
            foreach (int y in new[] { 0, -1 })
            {
                var t = new TestFight();
                t.Place(0f, 1.1f);
                Exchange(t, Buttons.Kick, 0, 0, Hold(0, y, Buttons.Block));
                Assert.AreEqual(1, t.Count(CombatEventType.Block), "y=" + y);
            }
        }

        [Test]
        public void Alto_PassaPorCimaDeAgachado()
        {
            var t = new TestFight();
            t.Place(0f, 1.0f);
            Exchange(t, Buttons.Punch, 0, 0, Hold(0, -1));
            Assert.AreEqual(0, t.Count(CombatEventType.Hit));
        }

        [Test]
        public void Agarrao_VenceDefesa_EAplicaDanoAposJanela()
        {
            var t = new TestFight();
            t.Place(0f, 0.9f);
            Exchange(t, Buttons.Grab, 0, 0, Hold(0, 0, Buttons.Block), 60);
            Assert.AreEqual(1, t.Count(CombatEventType.GrabStart));
            Assert.AreEqual(1, t.Count(CombatEventType.ThrowHit));
            Assert.AreEqual(t.B.Def.MaxHealth - t.A.Def.Grab.Damage, t.B.Health);
        }

        [Test]
        public void Agarrao_EscapeNaJanela()
        {
            var t = new TestFight();
            t.Place(0f, 0.9f);
            t.Step(Press(Buttons.Grab), InputFrame.Neutral);
            int guard = 0;
            while (t.Count(CombatEventType.GrabStart) == 0 && guard++ < 20) t.Step(InputFrame.Neutral, InputFrame.Neutral);
            Assert.AreEqual(1, t.Count(CombatEventType.GrabStart));
            t.Idle(3);
            t.Step(InputFrame.Neutral, Press(Buttons.Grab));
            t.Idle(40);
            Assert.AreEqual(1, t.Count(CombatEventType.ThrowTech));
            Assert.AreEqual(0, t.Count(CombatEventType.ThrowHit));
            Assert.AreEqual(t.B.Def.MaxHealth, t.B.Health);
            Assert.AreEqual(FighterStateId.Idle, t.A.State);
            Assert.AreEqual(FighterStateId.Idle, t.B.State);
        }

        [Test]
        public void Agarrao_FalhaForaDeAlcance_NoAr_EEmHitstun()
        {
            var t = new TestFight();
            t.Place(0f, 1.6f);
            Exchange(t, Buttons.Grab, 0, 0, InputFrame.Neutral);
            Assert.AreEqual(0, t.Count(CombatEventType.GrabStart), "fora de alcance");

            t = new TestFight();
            t.Place(0f, 0.9f);
            for (int i = 0; i < 8; i++) t.Step(InputFrame.Neutral, Hold(0, 1));
            Assert.IsTrue(t.B.Airborne);
            Exchange(t, Buttons.Grab, 0, 0, InputFrame.Neutral, 8);
            Assert.AreEqual(0, t.Count(CombatEventType.GrabStart), "alvo no ar");

            t = new TestFight();
            t.Place(0f, 0.9f);
            t.B.StunTicks = 40;
            t.B.Enter(FighterStateId.Hitstun);
            Exchange(t, Buttons.Grab, 0, 0, InputFrame.Neutral, 8);
            Assert.AreEqual(0, t.Count(CombatEventType.GrabStart), "alvo em hitstun");
        }

        [Test]
        public void Agarrao_SimultaneoResultaEmEscape()
        {
            var t = new TestFight();
            t.Place(0f, 0.9f);
            t.Step(Press(Buttons.Grab), Press(Buttons.Grab));
            t.Idle(30);
            Assert.AreEqual(1, t.Count(CombatEventType.ThrowTech));
            Assert.AreEqual(t.A.Def.MaxHealth, t.A.Health);
            Assert.AreEqual(t.B.Def.MaxHealth, t.B.Health);
        }

        // 4 ---------------------------------------------------------------
        [Test]
        public void NaoAtacaEmKO_Derrubado_OuBloqueado()
        {
            var t = new TestFight();
            t.Place(0f, 1.0f);
            t.B.Enter(FighterStateId.Knockdown);
            t.Step(InputFrame.Neutral, Press(Buttons.Punch));
            Assert.AreEqual(0, t.Count(CombatEventType.MoveStart));

            t.A.Health = 0;
            t.A.Enter(FighterStateId.KO);
            t.Step(Press(Buttons.Punch), InputFrame.Neutral);
            Assert.AreEqual(0, t.Count(CombatEventType.MoveStart));

            var u = new TestFight();
            u.Sim.AttacksLocked = true;
            u.Step(Press(Buttons.Punch), Press(Buttons.Kick));
            u.Idle(10);
            Assert.AreEqual(0, u.Count(CombatEventType.MoveStart));
        }

        [Test]
        public void Derrubado_NaoRecebeDano()
        {
            var t = new TestFight();
            t.Place(0f, 1.0f);
            t.B.Enter(FighterStateId.Knockdown);
            t.PressA(Buttons.Kick, thenTicks: 20);
            Assert.AreEqual(t.B.Def.MaxHealth, t.B.Health);
        }

        // 5 ---------------------------------------------------------------
        [Test]
        public void Energia_GanhoEmAcertoDefesaENadaNoVazio()
        {
            var t = new TestFight();
            t.Place(0f, 1.0f);
            t.PressA(Buttons.Punch, thenTicks: 30);
            int gain = t.A.Def.Punch.EnergyGain;
            Assert.AreEqual(gain, t.A.Energy);
            Assert.AreEqual(Mathf.RoundToInt(gain * t.Config.DefenderEnergyFactor), t.B.Energy);

            t = new TestFight();
            t.Place(0f, 1.0f);
            Exchange(t, Buttons.Punch, 0, 0, Hold(0, 0, Buttons.Block), 30);
            Assert.AreEqual(Mathf.RoundToInt(gain * t.Config.BlockEnergyFactor), t.A.Energy);

            t = new TestFight();
            t.Place(0f, 3f);
            t.PressA(Buttons.Punch, thenTicks: 30);
            Assert.AreEqual(0, t.A.Energy);
        }

        [Test]
        public void Assinatura_BloqueadaSemEnergia_ConsomeBarraCheia()
        {
            var t = new TestFight();
            t.Place(0f, 2.5f);
            t.A.Energy = t.Config.MaxEnergy - 1;
            t.PressA(Buttons.Signature, thenTicks: 5);
            Assert.AreEqual(0, t.Count(CombatEventType.MoveStart));
            Assert.AreEqual(1, t.Count(CombatEventType.SignatureDenied));
            Assert.AreEqual(t.Config.MaxEnergy - 1, t.A.Energy);

            t.A.Energy = t.Config.MaxEnergy;
            t.PressA(Buttons.Signature, thenTicks: 2);
            Assert.AreEqual(1, t.Count(CombatEventType.MoveStart));
            Assert.AreEqual(0, t.A.Energy);
            Assert.AreEqual(MoveSlot.Signature, t.A.Move.Slot);
        }

        // 6 ---------------------------------------------------------------
        [Test]
        public void Combo_ReducaoDeDanoComPiso()
        {
            var t = new TestFight();
            var c = t.Config;
            Assert.AreEqual(1f, t.Sim.ComboScale(1));
            Assert.AreEqual(1f, t.Sim.ComboScale(c.ScalingStartHit));
            Assert.AreEqual(1f - c.ScalingStep, t.Sim.ComboScale(c.ScalingStartHit + 1), 1e-5);
            Assert.AreEqual(c.ScalingFloor, t.Sim.ComboScale(50), 1e-5);
        }

        [Test]
        public void Combo_JanelaDeCancelamentoSoAposContato()
        {
            // Sem contato, Soco não cancela em Chute: o chute só sai após o fim do soco.
            var t = new TestFight();
            t.Place(0f, 3f);
            t.Step(Press(Buttons.Punch), InputFrame.Neutral);
            t.Idle(t.A.Def.Punch.Startup + 1);
            t.Step(Press(Buttons.Kick), InputFrame.Neutral);
            Assert.AreEqual(MoveSlot.Punch, t.A.Move.Slot);
        }

        [Test]
        public void Rebote_NoMaximoUmPorCombo()
        {
            var t = new TestFight();
            t.Place(1.5f, 2.6f);
            t.Step(Press(Buttons.Special), InputFrame.Neutral);
            t.Idle(60);
            Assert.AreEqual(1, t.Count(CombatEventType.RopeBounce));

            var u = new TestFight();
            u.Place(1.5f, 2.6f);
            u.B.BounceUsed = true;
            u.B.ComboHits = 1;
            u.B.StunTicks = 30;
            u.B.Enter(FighterStateId.Hitstun);
            u.Step(Press(Buttons.Special), InputFrame.Neutral);
            u.Idle(60);
            Assert.AreEqual(0, u.Count(CombatEventType.RopeBounce));
        }

        [Test]
        public void Rebote_NaoOcorreEmDefesa()
        {
            var t = new TestFight();
            t.Place(1.5f, 2.6f);
            Exchange(t, Buttons.Special, 0, 0, Hold(0, 0, Buttons.Block), 60);
            Assert.AreEqual(1, t.Count(CombatEventType.Block));
            Assert.AreEqual(0, t.Count(CombatEventType.RopeBounce));
        }

        [Test]
        public void Juggle_LimiteTornaAlvoImune()
        {
            var t = new TestFight();
            t.Place(0f, 1.0f);
            t.B.Airborne = true;
            t.B.Pos.y = 1.0f;
            t.B.Vel = new Vector2(0, 3f);
            t.B.JuggleHits = t.Config.MaxJuggleHits - 1;
            t.B.ComboHits = 2;
            t.B.Enter(FighterStateId.Launched);
            t.Sim.Step(Press(Buttons.Heavy, 0, -1), InputFrame.Neutral);
            for (int i = 0; i < 12 && t.B.ComboHits == 2; i++) t.Sim.Step(InputFrame.Neutral, InputFrame.Neutral);
            Assert.IsTrue(t.B.JuggleImmune);
        }

        [Test]
        public void Combo_LimiteForcaDerrubada()
        {
            var t = new TestFight();
            t.Place(0f, 1.0f);
            t.B.ComboHits = t.Config.MaxComboHits - 1;
            t.B.StunTicks = 20;
            t.B.Enter(FighterStateId.Hitstun);
            t.PressA(Buttons.Punch, thenTicks: 10);
            Assert.AreEqual(FighterStateId.Launched, t.B.State);
            Assert.IsTrue(t.B.JuggleImmune);
        }

        // 9 ---------------------------------------------------------------
        [Test]
        public void Espelhado_NaoCompartilhaEstado()
        {
            var def = DefaultRoster.Create("giovani");
            var t = new TestFight(def, def);
            t.Place(0f, 1.0f);
            t.PressA(Buttons.Punch, thenTicks: 30);
            Assert.AreEqual(def.MaxHealth, t.A.Health);
            Assert.Less(t.B.Health, def.MaxHealth);
            Assert.AreNotEqual(t.A.Energy, t.B.Energy);
            Assert.AreNotSame(t.A.Commands, t.B.Commands);
            Assert.AreNotEqual(t.A.PaletteIndex, t.B.PaletteIndex);
        }

        // 10 --------------------------------------------------------------
        [Test]
        public void Orientacao_ComandosRelativos()
        {
            var t = new TestFight();
            t.Place(1.0f, -1.0f);
            t.Idle(1);
            Assert.AreEqual(-1, t.A.Facing);
            float x0 = t.A.Pos.x;
            for (int i = 0; i < 10; i++) t.Step(Hold(-1, 0), InputFrame.Neutral);
            Assert.AreEqual(x0 - t.A.Def.WalkSpeed * 10 / 60f, t.A.Pos.x, 0.01f, "andar para a esquerda = frente");
            x0 = t.A.Pos.x;
            for (int i = 0; i < 10; i++) t.Step(Hold(1, 0), InputFrame.Neutral);
            Assert.AreEqual(x0 + t.A.Def.BackWalkSpeed * 10 / 60f, t.A.Pos.x, 0.01f, "andar para a direita = trás");

            // Especial + frente é relativo à orientação.
            var u = new TestFight();
            u.Place(1.0f, -1.0f);
            u.Idle(1);
            u.Step(Press(Buttons.Special, -1, 0), InputFrame.Neutral);
            Assert.AreEqual(MoveSlot.SpecialForward, u.A.Move.Slot);
        }

        [Test]
        public void TrocaDeLadoNoAr_OrientacaoMudaSoAoAterrissar()
        {
            var t = new TestFight();
            t.Place(-0.6f, 0.5f);
            bool crossedInAir = false;
            for (int i = 0; i < 80; i++)
            {
                t.Step(Hold(1, i < 4 ? 1 : 0), InputFrame.Neutral);
                if (t.A.Airborne && t.A.Pos.x > t.B.Pos.x)
                {
                    crossedInAir = true;
                    Assert.AreEqual(1, t.A.Facing, "orientação travada no ar");
                }
                if (!t.A.Airborne && t.A.State == FighterStateId.Idle) break;
            }
            Assert.IsTrue(crossedInAir, "deve cruzar por cima");
            Assert.Greater(t.A.Pos.x, t.B.Pos.x);
            t.Idle(2);
            Assert.AreEqual(-1, t.A.Facing);
            Assert.AreEqual(1, t.B.Facing);
        }

        [Test]
        public void Pushbox_NaoAtravessaNoChao()
        {
            var t = new TestFight();
            t.Place(-0.5f, 0.5f);
            for (int i = 0; i < 120; i++) t.Step(Hold(1, 0), Hold(-1, 0));
            float min = t.A.Def.PushHalfWidth + t.B.Def.PushHalfWidth;
            Assert.GreaterOrEqual(t.B.Pos.x - t.A.Pos.x, min - 0.001f);
        }

        [Test]
        public void Pushbox_NaCordaEmpurraSemPrender()
        {
            var t = new TestFight();
            float lim = t.Config.ArenaHalfWidth;
            t.Place(lim - 1f, lim - 0.2f);
            for (int i = 0; i < 120; i++) t.Step(Hold(1, 0), InputFrame.Neutral);
            Assert.LessOrEqual(t.B.Pos.x, lim + 1e-4f);
            Assert.GreaterOrEqual(t.B.Pos.x - t.A.Pos.x, t.A.Def.PushHalfWidth + t.B.Def.PushHalfWidth - 0.001f);
            // e não fica preso: A consegue se afastar andando para trás
            float gap = t.B.Pos.x - t.A.Pos.x;
            for (int i = 0; i < 30; i++) t.Step(Hold(-1, 0), InputFrame.Neutral);
            Assert.Greater(t.B.Pos.x - t.A.Pos.x, gap + 0.5f);
        }

        // Golpes simultâneos -----------------------------------------------
        [Test]
        public void Troca_GolpesSimultaneosAcertamAmbos()
        {
            var t = new TestFight();
            t.Place(0f, 1.0f);
            t.Step(Press(Buttons.Punch), Press(Buttons.Punch));
            t.Idle(30);
            Assert.AreEqual(2, t.Count(CombatEventType.Hit));
            Assert.AreEqual(t.A.Health, t.B.Health);
        }
    }
}
