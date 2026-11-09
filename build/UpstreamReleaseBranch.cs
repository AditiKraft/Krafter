#nullable enable
using System;

// Template-repository release guard. Both templates exclude this file.
internal static class UpstreamReleaseBranch
{
    internal static string GetImageTag(string? branch)
    {
        const string headsPrefix = "refs/heads/";
        if (branch?.StartsWith(headsPrefix, StringComparison.Ordinal) == true)
            branch = branch[headsPrefix.Length..];

        return branch switch
        {
            "main" => "latest",
            "dev" => "dev",
            _ => throw new InvalidOperationException("Upstream image publication requires the main or dev branch.")
        };
    }
}
