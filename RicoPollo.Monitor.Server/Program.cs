using Microsoft.AspNetCore.ResponseCompression;
using RicoPollo.Monitor.Server.BackgroundTasks;
using RicoPollo.Monitor.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
        {
            policy.WithOrigins("https://localhost:7046")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
        });
});

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

builder.Services.AddHostedService<PollBlobContainerService>();

var app = builder.Build();

app.MapHub<TicketsHub>("/ticketshub");
app.MapHub<ArchiveHub>("/archivehub");

app.UseHttpsRedirection();
app.UseCors();

app.Run();