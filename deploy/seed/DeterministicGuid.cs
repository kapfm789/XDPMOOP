using System.Security.Cryptography;
using System.Text;

namespace Oism.Seed;

// UUID phiên bản 5 (RFC 4122) dựng từ một chuỗi: cùng chuỗi luôn ra cùng id. Seed đặt id của đơn lịch sử, event và
// Idempotency-Key theo cách này, nên chạy lại không sinh bản ghi mới.
internal static class DeterministicGuid
{
    private static readonly Guid Namespace = new("5b0e4c1e-8f7a-4b8e-9a51-0d1f3c2e7a64");

    public static Guid From(string name)
    {
        Span<byte> space = stackalloc byte[16];
        Namespace.TryWriteBytes(space, bigEndian: true, out _);
        var hash = SHA1.HashData([.. space, .. Encoding.UTF8.GetBytes(name)]);

        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}
