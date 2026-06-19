using AwsProfileSelector.Model;

namespace AwsProfileSelector.Config;

/// <summary>Parses AWS shared-config text into the list of selectable profiles.</summary>
public static class AwsConfigParser
{
    public static IReadOnlyList<AwsProfile> Parse(string content)
    {
        var profiles = new List<AwsProfile>();

        string? currentName = null;
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Flush()
        {
            if (currentName is null)
            {
                return;
            }

            keys.TryGetValue("region", out var region);
            profiles.Add(new AwsProfile(currentName, region, InferType(keys)));
            keys.Clear();
            currentName = null;
        }

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';')
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                Flush();
                var inner = line[1..^1].Trim();
                if (inner == "default")
                {
                    currentName = "default";
                }
                else if (inner.StartsWith("profile ", StringComparison.Ordinal))
                {
                    currentName = inner["profile ".Length..].Trim();
                }
                // any other section (sso-session, services, ...) is ignored
                continue;
            }

            if (currentName is null)
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            keys[key] = value;
        }

        Flush();
        return profiles;
    }

    private static ProfileType InferType(IReadOnlyDictionary<string, string> keys)
    {
        if (keys.ContainsKey("role_arn"))
        {
            return ProfileType.Role;
        }

        if (keys.ContainsKey("sso_session") || keys.ContainsKey("sso_account_id"))
        {
            return ProfileType.Sso;
        }

        if (keys.ContainsKey("aws_access_key_id"))
        {
            return ProfileType.Static;
        }

        if (keys.ContainsKey("credential_process"))
        {
            return ProfileType.Process;
        }

        return ProfileType.Config;
    }
}
