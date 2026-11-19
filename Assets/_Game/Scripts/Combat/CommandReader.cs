namespace UltimoRound.Combat
{
    /// <summary>
    /// Buffer de comandos de um lutador. Registra pressionamentos com idade em ticks, detecta toque duplo
    /// (avanço/recuo rápido) e permite consumir comandos para que não se repitam.
    /// </summary>
    public sealed class CommandReader
    {
        public const int ButtonCount = 7;
        const int Never = 10000;

        readonly int[] _pressAge = new int[ButtonCount];
        readonly sbyte[] _pressRelX = new sbyte[ButtonCount];
        readonly sbyte[] _pressY = new sbyte[ButtonCount];
        int _bufferTicks;
        int _dashWindow;

        int _prevRelX;
        int _fwdTapAge = Never;
        int _backTapAge = Never;
        int _dashFwdAge = Never;
        int _dashBackAge = Never;

        public InputFrame Current { get; private set; }
        public int RelX { get; private set; }

        public CommandReader(int bufferTicks, int dashWindow)
        {
            Configure(bufferTicks, dashWindow);
            Clear();
        }

        public void Configure(int bufferTicks, int dashWindow)
        {
            _bufferTicks = bufferTicks < 0 ? 0 : bufferTicks;
            _dashWindow = dashWindow < 1 ? 1 : dashWindow;
        }

        public void Clear()
        {
            for (int i = 0; i < ButtonCount; i++) _pressAge[i] = Never;
            _fwdTapAge = _backTapAge = _dashFwdAge = _dashBackAge = Never;
            _prevRelX = 0;
            Current = InputFrame.Neutral;
            RelX = 0;
        }

        /// <param name="frozen">Durante hitstop/congelamento as idades não avançam, mas novos toques são registrados.</param>
        public void Feed(in InputFrame frame, int facing, bool frozen)
        {
            if (!frozen)
            {
                for (int i = 0; i < ButtonCount; i++)
                    if (_pressAge[i] < Never) _pressAge[i]++;
                if (_fwdTapAge < Never) _fwdTapAge++;
                if (_backTapAge < Never) _backTapAge++;
                if (_dashFwdAge < Never) _dashFwdAge++;
                if (_dashBackAge < Never) _dashBackAge++;
            }

            int relX = frame.X * (facing >= 0 ? 1 : -1);
            for (int i = 0; i < ButtonCount; i++)
            {
                if (((int)frame.Pressed & (1 << i)) == 0) continue;
                _pressAge[i] = 0;
                _pressRelX[i] = (sbyte)relX;
                _pressY[i] = frame.Y;
            }

            bool neutralY = frame.Y == 0;
            if (relX == 1 && _prevRelX != 1 && neutralY)
            {
                if (_fwdTapAge <= _dashWindow) { _dashFwdAge = 0; _fwdTapAge = Never; }
                else _fwdTapAge = 0;
            }
            else if (relX == -1 && _prevRelX != -1 && neutralY)
            {
                if (_backTapAge <= _dashWindow) { _dashBackAge = 0; _backTapAge = Never; }
                else _backTapAge = 0;
            }

            _prevRelX = relX;
            RelX = relX;
            Current = frame;
        }

        static int IndexOf(Buttons b)
        {
            int v = (int)b;
            for (int i = 0; i < ButtonCount; i++)
                if ((v & (1 << i)) != 0) return i;
            return -1;
        }

        public bool Buffered(Buttons b)
        {
            int i = IndexOf(b);
            return i >= 0 && _pressAge[i] <= _bufferTicks;
        }

        /// <summary>Direção (relativa à orientação) no momento em que o botão foi pressionado.</summary>
        public int PressRelX(Buttons b)
        {
            int i = IndexOf(b);
            return i >= 0 ? _pressRelX[i] : 0;
        }

        /// <summary>Direção vertical no momento em que o botão foi pressionado (-1 baixo).</summary>
        public int PressY(Buttons b)
        {
            int i = IndexOf(b);
            return i >= 0 ? _pressY[i] : 0;
        }

        public void Consume(Buttons b)
        {
            int i = IndexOf(b);
            if (i >= 0) _pressAge[i] = Never;
        }

        public bool DashForward => _dashFwdAge <= _bufferTicks;
        public bool DashBack => _dashBackAge <= _bufferTicks;
        public void ConsumeDash() { _dashFwdAge = _dashBackAge = Never; }

        public bool AnyAttackBuffered =>
            Buffered(Buttons.Punch) || Buffered(Buttons.Kick) || Buffered(Buttons.Heavy) ||
            Buffered(Buttons.Special) || Buffered(Buttons.Grab) || Buffered(Buttons.Signature);
    }
}
