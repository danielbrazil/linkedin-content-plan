using AspNetRequestPipeline;

if (args is ["--check"])
{
    await PipelineChecks.RunAsync();
    return;
}

await using WebApplication app = DemoApi.Build(args);
await app.RunAsync();
