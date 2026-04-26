using System.Text.RegularExpressions;

namespace Oficina.Domain.Validation
{
    public static partial class PlacaValidator
    {
        public static bool IsValid(string placa)
        {
            var normalized = Normalize(placa);
            return PlacaAntigaRegex().IsMatch(normalized) || PlacaMercosulRegex().IsMatch(normalized);
        }

        public static string Normalize(string placa) => placa.Trim().ToUpperInvariant().Replace("-", string.Empty);

        [GeneratedRegex("^[A-Z]{3}[0-9]{4}$")]
        private static partial Regex PlacaAntigaRegex();

        [GeneratedRegex("^[A-Z]{3}[0-9][A-Z][0-9]{2}$")]
        private static partial Regex PlacaMercosulRegex();
    }
}
