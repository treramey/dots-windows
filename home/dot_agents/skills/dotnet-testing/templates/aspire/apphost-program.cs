using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL — Session lifetime so the container is cleaned up at the end of the test session.
var postgres = builder.AddPostgres("postgres")
                     .WithLifetime(ContainerLifetime.Session);

var postgresDb = postgres.AddDatabase("productdb");

// Redis cache — same lifetime.
var redis = builder.AddRedis("redis")
                  .WithLifetime(ContainerLifetime.Session);

// API project — strongly-typed project reference.
// Aspire wires service discovery and connection-string injection automatically.
var apiProject = builder.AddProject<Projects.MyApp_Api>("myapp-api")
                       .WithReference(postgresDb)
                       .WithReference(redis);

// Notes:
// 1. Do NOT configure WithHttpEndpoint(port: ...); let Aspire allocate the port.
// 2. ContainerLifetime.Session ensures the container is cleaned up after the test session.
// 3. Use WithReference() to express dependencies between services.

builder.Build().Run();
