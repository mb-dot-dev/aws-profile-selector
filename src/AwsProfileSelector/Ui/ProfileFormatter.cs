using AwsProfileSelector.Model;

namespace AwsProfileSelector.Ui;

/// <summary>Renders a profile as a single-line label for the selection list.</summary>
public static class ProfileFormatter
{
    public static string Format(AwsProfile profile)
    {
        var type = profile.Type.ToString().ToLowerInvariant();
        return profile.Region is { Length: > 0 } region
            ? $"{profile.Name}  ({region}, {type})"
            : $"{profile.Name}  ({type})";
    }
}
