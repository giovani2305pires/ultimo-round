using System.Runtime.InteropServices;
using System.Text;

namespace UltimoRound.Net
{
    /// <summary>Tipos de pacote. Todos começam com o cabeçalho de <see cref="NetProtocol"/>.</summary>
    public enum NetMessage : byte
    {
        Discover = 1,
        HostInfo,
        Hello,
        Welcome,
        Reject,
        HostState,
        GuestState,
        Snapshot,
        Input,
        Bye,
    }

    public static class NetProtocol
    {
        public const int DefaultPort = 47777;
        public const byte Version = 1;
        public const int MaxPacket = 1200;
        const byte Magic0 = (byte)'U';
        const byte Magic1 = (byte)'R';

        public static void WriteHeader(NetWriter w, NetMessage type)
        {
            w.Reset();
            w.Byte(Magic0);
            w.Byte(Magic1);
            w.Byte(Version);
            w.Byte((byte)type);
        }

        /// <summary>Valida o cabeçalho. Pacote de outro programa ou de outra versão do protocolo é ignorado.</summary>
        public static bool ReadHeader(NetReader r, out NetMessage type)
        {
            type = 0;
            if (r.Byte() != Magic0 || r.Byte() != Magic1 || r.Byte() != Version) return false;
            type = (NetMessage)r.Byte();
            return r.Ok;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    struct FloatBits
    {
        [FieldOffset(0)] public float F;
        [FieldOffset(0)] public uint U;
    }

    /// <summary>Escreve em um vetor fixo, sem alocar a cada pacote.</summary>
    public sealed class NetWriter
    {
        public readonly byte[] Data;
        public int Length { get; private set; }

        public NetWriter(int capacity = NetProtocol.MaxPacket) { Data = new byte[capacity]; }

        public int Remaining => Data.Length - Length;

        public void Reset() => Length = 0;

        public void Byte(byte v)
        {
            if (Length < Data.Length) Data[Length++] = v;
        }

        public void Bool(bool v) => Byte(v ? (byte)1 : (byte)0);
        public void SByte(sbyte v) => Byte((byte)v);

        public void Short(short v)
        {
            Byte((byte)v);
            Byte((byte)(v >> 8));
        }

        public void UInt(uint v)
        {
            Byte((byte)v);
            Byte((byte)(v >> 8));
            Byte((byte)(v >> 16));
            Byte((byte)(v >> 24));
        }

        public void Int(int v) => UInt((uint)v);

        public void Float(float v)
        {
            var b = new FloatBits { F = v };
            UInt(b.U);
        }

        public void Text(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? "");
            int n = bytes.Length > 200 ? 200 : bytes.Length;
            Byte((byte)n);
            for (int i = 0; i < n; i++) Byte(bytes[i]);
        }

        public void Bytes(byte[] src, int offset, int count)
        {
            for (int i = 0; i < count; i++) Byte(src[offset + i]);
        }
    }

    /// <summary>Lê com verificação de limites: pacote truncado apenas marca <see cref="Ok"/> como falso.</summary>
    public sealed class NetReader
    {
        byte[] _data;
        int _length;
        int _pos;

        public bool Ok { get; private set; } = true;
        public int Position => _pos;
        public int Remaining => _length - _pos;

        public NetReader() { }
        public NetReader(byte[] data, int length, int offset = 0) { Reset(data, length, offset); }

        public void Reset(byte[] data, int length, int offset = 0)
        {
            _data = data;
            _length = length;
            _pos = offset;
            Ok = true;
        }

        public byte Byte()
        {
            if (_pos >= _length) { Ok = false; return 0; }
            return _data[_pos++];
        }

        public bool Bool() => Byte() != 0;
        public sbyte SByte() => (sbyte)Byte();

        public short Short()
        {
            int a = Byte();
            int b = Byte();
            return (short)(a | (b << 8));
        }

        public uint UInt()
        {
            uint a = Byte();
            uint b = Byte();
            uint c = Byte();
            uint d = Byte();
            return a | (b << 8) | (c << 16) | (d << 24);
        }

        public int Int() => (int)UInt();

        public float Float()
        {
            var b = new FloatBits { U = UInt() };
            return b.F;
        }

        public string Text()
        {
            int n = Byte();
            if (n > Remaining) { Ok = false; return ""; }
            var s = Encoding.UTF8.GetString(_data, _pos, n);
            _pos += n;
            return s;
        }
    }
}
