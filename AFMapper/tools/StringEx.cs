
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AFMapper;

/// <summary>
/// Extension methods for System.String
/// </summary>
public static class StringEx
{
    /// <summary>
    /// Checks if current text is empty, contains no text or 
    /// contains only white spaces
    /// </summary>
    /// <param name="source">current text</param>
    /// <returns>true text is empty</returns>
    public static bool IsEmpty(this string? source)
    {
        return string.IsNullOrWhiteSpace(source);
    }

    /// <summary>
    /// Encrypt the string using Crypto.Encrypt.
    /// </summary>
    /// <param name="source">text to be encrypted</param>.
    /// <returns>encrypted text as byte[]</returns>
    public static byte[] Encrypt(this string source)
    {
        return CryptoAES256.Encrypt(Encoding.UTF8.GetBytes(source));
    }

    /// <summary>
    /// Create SHA256 hash of the string
    /// </summary>
    /// <param name="source">String</param>.
    /// <returns>Hash of the string</returns>
    public static string GetSHA256Hash(this string source)
    {
        using var hasher = SHA256.Create();
        return BitConverter.ToString(hasher.ComputeHash(Encoding.UTF8.GetBytes(source)));
    }

    /// <summary>
    /// Create MD5 hash of the string
    /// </summary>
    /// <param name="source">String</param>.
    /// <returns>Hash of the string</returns>
    public static string GetMD5Hash(this string source)
    {
        using var hasher = MD5.Create();
        return BitConverter.ToString(hasher.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "");
    }

    /// <summary>
    /// Checks if current text is empty, contains no text or 
    /// contains only white spaces
    /// </summary>
    /// <param name="source">current text</param>
    /// <returns>true text is NOT empty</returns>
    public static bool IsNotEmpty(this string? source)
    {
        return !string.IsNullOrWhiteSpace(source);
    }

    /// <summary>
    /// Returns a formatted text and replaces string.Format(text, val1, val2)
    /// <example>
    /// "The value is currently {0} EUR".DisplayWith(100.55);
    /// </example>
    /// </summary>
    /// <param name="source">Text</param>.
    /// <param name="args">arguments to be included in the text</param>.
    /// <returns>extended text</returns>
    public static string DisplayWith(this string source, params object[] args)
    {
        return string.Format(source, args);
    }

    /// <summary>
    /// Returns the number of characters from the left
    /// If the string is shorter, the entire string is returned - no 'out of index' error is generated.
    /// </summary>
    /// <param name="source">String</param>.
    /// <param name="characters">Number of characters</param>.
    /// <returns>characters from left side of string</returns>
    public static string Left(this string source, int characters)
    {
        return source.Length > characters ? source[..characters] : source;
    }

    /// <summary>
    /// Returns the number of characters from the right
    /// If the string is shorter, the entire string is returned - no 'out of index' error is generated.
    /// </summary>
    /// <param name="source">String</param>.
    /// <param name="characters">Number of characters</param>.
    /// <returns>characters from right side of string</returns>
    public static string Right(this string source, int characters)
    {
        return source.Length > characters ? source.Substring(source.Length - characters, characters) : source;
    }

    /// <summary>
    /// Checks whether the string contains illegal characters.
    /// </summary>
    /// <param name="source">Text to be checked</param>
    /// <param name="notallowed">not allowed characters</param>
    /// <returns>true, if the text contains illegal characters</returns>
    public static bool ContainsNotAllowedChars(this string source, string notallowed)
    {
        return source.Select(c => notallowed.Contains(c) ? c : char.MinValue)
            .Where(c => c != char.MinValue).ToArray()
            .Length > 0;
    }

    /// <summary>
    /// Remove all characters that are not included in the allowed characters.
    /// </summary>
    /// <param name="source">string</param>
    /// <param name="chars">characters to remove</param>.
    /// <returns>String without the characters to be removed</returns>
    public static string RemoveAllExcept(this string source, string chars)
    {
        return new string(source.Select(c => chars.Contains(c) ? c : char.MinValue)
            .ToArray()).Replace(new string(new [] { char.MinValue }), "");
    }

    /// <summary>
    /// Checks whether the string contains only allowed characters.
    /// </summary>
    /// <param name="source">Text to be checked</param>
    /// <param name="allowed">allowed characters</param>
    /// <returns>true, if the text contains only allowed characters</returns>
    public static bool ContainsOnlyAllowedChars(this string source, string allowed)
    {
        return source.Select(c => allowed.Contains(c) ? char.MinValue : c)
            .Where(c => c != char.MinValue).ToArray().Length <= 0;
    }

