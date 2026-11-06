using System;
using System.Globalization;
using System.Linq;

namespace ASI.Basecode.Services.Utilities
{
    public static class EquipmentItemCode
    {
        public static string Format(string categoryCode, long number, string itemName)
        {
            if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
            var letters = new string((itemName ?? string.Empty).Where(char.IsLetter).Take(3).ToArray())
                .ToUpperInvariant().PadRight(3, 'X');
            return $"{categoryCode.ToUpperInvariant()}-{number.ToString("D3", CultureInfo.InvariantCulture)}-{letters}";
        }
    }
}
