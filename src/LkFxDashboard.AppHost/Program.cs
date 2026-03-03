var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .AddDatabase("lkfxdb");

builder.AddProject<Projects.LkFxDashboard_Web>("web")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.Build().Run();
