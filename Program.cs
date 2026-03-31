using AsyncJobProcessingApi.Application.Interfaces;
using AsyncJobProcessingApi.Application.UseCases;
using AsyncJobProcessingApi.Infrastructure;
using AsyncJobProcessingApi.Infrastructure.Messaging;
using AsyncJobProcessingApi.Workers;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IJobRepository, InMemoryJobRepository>();
builder.Services.AddSingleton<IMessagePublisher, ServiceBusPublisher>();

builder.Services.AddScoped<IJobProcessor, JobProcessor>();
builder.Services.AddSingleton<IMessageConsumer, ServiceBusConsumer>();

builder.Services.AddHostedService<ServiceBusWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
