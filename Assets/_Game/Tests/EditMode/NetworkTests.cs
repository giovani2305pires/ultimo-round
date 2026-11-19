using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using NUnit.Framework;
using UltimoRound.AI;
using UltimoRound.Combat;
using UltimoRound.Core;
using UltimoRound.Data;
using UltimoRound.Net;

namespace UltimoRound.Tests
{
    public class NetworkTests
    {
        static MatchController NewMatch(string a = "giovani", string b = "brutus")
        {
            return new MatchController(DefaultRoster.CreateConfig(), DefaultRoster.Create(a), DefaultRoster.Create(b), 0, a == b ? 1 : 0, false);
        }

        static void ClearEvents(MatchController m)
        {
            m.Sim.Events.Clear();
            m.Events.Clear();
        }

        static NetReader AfterHeader(NetWriter w, NetMessage expected)
        {
            var r = new NetReader(w.Data, w.Length);
            Assert.IsTrue(NetProtocol.ReadHeader(r, out var type));
            Assert.AreEqual(expected, type);
            return r;
        }

        [Test]
        public void Replica_FicaIgualAoAnfitriao_PartidaInteira_TodosOsPares()
        {
            string[] ids = { "giovani", "brutus", "faisca", "vertice" };
            foreach (var a in ids)
            foreach (var b in ids)
            {
                var host = NewMatch(a, b);
                var replica = NewMatch(a, b);
                ClearEvents(replica);
                var netHost = new NetFightHost();
                var netGuest = new NetFightGuest { BufferTarget = 1 };
                var writer = new NetWriter();
                var c0 = new CpuBrain(CpuDifficulty.Hard, 11);
                var c1 = new CpuBrain(CpuDifficulty.Hard, 23);
                int hostEvents = 0, replicaEvents = 0;
                netHost.CollectEvents(host);
                hostEvents += host.Sim.Events.Count + host.Events.Count;
                ClearEvents(host);

                for (int t = 0; t < 60 * 60 * 8 && host.Phase != MatchPhase.MatchOver; t++)
                {
                    bool allow = host.Phase == MatchPhase.Fighting;
                    netHost.BeginTick();
                    host.Tick(c0.Tick(host.Sim, 0, allow), c1.Tick(host.Sim, 1, allow));
                    netHost.CollectEvents(host);
                    hostEvents += host.Sim.Events.Count + host.Events.Count;
                    ClearEvents(host);
                    netHost.WriteSnapshot(writer, host);
                    Assert.LessOrEqual(writer.Length, NetProtocol.MaxPacket);

                    netGuest.ReadSnapshot(AfterHeader(writer, NetMessage.Snapshot));
                    Assert.IsTrue(netGuest.Step(replica), $"{a} x {b}: sem estado no tick {t}");
                    replicaEvents += replica.Sim.Events.Count + replica.Events.Count;
                    ClearEvents(replica);

                    Assert.AreEqual(host.Sim.StateHash(), replica.Sim.StateHash(), $"{a} x {b}: divergiu no tick {t}");
                    Assert.AreEqual(host.Phase, replica.Phase);
                    Assert.AreEqual(host.TimerTicks, replica.TimerTicks);
                    Assert.AreEqual(host.Wins[0], replica.Wins[0]);
                    Assert.AreEqual(host.Wins[1], replica.Wins[1]);
                    for (int i = 0; i < 2; i++)
                    {
                        // Cada lado tem as próprias instâncias dos dados: compara pela identidade do golpe.
                        Assert.AreEqual(host.Sim[i].Move?.Id, replica.Sim[i].Move?.Id, $"{a} x {b}: golpe diferente no tick {t}");
                    }

                    // Confirma os eventos, como o pacote de comandos faz.
                    netGuest.WriteInput(writer, InputFrame.Neutral);
                    netHost.ReadInput(AfterHeader(writer, NetMessage.Input));
                }
                Assert.AreEqual(MatchPhase.MatchOver, host.Phase, $"{a} x {b}: partida não terminou");
                Assert.AreEqual(hostEvents, replicaEvents, $"{a} x {b}: número de eventos");
                Assert.AreEqual(0, netHost.PendingEvents, $"{a} x {b}: eventos sem confirmação");
            }
        }

