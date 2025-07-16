namespace Instrument.Defaults.Provider.Sqlite;

using Instrument.Defaults.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class DefaultsDbContext : DbContext, IDefaultsDbContext
{
    public DefaultsDbContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = Path.Combine(path, "Defaults.db");
    }

    public DbSet<Unit> Unit { get; set; }
    public DbSet<Technology> Technology { get; set; }
    public DbSet<Sequence> Sequence { get; set; }
    public DbSet<TestMethod> TestMethod { get; set; }
    public DbSet<TestMethodSequences> TestMethodSequences { get; set; }
    public string DbPath { get; }
    public DbSet<VolumeDefault> VolumeDefaults { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        //base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseSqlite($"Data Source={DbPath}")
            .LogTo(Console.WriteLine, LogLevel.Information)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<Unit>()
            .HasData([
                    new Unit()
                        {
                            Id  = 1,
                            Name = "Millimeter",
                            Abbreviation = "mm",
                            Description = "Millimeter"
                        },
                ]);

        // Technology table
        modelBuilder
            .Entity<Technology>()
            .HasData([
                    new Technology(){
                          Id = 1,
                          Name = "ImmunoCap"
                      },
                      new Technology(){
                          Id = 2,
                          Name = "Elia"
                      },
                      new Technology(){
                          Id = 3,
                          Name = "ImmunoCapViewAllergy"
                      },
                      new Technology(){
                          Id = 4,
                          Name = "EliaDualWash"
                      }
                ]);

        // Method table
        modelBuilder
           .Entity<TestMethod>()
           .HasData([
               new TestMethod(){
                   Id = 1,
                   TechnologyId = 1,
                   Name = "sIgE"
               }]);

        modelBuilder
            .Entity<Sequence>()
            .HasData([
                new Sequence() {
                    Id = 1,
                    Created = DateTime.UtcNow,
                    IsActive = true,
                    Name = "SS_Home",
                    Description = "Stop Station Home"
                },
                new Sequence() {
                    Id = 2,
                    Created = DateTime.UtcNow,
                    IsActive = true,
                    Name = "ERW_Home",
                    Description = "Reaction Wheel Home"
                },
                ]);

        modelBuilder
            .Entity<TestMethodSequences>()
            .HasData([
                new TestMethodSequences() {
                    Id = 1,
                    IsActive = true,
                    SequenceId = 1,
                    TestMethodId = 1
                },
                new TestMethodSequences() {
                    Id = 2,
                    IsActive = true,
                    SequenceId = 2,
                    TestMethodId = 1
                }]);

    }
}