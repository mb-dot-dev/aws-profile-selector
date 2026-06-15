namespace AwsProfileSelector.Config;

/// <summary>Resolves the path to the AWS shared config file.</summary>
public static class AwsConfigLocator
{
    public static string Resolve(string? envConfigFile, string homeDirectory)
    {
        if (!string.IsNullOrEmpty(envConfigFile))
        {
            return envConfigFile;
        }

        return Path.Combine(homeDirectory, ".aws", "config");
    }

    /// <summary>Resolves using the current process environment.</summary>
    public static string Resolve()
    {
        var env = Environment.GetEnvironmentVariable("AWS_CONFIG_FILE");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Resolve(env, home);
    }
}
