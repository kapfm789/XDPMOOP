namespace Oism.Catalog.Domain;

// EAN-13: 12 chữ số dữ liệu và một số kiểm tra (UC-PROD-03).
public static class Ean13
{
    // GS1 dành dải 200 đến 299 cho lưu hành nội bộ; mã tự sinh dùng 200.
    public const string InternalPrefix = "200";

    public const long MaxSequence = 999_999_999;

    public static bool IsValid(string code) =>
        code.Length == 13 && code.All(char.IsAsciiDigit) && CheckDigit(code) == code[12] - '0';

    // Mã tự sinh thứ `sequence` của một tenant: tiền tố 200, 9 chữ số, số kiểm tra.
    public static string Generate(long sequence)
    {
        // ponytail: một tenant hết 1 tỷ mã thì ném lỗi; khi đó mở thêm tiền tố 201 đến 299.
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, MaxSequence);

        var body = $"{InternalPrefix}{sequence:D9}";
        return $"{body}{CheckDigit(body)}";
    }

    // Số thứ tự nằm trong một mã mang tiền tố nội bộ.
    public static long SequenceOf(string code) => long.Parse(code.AsSpan(InternalPrefix.Length, 9));

    // Tính trên 12 chữ số đầu: vị trí lẻ nhân 1, vị trí chẵn nhân 3.
    private static int CheckDigit(string code)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (code[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - sum % 10) % 10;
    }
}
