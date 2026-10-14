using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Data;

public sealed class ErpDb(DbContextOptions<ErpDb> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockMovement> Movements => Set<StockMovement>();
    public DbSet<SalesOrderLine> SalesOrders => Set<SalesOrderLine>();
    public DbSet<PurchaseOrderLine> PurchaseOrders => Set<PurchaseOrderLine>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var b = modelBuilder;
        b.Entity<Workspace>(e => e.HasIndex(w => w.LastSeenAt));

        // Deleting a workspace removes all of its data.
        void Owned<T>() where T : class =>
            b.Entity<T>().HasOne<Workspace>().WithMany().HasForeignKey("WorkspaceId").OnDelete(DeleteBehavior.Cascade);

        b.Entity<Article>(e =>
        {
            e.HasKey(a => new { a.WorkspaceId, a.Number });
            e.Property(a => a.UnitCost).HasPrecision(12, 2);
        });
        b.Entity<Supplier>().HasKey(s => new { s.WorkspaceId, s.Number });
        b.Entity<StockMovement>(e =>
        {
            e.HasIndex(m => new { m.WorkspaceId, m.ArticleNumber, m.Date });
            e.HasIndex(m => m.ImportBatchId);
            e.Property(m => m.Quantity).HasPrecision(12, 3);
        });
        b.Entity<SalesOrderLine>(e =>
        {
            e.HasKey(l => new { l.WorkspaceId, l.OrderNumber, l.LineNo });
            e.Property(l => l.Quantity).HasPrecision(12, 3);
            e.Property(l => l.UnitPrice).HasPrecision(12, 2);
        });
        b.Entity<PurchaseOrderLine>(e =>
        {
            e.HasKey(l => new { l.WorkspaceId, l.OrderNumber, l.LineNo });
            e.Property(l => l.Quantity).HasPrecision(12, 3);
        });
        b.Entity<ImportBatch>(e =>
        {
            e.HasIndex(i => new { i.WorkspaceId, i.CreatedAt });
            e.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(i => i.UndoJson).HasColumnType("jsonb");
        });

        Owned<Article>(); Owned<Supplier>(); Owned<StockMovement>(); Owned<SalesOrderLine>(); Owned<PurchaseOrderLine>(); Owned<ImportBatch>();
    }
}
