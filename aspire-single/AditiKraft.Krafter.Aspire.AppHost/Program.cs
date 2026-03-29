IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Database
IResourceBuilder<ParameterResource> username = builder.AddParameter("postgresUsername", true);
IResourceBuilder<ParameterResource> password = builder.AddParameter("postgresPassword", true);

IResourceBuilder<PostgresServerResource> databaseServer = builder.AddPostgres("postgres", username, password)
    .WithDataVolume(isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("KrafterPostgres")
    .WithPgAdmin();

IResourceBuilder<PostgresDatabaseResource> database = databaseServer.AddDatabase("krafterDb");

// Migrator
string solutionRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));
string migratorProject = Path.Combine(solutionRoot, "src", "AditiKraft.Krafter.Backend.Migrator",
    "AditiKraft.Krafter.Backend.Migrator.csproj");

IResourceBuilder<ExecutableResource> migrator = builder.AddExecutable(
        "krafter-migrator",
        "dotnet",
        solutionRoot,
        "run",
        "--project",
        migratorProject,
        "--no-launch-profile")
    .WithReference(database)
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WaitFor(database);

// Single combined host (API + Blazor UI in one process)
IResourceBuilder<ProjectResource> app = builder.AddProject<Projects.AditiKraft_Krafter_UI_Web>("krafter-app")
    .WithExternalHttpEndpoints()
    .WithReference(database)
    .WaitForCompletion(migrator);

// In single-host mode, RemoteHostUrl points to self (API is in-process).
// Inject the app's own HTTPS endpoint so Refit calls loop back correctly.
app.WithEnvironment("RemoteHostUrl", app.GetEndpoint("https"));

builder.Build().Run();
