IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
// The postgres username and password are provided via parameters (marked secret)
IResourceBuilder<ParameterResource> username = builder.AddParameter("postgresUsername", true);
IResourceBuilder<ParameterResource> password = builder.AddParameter("postgresPassword", true);

IResourceBuilder<PostgresServerResource> databaseServer = builder.AddPostgres("postgres", username, password)
    .WithDataVolume(isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("KrafterPostgres")
    .WithPgAdmin();

IResourceBuilder<PostgresDatabaseResource> database = databaseServer.AddDatabase("krafterDb");

IResourceBuilder<GarnetResource> cache = builder.AddGarnet("cache")
        .WithDataVolume(isReadOnly: false)
        .WithPersistence(
            TimeSpan.FromMinutes(5),
            100)
        .WithArgs("--lua", "true")
    ;
IResourceBuilder<ProjectResource> backend = builder.AddProject<Projects.AditiKraft_Krafter_Backend>("krafter-api")
    .WithReference(database);

builder.AddProject<Projects.AditiKraft_Krafter_UI_Web>("krafter-ui-web")
    .WithExternalHttpEndpoints()
    .WithReference(backend)
    .WithReference(cache)
    ;


builder.Build().Run();