    /// <summary>
    /// Counts how many times a string occurs in another string.
    /// Extends the class String
    /// <example>
    /// string test="This is a test";
    /// int blank=test.Count(" ");
    /// </example>
    /// </summary>
    /// <param name="source">String to search in</param>.
    /// <param name="search">String to be searched</param>.
    /// <returns>number of occurrences of search in source</returns>
    public static int Count(this string source, string search)
    {
        return new Regex(Regex.Escape(search)).Matches(source).Count;
    }

    /// <summary>
    /// Returns a substring
    /// </summary>
    /// <param name="source">String whose substring is to be determined</param>.
    /// <param name="startpos">Position of the first character</param>.
    /// <param name="endpos">Position of the last character</param>.
    /// <returns>substring</returns>
    public static string SubString(this string source, int startpos, int endpos)
    {
        return source.Substring(startpos, endpos - startpos + 1);
    }

    /// <summary>
    /// Prüft, ob das erste Zeichen eine Ziffer ist.
    /// </summary>
    /// <param name="source">der zu prüfende Text</param>
    /// <returns>true wenn das erste Zeichen eine Ziffer ist, sonst false (Leerzeichen werden ignoriert)</returns>
    public static bool IsDigit(this string source)
    {
        return "0123456789".Contains(source.Trim().Left(1));
    }

    /// <summary>
    /// Returns all digits at the beginning of a string.
    /// Can be used, for example, to extract a postcode of unknown length from a string if it starts with the postcode.
    /// <example>
    /// string test="01309 Dresden";
    /// string plz=test.GetDigits();
    /// </example>
    /// </summary>
    /// <param name="source">the text to be tested</param>.
    /// <returns>All digits with which a string begins (spaces are ignored)</returns>
    public static string GetDigits(this string source)
    {
        string ret = "";

        int max = source.Trim().Length;
        
        for (int i = 0; i < max; i++)
        {
            if (source.Trim().Substring(i, 1).IsDigit())
                ret += source.Trim().Substring(i, 1);
            else
                break;
        }

        return ret;
    }

    /// <summary>
    /// Replace all digits with a placeholder
    /// </summary>
    /// <param name="source">the text in which to replace the digits</param>.
    /// <param name="pattern">character to replace the digits in the string</param>.
    /// <returns>Text containing the placeholder instead of the digits</returns>
    public static string ReplaceDigits(this string source, char pattern)
    {
        StringBuilder sb = new(source);

        foreach (char chr in "0123456789")
            sb.Replace(chr, pattern);

        return sb.ToString();
    }


    /// <summary>
    /// Comparison with a simplified pattern
    /// 
    /// ? = exactly one arbitrary character 
    /// % = no or any number of characters (any string)
    /// * = no or any number of characters, but no space (=one word)
    /// # = exactly one of the digits 0-9
    /// 
    /// a Guid : ????????-????-????-????-????????????
    /// an email address : %?@?%.?%
    /// a simple date : ##.##.####
    /// </summary>
    /// <param name="source">string to match</param>
    /// <param name="simplepattern"></param>
    /// <returns>true, if source matches the pattern</returns>
    public static bool Like(this string source, string simplepattern)
    {
        return simplepattern.GetRegexFromPattern().IsMatch(source);
    }

