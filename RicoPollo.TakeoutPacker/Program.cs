using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = new HostBuilder();

builder.UseEnvironment(EnvironmentName.Development);

builder.ConfigureWebJobs(b =>
{
    b.AddAzureStorageCoreServices();
    b.AddAzureStorageQueues();
});

builder.ConfigureLogging((context, b) =>
{
    b.SetMinimumLevel(LogLevel.Error);
    b.AddConsole();
});

var host = builder.Build();
using (host)
{
    await host.RunAsync();
}