        [Test]
        public void Eventos_ChegamUmaVezENaOrdem_ComPacotesPerdidosERepetidos()
        {
            var host = NewMatch();
            var replica = NewMatch();
            ClearEvents(replica);
            var netHost = new NetFightHost();
            var netGuest = new NetFightGuest { BufferTarget = 1 };
            var w = new NetWriter();
            var c0 = new CpuBrain(CpuDifficulty.Hard, 5);
            var c1 = new CpuBrain(CpuDifficulty.Hard, 6);
            var random = new Random(1234);
            var sent = new List<string>();
            var received = new List<string>();
            netHost.CollectEvents(host);
            foreach (var e in host.Events) sent.Add(e.ToString());
            ClearEvents(host);

            for (int t = 0; t < 60 * 60 * 8 && host.Phase != MatchPhase.MatchOver; t++)
            {
                bool allow = host.Phase == MatchPhase.Fighting;
                netHost.BeginTick();
                host.Tick(c0.Tick(host.Sim, 0, allow), c1.Tick(host.Sim, 1, allow));
                netHost.CollectEvents(host);
                foreach (var e in host.Sim.Events) sent.Add(e.ToString());
                foreach (var e in host.Events) sent.Add(e.ToString());
                ClearEvents(host);
                netHost.WriteSnapshot(w, host);

                // 25% de perda e 10% de repetição.
                if (random.NextDouble() > 0.25)
                {
                    netGuest.ReadSnapshot(AfterHeader(w, NetMessage.Snapshot));
                    if (random.NextDouble() < 0.1) netGuest.ReadSnapshot(AfterHeader(w, NetMessage.Snapshot));
                }
                netGuest.Step(replica);
                foreach (var e in replica.Sim.Events) received.Add(e.ToString());
                foreach (var e in replica.Events) received.Add(e.ToString());
                ClearEvents(replica);
                netGuest.WriteInput(w, InputFrame.Neutral);
                if (random.NextDouble() > 0.25) netHost.ReadInput(AfterHeader(w, NetMessage.Input));
            }

            // O último pacote costuma já estar confirmado; repete até sobrar nada para os que se perderam no fim.
            for (int i = 0; i < 24; i++)
            {
                netHost.BeginTick();
                netHost.WriteSnapshot(w, host);
                netGuest.ReadSnapshot(AfterHeader(w, NetMessage.Snapshot));
                netGuest.Step(replica);
                foreach (var e in replica.Sim.Events) received.Add(e.ToString());
                foreach (var e in replica.Events) received.Add(e.ToString());
                ClearEvents(replica);
            }
            sent.Sort();
            received.Sort();
            CollectionAssert.AreEqual(sent, received);
            Assert.AreEqual(host.Sim.StateHash(), replica.Sim.StateHash());
        }

