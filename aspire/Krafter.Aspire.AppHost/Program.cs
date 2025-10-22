using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);
// The postgres username and password are provided via parameters (marked secret)
var username = builder.AddParameter("postgresUsername", secret: true);
var password = builder.AddParameter("postgresPassword", secret: true);

var databaseServer = builder.AddPostgres("postgres", username, password)
    .WithDataVolume(isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("KrafterPostgres")
    .WithPgAdmin();

var database = databaseServer.AddDatabase("krafterDb");

var cache = builder.AddGarnet("cache")
    .WithDataVolume(isReadOnly: false)
    .WithPersistence(
        interval: TimeSpan.FromMinutes(5),
        keysChangedThreshold: 100)
    .WithArgs("--lua", "true")
    ; 
var backend = builder.AddProject<Projects.Backend>("krafter-api")
    .WithReference(database);

builder.AddProject<Projects.Krafter_UI_Web>("krafter-ui-web")
    .WithExternalHttpEndpoints()
    .WithReference(backend)
    .WithReference(cache)
    ;



builder.Build().Run();
