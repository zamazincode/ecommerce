using System.Security.Cryptography;

namespace Commerce.Domain.Orders;

public static class OrderNumberGenerator
{
    public const string Prefix = "ORD";
    private const int SuffixLength = 6;

    /// Karıştırılabilir karakterler YOK: 0/O, 1/I/l.
    /// Müşteri telefonda sipariş numarasını okuyacak — "sıfır mı O mu"
    /// tartışması yaşatma.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <param name="utcNow">TimeProvider'dan gelir — DateTime.UtcNow değil.</param>
    public static string Generate(DateTime utcNow)
        => $"{Prefix}-{utcNow:yyyyMMdd}-{RandomSuffix()}";

    private static string RandomSuffix()
    {
        var chars = new char[SuffixLength];
        for (var i = 0; i < SuffixLength; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return new string(chars);
    }
}
