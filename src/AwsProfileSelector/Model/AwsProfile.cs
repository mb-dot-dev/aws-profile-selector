namespace AwsProfileSelector.Model;

/// <summary>How a profile obtains credentials, inferred from its config keys.</summary>
public enum ProfileType
{
    Sso,
    Role,
    Static,
    Process,
    Config,
}

/// <summary>Whether a selection applies to the current session or is persisted as the default.</summary>
public enum ApplyMode
{
    Session,
    Default,
}

/// <summary>A selectable AWS profile parsed from the config file.</summary>
public sealed record AwsProfile(string Name, string? Region, ProfileType Type);
