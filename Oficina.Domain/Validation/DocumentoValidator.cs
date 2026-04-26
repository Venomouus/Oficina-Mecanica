namespace Oficina.Domain.Validation
{
    public static class DocumentoValidator
    {
        public static bool IsValid(string documento)
        {
            var digits = OnlyDigits(documento);
            return digits.Length switch
            {
                11 => IsCpf(digits),
                14 => IsCnpj(digits),
                _ => false
            };
        }

        public static string Normalize(string documento) => OnlyDigits(documento);

        private static string OnlyDigits(string value) => new(value.Where(char.IsDigit).ToArray());

        private static bool IsCpf(string cpf)
        {
            if (cpf.Distinct().Count() == 1)
                return false;

            var first = CalculateCpfDigit(cpf[..9], 10);
            var second = CalculateCpfDigit(cpf[..9] + first, 11);
            return cpf.EndsWith($"{first}{second}", StringComparison.Ordinal);
        }

        private static bool IsCnpj(string cnpj)
        {
            if (cnpj.Distinct().Count() == 1)
                return false;

            var first = CalculateCnpjDigit(cnpj[..12]);
            var second = CalculateCnpjDigit(cnpj[..12] + first);
            return cnpj.EndsWith($"{first}{second}", StringComparison.Ordinal);
        }

        private static int CalculateCpfDigit(string baseNumber, int initialWeight)
        {
            var sum = baseNumber.Select((digit, index) => (digit - '0') * (initialWeight - index)).Sum();
            var result = 11 - (sum % 11);
            return result >= 10 ? 0 : result;
        }

        private static int CalculateCnpjDigit(string baseNumber)
        {
            int[] weights = baseNumber.Length == 12
                ? new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 }
                : new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

            var sum = baseNumber.Select((digit, index) => (digit - '0') * weights[index]).Sum();
            var result = sum % 11;
            return result < 2 ? 0 : 11 - result;
        }
    }
}
