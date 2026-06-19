namespace AwsProfileSelector.Shell;

public static class ShellQuote
{
    /// <summary>Wraps value in single quotes, escaping any embedded single quotes.</summary>
    public static string Escape(string value) =>
        "'" + value.Replace("'", "'\\''") + "'";
}
