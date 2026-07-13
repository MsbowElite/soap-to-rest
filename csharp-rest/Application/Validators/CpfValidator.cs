namespace CsharpRest.Application.Validators
{
    internal static class CpfValidator
    {
        public static bool IsValid(string? cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf)) return false;
            var digits = new string(cpf.Where(char.IsDigit).ToArray());
            if (digits.Length != 11) return false;

            // Discard known invalid CPFs (all digits equal)
            if (new string(digits[0], 11) == digits) return false;

            int[] nums = digits.Select(c => c - '0').ToArray();

            // first verifier
            int sum = 0;
            for (int i = 0; i < 9; i++) sum += nums[i] * (10 - i);
            int rem = sum % 11;
            int dig1 = rem < 2 ? 0 : 11 - rem;
            if (nums[9] != dig1) return false;

            // second verifier
            sum = 0;
            for (int i = 0; i < 10; i++) sum += nums[i] * (11 - i);
            rem = sum % 11;
            int dig2 = rem < 2 ? 0 : 11 - rem;
            if (nums[10] != dig2) return false;

            return true;
        }
    }
}
