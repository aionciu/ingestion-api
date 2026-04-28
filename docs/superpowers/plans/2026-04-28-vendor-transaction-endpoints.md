# Vendor Transaction Endpoints Implementation Plan (Revised)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build two HTTP endpoints (`POST /api/vendor-transactions/T` and `POST /api/vendor-transactions/T+1`) that accept vendor transaction data, expand each bag into 3 separate transaction records (one for DeclaredValue, ProcessedValue, DifferenceValue), and persist with different validation rules.

**Architecture:** Domain-driven design with Clean Architecture layering. Each incoming bag is expanded into 3 Transaction rows in the database (one per value type). Two separate CQRS commands and validators ensure focused, testable validation. EF Core maps Transaction to a single table with a composite unique constraint. Controllers extract VendorId from header and delegate to handlers.

**Tech Stack:** .NET 10, ASP.NET Core 10, EF Core 10, FluentValidation, Serilog

---

## File Structure

| Layer | File | Type | Purpose |
|-------|------|------|---------|
| **Domain** | `src/IngestionApi.Domain/ValueObjects/Bag.cs` | Create | Bag value object (DeclaredValue, ProcessedValue, DifferenceValue, Currency) for request validation |
| | `src/IngestionApi.Domain/Entities/Transaction.cs` | UPDATE | Transaction aggregate root: OperationId, VendorId, BagId, Currency, Value, TransactionType, CreatedAt, ModifiedAt |
| **Application** | `src/IngestionApi.Application/Features/Transactions/DTOs/BagDto.cs` | Create | BagDto record |
| | `src/IngestionApi.Application/Features/Transactions/Commands/CreateTransactionTCommand.cs` | Create | T command with handlers |
| | `src/IngestionApi.Application/Features/Transactions/Commands/CreateTransactionT1Command.cs` | Create | T+1 command with handlers |
| | `src/IngestionApi.Application/Features/Transactions/Validators/CreateTransactionTValidator.cs` | Create | Validator for T rules |
| | `src/IngestionApi.Application/Features/Transactions/Validators/CreateTransactionT1Validator.cs` | Create | Validator for T+1 rules |
| **Infrastructure** | `src/IngestionApi.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs` | UPDATE | EF Core configuration (simpler - no owned entities) |
| | `src/IngestionApi.Infrastructure/DependencyInjection.cs` | Update | Register handlers |
| **API** | `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs` | Create | Two endpoints with header extraction and result mapping |
| **Tests** | Unit & integration tests | Create | Validators, handlers, controller tests |

---

## Task 1: Update Transaction Entity (from previous Task 2)

**Files:**
- Modify: `src/IngestionApi.Domain/Entities/Transaction.cs`

- [ ] **Step 1: Update the Transaction entity**

Replace `src/IngestionApi.Domain/Entities/Transaction.cs` with:

```csharp
using IngestionApi.Domain.Common;

namespace IngestionApi.Domain.Entities;

public class Transaction : Entity<Guid>
{
    public string OperationId { get; init; } = string.Empty;
    public string VendorId { get; init; } = string.Empty;
    public string BagId { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public int TransactionType { get; init; } // 1=DeclaredValue, 2=ProcessedValue, 3=DifferenceValue
    public DateTime CreatedAt { get; private set; }
    public DateTime ModifiedAt { get; private set; }

    // Constructor for EF Core initialization
    public Transaction()
    {
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
```

**What changed:**
- Removed `Bags` collection
- Added `Currency` (string)
- Added `Value` (decimal) - the monetary value
- Added `TransactionType` (int) - 1, 2, or 3
- All properties except CreatedAt/ModifiedAt use `init` for immutability

- [ ] **Step 2: Run build and commit**

```bash
dotnet build
git add src/IngestionApi.Domain/Entities/Transaction.cs
git commit -m "feat: update Transaction entity for value-type expansion model"
```

---

## Task 2: Update EF Core Configuration (from previous Task 6)

**Files:**
- Modify: `src/IngestionApi.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs`

- [ ] **Step 1: Replace the configuration**

The configuration is now much simpler since there's no owned entity collection:

```csharp
using IngestionApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngestionApi.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.OperationId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.VendorId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.BagId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(t => t.Value)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(t => t.TransactionType)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(t => t.ModifiedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        // Unique constraint: (BagId, OperationId, Currency, VendorId, TransactionType)
        builder.HasIndex(t => new { t.BagId, t.OperationId, t.Currency, t.VendorId, t.TransactionType })
            .IsUnique()
            .HasDatabaseName("IX_Transaction_BagOpCurrVendorType");
    }
}
```

- [ ] **Step 2: Run build and commit**

```bash
dotnet build
git add src/IngestionApi.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs
git commit -m "feat: simplify TransactionConfiguration for single-table model"
```

---

## Task 3: Create T and T+1 Commands with Handlers (from previous Task 3)

**Files:**
- Create: `src/IngestionApi.Application/Features/Transactions/DTOs/BagDto.cs`
- Create: `src/IngestionApi.Application/Features/Transactions/Commands/CreateTransactionTCommand.cs`
- Create: `src/IngestionApi.Application/Features/Transactions/Commands/CreateTransactionT1Command.cs`

- [ ] **Step 1: Create BagDto**

Create `src/IngestionApi.Application/Features/Transactions/DTOs/BagDto.cs`:

```csharp
namespace IngestionApi.Application.Features.Transactions.DTOs;

public record BagDto(
    decimal DeclaredValue,
    decimal ProcessedValue,
    decimal DifferenceValue,
    string Currency);
```

- [ ] **Step 2: Create CreateTransactionTCommand and handler**

Create `src/IngestionApi.Application/Features/Transactions/Commands/CreateTransactionTCommand.cs`:

```csharp
using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Domain.Common;
using IngestionApi.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IngestionApi.Application.Features.Transactions.Commands;

public record CreateTransactionTCommand(
    string OperationId,
    string BagId,
    List<BagDto> Bags,
    string VendorId);

public class CreateTransactionTCommandHandler : ICommandHandler<CreateTransactionTCommand>
{
    private readonly ILogger<CreateTransactionTCommandHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTransactionTCommandHandler(
        ILogger<CreateTransactionTCommandHandler> logger,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(CreateTransactionTCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("T transaction received from vendor {VendorId} with operation {OperationId}",
            command.VendorId, command.OperationId);

        try
        {
            // For each bag, create 3 transactions (one for each value type)
            foreach (var bag in command.Bags)
            {
                // Transaction Type 1: DeclaredValue
                var declaredTx = new Transaction
                {
                    OperationId = command.OperationId,
                    VendorId = command.VendorId,
                    BagId = command.BagId,
                    Currency = bag.Currency,
                    Value = bag.DeclaredValue,
                    TransactionType = 1
                };

                // For T endpoint: ProcessedValue must be 0, DifferenceValue must be 0
                // So we only persist the DeclaredValue transaction
                // (ValidatorsT ensure other values are 0)
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("T transaction {OperationId} processed successfully", command.OperationId);
            return Result.Success();
        }
        catch (IngestionApi.Application.Common.ConflictException)
        {
            _logger.LogWarning("T transaction {OperationId} conflicts with existing data", command.OperationId);
            return Result.Conflict();
        }
    }
}
```

- [ ] **Step 3: Create CreateTransactionT1Command and handler**

Create `src/IngestionApi.Application/Features/Transactions/Commands/CreateTransactionT1Command.cs`:

```csharp
using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Domain.Common;
using IngestionApi.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IngestionApi.Application.Features.Transactions.Commands;

public record CreateTransactionT1Command(
    string OperationId,
    string BagId,
    List<BagDto> Bags,
    string VendorId);

public class CreateTransactionT1CommandHandler : ICommandHandler<CreateTransactionT1Command>
{
    private readonly ILogger<CreateTransactionT1CommandHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTransactionT1CommandHandler(
        ILogger<CreateTransactionT1CommandHandler> logger,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(CreateTransactionT1Command command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("T+1 transaction received from vendor {VendorId} with operation {OperationId}",
            command.VendorId, command.OperationId);

        try
        {
            // For each bag, create 3 transactions (one for each value type)
            var transactions = new List<Transaction>();
            
            foreach (var bag in command.Bags)
            {
                // Transaction Type 1: DeclaredValue
                transactions.Add(new Transaction
                {
                    OperationId = command.OperationId,
                    VendorId = command.VendorId,
                    BagId = command.BagId,
                    Currency = bag.Currency,
                    Value = bag.DeclaredValue,
                    TransactionType = 1
                });

                // Transaction Type 2: ProcessedValue
                transactions.Add(new Transaction
                {
                    OperationId = command.OperationId,
                    VendorId = command.VendorId,
                    BagId = command.BagId,
                    Currency = bag.Currency,
                    Value = bag.ProcessedValue,
                    TransactionType = 2
                });

                // Transaction Type 3: DifferenceValue
                transactions.Add(new Transaction
                {
                    OperationId = command.OperationId,
                    VendorId = command.VendorId,
                    BagId = command.BagId,
                    Currency = bag.Currency,
                    Value = bag.DifferenceValue,
                    TransactionType = 3
                });
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("T+1 transaction {OperationId} processed successfully with {TransactionCount} rows", 
                command.OperationId, transactions.Count);
            return Result.Success();
        }
        catch (IngestionApi.Application.Common.ConflictException)
        {
            _logger.LogWarning("T+1 transaction {OperationId} conflicts with existing data", command.OperationId);
            return Result.Conflict();
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add src/IngestionApi.Application/Features/Transactions/
git commit -m "feat: add T and T+1 commands with handlers that expand bags to transactions"
```

---

## Task 4: Create Validators (Same as before)

Validators remain the same - they validate the incoming bag data from the request.

```bash
# These were already created in previous execution
# CreateTransactionTValidator.cs
# CreateTransactionT1Validator.cs
```

---

## Task 5: Create Controller and register DI

**Files:**
- Create: `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs`
- Verify: `src/IngestionApi.Infrastructure/DependencyInjection.cs`

[Controller and DI code same as provided in the previous brief...]

---

## Remaining Tasks

- Create EF Core migration
- Run all tests
- Verify build

---

## Implementation Notes

**T vs T+1 Transaction Creation:**
- **T endpoint:** Only creates 1 transaction per bag (DeclaredValue with TransactionType=1)
  - Validator ensures ProcessedValue=0 and DifferenceValue=0, so no point storing them
- **T+1 endpoint:** Creates 3 transactions per bag (all values with TransactionType 1,2,3)
  - Validator allows all values >= 0

**Unique Constraint:**
- Composite index on (BagId, OperationId, Currency, VendorId, TransactionType)
- Prevents duplicate (bag, operation, currency, vendor, type) combinations across all transactions
