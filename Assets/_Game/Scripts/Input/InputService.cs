using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UltimoRound.Input
{
    public sealed class PlayerSlot
    {
        public readonly int Index;
        public InputDevice Device;
        public DeviceReader Reader;
        /// <summary>Dispositivo desconectado durante o uso.</summary>
        public bool Lost;
        public int LostDeviceId = -1;
        /// <summary>Reconectado; aguardando o jogador confirmar para retomar.</summary>
        public bool AwaitingConfirm;

        public PlayerSlot(int index) { Index = index; }
        public bool Assigned => Device != null;
        public bool Ready => Assigned && !Lost && Reader != null && Reader.Connected;
    }

    /// <summary>
    /// Associação jogador ↔ dispositivo (um dispositivo por jogador), leitura de menus e tratamento de
    /// desconexão/reconexão. Persistente entre cenas (não duplica jogadores).
    /// </summary>
    public sealed class InputService : IDisposable
    {
        public readonly PlayerSlot[] Slots = { new PlayerSlot(0), new PlayerSlot(1) };
        public event Action<int> DeviceLost;
        public event Action<int> DeviceRegained;

        ControlBindings _bindings;
        readonly Dictionary<int, DeviceReader> _menuReaders = new Dictionary<int, DeviceReader>();

        public InputService(ControlBindings bindings)
        {
            _bindings = bindings;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        public void Dispose()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        public void SetBindings(ControlBindings b)
        {
            _bindings = b;
            foreach (var s in Slots) s.Reader?.Rebind(b);
            foreach (var r in _menuReaders.Values) r.Rebind(b);
        }

        public bool IsAssigned(InputDevice d)
        {
            foreach (var s in Slots) if (s.Device == d) return true;
            return false;
        }

        public void Assign(int slot, InputDevice device)
        {
            // Um dispositivo nunca controla os dois lutadores.
            foreach (var s in Slots)
                if (s.Index != slot && s.Device == device) Unassign(s.Index);
            var p = Slots[slot];
            p.Device = device;
            p.Reader = device != null ? new DeviceReader(device, _bindings) : null;
            p.Lost = false;
            p.AwaitingConfirm = false;
            p.LostDeviceId = -1;
        }

        public void Unassign(int slot)
        {
            var p = Slots[slot];
            p.Device = null;
            p.Reader = null;
            p.Lost = false;
            p.AwaitingConfirm = false;
            p.LostDeviceId = -1;
        }

        public void ClearAll()
        {
            Unassign(0);
            Unassign(1);
        }

        static IEnumerable<InputDevice> CandidateDevices()
        {
            if (Keyboard.current != null) yield return Keyboard.current;
            foreach (var g in Gamepad.all) yield return g;
        }

        /// <summary>Retorna o dispositivo não associado que pressionou "entrar" neste quadro.</summary>
        public InputDevice PollJoin()
        {
            foreach (var d in CandidateDevices())
                if (d.added && !IsAssigned(d) && MenuReaderFor(d).JoinPressed()) return d;
            return null;
        }

        DeviceReader MenuReaderFor(InputDevice d)
        {
            if (!_menuReaders.TryGetValue(d.deviceId, out var r) || r.Device != d)
            {
                r = new DeviceReader(d, _bindings);
                _menuReaders[d.deviceId] = r;
            }
            return r;
        }

        /// <summary>Entrada de menu combinada de todos os dispositivos (telas antes da associação).</summary>
        public MenuInput ReadAnyMenu(float dt)
        {
            var m = new MenuInput();
            foreach (var d in CandidateDevices())
            {
                if (!d.added) continue;
                var r = MenuReaderFor(d).ReadMenu(dt);
                if (m.NavX == 0) m.NavX = r.NavX;
                if (m.NavY == 0) m.NavY = r.NavY;
                if (m.HeldY == 0) m.HeldY = r.HeldY;
                m.Confirm |= r.Confirm;
                m.Cancel |= r.Cancel;
                m.Start |= r.Start;
                m.Alt |= r.Alt;
            }
            return m;
        }

        /// <summary>Qualquer tecla, botão de controle ou clique do mouse neste quadro.</summary>
        public bool AnyPressed()
        {
            bool any = false;
            foreach (var d in CandidateDevices())
                if (d.added) any |= MenuReaderFor(d).AnyPressed();
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) any = true;
            return any;
        }

        /// <summary>Entrada de menu apenas do dispositivo do jogador.</summary>
        public MenuInput ReadMenu(int slot, float dt)
        {
            var p = Slots[slot];
            if (!p.Ready) return default;
            return MenuReaderFor(p.Device).ReadMenu(dt);
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    foreach (var s in Slots)
                    {
                        if (s.Device != device || s.Lost) continue;
                        s.Lost = true;
                        s.AwaitingConfirm = false;
                        s.LostDeviceId = device.deviceId;
                        DeviceLost?.Invoke(s.Index);
                    }
                    _menuReaders.Remove(device.deviceId);
                    break;

                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    if (!(device is Gamepad) && !(device is Keyboard)) break;
                    // Prioriza o mesmo dispositivo; senão, um gamepad novo não usado pelo outro jogador.
                    PlayerSlot target = null;
                    foreach (var s in Slots)
                        if (s.Lost && s.LostDeviceId == device.deviceId) { target = s; break; }
                    if (target == null)
                        foreach (var s in Slots)
                            if (s.Lost && !IsAssignedToOther(device, s.Index)) { target = s; break; }
                    if (target != null)
                    {
                        target.Device = device;
                        target.Reader = new DeviceReader(device, _bindings);
                        target.Lost = false;
                        target.AwaitingConfirm = true;
                        DeviceRegained?.Invoke(target.Index);
                    }
                    break;
            }
        }

        bool IsAssignedToOther(InputDevice d, int slot)
        {
            foreach (var s in Slots)
                if (s.Index != slot && s.Device == d && !s.Lost) return true;
            return false;
        }

        public bool AnyLostOrAwaiting(out int slot)
        {
            foreach (var s in Slots)
                if (s.Assigned && (s.Lost || s.AwaitingConfirm)) { slot = s.Index; return true; }
            slot = -1;
            return false;
        }
    }
}
