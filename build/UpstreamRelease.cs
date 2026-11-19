using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using Fallout.Common;
using Fallout.Common.Git;
using Fallout.Common.IO;
using Fallout.Common.Tools.Docker;
using Fallout.Common.Tools.DotNet;
using NuGet.Versioning;

// Template-repository release actions. Both templates exclude this file.
internal partial class Build
{
    [Parameter("NuGet API key for an explicitly requested template release")]
    private readonly string NuGetPAT;

    [Parameter("Required template release version, for example 1.2.3 or 1.2.3-preview.1")]
    private readonly string TemplateVersion;

    [Parameter("GitHub Container Registry token for an explicitly requested image release")]
    private readonly string PAT;

    [Parameter("Container registry login user")]
    private readonly string ContainerUser = "aditikraft";

    [Parameter("Container repository namespace (without the registry hostname)")]
    private readonly string ContainerNamespace = "aditikraft";

    [Parameter("Optional deployment webhook for the explicit deployment target")]
    private readonly string DeploymentWebhookUrl;

    [GitRepository] private readonly GitRepository Repository;

    private AbsolutePath TemplateProjectPath => RootDirectory / "AditiKraft.Krafter.Templates.csproj";
    private AbsolutePath TemplateOutputDirectory => RootDirectory / "nupkg";
    private AbsolutePath TemplatePackagePath => TemplateOutputDirectory / $"AditiKraft.Krafter.Templates.{ReleaseVersion}.nupkg";

    private string ReleaseVersion
    {
        get
        {
            if (!NuGetVersion.TryParse(TemplateVersion, out NuGetVersion version) ||
                version.HasMetadata || version.ToNormalizedString() != TemplateVersion)
                throw new InvalidOperationException("Supply --template-version as a normalized NuGet version without build metadata.");
            return version.ToNormalizedString();
        }
    }

    private Target PackTemplate => _ => _
        .Description("Validate the source and pack the requested template version locally")
        .DependsOn(Validate)
        .Requires(() => TemplateVersion)
        .Requires(() => File.Exists(TemplateProjectPath))
        .Executes(() =>
        {
            DotNetTasks.DotNetPack(settings => settings
                .SetProject(TemplateProjectPath)
                .SetConfiguration(Configuration.Release)
                .SetOutputDirectory(TemplateOutputDirectory)
                .SetProperty("PackageVersion", ReleaseVersion));
            if (!File.Exists(TemplatePackagePath))
                throw new FileNotFoundException("The requested template package was not produced.", TemplatePackagePath);
        });

    private Target ValidateTemplates => _ => _
        .Description("Validate both freshly generated applications from the exact release package")
        .DependsOn(PackTemplate)
        .Executes(() =>
        {
            string workspace = Path.Combine(Path.GetTempPath(), $"krafter-release-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workspace);
            try
            {
                string hive = Path.Combine(workspace, "template-hive");
                RunDotNet(workspace, "new", "install", TemplatePackagePath, "--debug:custom-hive", hive);
                foreach ((string template, string name) in new[]
                         { ("krafter", "ReleaseSplit"), ("krafter-single", "ReleaseSingle") })
                {
                    string output = Path.Combine(workspace, name);
                    RunDotNet(workspace, "new", template, "--name", name, "--output", output,
                        "--no-update-check", "--debug:custom-hive", hive);
                    if (Directory.EnumerateFiles(output, "*.Templates.csproj").Any() ||
                        File.Exists(Path.Combine(output, "build", "UpstreamRelease.cs")) ||
                        File.Exists(Path.Combine(output, "build", "UpstreamReleaseBranch.cs")) ||
                        File.Exists(Path.Combine(output, ".github", "workflows", "main.yml")))
                        throw new InvalidOperationException("Generated applications must not contain the upstream publisher.");

                    // Exercise the generated default entry point without any release credentials.
                    RunDotNet(output, "run", "--project", "build/_build.csproj", "--", "--configuration", "Release");
                }
            }
            finally
            {
                Directory.Delete(workspace, recursive: true);
            }
        });

    private Target PublishTemplate => _ => _
        .Description("Explicitly publish the validated, exact-version template package to NuGet.org")
        .DependsOn(ValidateTemplates)
        .Requires(() => NuGetPAT)
        .Executes(() => DotNetTasks.DotNetNuGetPush(settings => settings
            .SetTargetPath(TemplatePackagePath)
            .SetSource("https://api.nuget.org/v3/index.json")
            .SetApiKey(NuGetPAT)));

    private Target BuildAndPublishDockerImage => _ => _
        .Description("Explicitly validate and publish the upstream main/dev application images")
        .DependsOn(Validate)
        .Requires(() => PAT)
        .Executes(() =>
        {
            string imageTag = UpstreamReleaseBranch.GetImageTag(Repository.Branch);

            DockerTasks.DockerLogin(settings => settings.SetServer("ghcr.io").SetUsername(ContainerUser).SetPassword(PAT));
            try
            {
                foreach (AbsolutePath project in new[]
                         {
                             RootDirectory / "src" / "AditiKraft.Krafter.Backend" / "AditiKraft.Krafter.Backend.csproj",
                             RootDirectory / "src" / "UI" / "AditiKraft.Krafter.UI.Web" / "AditiKraft.Krafter.UI.Web.csproj"
                         })
                    DotNetTasks.DotNetPublish(settings => settings.SetProject(project)
                        .SetConfiguration(Configuration.Release)
                        .SetProperty("PublishProfile", "DefaultContainer")
                        .SetProperty("ContainerNamespace", ContainerNamespace)
                        .SetProperty("ContainerImageTag", imageTag));
            }
            finally
            {
                DockerTasks.DockerLogout(settings => settings.SetServer("ghcr.io"));
            }
        });

    private Target PublishImageAndMakeCallToWebhook => _ => _
        .Description("Explicitly publish validated images and invoke the configured deployment webhook")
        .DependsOn(BuildAndPublishDockerImage)
        .Requires(() => DeploymentWebhookUrl)
        .Executes(async () =>
        {
            using var http = new HttpClient();
            using HttpResponseMessage response = await http.PostAsync(DeploymentWebhookUrl, null);
            response.EnsureSuccessStatusCode();
        });

    private static void RunDotNet(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment.Remove("NuGetPAT");
        start.Environment.Remove("PAT");
        start.Environment.Remove("DeploymentWebhookUrl");
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Generated template validation failed (exit code {process.ExitCode}).");
    }
}
