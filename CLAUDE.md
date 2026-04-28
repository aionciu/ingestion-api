# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build entire solution
dotnet build IngestionApi.slnx

# Run the API
dotnet run --project src/IngestionApi.Api

# Run all tests
dotnet test

# Run a single test class
dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~MyTestClass"

# Add a new EF Core migration (always run from repo root)
dotnet ef migrations add <MigrationName> --project src/IngestionApi.Infrastructure --startup-project src/IngestionApi.Api

# Apply migrations
dotnet ef database update --project src/IngestionApi.Infrastructure --startup-project src/IngestionApi.Api
```

OpenAPI schema is available at `/openapi/v1.json` in Development.

## Architecture

Clean Architecture with four layers. Dependency flow is strictly inward:

```
Api → Application → Domain
Api → Infrastructure → Application → Domain
```

### Domain (`src/IngestionApi.Domain/`)
Pure C# — no framework or NuGet dependencies. Contains:
- `Common/Entity<TId>` — base class for all aggregate roots
- `Common/IRepository<TEntity, TId>` — generic repository contract
- `Common/IUnitOfWork` — commit abstraction

### Application (`src/IngestionApi.Application/`)
Orchestration layer. Depends only on Domain. Contains:
- `Common/Result` / `Result<T>` — explicit success/failure return type used by all handlers
- `Common/ICommandHandler<TCommand>` / `ICommandHandler<TCommand, TResponse>` — write side
- `Common/IQueryHandler<TQuery, TResponse>` — read side
- FluentValidation validators (auto-registered via `AddValidatorsFromAssembly`)

### Infrastructure (`src/IngestionApi.Infrastructure/`)
EF Core + SQL Server implementation. Contains:
- `Persistence/AppDbContext` — EF Core context; entity configurations loaded from the assembly via `ApplyConfigurationsFromAssembly`
- `Persistence/UnitOfWork` — implements `IUnitOfWork` by wrapping `AppDbContext.SaveChangesAsync`
- `Persistence/Repositories/GenericRepository<TEntity, TId>` — base repository; extend with entity-specific repositories in the same folder

### Api (`src/IngestionApi.Api/`)
ASP.NET Core 10 host. Bootstraps Serilog, DI, and the HTTP pipeline in `Program.cs`. No business logic.

## Adding a Feature (pattern)

1. **Domain** — add entity inheriting `Entity<TId>`; add a typed repository interface if queries beyond generic are needed
2. **Application** — add a command/query record + a handler implementing `ICommandHandler<>` or `IQueryHandler<>`; add a FluentValidation validator for the command/query
3. **Infrastructure** — add EF Core entity configuration (`IEntityTypeConfiguration<T>`); register a typed repository in `DependencyInjection.cs`
4. **Api** — add a controller that resolves the handler via DI and calls `HandleAsync`; map `Result` errors to appropriate HTTP status codes

## Key conventions

- Handlers return `Result` or `Result<T>`, never throw for expected failures
- Command handlers are registered as **Scoped**; they receive `IUnitOfWork` and call `SaveChangesAsync` themselves
- EF Core entity configurations live in `Infrastructure/Persistence/Configurations/` as `IEntityTypeConfiguration<T>` classes
- Connection string key: `ConnectionStrings:DefaultConnection` in `appsettings.json`
- Serilog is configured entirely from `appsettings.json`; logs roll daily to `logs/ingestion-<date>.log`
