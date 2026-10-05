var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.YAFT_Backend>("yaft-backend");

builder.Build().Run();
