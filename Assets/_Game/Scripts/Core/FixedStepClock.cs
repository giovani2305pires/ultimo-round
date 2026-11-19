namespace UltimoRound.Core
{
    /// <summary>
    /// Converte o tempo variável de renderização em ticks fixos de simulação (60/s).
    /// A lógica avança sempre em passos iguais, independentemente do FPS.
    /// </summary>
    public sealed class FixedStepClock
    {
        public readonly double StepSeconds;
        public readonly int MaxStepsPerFrame;
        double _accumulator;

        public FixedStepClock(int ticksPerSecond = 60, int maxStepsPerFrame = 6)
        {
            StepSeconds = 1.0 / ticksPerSecond;
            MaxStepsPerFrame = maxStepsPerFrame;
        }

        /// <summary>Fração do próximo tick já decorrida (para interpolação visual).</summary>
        public float Alpha => (float)(_accumulator / StepSeconds);

        public void Reset() => _accumulator = 0;

        /// <summary>Acumula o tempo do quadro e retorna quantos ticks devem ser executados.</summary>
        public int Advance(double frameSeconds)
        {
            if (frameSeconds < 0) frameSeconds = 0;
            // Limita picos (ex.: janela arrastada) para não acelerar a luta depois de travamentos.
            double max = StepSeconds * MaxStepsPerFrame;
            if (frameSeconds > max) frameSeconds = max;
            _accumulator += frameSeconds;
            int steps = 0;
            while (_accumulator + 1e-9 >= StepSeconds && steps < MaxStepsPerFrame)
            {
                _accumulator -= StepSeconds;
                steps++;
            }
            if (_accumulator < 0) _accumulator = 0;
            return steps;
        }
    }
}