        [Test]
        public void Comandos_ToqueNaoSePerde_ComPerdaEAtraso()
        {
            var host = new NetFightHost();
            var guest = new NetFightGuest();
            var w = new NetWriter();
            // Dois pacotes perdidos: o terceiro repete os comandos anteriores.
            guest.WriteInput(w, new InputFrame(0, 0, Buttons.Punch, Buttons.Punch));
            guest.WriteInput(w, new InputFrame(0, 0, Buttons.None, Buttons.None));
            guest.WriteInput(w, new InputFrame(1, 0, Buttons.Kick, Buttons.Kick));
            host.ReadInput(AfterHeader(w, NetMessage.Input));
            Assert.AreEqual(Buttons.Punch, host.NextRemoteInput().Pressed);
            Assert.AreEqual(Buttons.None, host.NextRemoteInput().Pressed);
            Assert.AreEqual(Buttons.Kick, host.NextRemoteInput().Pressed);

            // Sem comando novo: mantém o que está segurado, sem repetir o toque.
            var repeat = host.NextRemoteInput();
            Assert.AreEqual(Buttons.None, repeat.Pressed);
            Assert.AreEqual(Buttons.Kick, repeat.Held);
            Assert.AreEqual(1, repeat.X);
            Assert.AreEqual(1, host.MissingInputs);

            // Pacote repetido não duplica comandos.
            host.ReadInput(AfterHeader(w, NetMessage.Input));
            Assert.AreEqual(Buttons.None, host.NextRemoteInput().Pressed);

            // Fila atrasada encurta juntando os toques.
            var late = new NetFightHost();
            var lateGuest = new NetFightGuest();
            for (int i = 0; i < 8; i++)
            {
                var b = i == 1 ? Buttons.Heavy : Buttons.None;
                lateGuest.WriteInput(w, new InputFrame(0, 0, b, b));
            }
            late.ReadInput(AfterHeader(w, NetMessage.Input));
            Assert.IsTrue((late.NextRemoteInput().Pressed & Buttons.Heavy) != 0);
            Assert.Greater(late.MergedInputs, 0);
        }

        [Test]
        public void PacotesPerdidos_NaoEsvaziamAFila_NemAumentamOAtraso()
        {
            var host = NewMatch();
            var replica = NewMatch();
            ClearEvents(replica);
            var netHost = new NetFightHost();
            var netGuest = new NetFightGuest { BufferTarget = 2 };
            var w = new NetWriter();
            var random = new Random(77);
            // 5% de perda por um minuto, sem pacotes atrasados: nunca pode faltar estado por muito tempo.
            for (int t = 0; t < 3600; t++)
            {
                netHost.BeginTick();
                host.Tick(InputFrame.Neutral, InputFrame.Neutral);
                ClearEvents(host);
                netHost.WriteSnapshot(w, host);
                if (random.NextDouble() > 0.05) netGuest.ReadSnapshot(AfterHeader(w, NetMessage.Snapshot));
                netGuest.Step(replica);
                ClearEvents(replica);
            }
            Assert.Less(netGuest.Holds, 20, "só falta estado quando o pacote mais novo se perde");
            Assert.LessOrEqual(netGuest.BufferTarget, 4, "a perda não deve levar o atraso ao máximo");
            Assert.LessOrEqual(netHost.NetTick - netGuest.LastAppliedTick, 8u);
        }

        [Test]
        public void Revanche_ConvidadoDescartaOQueSobrouDaPartidaAnterior()
        {
            var host = NewMatch();
            var replica = NewMatch();
            ClearEvents(replica);
            var netHost = new NetFightHost();
            var netGuest = new NetFightGuest { BufferTarget = 1 };
            var w = new NetWriter();
            for (int t = 0; t < 5; t++)
            {
                netHost.BeginTick();
                host.Tick(InputFrame.Neutral, InputFrame.Neutral);
                netHost.WriteSnapshot(w, host);
                netGuest.ReadSnapshot(AfterHeader(w, NetMessage.Snapshot));
            }
            netHost.NewEpoch();
            host.StartMatch();
            netHost.CollectEvents(host);
            ClearEvents(host);
            netHost.BeginTick();
            host.Tick(InputFrame.Neutral, InputFrame.Neutral);
            netHost.WriteSnapshot(w, host);
            netGuest.ReadSnapshot(AfterHeader(w, NetMessage.Snapshot));

            Assert.AreEqual(1, netGuest.NewestEpoch);
            netGuest.PurgeOldEpoch();
            Assert.AreEqual(1, netGuest.BufferedCount);
            Assert.IsTrue(netGuest.Step(replica));
            Assert.AreEqual(1, replica.Events.Count, "só o anúncio do novo round");
            Assert.AreEqual(MatchEventType.RoundAnnounce, replica.Events[0].Type);
        }