    /// <summary>
    /// Converts a simple pattern into a RegEx expression that can then be used for comparisons etc.
    /// 
    /// The simplified pattern can contain the following characters:
    /// 
    /// ? = exactly one character 
    /// % = no or any number of characters (any string)
    /// * = no or any number of characters, but no space (=one word)
    /// # = exactly one of the digits 0-9
    /// 
    /// a Guid : ????????-????-????-????-????????????
    /// an email address : %?@?%.?%
    /// a simple date : ##.##.####
    /// </summary>
    /// <param name="pattern">simplified pattern for the search</param>.
    /// <returns>RegEx for the simplified pattern</returns>
    public static Regex GetRegexFromPattern(this string pattern)
    {
        pattern = "^" + pattern;
        return new Regex(pattern
                .Replace("\\", "\\\\")
                .Replace(".", "\\.")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("[", "\\[")
                .Replace("]", "\\]")
                .Replace("+", "\\+")
                .Replace("$", "\\$")
                .Replace(" ", "\\s")
                .Replace("#", "[0-9]")
                .Replace("?", ".")
                .Replace("*", "\\w*")
                .Replace("%", ".*")
            , RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Create the phonetic code of a string.
    /// 
    /// The so-called Cologne Phonetic is used for phonetic coding.
    /// </summary>
    /// <param name="input">Input string</param>.
    /// <returns>phonetic code (Cologne Phonetic)</returns>
    public static string PhoneticEncode(this string input)
    {
        string[] words = input.Split(new[] { ' ', '\r', '\n', '-', '/', ',', ';', '&' },
            StringSplitOptions.RemoveEmptyEntries);
        string ret = "";

        foreach (string word in words)
        {
            char[] valueChars = word.ToUpperInvariant().ToCharArray();

            // create an array for all the characters without specialities
            char[] value0Chars = { 'A', 'E', 'I', 'J', 'O', 'U', 'Y', 'Ä', 'Ö', 'Ü' };
            char[] value1Chars = { 'B' };
            char[] value3Chars = { 'F', 'V', 'W' };
            char[] value4Chars = { 'G', 'K', 'Q' };
            char[] value5Chars = { 'L' };
            char[] value6Chars = { 'M', 'N' };
            char[] value7Chars = { 'R' };
            char[] value8Chars = { 'S', 'Z', 'ß' };

            StringBuilder cpCode = new();

            for (int i = 0; i < valueChars.Length; i++)
            {
                char previousChar = i > 0 ? valueChars[i - 1] : ' ';
                char currentChar = valueChars[i];
                char nextChar = i < valueChars.Length - 1 ? valueChars[i + 1] : ' ';

                bool isFirstChar = i == 0 || !char.IsLetter(previousChar);

                if (!char.IsLetter(currentChar))
                {
                    if (char.IsWhiteSpace(currentChar))
                        cpCode.Append(' ');

                    continue;
                }

                if (value0Chars.Contains(currentChar))
                {
                    cpCode.Append('0');
                    continue;
                }

                if (value1Chars.Contains(currentChar))
                {
                    cpCode.Append('1');
                    continue;
                }

                if (value3Chars.Contains(currentChar))
                {
                    cpCode.Append('3');
                    continue;
                }

                if (value4Chars.Contains(currentChar))
                {
                    cpCode.Append('4');
                    continue;
                }

                if (value5Chars.Contains(currentChar))
                {
                    cpCode.Append('5');
                    continue;
                }

                if (value6Chars.Contains(currentChar))
                {
                    cpCode.Append('6');
                    continue;
                }

                if (value7Chars.Contains(currentChar))
                {
                    cpCode.Append('7');
                    continue;
                }

                if (value8Chars.Contains(currentChar))
                {
                    cpCode.Append('8');
                    continue;
                }

                switch (currentChar)
                {
                    case 'C' when isFirstChar:
                    {
                        cpCode.Append(new[] { 'A', 'H', 'K', 'L', 'O', 'Q', 'R', 'U', 'X' }.Contains(nextChar)
                            ? '4'
                            : '8');
                        break;
                    }
                    case 'C' when new[] { 'S', 'Z', 'ß' }.Contains(previousChar):
                        cpCode.Append('8');
                        break;
                    case 'C' when new[] { 'A', 'H', 'K', 'O', 'Q', 'U', 'X' }.Contains(nextChar):
                        cpCode.Append('4');
                        break;
                    case 'C':
                        cpCode.Append('8');
                        break;
                    case 'D':
                    case 'T':
                    {
                        cpCode.Append(new[] { 'C', 'S', 'Z', 'ß' }.Contains(nextChar) ? '8' : '2');
                        break;
                    }
                    case 'P' when nextChar.Equals('H'):
                        cpCode.Append('3');
                        break;
                    case 'P':
                        cpCode.Append('1');
                        break;
                    case 'X' when new[] { 'C', 'K', 'Q' }.Contains(previousChar):
                        cpCode.Append('8');
                        break;
                    case 'X':
                        cpCode.Append('4');
                        cpCode.Append('8');
                        break;
                }
            }

            // cleanup the code (remove double characters and remove 0 values)
            StringBuilder cleanedCpCode = new(cpCode.Length);

            for (int i = 0; i < cpCode.Length; i++)
            {
                char lastAddedChar = cleanedCpCode.Length > 0 ? cleanedCpCode[^1] : ' ';
                
                if (lastAddedChar == cpCode[i]) continue;

                if (cpCode[i] != '0' || (cpCode[i] == '0' && lastAddedChar == ' '))
                    cleanedCpCode.Append(cpCode[i]);
            }

            ret += valueChars[0] + cleanedCpCode.ToString() + "|";
        }

        return ret;
    }
}

