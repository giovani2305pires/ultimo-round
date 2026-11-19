using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UltimoRound.Combat;

namespace UltimoRound.Net
{
    public enum NetRole { None, Host, Guest }

    public enum NetStatus { Idle, Browsing, Hosting, Connecting, Connected, Failed }

    public enum NetLobbyPhase : byte { Select, Fight }

    public sealed class NetHostEntry
    {
        public string Name;
        public IPAddress Address;
        public int Port;
        public string GameVersion;
        public bool Busy;
        public float LastSeen;
    }

    /// <summary>Estado compartilhado da seleção. O anfitrião decide; o convidado só informa as próprias escolhas.</summary>
    public sealed class NetLobby
    {
        public readonly int[] Cursor = { 0, 1 };
        public readonly int[] Palette = { 0, 0 };
        public readonly bool[] Ready = new bool[2];
        public readonly int[] FinalFighter = new int[2];
        public readonly int[] FinalPalette = new int[2];
        public NetLobbyPhase Phase;
        public byte SelectEpoch;
        public byte LaunchEpoch;
        public byte FightStartEpoch;
        public byte GuestSceneEpoch;
    }

    /// <summary>
    /// Conexão de rede local entre dois computadores, por UDP: busca, entrada, seleção, envio da luta e queda de conexão.
    /// A luta roda só no anfitrião. As mensagens de estado são repetidas, então uma perda se corrige na seguinte.
    /// </summary>
    public sealed class NetSession : IDisposable
    {
        const float HeartbeatSeconds = 0.1f;
        const float HelloSeconds = 0.25f;
        const float DiscoverSeconds = 1f;
        const float ConnectTimeout = 6f;
        const float HostEntryLifetime = 3.5f;

        readonly string _gameVersion;
        readonly string _machineName;
        readonly int _port;
        readonly NetWriter _writer = new NetWriter();
        readonly NetReader _reader = new NetReader();
        readonly List<IPEndPoint> _discoveryTargets = new List<IPEndPoint>();

        NetTransport _transport;
        IPEndPoint _peer;
        float _now;
        float _lastReceive;
        float _nextHeartbeat;
        float _nextHello;
        float _nextDiscover;
        float _connectStart;
        float _lastPingEcho;
        float _lastPingReceivedAt;
        bool _heartbeatDirty;
        byte _guestSelectEpoch;

        public NetRole Role { get; private set; }
        public NetStatus Status { get; private set; }
        public string FailReason { get; private set; } = "";
        public NetLobby Lobby { get; private set; } = new NetLobby();
        public NetFightHost FightHost { get; private set; }
        public NetFightGuest FightGuest { get; private set; }
        public readonly List<NetHostEntry> Hosts = new List<NetHostEntry>();

        /// <summary>Tempo de ida e volta medido, em milissegundos.</summary>
        public float RttMs { get; private set; }
        public float RttMaxMs { get; private set; }

        /// <summary>Silêncio tolerado. É maior fora da luta, porque carregar a cena trava o laço por alguns segundos.</summary>
        public float TimeoutSeconds = 20f;

        /// <summary>A carga da cena distorce a medição: a luta começa com os valores zerados.</summary>
        public void ResetRttStats()
        {
            RttMs = 0f;
            RttMaxMs = 0f;
        }

        public float SimLoss;
        public float SimDelaySeconds;
        public float SimJitterSeconds;

        public bool Active => Status == NetStatus.Connected && _peer != null;
        public int LocalIndex => Role == NetRole.Guest ? 1 : 0;
        public int RemoteIndex => 1 - LocalIndex;
        public int LocalPort => _transport != null ? _transport.LocalPort : 0;
        public string PeerAddress => _peer != null ? _peer.Address.ToString() : "";

        public NetSession(string gameVersion, string machineName = null, int port = NetProtocol.DefaultPort)
        {
            _gameVersion = gameVersion ?? "";
            _machineName = string.IsNullOrEmpty(machineName) ? Environment.MachineName : machineName;
            _port = port;
        }

