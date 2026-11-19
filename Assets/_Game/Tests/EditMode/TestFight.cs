using System.Collections.Generic;
using UltimoRound.Combat;
using UltimoRound.Data;
using UnityEngine;

namespace UltimoRound.Tests
{
    /// <summary>Utilitário para montar lutas em memória nos testes.</summary>
    internal sealed class TestFight
    {
        public readonly CombatConfig Config;
        public readonly FightSimulation Sim;
        public readonly List<CombatEvent> AllEvents = new List<CombatEvent>();

        public FighterState A => Sim[0];
        public FighterState B => Sim[1];

        public TestFight(string a = "giovani", string b = "giovani", CombatConfig cfg = null)
        {
            Config = cfg ?? DefaultRoster.CreateConfig();
            Sim = new FightSimulation(Config, DefaultRoster.Create(a), DefaultRoster.Create(b), 0, 1);
        }

        public TestFight(FighterDefinition a, FighterDefinition b, CombatConfig cfg = null)
        {
            Config = cfg ?? DefaultRoster.CreateConfig();
            Sim = new FightSimulation(Config, a, b, 0, 1);
        }

        public void Place(float ax, float bx)
        {
            A.Pos = new Vector2(ax, 0);
            B.Pos = new Vector2(bx, 0);
            A.PrevPos = A.Pos;
            B.PrevPos = B.Pos;
            A.Facing = ax < bx ? 1 : -1;
            B.Facing = -A.Facing;
        }

        public void Step(InputFrame a, InputFrame b)
        {
            Sim.Step(a, b);
            AllEvents.AddRange(Sim.Events);
            Sim.Events.Clear();
        }

        public void Idle(int ticks, InputFrame? holdA = null, InputFrame? holdB = null)
        {
            for (int i = 0; i < ticks; i++)
                Step(holdA ?? InputFrame.Neutral, holdB ?? InputFrame.Neutral);
        }

        /// <summary>Pressiona um botão no primeiro tick e segura a direção/estado indicado pelos ticks seguintes.</summary>
        public void PressA(Buttons b, int x = 0, int y = 0, int thenTicks = 0, InputFrame? holdB = null)
        {
            Step(new InputFrame(x, y, b, b), holdB ?? InputFrame.Neutral);
            for (int i = 0; i < thenTicks; i++)
                Step(new InputFrame(x, y, Buttons.None, Buttons.None), holdB ?? InputFrame.Neutral);
        }

        public static InputFrame Hold(int x, int y, Buttons held = Buttons.None) => new InputFrame(x, y, held, Buttons.None);
        public static InputFrame Press(Buttons b, int x = 0, int y = 0) => new InputFrame(x, y, b, b);

        public int Count(CombatEventType t)
        {
            int n = 0;
            foreach (var e in AllEvents) if (e.Type == t) n++;
            return n;
        }
    }
}
