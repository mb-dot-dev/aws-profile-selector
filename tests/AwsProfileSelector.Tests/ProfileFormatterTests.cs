using AwsProfileSelector.Model;
using AwsProfileSelector.Ui;
using Xunit;

namespace AwsProfileSelector.Tests;

public class ProfileFormatterTests
{
    [Fact]
    public void Formats_name_region_and_type()
    {
        var label = ProfileFormatter.Format(new AwsProfile("alpha", "eu-west-1", ProfileType.Role));
        Assert.Equal("alpha  (eu-west-1, role)", label);
    }

    [Fact]
    public void Omits_region_when_null()
    {
        var label = ProfileFormatter.Format(new AwsProfile("beta", null, ProfileType.Sso));
        Assert.Equal("beta  (sso)", label);
    }
}
