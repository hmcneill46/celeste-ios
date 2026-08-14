#if CELESTE_RUNTIME && TVOS_CELESTE_RUNTIME_HOST
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace CelesteTvOSHost;

internal static class SaveManagerLanAddressPolicy
{
    private const byte AddressFamilyInterNetwork = 2;
    private const byte AddressFamilyInterNetworkV6 = 30;

    internal static IPAddress? ReadDarwinSockAddr(IntPtr address)
    {
        byte family = Marshal.ReadByte(address, 1);
        if (family == AddressFamilyInterNetwork)
        {
            byte[] bytes = new byte[4];
            Marshal.Copy(IntPtr.Add(address, 4), bytes, 0, bytes.Length);
            return new IPAddress(bytes);
        }
        if (family == AddressFamilyInterNetworkV6)
        {
            byte[] bytes = new byte[16];
            Marshal.Copy(IntPtr.Add(address, 8), bytes, 0, bytes.Length);
            return new IPAddress(bytes);
        }
        return null;
    }

    internal static bool IsExcludedInterface(string name)
    {
        string value = name.ToLowerInvariant();
        if (value == "lo0") return true;
        return new[] { "loopback", "utun", "tun", "tap", "vpn", "awdl", "llw", "bridge", "pdp_ip" }
            .Any(marker => value.Contains(marker, StringComparison.Ordinal));
    }

    internal static int InterfaceRank(string name) => name.Equals("en0", StringComparison.Ordinal) ? 0
        : name.StartsWith("en", StringComparison.Ordinal) ? 1
        : name.StartsWith("eth", StringComparison.Ordinal) ? 2
        : 5;

    internal static bool IsUsableIpv4(IPAddress value)
    {
        byte[] bytes = value.GetAddressBytes();
        if (IPAddress.IsLoopback(value) || value.Equals(IPAddress.Any) || bytes[0] == 169 && bytes[1] == 254) return false;
        return bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31;
    }

    internal static bool IsUsableIpv6(IPAddress value) => !IPAddress.IsLoopback(value) && !value.Equals(IPAddress.IPv6Any) &&
        !value.IsIPv6LinkLocal && !value.IsIPv6Multicast;

    internal static string FormatUrl(string address, ushort port) => address.Contains(':', StringComparison.Ordinal)
        ? $"http://[{address}]:{port}/"
        : $"http://{address}:{port}/";
}
#endif
