using System.Collections.Generic;
using UnityEngine;

namespace UltimoRound.Combat
{
    public enum MatchPhase
    {
        PreRound,
        Fighting,
        RoundOver,
        MatchOver,
    }

    public enum MatchEventType
    {
        RoundAnnounce,
        Fight,
        KO,
        DoubleKO,
        TimeUp,
        RoundWon,
        RoundDraw,
        MatchWon,
    }

    public struct MatchEvent
    {
        public MatchEventType Type;
        public int Round;
        public int Winner; // -1 = empate / n/a
        public override string ToString() => $"{Type} round={Round} winner={Winner}";
    }

    public enum RoundEndReason { None, KO, DoubleKO, TimeUp }

    /// <summary>
    /// Regras da partida: melhor de N rounds, cronômetro, KO, tempo esgotado, empate (repete o round sem pontuar).
    /// Não conhece apresentação nem dispositivos.
    /// </summary>
    public sealed class MatchController
    {
        public readonly FightSimulation Sim;
        public readonly CombatConfig Config;
        public readonly bool Training;
        public readonly List<MatchEvent> Events = new List<MatchEvent>(16);
        public readonly int[] Wins = new int[2];

        public MatchPhase Phase { get; private set; }
        public int PhaseTick { get; private set; }
        public int Round { get; private set; }
        public int RoundNumberShown { get; private set; }
        public int TimerTicks { get; private set; }
        public int MatchWinner { get; private set; } = -1;
        public int RoundWinner { get; private set; } = -1;
        public RoundEndReason LastReason { get; private set; }
        public int DrawsThisMatch { get; private set; }

        public bool TimerEnabled => !Training;
        public int TimerSeconds => Mathf.CeilToInt(TimerTicks / (float)CombatConfig.TickRate);

        public MatchController(CombatConfig config, FighterDefinition a, FighterDefinition b, int paletteA, int paletteB, bool training)
        {
            Config = config;
            Training = training;
            Sim = new FightSimulation(config, a, b, paletteA, paletteB);
            StartMatch();
        }

        public void StartMatch()
        {
            Wins[0] = Wins[1] = 0;
            Round = 0;
            RoundNumberShown = 0;
            MatchWinner = -1;
            DrawsThisMatch = 0;
            Events.Clear();
            Sim.Events.Clear();
            BeginRound();
        }

        void BeginRound()
        {
            Round++;
            RoundNumberShown++;
            Sim.ResetRound();
            TimerTicks = Config.RoundSeconds * CombatConfig.TickRate;
            RoundWinner = -1;
            LastReason = RoundEndReason.None;
            if (Training)
            {
                SetPhase(MatchPhase.Fighting);
                return;
            }
            Sim.AttacksLocked = true;
            SetPhase(MatchPhase.PreRound);
            Events.Add(new MatchEvent { Type = MatchEventType.RoundAnnounce, Round = RoundNumberShown, Winner = -1 });
        }

        void SetPhase(MatchPhase p)
        {
            Phase = p;
            PhaseTick = 0;
        }

        /// <summary>Encurta a introdução do round (pular).</summary>
        public void SkipPreRound()
        {
            if (Phase == MatchPhase.PreRound && PhaseTick < Config.PreRoundTicks - 30)
                PhaseTick = Config.PreRoundTicks - 30;
        }

