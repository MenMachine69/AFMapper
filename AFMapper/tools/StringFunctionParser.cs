using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AFMapper;

/// <summary>
/// Universal parser that replaces function calls and placeholders inside a string against language
/// specific function calls.
/// 
/// This can be used for example in a SQL Query Parser that supports multiple SQL dialects.
/// Because the replacement can also a longer part of source code this can also be used for something
/// like 'Macros' in Scripts (replace a simple function call or placeholder against a longer code snippet).  
/// </summary>
public sealed class StringFunctionParser
{
    private List<StringParserSnippet> _snippets = new();

    /// <summary>
    /// Assign available snippets...
    /// </summary>
    /// <param name="snippets">Dictionary with available snippets</param>
    public void SetSnippets(List<StringParserSnippet> snippets)
    {
        _snippets = snippets;
    }

    /// <summary>
    /// Add more Snippets
    /// </summary>
    /// <param name="snippets">List with snippets to add</param>
    public void AddSnippets(IEnumerable<StringParserSnippet> snippets)
    {
        _snippets.AddRange(snippets);
    }

    /// <summary>
    /// Parse a string and replace all function calls and placeholders against the given snippets
    /// </summary>
    /// <param name="query">source string</param>
    /// <returns>strings with replacments</returns>
    /// <exception cref="ArgumentException">Exception if parameter count in a function mismatch</exception>
    public string Parse(string query)
    {
        StringBuilder sbInput = new StringBuilder(query);
        List<string> parameters = new();

        foreach (var func in CollectionsMarshal.AsSpan(_snippets))
        {
            if (func.IsPlaceHolder)
            {
                sbInput.Replace(func.FuncName.Trim(), func.SnippetCode.Trim());
                query = sbInput.ToString();
                continue;
            }

            while (_extractParameters(func.Pattern.Match(query), out var matchcode, ref parameters))
            {
                if (parameters.Count != func.ParameterCount)
                    continue;

                if (parameters.Count > 0)
                {
                    StringBuilder sbReplace = new StringBuilder(func.SnippetCode);

                    for (int i = 1; i <= parameters.Count; i++)
                        sbReplace.Replace(string.Concat("<p", i, ">"), parameters[i - 1]);

                    sbInput.Replace(string.Concat(func.FuncName, "(", matchcode, ")"), sbReplace.ToString());
                }
                else
                    sbInput.Replace(string.Concat(func.FuncName, "(", matchcode, ")"), func.SnippetCode);

                query = sbInput.ToString();
            }

        }

        return query;
    }

    bool _extractParameters(Match match, out string matchcode, ref List<string> parameters)
    {
        parameters.Clear();

        if (!match.Success)
        {
            matchcode = "";
            return false;
        }

        matchcode = match.Groups["params"].Value;

        int depth = 0;
        int start = 0;

        for (int i = 0; i < matchcode.Length; i++)
        {
            if (matchcode[i] == '(')
                depth++;
            else if (matchcode[i] == ')')
                depth--;

            if (matchcode[i] != ',' || depth != 0)
                continue;

            if (!string.IsNullOrWhiteSpace(matchcode[start..i]))
                parameters.Add(matchcode[start..i]);

            start = i + 1;
        }

        // Add the final parameter to the list
        if (!string.IsNullOrWhiteSpace(matchcode[start..]))
            parameters.Add(matchcode[start..]);
        
        return true;
    }
}


/// <summary>
/// Snippet for a string Parser
/// </summary>
public struct StringParserSnippet
{

    private Regex? _pattern = null;

    /// <summary>
    /// Constructor
    /// </summary>
    public StringParserSnippet(string funcName, int paramCount, string snippet)
    {
        FuncName = funcName;
        ParameterCount = paramCount;
        SnippetCode = snippet;
    }

    /// <summary>
    /// Pattern for Regex matching
    /// </summary>
    public Regex Pattern
    {
        get
        {
            if (_pattern != null) return _pattern;

            string pattern =
                @$"{FuncName}\s*\((?<params>[^()]*(\((?<depth>)[^()]*\)(?<-depth>[^()]*)+)*(?(depth)(?!)))\)";
            _pattern = new Regex(pattern);

            return _pattern;
        }
    }

    /// <summary>
    /// Name of the Function (like 'CRUpper')
    ///
    /// This snippet is used to replace all function calls like 'CRUpper(...)' against the given snippet code.
    ///
    /// Use a FuncName starting with '#' for PlaceHolder snippets (like '#CURRENTDATE#').
    /// </summary>
    public string FuncName { get; }

    /// <summary>
    /// Is this a placeholder snippet?
    ///
    /// PlaceHolder snippets are not functions with parameters but a placeholder for a snippet code.
    /// FuncName of this Placeholders are allways starting with '#'.
    /// </summary>
    public bool IsPlaceHolder => FuncName[0] == '#';

    /// <summary>
    /// Total count of parameters (count of pX in function call)
    /// </summary>
    public int ParameterCount { get; }

    /// <summary>
    /// Code that replaces the function call.
    /// Use pX as parameters inside the function call.
    /// <example>
    /// DATEFROMPARTS(&lt;p1&gt;, &lt;p2&gt;, &lt;p3&gt;)
    /// </example>
    /// </summary>
    public string SnippetCode { get; }
}

