using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Utilities.Collections;
using System.Net.Http;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;

internal class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.PublishImageAndMakeCallToWebhook);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    private readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] private readonly Solution Solution;
    private readonly string RepositoryUrl = "https://github.com/AditiKraft/Krafter";
    private AbsolutePath SourceDirectory => RootDirectory / "src";

    private AbsolutePath BuildInfoPath => SourceDirectory / "AditiKraft.Krafter.Backend" / "Features" / "AppInfo" / "Get.cs";
    private AbsolutePath KrafterAPIPath => SourceDirectory / "AditiKraft.Krafter.Backend" / "AditiKraft.Krafter.Backend.csproj";
    private AbsolutePath KrafterUIPath => SourceDirectory / "UI" / "AditiKraft.Krafter.UI.Web" / "AditiKraft.Krafter.UI.Web.csproj";
    private AbsolutePath TemplateProjectPath => RootDirectory / "AditiKraft.Krafter.Templates.csproj";
    private readonly int MajorVersion = DateTime.UtcNow.Year;
    private readonly int MinorVersion = DateTime.UtcNow.Month;
    private readonly int PatchVersion = DateTime.UtcNow.Day;
    private string VersionMode = "dev-pre-release";
    private const string User = "aditikraft";
    private string DockerTag = "";
    private string BranchName = "";
    private bool IsMaster;
    [GitRepository] private readonly GitRepository Repository;
    [Parameter("Personal Access Token")] private readonly string PAT;
    [Parameter("NuGet API Key for publishing templates")] private readonly string NuGetPAT;
    [Parameter("Deployment Webhook Url")] private readonly string DeploymentWebhookUrl;
    [Parameter("Template version (default: 1.0.0)")] private readonly string TemplateVersion = "1.0.0";
    private GitHubActions GitHubActions => GitHubActions.Instance;

    private Target SetBuildInfo => _ => _
        .Executes(() =>
        {
            string text = System.IO.File.ReadAllText(BuildInfoPath);
            text = text.Replace("#DateTimeUtc", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            text = text.Replace("#Build", VersionMode);
            System.IO.File.WriteAllText(BuildInfoPath, text);
        });


    private Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            string text = System.IO.File.ReadAllText(BuildInfoPath);
            text = text.Replace("#DateTimeUtc", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            text = text.Replace("#Build", VersionMode);
            System.IO.File.WriteAllText(BuildInfoPath, text);

            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(absolutePath => absolutePath.DeleteDirectory());
        });

    private Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    private Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(KrafterAPIPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());

            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(KrafterUIPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });


    private Target LoginIntoDockerHub => _ => _
        .Executes(() =>
        {
            if (BranchName == "main" || BranchName == "dev")
            {
                DockerTasks.DockerLogin(l => l
                    .SetServer("ghcr.io")
                    .SetUsername(User)
                    .SetPassword(PAT)
                );
            }
        });

    private Target BuildAndPublishDockerImage => _ => _
        .After(DetermineBranchAndDockerTag)
        .DependsOn(LoginIntoDockerHub)
        .DependsOn(SetBuildInfo)
        .Executes(() =>
        {
            if (BranchName == "main" || BranchName == "dev")
            {
                DotNetTasks.DotNetPublish(s => s
                    .SetProject(KrafterAPIPath)
                    .SetConfiguration(Configuration)
                    .SetProperty("PublishProfile", "DefaultContainer")
                    .SetProperty("ContainerImageTag", DockerTag));

                DotNetTasks.DotNetPublish(s => s
                    .SetProject(KrafterUIPath)
                    .SetConfiguration(Configuration)
                    .SetProperty("PublishProfile", "DefaultContainer")
                    .SetProperty("ContainerImageTag", DockerTag));
            }
        });

    private Target PublishImageAndMakeCallToWebhook => definition => definition
        .DependsOn(DetermineBranchAndDockerTag)
        .DependsOn(BuildAndPublishDockerImage)
        .Executes(async () =>
        {
            if (BranchName == "main" || BranchName == "dev")
            {
                //make http post request to the server to restart the container

                DockerTasks.DockerLogout();

                string webhookUrl = DeploymentWebhookUrl;
                if (!string.IsNullOrWhiteSpace(webhookUrl))
                {
                    using (var httpClient = new HttpClient())
                    {
                        HttpResponseMessage response = await httpClient.PostAsync(webhookUrl, null);
                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine("Server start request successfully sent.");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to send start request. Status code: {response.StatusCode}");
                        }
                    }
                }
            }
        });


    private Target DetermineBranchAndDockerTag => _ => _
        .Executes(() =>
        {
            DockerTag = "dev";
            if (Repository.Branch != null)
            {
                BranchName = Repository.Branch.Split("/").Last().ToLower();
                IsMaster = BranchName == "main";
                if (IsMaster)
                {
                    DockerTag = "latest";
                }
                else if (BranchName == "dev")
                {
                    DockerTag = "dev";
                }
                else
                {
                    long buildNumber = 0;
                    if (IsServerBuild)
                    {
                        buildNumber = GitHubActions.Instance.RunNumber;
                    }

                    DockerTag = $"{BranchName}-{buildNumber}";
                }
            }
        });

    // ============================================
    // Template Targets
    // ============================================

    private Target PackTemplate => _ => _
        .Description("Pack the Krafter template as a NuGet package")
        .Executes(() =>
        {
            DotNetTasks.DotNetPack(s => s
                .SetProject(TemplateProjectPath)
                .SetConfiguration(Configuration.Release)
                .SetOutputDirectory(RootDirectory / "bin" / "Release")
                .SetProperty("PackageVersion", TemplateVersion));

            Serilog.Log.Information($"✅ Template package v{TemplateVersion} created successfully!");
        });

    private Target PublishTemplate => _ => _
        .DependsOn(PackTemplate)
        .Description("Publish the template to NuGet.org")
        .Requires(() => NuGetPAT)
        .Executes(() =>
        {
            var packagePath = (RootDirectory / "bin" / "Release").GlobFiles("*.nupkg").FirstOrDefault();
            if (packagePath != null)
            {
                DotNetTasks.DotNetNuGetPush(s => s
                    .SetTargetPath(packagePath)
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .SetApiKey(NuGetPAT));

                Serilog.Log.Information("✅ Template published to NuGet successfully!");
                Serilog.Log.Information("🌐 Package: https://www.nuget.org/packages/AditiKraft.Krafter.Templates");
            }
            else
            {
                throw new Exception("❌ Package file not found!");
            }
        });
}
