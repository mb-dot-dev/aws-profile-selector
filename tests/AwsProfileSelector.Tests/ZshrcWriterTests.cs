using AwsProfileSelector.Shell;
using Xunit;

namespace AwsProfileSelector.Tests;

public class ZshrcWriterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"zshrc-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public void Creates_file_with_block_when_missing()
    {
        ZshrcWriter.UpsertBlock(_path, "init", "awsp() { :; }");

        var text = File.ReadAllText(_path);
        Assert.Contains("# >>> aws-profile-selector init >>>", text);
        Assert.Contains("awsp() { :; }", text);
        Assert.Contains("# <<< aws-profile-selector init <<<", text);
        Assert.EndsWith("\n", text);
    }

    [Fact]
    public void Appends_block_preserving_existing_content()
    {
        File.WriteAllText(_path, "export EDITOR=vim\n");

        ZshrcWriter.UpsertBlock(_path, "default", "export AWS_PROFILE=alpha");

        var text = File.ReadAllText(_path);
        Assert.Contains("export EDITOR=vim", text);
        Assert.Contains("export AWS_PROFILE=alpha", text);
    }

    [Fact]
    public void Replaces_existing_block_in_place()
    {
        ZshrcWriter.UpsertBlock(_path, "default", "export AWS_PROFILE=alpha");
        ZshrcWriter.UpsertBlock(_path, "default", "export AWS_PROFILE=beta");

        var text = File.ReadAllText(_path);
        Assert.Contains("export AWS_PROFILE=beta", text);
        Assert.DoesNotContain("export AWS_PROFILE=alpha", text);
        // Exactly one start marker remains
        var occurrences = text.Split("# >>> aws-profile-selector default >>>").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void Updates_one_block_without_touching_another()
    {
        ZshrcWriter.UpsertBlock(_path, "init", "awsp() { :; }");
        ZshrcWriter.UpsertBlock(_path, "default", "export AWS_PROFILE=alpha");

        ZshrcWriter.UpsertBlock(_path, "default", "export AWS_PROFILE=beta");

        var text = File.ReadAllText(_path);
        Assert.Contains("awsp() { :; }", text);
        Assert.Contains("export AWS_PROFILE=beta", text);
        Assert.DoesNotContain("export AWS_PROFILE=alpha", text);
    }
}
