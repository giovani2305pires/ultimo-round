using UltimoRound.Combat;

namespace UltimoRound.Input
{
    /// <summary>
    /// Captura a entrada a cada quadro de renderização e entrega um <see cref="InputFrame"/> por tick.
    /// Pressionamentos ocorridos entre ticks são acumulados para não se perderem quando o FPS varia;
    /// botão segurado não gera novos pressionamentos.
    /// </summary>
    public sealed class InputAccumulator
    {
        int _x, _y;
        Buttons _held;
        Buttons _pendingPressed;

        public Buttons Held => _held;

        /// <param name="pressedThisFrame">Bordas de pressão detectadas pelo dispositivo neste quadro (capta toques curtos).</param>
        public void Sample(int x, int y, Buttons held, Buttons pressedThisFrame)
        {
            _pendingPressed |= (held & ~_held) | pressedThisFrame;
            _held = held;
            _x = x;
            _y = y;
        }

        public InputFrame ConsumeTick()
        {
            var f = new InputFrame(_x, _y, _held, _pendingPressed);
            _pendingPressed = Buttons.None;
            return f;
        }

        /// <summary>Descarta pressionamentos pendentes (pausa, menus, troca de contexto).
        /// Botões ainda segurados não serão contados como novo pressionamento ao retomar.</summary>
        public void Discard()
        {
            _pendingPressed = Buttons.None;
        }

        public void Reset()
        {
            _pendingPressed = Buttons.None;
            _held = Buttons.None;
            _x = _y = 0;
        }

        /// <summary>Retoma após pausa: considera os botões atualmente segurados como já vistos.</summary>
        public void ResumeWithHeld(Buttons currentlyHeld)
        {
            _pendingPressed = Buttons.None;
            _held = currentlyHeld;
        }
    }
}
