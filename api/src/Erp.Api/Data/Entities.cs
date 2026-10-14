namespace Erp.Api.Data;

// Every row belongs to a workspace: each visitor works on a private copy of the demo data.

public sealed class Workspace
{
    /// <summary>The template every visitor copy is made from (a fixed id: EF treats Guid.Empty in keys as "not set").</summary>
    public static readonly Guid Template = new("7e3a1a7e-0000-4000-8000-000000000001");
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateOnly SeedDate { get; set; }
}

public sealed class Article
{
    public Guid WorkspaceId { get; init; }
    public required string Number { get; init; }
    public required string Name { get; set; }
    public required string Group { get; set; }
    public required string Unit { get; set; }
    public decimal UnitCost { get; set; }
    public string? SupplierNumber { get; set; }
}

public sealed class Supplier
{
    public Guid WorkspaceId { get; init; }
    public required string Number { get; init; }
    public required string Name { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

public sealed class StockMovement
{
    public long Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public required string ArticleNumber { get; init; }
    public DateOnly Date { get; init; }
    public decimal Quantity { get; init; }
    public required string Type { get; init; }
    public string? Reference { get; init; }
    public Guid? ImportBatchId { get; init; }
}

public sealed class SalesOrderLine
{
    public Guid WorkspaceId { get; init; }
    public required string OrderNumber { get; init; }
    public int LineNo { get; init; }
    public required string Customer { get; set; }
    public required string ArticleNumber { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? ShippedDate { get; set; }
}

public sealed class PurchaseOrderLine
{
    public Guid WorkspaceId { get; init; }
    public required string OrderNumber { get; init; }
    public int LineNo { get; init; }
    public required string SupplierNumber { get; set; }
    public required string ArticleNumber { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly PromisedDate { get; set; }
    public DateOnly? ReceivedDate { get; set; }
}

public enum BatchStatus { Active, RolledBack }

/// <summary>One import. <see cref="UndoJson"/> holds what is needed to take it back.</summary>
public sealed class ImportBatch
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorkspaceId { get; init; }
    public required string Dataset { get; init; }
    public required string FileName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public int RowsTotal { get; init; }
    public int Inserted { get; init; }
    public int Updated { get; init; }
    public int Rejected { get; init; }
    public BatchStatus Status { get; set; }
    public required string UndoJson { get; init; }
}
