using System.IO;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tools.DotNet;

internal partial class Build : FalloutBuild
{
    public static int Main() => Execute<Build>(x => x.Validate);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    private readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    // The source repository validates both compositions; generated apps have one solution.
    private AbsolutePath SolutionPath => File.Exists(RootDirectory / "AditiKraft.Krafter.Dev.slnx")
        ? RootDirectory / "AditiKraft.Krafter.Dev.slnx"
        : RootDirectory / "AditiKraft.Krafter.slnx";

    private AbsolutePath TestProjectPath => RootDirectory / "tests" / "AditiKraft.Krafter.Tests" / "AditiKraft.Krafter.Tests.csproj";

    private Target Restore => _ => _
        .Executes(() => DotNetTasks.DotNetRestore(settings => settings.SetProjectFile(SolutionPath)));

    private Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() => DotNetTasks.DotNetBuild(settings => settings
            .SetProjectFile(SolutionPath)
            .SetConfiguration(Configuration)
            .EnableNoRestore()));

    private Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() => DotNetTasks.DotNetTest(settings => settings
            .SetProjectFile(TestProjectPath)
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .EnableNoRestore()));

    private Target Validate => _ => _
        .Description("Restore, build and test the application without publishing or deploying")
        .DependsOn(Test);
}
