namespace AwsProfileSelector.Shell;

/// <summary>Maintains idempotent marker-delimited managed blocks in a shell rc file.</summary>
public static class ZshrcWriter
{
    public static void UpsertBlock(string filePath, string blockId, string body)
    {
        var start = $"# >>> aws-profile-selector {blockId} >>>";
        var end = $"# <<< aws-profile-selector {blockId} <<<";
        var blockLines = new[] { start, body, end };

        var lines = File.Exists(filePath)
            ? new List<string>(File.ReadAllLines(filePath))
            : new List<string>();

        var startIndex = lines.IndexOf(start);
        if (startIndex >= 0)
        {
            var endIndex = lines.IndexOf(end, startIndex);
            if (endIndex < 0)
            {
                // Malformed: end marker absent — remove only the orphaned start marker.
                endIndex = startIndex;
            }

            lines.RemoveRange(startIndex, endIndex - startIndex + 1);
            lines.InsertRange(startIndex, blockLines);
        }
        else
        {
            if (lines.Count > 0 && lines[^1].Length > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(blockLines);
        }

        File.WriteAllText(filePath, string.Join('\n', lines) + "\n");
    }
}