        public void Tick(in InputFrame p0, in InputFrame p1)
        {
            PhaseTick++;
            switch (Phase)
            {
                case MatchPhase.PreRound:
                    Sim.Step(InputFrame.Neutral, InputFrame.Neutral);
                    if (PhaseTick >= Config.PreRoundTicks)
                    {
                        Sim.AttacksLocked = false;
                        Sim.ClearInputBuffers();
                        SetPhase(MatchPhase.Fighting);
                        Events.Add(new MatchEvent { Type = MatchEventType.Fight, Round = RoundNumberShown, Winner = -1 });
                    }
                    break;

                case MatchPhase.Fighting:
                {
                    bool frozen = Sim.Hitstop > 0 || Sim.FreezeTicks > 0;
                    Sim.Step(p0, p1);
                    if (!Training)
                    {
                        // O cronômetro pausa durante hitstop e congelamento da assinatura.
                        if (!frozen && TimerTicks > 0) TimerTicks--;
                        CheckRoundEnd();
                    }
                    break;
                }

                case MatchPhase.RoundOver:
                    Sim.Step(InputFrame.Neutral, InputFrame.Neutral);
                    if (PhaseTick == 70 && RoundWinner >= 0) Sim.SetVictory(RoundWinner);
                    if (PhaseTick > 70 && RoundWinner >= 0 && Sim[RoundWinner].State != FighterStateId.Victory)
                        Sim.SetVictory(RoundWinner);
                    if (PhaseTick >= Config.RoundOverTicks)
                    {
                        if (MatchWinner >= 0)
                        {
                            SetPhase(MatchPhase.MatchOver);
                            Events.Add(new MatchEvent { Type = MatchEventType.MatchWon, Round = RoundNumberShown, Winner = MatchWinner });
                        }
                        else BeginRound();
                    }
                    break;

                case MatchPhase.MatchOver:
                    Sim.Step(InputFrame.Neutral, InputFrame.Neutral);
                    break;
            }
        }

        void CheckRoundEnd()
        {
            var a = Sim[0];
            var b = Sim[1];
            bool koA = a.Health <= 0;
            bool koB = b.Health <= 0;
            if (koA || koB)
            {
                if (koA && koB)
                {
                    EndRound(-1, RoundEndReason.DoubleKO);
                    Events.Add(new MatchEvent { Type = MatchEventType.DoubleKO, Round = RoundNumberShown, Winner = -1 });
                }
                else
                {
                    int w = koA ? 1 : 0;
                    Events.Add(new MatchEvent { Type = MatchEventType.KO, Round = RoundNumberShown, Winner = w });
                    EndRound(w, RoundEndReason.KO);
                }
                return;
            }
            if (TimerTicks <= 0)
            {
                Events.Add(new MatchEvent { Type = MatchEventType.TimeUp, Round = RoundNumberShown, Winner = -1 });
                float ra = a.HealthRatio;
                float rb = b.HealthRatio;
                // Compara porcentagem em inteiros (milésimos) para igualdade exata previsível.
                int pa = Mathf.RoundToInt(ra * 1000f);
                int pb = Mathf.RoundToInt(rb * 1000f);
                if (pa == pb) EndRound(-1, RoundEndReason.TimeUp);
                else EndRound(pa > pb ? 0 : 1, RoundEndReason.TimeUp);
            }
        }

        void EndRound(int winner, RoundEndReason reason)
        {
            Sim.AttacksLocked = true;
            Sim.DamageLocked = true;
            RoundWinner = winner;
            LastReason = reason;
            SetPhase(MatchPhase.RoundOver);
            if (winner < 0)
            {
                // Empate: repete o round sem pontuar. O número exibido não avança.
                DrawsThisMatch++;
                RoundNumberShown--;
                Events.Add(new MatchEvent { Type = MatchEventType.RoundDraw, Round = RoundNumberShown + 1, Winner = -1 });
                return;
            }
            Wins[winner]++;
            Events.Add(new MatchEvent { Type = MatchEventType.RoundWon, Round = RoundNumberShown, Winner = winner });
            if (Wins[winner] >= Config.RoundsToWin) MatchWinner = winner;
        }

        /// <summary>Rede local: copia na réplica do convidado o estado decidido pelo anfitrião. Não aplica regras.</summary>
        public void NetRestore(MatchPhase phase, int phaseTick, int round, int roundShown, int timerTicks, int matchWinner, int roundWinner, RoundEndReason reason, int draws, int wins0, int wins1)
        {
            Phase = phase;
            PhaseTick = phaseTick;
            Round = round;
            RoundNumberShown = roundShown;
            TimerTicks = timerTicks;
            MatchWinner = matchWinner;
            RoundWinner = roundWinner;
            LastReason = reason;
            DrawsThisMatch = draws;
            Wins[0] = wins0;
            Wins[1] = wins1;
        }

        // ------------------------------------------------------------------ TREINO

        /// <summary>Reposiciona e restaura os lutadores (somente treino).</summary>
        public void TrainingReset()
        {
            if (!Training) return;
            Sim.ResetRound();
            Sim.Events.Clear();
        }
    }
}
