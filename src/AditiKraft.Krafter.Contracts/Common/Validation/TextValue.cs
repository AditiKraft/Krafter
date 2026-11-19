namespace AditiKraft.Krafter.Contracts.Common.Validation;

public static class TextValue
{
    // PostgreSQL text excludes NUL; its UTF-8 encoder also requires paired UTF-16 surrogates.
    // Nullability and credential alphabets remain the responsibility of their own validators.
    public static bool IsValid(string? value)
    {
        if (value is null) return true;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character == '\0') return false;
            if (!char.IsSurrogate(character)) continue;
            if (!char.IsHighSurrogate(character) || index + 1 == value.Length ||
                !char.IsLowSurrogate(value[++index])) return false;
        }
        return true;
    }
}
