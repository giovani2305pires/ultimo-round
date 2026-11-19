using UltimoRound.Combat;

namespace UltimoRound.Core
{
    public enum GameMode
    {
        Versus,
        VsCpu,
        Training,
        Network,
    }

    public enum CpuDifficulty
    {
        Easy,
        Normal,
        Hard,
    }

    public enum DummyMode
    {
        Stand,
        Guard,
        Crouch,
        Cpu,
    }

    /// <summary>Escolhas que atravessam cenas: modo, lutadores, cores e opções de treino. Estado de combate não fica aqui.</summary>
    public sealed class Session
    {
        public GameMode Mode = GameMode.Versus;
        public CpuDifficulty Difficulty = CpuDifficulty.Normal;
        public readonly FighterDefinition[] Fighters = new FighterDefinition[2];
        public readonly int[] Palettes = { 0, 0 };

        /// <summary>Ao voltar da luta, abrir direto a seleção de personagens.</summary>
        public bool ReturnToSelect;

        // Treino (não vaza para os outros modos: lido somente quando Mode == Training)
        public DummyMode Dummy = DummyMode.Stand;
        public bool TrainingInfiniteHealth = true;
        public bool TrainingInfiniteEnergy = false;
        public bool TrainingShowBoxes = false;
        public CpuDifficulty TrainingCpuDifficulty = CpuDifficulty.Normal;

        /// <summary>Somente para testes automatizados/demonstração: CPU controla os dois lados.</summary>
        public bool AutoPlay;

        public bool IsCpu(int slot) => AutoPlay || (slot == 1 && (Mode == GameMode.VsCpu || Mode == GameMode.Training));

        public bool HasFighters => Fighters[0] != null && Fighters[1] != null;

        public string ModeLabel => Mode == GameMode.Versus ? "Versus Local" : Mode == GameMode.VsCpu ? "Contra CPU" : Mode == GameMode.Network ? "Rede Local" : "Treino";

        public static string DifficultyLabel(CpuDifficulty d) => d == CpuDifficulty.Easy ? "Fácil" : d == CpuDifficulty.Normal ? "Normal" : "Difícil";

        public static string DummyLabel(DummyMode d)
        {
            switch (d)
            {
                case DummyMode.Stand: return "Parado";
                case DummyMode.Guard: return "Defendendo";
                case DummyMode.Crouch: return "Agachado";
                default: return "CPU";
            }
        }
    }
}
