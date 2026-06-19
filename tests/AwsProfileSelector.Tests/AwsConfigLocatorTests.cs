using AwsProfileSelector.Config;
using Xunit;

namespace AwsProfileSelector.Tests;

public class AwsConfigLocatorTests
{
    [Fact]
    public void Uses_env_override_when_set()
    {
        var path = AwsConfigLocator.Resolve(envConfigFile: "/custom/aws.cfg", homeDirectory: "/home/u");
        Assert.Equal("/custom/aws.cfg", path);
    }

    [Fact]
    public void Falls_back_to_home_aws_config()
    {
        var path = AwsConfigLocator.Resolve(envConfigFile: null, homeDirectory: "/home/u");
        Assert.Equal(Path.Combine("/home/u", ".aws", "config"), path);
    }

    [Fact]
    public void Treats_empty_env_as_unset()
    {
        var path = AwsConfigLocator.Resolve(envConfigFile: "", homeDirectory: "/home/u");
        Assert.Equal(Path.Combine("/home/u", ".aws", "config"), path);
    }
}
