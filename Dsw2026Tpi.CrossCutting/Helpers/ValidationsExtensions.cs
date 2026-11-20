using System.Text.RegularExpressions;

namespace Dsw2026Tpi.CrossCutting.Helpers;

public static class ValidationsExtensions
{
    public const string EmailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$";
    public static bool IsEmailValid(this string? email)
    {
        return !string.IsNullOrWhiteSpace(email) &&
            Regex.IsMatch(email, EmailPattern);
    }
    public static bool IsDniValid(this long dni, int minDigits = 7, int maxDigits = 8)
    {
        if (dni <= 0)
            return false;

        var length = dni.ToString().Length;
        return length >= minDigits && length <= maxDigits;
    }
}
