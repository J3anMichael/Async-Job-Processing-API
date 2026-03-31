using AsyncJobProcessingApi.Application.Interfaces;
using AsyncJobProcessingApi.Application.UseCases;
using AsyncJobProcessingApi.Infrastructure;
using AsyncJobProcessingApi.Infrastructure.Messaging;
using AsyncJobProcessingApi.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. Register Core / Infrastructure Services
// Use Singleton for InMemoryJobRepository to simulate a persistent database across requests
builder.Services.AddSingleton<IJobRepository, InMemoryJobRepository>();

// Register ServiceBusPublisher as Scoped (or Singleton since ServiceBusClient is thread-safe)
builder.Services.AddSingleton<IMessagePublisher, ServiceBusPublisher>();

builder.Services.AddScoped<IJobProcessor, JobProcessor>();
builder.Services.AddSingleton<IMessageConsumer, ServiceBusConsumer>();

// 2. Register Hosted Services (Workers)
// The background service runs as a singleton hosted service
builder.Services.AddHostedService<ServiceBusWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
