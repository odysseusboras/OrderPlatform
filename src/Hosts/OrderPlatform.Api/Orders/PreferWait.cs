using System.Globalization;

namespace OrderPlatform.Api.Orders;

/// <summary>
/// Reads the <c>wait</c> preference of the HTTP <c>Prefer</c> header (RFC 7240), for example <c>Prefer: wait=5</c>.
/// A preference is a hint: anything that is missing, zero or unreadable means "do not wait", and a long wait is shortened.
/// </summary>
public static class PreferWait
{
    public const string HeaderName = "Prefer";
    public const string AppliedHeaderName = "Preference-Applied";
    private const string WaitToken = "wait";

    public static TimeSpan? Read(string? prefer, TimeSpan maxWait)
    {
        if (string.IsNullOrWhiteSpace(prefer))
        {
            return null;
        }

        foreach (var preference in prefer.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = preference.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && parts[0].Equals(WaitToken, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[1].Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                && seconds > 0)
            {
                var wait = TimeSpan.FromSeconds(seconds);
                return wait < maxWait ? wait : maxWait;
            }
        }

        return null;
    }

    public static string Applied(TimeSpan wait) => $"{WaitToken}={(int)wait.TotalSeconds}";
}