        public void Dispose() => Stop();

        // ---------------------------------------------------------------- COMANDOS

        public bool StartHost()
        {
            Stop();
            ResetState();
            if (!Open(_port))
            {
                Fail($"Não foi possível usar a porta {_port}. Outra cópia do jogo já pode estar hospedando.");
                return false;
            }
            Role = NetRole.Host;
            Status = NetStatus.Hosting;
            return true;
        }

        public bool StartBrowse()
        {
            Stop();
            ResetState();
            if (!Open(0))
            {
                Fail("Não foi possível abrir a conexão de rede.");
                return false;
            }
            Role = NetRole.Guest;
            Status = NetStatus.Browsing;
            _discoveryTargets.Clear();
            _discoveryTargets.AddRange(NetTransport.DiscoveryTargets(_port));
            _nextDiscover = 0f;
            return true;
        }

        public bool Join(IPAddress address, int port = 0)
        {
            if (_transport == null && !StartBrowse()) return false;
            Role = NetRole.Guest;
            Status = NetStatus.Connecting;
            FailReason = "";
            _peer = new IPEndPoint(address, port > 0 ? port : _port);
            _connectStart = -1f;
            _nextHello = 0f;
            return true;
        }

        /// <summary>Encerra e avisa o outro lado. Não é um erro: volta ao estado inicial.</summary>
        public void Stop()
        {
            if (_transport != null && _peer != null && (Status == NetStatus.Connected || Status == NetStatus.Connecting))
                for (int i = 0; i < 3; i++) SendSimple(NetMessage.Bye);
            _transport?.Dispose();
            _transport = null;
            _peer = null;
            FightHost = null;
            FightGuest = null;
            Hosts.Clear();
            Role = NetRole.None;
            Status = NetStatus.Idle;
        }

        void ResetState()
        {
            Lobby = new NetLobby();
            FailReason = "";
            RttMs = 0f;
            RttMaxMs = 0f;
            _guestSelectEpoch = 0;
            TimeoutSeconds = 20f;
        }

        bool Open(int port)
        {
            try
            {
                _transport = new NetTransport(port)
                {
                    SimLoss = SimLoss,
                    SimDelaySeconds = SimDelaySeconds,
                    SimJitterSeconds = SimJitterSeconds,
                };
                return true;
            }
            catch (SocketException)
            {
                _transport = null;
                return false;
            }
        }

        void Fail(string reason)
        {
            FailReason = reason;
            Status = NetStatus.Failed;
            _peer = null;
        }

        // ---------------------------------------------------------------- SELEÇÃO

        public void SetLocalSelection(int cursor, int palette, bool ready)
        {
            int i = LocalIndex;
            bool changed = Lobby.Cursor[i] != cursor || Lobby.Palette[i] != palette || Lobby.Ready[i] != ready;
            Lobby.Cursor[i] = cursor;
            Lobby.Palette[i] = palette;
            Lobby.Ready[i] = ready;
            if (Role == NetRole.Guest) _guestSelectEpoch = Lobby.SelectEpoch;
            _heartbeatDirty |= changed;
        }

        /// <summary>Anfitrião: fixa os lutadores e manda os dois computadores carregarem a luta.</summary>
        public void Launch(int fighter0, int fighter1, int palette0, int palette1)
        {
            if (Role != NetRole.Host) return;
            Lobby.FinalFighter[0] = fighter0;
            Lobby.FinalFighter[1] = fighter1;
            Lobby.FinalPalette[0] = palette0;
            Lobby.FinalPalette[1] = palette1;
            Lobby.LaunchEpoch++;
            Lobby.Phase = NetLobbyPhase.Fight;
            _heartbeatDirty = true;
            Flush();
        }