        [Test]
        public void Pacote_Truncado_NaoQuebraNemAplica()
        {
            var host = NewMatch();
            var netHost = new NetFightHost();
            var w = new NetWriter();
            netHost.CollectEvents(host);
            netHost.BeginTick();
            netHost.WriteSnapshot(w, host);
            for (int cut = 0; cut < w.Length; cut += 3)
            {
                var guest = new NetFightGuest { BufferTarget = 1 };
                var r = new NetReader(w.Data, cut);
                if (!NetProtocol.ReadHeader(r, out var type)) continue;
                Assert.DoesNotThrow(() => guest.ReadSnapshot(r), $"corte em {cut}");
                Assert.IsFalse(guest.HasSnapshot, $"corte em {cut} não deveria gerar estado");
            }
            var other = new NetReader(new byte[] { 1, 2, 3, 4, 5 }, 5);
            Assert.IsFalse(NetProtocol.ReadHeader(other, out _), "pacote de outro programa é ignorado");
        }

        // ------------------------------------------------------------------ SOQUETES REAIS (127.0.0.1)

        const int TestPort = 47911;

        static void Pump(Stopwatch clock, Func<bool> until, params NetSession[] sessions)
        {
            float deadline = (float)clock.Elapsed.TotalSeconds + 5f;
            while (!until())
            {
                float now = (float)clock.Elapsed.TotalSeconds;
                Assert.Less(now, deadline, "tempo esgotado esperando a condição");
                foreach (var s in sessions) s.Update(now);
                Thread.Sleep(2);
            }
        }

        [Test]
        public void Conexao_Selecao_Luta_E_Saida_PorSoquete()
        {
            var clock = Stopwatch.StartNew();
            using (var host = new NetSession("1.0", "Anfitriao", TestPort))
            using (var guest = new NetSession("1.0", "Convidado", TestPort))
            {
                Assert.IsTrue(host.StartHost());
                Assert.IsTrue(guest.StartBrowse());
                Pump(clock, () => guest.Hosts.Count > 0, host, guest);
                Assert.AreEqual("Anfitriao", guest.Hosts[0].Name);
                Assert.IsFalse(guest.Hosts[0].Busy);

                guest.Join(IPAddress.Loopback);
                Pump(clock, () => host.Active && guest.Active, host, guest);
                Assert.AreEqual(0, host.LocalIndex);
                Assert.AreEqual(1, guest.LocalIndex);

                // Seleção
                guest.SetLocalSelection(3, 2, true);
                host.SetLocalSelection(1, 1, true);
                Pump(clock, () => host.Lobby.Ready[1] && guest.Lobby.Ready[0], host, guest);
                Assert.AreEqual(3, host.Lobby.Cursor[1]);
                Assert.AreEqual(2, host.Lobby.Palette[1]);
                Assert.AreEqual(1, guest.Lobby.Cursor[0]);

                // Início da luta
                host.Launch(1, 3, 1, 2);
                Pump(clock, () => guest.Lobby.Phase == NetLobbyPhase.Fight, host, guest);
                Assert.AreEqual(3, guest.Lobby.FinalFighter[1]);
                Assert.AreEqual(2, guest.Lobby.FinalPalette[1]);
                host.BeginFight();
                guest.BeginFight();
                guest.ReportSceneReady();
                Pump(clock, () => host.GuestSceneReady, host, guest);
                host.StartFightFlow();
                Pump(clock, () => guest.HostStartedFight, host, guest);

                // Estado e comandos
                var hostMatch = NewMatch();
                var replica = NewMatch();
                ClearEvents(replica);
                host.FightHost.BeginTick();
                hostMatch.Tick(InputFrame.Neutral, InputFrame.Neutral);
                host.SendSnapshot(hostMatch);
                Pump(clock, () => guest.FightGuest.BufferedCount > 0, host, guest);
                guest.FightGuest.BufferTarget = 1;
                Assert.IsTrue(guest.FightGuest.Step(replica));
                Assert.AreEqual(hostMatch.Sim.StateHash(), replica.Sim.StateHash());

                guest.SendInput(new InputFrame(1, 0, Buttons.Special, Buttons.Special));
                Pump(clock, () => host.FightHost.PendingEvents == 0 && host.FightHost.NextRemoteInput().Pressed == Buttons.Special, host, guest);

                // Eco medido
                Pump(clock, () => host.RttMs > 0f && guest.RttMs > 0f, host, guest);
                Assert.Less(guest.RttMs, 500f);

                // "Pronto" anterior à volta à seleção não dispara nova luta.
                host.ReturnToSelect();
                Pump(clock, () => guest.Lobby.Phase == NetLobbyPhase.Select, host, guest);
                Assert.IsFalse(host.Lobby.Ready[1], "aguarda o convidado escolher de novo");
                guest.SetLocalSelection(3, 2, true);
                Pump(clock, () => host.Lobby.Ready[1], host, guest);

                // Saída
                guest.Stop();
                Pump(clock, () => host.Status == NetStatus.Failed, host);
                StringAssert.Contains("saiu", host.FailReason);
            }
        }

