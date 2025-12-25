using System.Text;

namespace Backend.Common;

public static class PasswordGenerator
{
    private static readonly Random Random = new();
    private const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowercaseLetters = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string SpecialCharacters = "!@#$%^&*()";

    public static string GeneratePassword(int length = 8)
    {
        var passwordBuilder = new StringBuilder();
        // Ensure the password meets the criteria by adding at least one of each required character type
        passwordBuilder.Append(GetRandomCharacter(UppercaseLetters));
        passwordBuilder.Append(GetRandomCharacter(LowercaseLetters));
        passwordBuilder.Append(GetRandomCharacter(Digits));
        passwordBuilder.Append(GetRandomCharacter(SpecialCharacters));

        // Fill the rest of the password length with random characters from all types
        string allCharacters = UppercaseLetters + LowercaseLetters + Digits + SpecialCharacters;
        int remainingLength = length - 4; // Subtract 4 because we've already added one of each type
        for (int i = 0; i < remainingLength; i++)
        {
            passwordBuilder.Append(GetRandomCharacter(allCharacters));
        }

        // Shuffle the constructed password to randomize character positions
        char[] passwordArray = passwordBuilder.ToString().ToCharArray();
        passwordArray = ShuffleArray(passwordArray);
        return new string(passwordArray);
    }

    private static char GetRandomCharacter(string validCharacters)
    {
        int index = Random.Next(validCharacters.Length);
        return validCharacters[index];
    }

    private static T[] ShuffleArray<T>(T[] array)
    {
        int n = array.Length;
        for (int i = 0; i < n; i++)
        {
            int r = i + Random.Next(n - i);
            (array[r], array[i]) = (array[i], array[r]);
        }

        return array;
    }
}
