using AwsProfileSelector.Config;
using AwsProfileSelector.Model;
using Xunit;

namespace AwsProfileSelector.Tests;

public class AwsConfigParserTests
{
    [Fact]
    public void Parses_default_and_named_profiles()
    {
        const string config = """
            [default]
            region = us-east-1
            sso_session = my-sso

            [profile alpha]
            region = eu-west-1
            aws_access_key_id = AKIA...
            """;

        var profiles = AwsConfigParser.Parse(config);

        Assert.Collection(profiles,
            p =>
            {
                Assert.Equal("default", p.Name);
                Assert.Equal("us-east-1", p.Region);
                Assert.Equal(ProfileType.Sso, p.Type);
            },
            p =>
            {
                Assert.Equal("alpha", p.Name);
                Assert.Equal("eu-west-1", p.Region);
                Assert.Equal(ProfileType.Static, p.Type);
            });
    }

    [Fact]
    public void Ignores_non_profile_sections()
    {
        const string config = """
            [sso-session my-sso]
            sso_start_url = https://example.awsapps.com/start
            sso_region = us-east-1

            [services my-services]
            s3 =

            [profile beta]
            role_arn = arn:aws:iam::123:role/r
            """;

        var profiles = AwsConfigParser.Parse(config);

        var single = Assert.Single(profiles);
        Assert.Equal("beta", single.Name);
        Assert.Equal(ProfileType.Role, single.Type);
        Assert.Null(single.Region);
    }

    [Fact]
    public void Infers_process_type_and_skips_comments()
    {
        const string config = """
            # a comment
            ; another comment
            [profile gamma]
            credential_process = /usr/bin/cred
            region = ap-south-1
            """;

        var single = Assert.Single(AwsConfigParser.Parse(config));
        Assert.Equal("gamma", single.Name);
        Assert.Equal(ProfileType.Process, single.Type);
        Assert.Equal("ap-south-1", single.Region);
    }

    [Fact]
    public void Defaults_to_config_type_when_no_credential_keys()
    {
        const string config = """
            [profile delta]
            region = us-west-2
            output = json
            """;

        var single = Assert.Single(AwsConfigParser.Parse(config));
        Assert.Equal(ProfileType.Config, single.Type);
    }

    [Fact]
    public void Returns_empty_for_blank_input()
    {
        Assert.Empty(AwsConfigParser.Parse(""));
        Assert.Empty(AwsConfigParser.Parse("   \n  \n"));
    }
}