        /// <summary>Anfitrião: os dois voltam à seleção de lutadores.</summary>
        public void ReturnToSelect()
        {
            if (Role != NetRole.Host) return;
            Lobby.SelectEpoch++;
            Lobby.Phase = NetLobbyPhase.Select;
            Lobby.Ready[0] = Lobby.Ready[1] = false;
            FightHost = null;
            _heartbeatDirty = true;
            Flush();
        }

        // ---------------------------------------------------------------- LUTA

        public void BeginFight()
        {
            if (Role == NetRole.Host) FightHost = new NetFightHost();
            else FightGuest = new NetFightGuest();
        }

        public void EndFight()
        {
            FightHost = null;
            FightGuest = null;
        }

        /// <summary>Convidado: avisa que a cena de luta terminou de carregar.</summary>
        public void ReportSceneReady()
        {
            if (Role == NetRole.Guest && Lobby.GuestSceneEpoch != Lobby.LaunchEpoch)
            {
                Lobby.GuestSceneEpoch = Lobby.LaunchEpoch;
                _heartbeatDirty = true;
            }
        }

        public bool GuestSceneReady => Lobby.GuestSceneEpoch == Lobby.LaunchEpoch;
        public bool HostStartedFight => Lobby.FightStartEpoch == Lobby.LaunchEpoch;

        /// <summary>Anfitrião: a tela de confronto começa nos dois computadores.</summary>
        public void StartFightFlow()
        {
            if (Role != NetRole.Host) return;
            Lobby.FightStartEpoch = Lobby.LaunchEpoch;
            _heartbeatDirty = true;
        }

        public void SendSnapshot(MatchController match)
        {
            if (!Active || FightHost == null) return;
            FightHost.WriteSnapshot(_writer, match);
            _transport.Send(_peer, _writer.Data, _writer.Length);
        }

        public void SendInput(in InputFrame frame)
        {
            if (!Active || FightGuest == null) return;
            FightGuest.WriteInput(_writer, frame);
            _transport.Send(_peer, _writer.Data, _writer.Length);
        }

        // ---------------------------------------------------------------- LAÇO

        public void Update(float now)
        {
            _now = now;
            if (_transport == null) return;
            _transport.Pump(now);

            int length;
            while ((length = _transport.Receive(out var from)) > 0)
            {
                _reader.Reset(_transport.Buffer, length);
                if (NetProtocol.ReadHeader(_reader, out var type)) Dispatch(type, from);
                if (_transport == null) return; // o pacote encerrou a sessão
            }

            switch (Status)
            {
                case NetStatus.Browsing: TickBrowse(); break;
                case NetStatus.Connecting: TickConnect(); break;
                case NetStatus.Connected: TickConnected(); break;
            }
        }

        void TickBrowse()
        {
            if (_now >= _nextDiscover)
            {
                _nextDiscover = _now + DiscoverSeconds;
                NetProtocol.WriteHeader(_writer, NetMessage.Discover);
                foreach (var t in _discoveryTargets) _transport.Send(t, _writer.Data, _writer.Length);
            }
            Hosts.RemoveAll(h => _now - h.LastSeen > HostEntryLifetime);
        }

        void TickConnect()
        {
            // O prazo conta do primeiro envio: o relógio ainda pode estar parado na hora do pedido.
            if (_connectStart < 0f) _connectStart = _now;
            if (_now - _connectStart > ConnectTimeout)
            {
                Fail("Sem resposta do anfitrião. Confira o endereço, se os dois estão na mesma rede e se o firewall liberou o jogo.");
                return;
            }
            if (_now < _nextHello) return;
            _nextHello = _now + HelloSeconds;
            NetProtocol.WriteHeader(_writer, NetMessage.Hello);
            _writer.Text(_gameVersion);
            _transport.Send(_peer, _writer.Data, _writer.Length);
        }

        void TickConnected()
        {
            if (_now - _lastReceive > TimeoutSeconds)
            {
                Fail("A conexão com o outro jogador foi perdida.");
                return;
            }
            if (_heartbeatDirty || _now >= _nextHeartbeat) Flush();
        }

