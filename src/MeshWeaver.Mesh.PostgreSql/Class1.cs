using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace MeshWeaver.Mesh.PostgreSql;

public class MeshDbContext : DbContext
{
    public DbSet<MeshArticle> Items { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=your_host;Username=your_username;Password=your_password;Database=your_database",
            o => o.UseVector());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.Entity<MeshArticle>()
            .Property(e => e.Embedding)
            .HasColumnType("vector(1536)");
    }

    public record MeshArticle(
        string Url,
        string Name,
        string Description,
        string Thumbnail,
        DateTime Published,
        IReadOnlyCollection<string> Authors,
        IReadOnlyCollection<string> Tags)
        : MeshWeaver.Mesh.Contract.MeshArticle(Url, Name, Description, Thumbnail, Published, Authors, Tags)
    {
        public Vector Embedding { get; init; }
    }
}
