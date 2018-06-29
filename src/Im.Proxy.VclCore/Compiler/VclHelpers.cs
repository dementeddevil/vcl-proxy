using System.Globalization;

namespace Im.Proxy.VclCore.Compiler
{
    public static class VclHelpers
    {
        public static string SafeIdentifier(this string identifier, string prefix = null)
        {
            return (prefix ?? string.Empty) + CultureInfo
                       .CurrentCulture
                       .TextInfo
                       .ToTitleCase(identifier.Replace("-", " "))
                       .Replace(" ", string.Empty);
        }
    }
}