        void Flush()
        {
            if (!Active) return;
            _heartbeatDirty = false;
            _nextHeartbeat = _now + HeartbeatSeconds;
            if (Role == NetRole.Host) WriteHostState();
            else WriteGuestState();
            _transport.Send(_peer, _writer.Data, _writer.Length);
        }

        void WritePing()
        {
            _writer.Float(_now);
            _writer.Float(_lastPingEcho);
            _writer.Float(_now - _lastPingReceivedAt);
        }

        void ReadPing()
        {
            float theirTime = _reader.Float();
            float echoOfMine = _reader.Float();
            float heldFor = _reader.Float();
            if (!_reader.Ok) return;
            _lastPingEcho = theirTime;
            _lastPingReceivedAt = _now;
            if (echoOfMine <= 0f) return;
            // Desconta o tempo que o outro lado segurou o eco até o próximo envio.
            float rtt = (_now - echoOfMine - heldFor) * 1000f;
            if (rtt < 0f || rtt > 5000f) return;
            RttMs = RttMs <= 0f ? rtt : RttMs * 0.8f + rtt * 0.2f;
            if (rtt > RttMaxMs) RttMaxMs = rtt;
        }

        void WriteHostState()
        {
            var l = Lobby;
            NetProtocol.WriteHeader(_writer, NetMessage.HostState);
            WritePing();
            _writer.Byte((byte)l.Phase);
            _writer.Byte(l.SelectEpoch);
            _writer.Byte(l.LaunchEpoch);
            _writer.Byte(l.FightStartEpoch);
            for (int i = 0; i < 2; i++)
            {
                _writer.Byte((byte)l.Cursor[i]);
                _writer.Byte((byte)l.Palette[i]);
                _writer.Bool(l.Ready[i]);
                _writer.Byte((byte)l.FinalFighter[i]);
                _writer.Byte((byte)l.FinalPalette[i]);
            }
        }

        void WriteGuestState()
        {
            NetProtocol.WriteHeader(_writer, NetMessage.GuestState);
            WritePing();
            _writer.Byte((byte)Lobby.Cursor[1]);
            _writer.Byte((byte)Lobby.Palette[1]);
            _writer.Bool(Lobby.Ready[1]);
            _writer.Byte(_guestSelectEpoch);
            _writer.Byte(Lobby.GuestSceneEpoch);
        }

        void Dispatch(NetMessage type, IPEndPoint from)
        {
            switch (type)
            {
                case NetMessage.Discover: if (Role == NetRole.Host) ReplyDiscover(from); return;
                case NetMessage.HostInfo: if (Status == NetStatus.Browsing) ReadHostInfo(from); return;
                case NetMessage.Hello: if (Role == NetRole.Host) ReadHello(from); return;
            }

            // Daqui em diante, só o par conectado é ouvido.
            if (_peer == null || !_peer.Equals(from)) return;
            _lastReceive = _now;
            switch (type)
            {
                case NetMessage.Welcome:
                    if (Status == NetStatus.Connecting) { Status = NetStatus.Connected; _heartbeatDirty = true; }
                    break;
                case NetMessage.Reject:
                    if (Status == NetStatus.Connecting) { string why = _reader.Text(); Fail(why); }
                    break;
                case NetMessage.Bye:
                    Fail("O outro jogador saiu da partida.");
                    break;
                case NetMessage.HostState:
                    if (Role == NetRole.Guest) ReadHostState();
                    break;
                case NetMessage.GuestState:
                    if (Role == NetRole.Host) ReadGuestState();
                    break;
                case NetMessage.Snapshot:
                    if (Role == NetRole.Guest) FightGuest?.ReadSnapshot(_reader);
                    break;
                case NetMessage.Input:
                    if (Role == NetRole.Host) FightHost?.ReadInput(_reader);
                    break;
            }
        }

