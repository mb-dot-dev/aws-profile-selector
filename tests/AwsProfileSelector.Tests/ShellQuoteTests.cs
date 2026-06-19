using AwsProfileSelector.Shell;

namespace AwsProfileSelector.Tests;

public class ShellQuoteTests
{
    [Theory]
    [InlineData("simple",       "'simple'")]
    [InlineData("with space",   "'with space'")]
    [InlineData("with$var",     "'with$var'")]
    [InlineData("with$(cmd)",   "'with$(cmd)'")]
    [InlineData("with`cmd`",    "'with`cmd`'")]
    [InlineData("with'quote",   "'with'\\''quote'")]
    public void Escape_wraps_value_in_single_quotes_preventing_shell_expansion(string input, string expected)
    {
        Assert.Equal(expected, ShellQuote.Escape(input));
    }
}
