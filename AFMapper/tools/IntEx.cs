namespace AFMapper;

/// <summary>
/// Extension methods for Int32
/// </summary>
public static class Int32Ex
{
    /// <summary>
    /// Range erlauben für Int32
    /// 
    /// Beispiel
    /// 
    /// foreach (int value in 1..100)
    /// ....
    /// </summary>
    /// <param name="range">range of Int32 values to enumerate</param>
    /// <returns>the enumerator for that range</returns>
    public static CustomIntEnumerator GetEnumerator(this Range range)
    {
        return new CustomIntEnumerator(range);
    }

    /// <summary>
    /// Range erlauben für Int32
    /// 
    /// Beispiel
    /// 
    /// foreach (int value in ..100)
    /// ....
    /// </summary>
    /// <param name="end">end of Int32 values to enumerate</param>
    /// <returns>the enumerator for that range</returns>
    public static CustomIntEnumerator GetEnumerator(this int end)
    {
        return new CustomIntEnumerator(new Range(0, end));
    }
}

/// <summary>
/// Enumerator to use Int32 with Range
/// </summary>
public ref struct CustomIntEnumerator
{
    private readonly int _max;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="range"></param>
    public CustomIntEnumerator(Range range)
    {
        if (range.End.IsFromEnd)
            throw new NotSupportedException();

        Current = range.Start.Value - 1;
        _max = range.End.Value;
    }

    /// <summary>
    /// Current value
    /// </summary>
    public int Current { get; private set; }

    /// <summary>
    /// Move to next value
    /// </summary>
    /// <returns></returns>
    public bool MoveNext()
    {
        Current++;
        return Current <= _max;
    }
}
