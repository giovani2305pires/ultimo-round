using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace UltimoRound.Net
{
    /// <summary>
    /// Soquete UDP sem bloqueio. Pode simular perda e atraso de saída para testar condições de Wi-Fi em um só computador.
    /// </summary>
    public sealed class NetTransport : IDisposable
    {
        struct Delayed
        {
            public float Time;
            public EndPoint To;
            public byte[] Data;
        }

        readonly Socket _socket;
        readonly byte[] _receive = new byte[1500];
        readonly List<Delayed> _delayed = new List<Delayed>();
        readonly Random _random = new Random();
        float _now;

        public float SimLoss;
        public float SimDelaySeconds;
        public float SimJitterSeconds;

        public int LocalPort => ((IPEndPoint)_socket.LocalEndPoint).Port;
        public byte[] Buffer => _receive;

        /// <param name="port">0 escolhe uma porta livre (convidado).</param>
        public NetTransport(int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Blocking = false;
            _socket.EnableBroadcast = true;
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        public void Dispose() => _socket.Dispose();

        public void Pump(float now)
        {
            _now = now;
            for (int i = _delayed.Count - 1; i >= 0; i--)
            {
                if (_delayed[i].Time > now) continue;
                SendRaw(_delayed[i].To, _delayed[i].Data, _delayed[i].Data.Length);
                _delayed.RemoveAt(i);
            }
        }

        public void Send(EndPoint to, byte[] data, int length)
        {
            if (SimLoss > 0f && _random.NextDouble() < SimLoss) return;
            if (SimDelaySeconds > 0f || SimJitterSeconds > 0f)
            {
                var copy = new byte[length];
                Array.Copy(data, copy, length);
                _delayed.Add(new Delayed { Time = _now + SimDelaySeconds + (float)_random.NextDouble() * SimJitterSeconds, To = to, Data = copy });
                return;
            }
            SendRaw(to, data, length);
        }

        void SendRaw(EndPoint to, byte[] data, int length)
        {
            try { _socket.SendTo(data, 0, length, SocketFlags.None, to); }
            catch (SocketException) { } // rede indisponível: o tempo limite da conexão trata o caso
        }

        /// <summary>Lê um pacote pendente. Retorna 0 quando não há mais nenhum.</summary>
        public int Receive(out IPEndPoint from)
        {
            from = null;
            while (_socket.Available > 0)
            {
                EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    int n = _socket.ReceiveFrom(_receive, ref remote);
                    from = (IPEndPoint)remote;
                    return n;
                }
                catch (SocketException) { } // no Windows, "porta inalcançável" de um envio anterior aparece aqui
            }
            return 0;
        }

        /// <summary>Endereços IPv4 desta máquina, os que têm roteador primeiro.</summary>
        public static List<string> LocalAddresses()
        {
            var withGateway = new List<string>();
            var others = new List<string>();
            foreach (var nic in SafeInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var props = nic.GetIPProperties();
                foreach (var a in props.UnicastAddresses)
                {
                    if (a.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    (props.GatewayAddresses.Count > 0 ? withGateway : others).Add(a.Address.ToString());
                }
            }
            withGateway.AddRange(others);
            return withGateway;
        }

        /// <summary>Destinos de busca: difusão geral, difusão de cada rede (há máquinas com várias placas) e a própria máquina.</summary>
        public static List<IPEndPoint> DiscoveryTargets(int port)
        {
            var list = new List<IPEndPoint>
            {
                new IPEndPoint(IPAddress.Broadcast, port),
                new IPEndPoint(IPAddress.Loopback, port),
            };
            foreach (var nic in SafeInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var a in nic.GetIPProperties().UnicastAddresses)
                {
                    if (a.Address.AddressFamily != AddressFamily.InterNetwork || a.IPv4Mask == null) continue;
                    var ip = a.Address.GetAddressBytes();
                    var mask = a.IPv4Mask.GetAddressBytes();
                    for (int i = 0; i < 4; i++) ip[i] = (byte)(ip[i] | ~mask[i]);
                    list.Add(new IPEndPoint(new IPAddress(ip), port));
                }
            }
            return list;
        }

        static NetworkInterface[] SafeInterfaces()
        {
            try { return NetworkInterface.GetAllNetworkInterfaces(); }
            catch (NetworkInformationException) { return new NetworkInterface[0]; }
        }
    }
}