        void ReplyDiscover(IPEndPoint from)
        {
            NetProtocol.WriteHeader(_writer, NetMessage.HostInfo);
            _writer.Text(_machineName);
            _writer.Text(_gameVersion);
            _writer.Bool(Status != NetStatus.Hosting);
            _transport.Send(from, _writer.Data, _writer.Length);
        }

        void ReadHostInfo(IPEndPoint from)
        {
            string name = _reader.Text();
            string version = _reader.Text();
            bool busy = _reader.Bool();
            if (!_reader.Ok) return;
            foreach (var h in Hosts)
            {
                // O mesmo computador pode responder por mais de uma placa de rede.
                if (h.Name != name || h.Port != from.Port) continue;
                h.GameVersion = version;
                h.Busy = busy;
                h.LastSeen = _now;
                return;
            }
            Hosts.Add(new NetHostEntry { Name = name, Address = from.Address, Port = from.Port, GameVersion = version, Busy = busy, LastSeen = _now });
        }

        void ReadHello(IPEndPoint from)
        {
            string version = _reader.Text();
            if (!_reader.Ok) return;
            if (_peer != null && !_peer.Equals(from))
            {
                SendReject(from, "Este anfitrião já está em uma partida.");
                return;
            }
            if (version != _gameVersion)
            {
                SendReject(from, $"Versões diferentes: anfitrião {_gameVersion}, você {version}.");
                return;
            }
            _peer = from;
            _lastReceive = _now;
            if (Status == NetStatus.Hosting)
            {
                Status = NetStatus.Connected;
                _heartbeatDirty = true;
            }
            SendSimple(NetMessage.Welcome);
        }

        void SendReject(IPEndPoint to, string reason)
        {
            NetProtocol.WriteHeader(_writer, NetMessage.Reject);
            _writer.Text(reason);
            _transport.Send(to, _writer.Data, _writer.Length);
        }

        void SendSimple(NetMessage type)
        {
            NetProtocol.WriteHeader(_writer, type);
            _transport.Send(_peer, _writer.Data, _writer.Length);
        }

        void ReadHostState()
        {
            ReadPing();
            var l = Lobby;
            var phase = (NetLobbyPhase)_reader.Byte();
            byte selectEpoch = _reader.Byte();
            byte launchEpoch = _reader.Byte();
            byte fightStart = _reader.Byte();
            int c0 = _reader.Byte(), p0 = _reader.Byte();
            bool r0 = _reader.Bool();
            int f0 = _reader.Byte(), fp0 = _reader.Byte();
            int c1 = _reader.Byte(), p1 = _reader.Byte();
            bool r1 = _reader.Bool();
            int f1 = _reader.Byte(), fp1 = _reader.Byte();
            if (!_reader.Ok) return;
            l.Phase = phase;
            l.SelectEpoch = selectEpoch;
            l.LaunchEpoch = launchEpoch;
            l.FightStartEpoch = fightStart;
            l.Cursor[0] = c0;
            l.Palette[0] = p0;
            l.Ready[0] = r0;
            l.FinalFighter[0] = f0;
            l.FinalPalette[0] = fp0;
            l.FinalFighter[1] = f1;
            l.FinalPalette[1] = fp1;
            // O próprio cursor do convidado é local: o valor de volta chega atrasado e faria a seleção "pular".
            _ = c1;
            _ = p1;
            _ = r1;
        }

        void ReadGuestState()
        {
            ReadPing();
            int cursor = _reader.Byte();
            int palette = _reader.Byte();
            bool ready = _reader.Bool();
            byte selectEpoch = _reader.Byte();
            byte sceneEpoch = _reader.Byte();
            if (!_reader.Ok) return;
            var l = Lobby;
            l.Cursor[1] = cursor;
            l.Palette[1] = palette;
            // "Pronto" de antes de voltar à seleção não vale para a nova escolha.
            l.Ready[1] = ready && selectEpoch == l.SelectEpoch;
            l.GuestSceneEpoch = sceneEpoch;
        }
    }
}
