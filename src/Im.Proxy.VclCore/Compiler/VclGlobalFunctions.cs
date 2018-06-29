using System;
using System.Linq;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclGlobalFunctions
    {
        public static DateTime Now()
        {
            return DateTime.UtcNow;
        }

        public static bool HttpStatusMatches(int statusCode, string commaSeparatedStatuses)
        {
            if (string.IsNullOrWhiteSpace(commaSeparatedStatuses))
            {
                return false;
            }

            return commaSeparatedStatuses
                .Split(',')
                .Select(s => s.Trim(' '))
                .Select(int.Parse)
                .Any(candidateStatusCode => candidateStatusCode == statusCode);
        }
    }
}