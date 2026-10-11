var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant")
    .WithDataVolume();

var api = builder.AddProject<Projects.Kestrel_Api>("api")
    .WithReference(qdrant)
    .WaitFor(qdrant)
    .WithExternalHttpEndpoints();

// Native Ollama on the host (GPU). Override via Llm__ActiveChatProfile=desktop to use another machine.
api.WithEnvironment("Llm__Profiles__local__Endpoint", "http://localhost:11434");

var web = builder.AddJavaScriptApp("web", "../kestrel-web")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

web.WithEnvironment("KESTREL_API_URL", api.GetEndpoint("http"));

builder.Build().Run();
