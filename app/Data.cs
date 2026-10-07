using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TrainingApp;

public class Visit
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; }
    public string Pod { get; set; } = "";
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Visit> Visits => Set<Visit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Visit>(e =>
        {
            e.ToTable("visits");
            e.Property(v => v.Id).HasColumnName("id");
            e.Property(v => v.At).HasColumnName("at");
            e.Property(v => v.Pod).HasColumnName("pod").HasMaxLength(253);
        });
    }
}

// Written by hand so the image builds without the dotnet-ef tool.
[DbContext(typeof(AppDbContext))]
[Migration("20261012000000_CreateVisits")]
public class CreateVisits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "visits",
            columns: table => new
            {
                id = table.Column<long>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                at = table.Column<DateTimeOffset>(nullable: false),
                pod = table.Column<string>(maxLength: 253, nullable: false),
            },
            constraints: table => table.PrimaryKey("pk_visits", x => x.id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "visits");
    }
}
