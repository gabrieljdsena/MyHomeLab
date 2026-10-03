using System.Runtime.InteropServices;

namespace MyHomeLab.Infrastructure.Lan;

/// Reads the whole IPv4 neighbour table via the IP helper API, so discovery sees
/// every device the stack has recently talked to — including ones that ignore ping.
internal static class ArpTableReader
{
    // MIB_IPNET_TYPE values; invalid rows carry no usable address.
    private const uint TypeInvalid = 2;

    private const int RowSize = 24;
    private const int HeaderSize = 4;
    private const int MacOffset = 8;
    private const int AddressOffset = 16;
    private const int TypeOffset = 20;

    public static IReadOnlyList<(string Ip, string Mac)> Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        uint size = 0;
        GetIpNetTable(null, ref size, sort: false);
        if (size < HeaderSize + RowSize)
        {
            return [];
        }

        var buffer = new byte[size];
        if (GetIpNetTable(buffer, ref size, sort: false) != 0)
        {
            return [];
        }

        var count = BitConverter.ToUInt32(buffer, 0);
        var entries = new List<(string Ip, string Mac)>();
        for (uint i = 0; i < count; i++)
        {
            var offset = HeaderSize + (int)(i * RowSize);
            if (offset + RowSize > buffer.Length)
            {
                break;
            }

            if (BitConverter.ToUInt32(buffer, offset + TypeOffset) == TypeInvalid)
            {
                continue;
            }

            var macLength = BitConverter.ToUInt32(buffer, offset + 4);
            if (macLength != 6)
            {
                continue;
            }

            var mac = string.Join(':', buffer
                .Skip(offset + MacOffset)
                .Take(6)
                .Select(octet => octet.ToString("x2")));
            var ip = string.Join('.', buffer
                .Skip(offset + AddressOffset)
                .Take(4)
                .Select(octet => octet.ToString()));
            entries.Add((ip, mac));
        }

        return entries;
    }

#pragma warning disable SYSLIB1054
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetIpNetTable(byte[]? ipNetTable, ref uint sizePointer, [MarshalAs(UnmanagedType.Bool)] bool sort);
#pragma warning restore SYSLIB1054
}
