# 🚀 Async Job Processing API

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![Azure Service Bus](https://img.shields.io/badge/Azure%20Service%20Bus-0078D4?style=for-the-badge&logo=microsoftazure&logoColor=white)
![Clean Architecture](https://img.shields.io/badge/Architecture-Clean-success?style=for-the-badge)

A high-performance, asynchronous Job Processing API built with **.NET 8** and **Azure Service Bus**. 
This project was designed from the ground up prioritizing **Scale, Decoupling, and Clean Architecture** (Single Responsibility Principle).

## 💡 The Architecture & Flow
When a typical REST API needs to execute a massive or slow task (e.g., Video Processing, Heavy PDF Generation), keeping the client waiting results in poor UX and timeouts. 

This API solves that by offloading work to the Cloud via Asynchronous Messaging:
1. **API Layer**: Receives the job from the User, saves a `Pending` state, and instantly drops the payload into the **Azure Service Bus**.
2. **Infrastructure Layer**: Completely decoupled publishers and consumers handle Azure Cloud integrations seamlessly.
3. **Application Layer**: A Background `Worker` safely dequeues jobs using concurrent configurations (`PrefetchCount` and `SemaphoreSlim`) and delegates the task to a pure C# **Business Processor**.

### 🏗️ Project Structure (Clean Architecture)
* `Api/`: Web API Controllers representing the entry points.
* `Application/`: Use Cases (JobProcessor) and Core Interfaces. **100% pure business logic.**
* `Domain/`: Core Entities and Status Enums (`Job`, `JobStatus`).
* `Infrastructure/`: External integrations (`InMemoryJobRepository`, `ServiceBusPublisher`, `ServiceBusConsumer`).
* `Workers/`: The minimalist BackgroundService host activating the consumer infinitely.

## ⚙️ Features

- **Advanced Throughput (Dynamic Batching)**: Instead of one-by-one polling, the consumer calculates available processing slots in real-time (`semaphore.CurrentCount + 1`) to fetch messages in optimized batches from Azure.
- **Enterprise Resilience (Polly)**: Integrates **Polly** policies for business logic execution, using **Exponential Backoff + Jitter** to handle transient failures gracefully without overloading downstream resources.
- **Parallel Processing**: Supports fetching and processing hundreds of jobs concurrently (configurable via `MaxConcurrentJobs`).
- **Resilience**: Implements _PeekLock_ receive-mode. If the worker crashes mid-task, the message is automatically unlocked by Azure for retry.
- **Observability**: Integrated with **Serilog** for structured logging, allowing audit trails in both Console and daily rolling Files (`logs/`).
- **Poison Message Handling**: Automatically relocates malformed payloads (JSON errors) to a _Dead-Letter Queue_ to avoid blocking the pipeline.
- **Clean Code & SRP**: The codebase follows strict Clean Architecture and SRP. It is "self-documenting" with zero comments, relying on expressive naming and clear structure.
- **Dependency Injection**: Strongly typed and scoped lifecycles across the application.

---

## 🚀 Getting Started

### 1. Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- An active [Azure Service Bus Namespace](https://azure.microsoft.com/en-us/services/service-bus/) (Basic Tier is enough).

### 2. Configuration
Do not commit your real Azure connection strings. Rename the provided example config file:
1. Rename `appsettings.example.json` to `appsettings.json`.
2. Insert your Azure connection string:
```json
"ServiceBus": {
  "ConnectionString": "Endpoint=sb://<your-namespace>.servicebus.windows.net/;SharedAccessKeyName=...",
  "QueueName": "jobs-queue",
  "MaxConcurrentJobs": 10
}
```

### 3. Run the Application
Start the project locally using:
```bash
dotnet run
```
Navigate to `http://localhost:{port}/swagger` to interact with the interactive Swagger dashboard.

### 4. Stress Testing 
A custom `LoadTester` script is available to barrage your Azure queue with 1,000+ messages instantly to test maximum consumer throughput capability.

## 🤝 Contact
Built by **[Jean Michael](https://github.com/J3anMichael)**. Open to feedback, connections, and exciting opportunities!
