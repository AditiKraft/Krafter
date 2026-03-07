IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
// The postgres username and password are provided via parameters (marked secret)
IResourceBuilder<ParameterResource> username = builder.AddParameter("postgresUsername", true);
IResourceBuilder<ParameterResource> password = builder.AddParameter("postgresPassword", true);

IResourceBuilder<PostgresServerResource> databaseServer = builder.AddPostgres("postgres", username, password)
    .WithDataVolume(isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("KrafterPostgres")
    .WithPgAdmin();

string solutionRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));
string migratorProject = Path.Combine(solutionRoot, "src", "AditiKraft.Krafter.Backend.Migrator",
    "AditiKraft.Krafter.Backend.Migrator.csproj");

IResourceBuilder<PostgresDatabaseResource> database = databaseServer.AddDatabase("krafterDb");

IResourceBuilder<GarnetResource> cache = builder.AddGarnet("cache")
        .WithDataVolume(isReadOnly: false)
        .WithPersistence(
            TimeSpan.FromMinutes(5),
            100)
        .WithArgs("--lua", "true")
    ;
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

IResourceBuilder<ProjectResource> backend = builder.AddProject<Projects.AditiKraft_Krafter_Backend>("krafter-api")
    .WithReference(database)
    .WaitForCompletion(migrator);

builder.AddProject<Projects.AditiKraft_Krafter_UI_Web>("krafter-ui-web")
    .WithExternalHttpEndpoints()
    .WithReference(backend)
    .WithReference(cache)
    ;


builder.Build().Run();
