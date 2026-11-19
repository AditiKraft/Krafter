namespace AditiKraft.Krafter.Tests.Configuration;

public sealed class UpstreamReleaseBranchTests
{
    [Theory]
    [InlineData("main", "latest")]
    [InlineData("dev", "dev")]
    [InlineData("refs/heads/main", "latest")]
    [InlineData("refs/heads/dev", "dev")]
    public void ExactReleaseBranchesSelectTheirImageTag(string branch, string expected) =>
        Assert.Equal(expected, UpstreamReleaseBranch.GetImageTag(branch));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("HEAD")]
    [InlineData("feature/main")]
    [InlineData("release/dev")]
    [InlineData("main/feature")]
    [InlineData("refs/heads/feature/main")]
    [InlineData("refs/tags/main")]
    [InlineData("refs/remotes/origin/main")]
    [InlineData("MAIN")]
    public void OtherRefsCannotSelectAReleaseTag(string? branch) =>
        Assert.Throws<InvalidOperationException>(() => UpstreamReleaseBranch.GetImageTag(branch));
}