        [Test]
        public void VersaoDiferente_E_AnfitriaoOcupado_SaoRecusados()
        {
            var clock = Stopwatch.StartNew();
            using (var host = new NetSession("1.0", "A", TestPort + 1))
            using (var old = new NetSession("0.9", "B", TestPort + 1))
            using (var first = new NetSession("1.0", "C", TestPort + 1))
            using (var late = new NetSession("1.0", "D", TestPort + 1))
            {
                Assert.IsTrue(host.StartHost());
                old.Join(IPAddress.Loopback);
                Pump(clock, () => old.Status == NetStatus.Failed, host, old);
                StringAssert.Contains("Versões diferentes", old.FailReason);
                Assert.AreEqual(NetStatus.Hosting, host.Status, "continua esperando");

                first.Join(IPAddress.Loopback);
                Pump(clock, () => first.Active, host, first);
                late.Join(IPAddress.Loopback);
                Pump(clock, () => late.Status == NetStatus.Failed, host, first, late);
                StringAssert.Contains("já está em uma partida", late.FailReason);
                Assert.IsTrue(first.Active);
            }
        }

        [Test]
        public void SilencioDoOutroLado_EncerraPorTempoLimite_E_PortaOcupadaAvisa()
        {
            var clock = Stopwatch.StartNew();
            using (var host = new NetSession("1.0", "A", TestPort + 2))
            using (var guest = new NetSession("1.0", "B", TestPort + 2))
            using (var second = new NetSession("1.0", "C", TestPort + 2))
            {
                Assert.IsTrue(host.StartHost());
                Assert.IsFalse(second.StartHost(), "a porta já está em uso");
                StringAssert.Contains("porta", second.FailReason);

                guest.Join(IPAddress.Loopback);
                Pump(clock, () => host.Active && guest.Active, host, guest);
                host.TimeoutSeconds = 0.3f;
                Pump(clock, () => host.Status == NetStatus.Failed, host);
                StringAssert.Contains("perdida", host.FailReason);
            }
        }

        [Test]
        public void Conexao_PrazoContaDoPrimeiroEnvio_NaoDoInicioDoPrograma()
        {
            using (var host = new NetSession("1.0", "A", TestPort + 4))
            using (var guest = new NetSession("1.0", "B", TestPort + 4))
            {
                Assert.IsTrue(host.StartHost());
                // O relógio do jogo já está em 30 s no primeiro quadro depois do pedido.
                guest.Join(IPAddress.Loopback);
                guest.Update(30f);
                Assert.AreEqual(NetStatus.Connecting, guest.Status);
            }
        }

        [Test]
        public void SemAnfitriao_ConexaoFalhaComExplicacao()
        {
            using (var guest = new NetSession("1.0", "B", TestPort + 3))
            {
                guest.Join(IPAddress.Loopback);
                // O relógio é simulado: não precisa esperar os 6 segundos reais.
                for (float t = 0f; t < 7f && guest.Status == NetStatus.Connecting; t += 0.25f) guest.Update(t);
                Assert.AreEqual(NetStatus.Failed, guest.Status);
                StringAssert.Contains("firewall", guest.FailReason);
            }
        }
    }
}
