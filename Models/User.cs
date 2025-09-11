using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Shioko.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string? DisplayName { get; set; }
        // public string? Email { get; set; }
        public bool IsGuest { get; set; }
        public virtual ICollection<Pet> Pets { get; set; }
        public virtual ICollection<AuthProviders> AuthProviders { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Active { get; set; } = true;
    }

    public static class PROVIDER_TYPE
    {
        public const string LOCAL = "LOCAL";
        public const string EMAIL = "EMAIL";
        public const string GOOGLE = "GOOGLE";
        public const string PHONE = "PHONE";
    }

    public class AuthProviders
    {
        [Key]
        public int AuthProviderId { get; set; }
        public required string ProvideType { get; set; }
        public required string ProviderKey { get; set; }
        public string? PasswordHash { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }

    public class PetPicture
    {
        [Key]
        public int PetPictureId { get; set; }
        public required string Url { get; set; }
        [ForeignKey("Pet")]
        public int PetId { get; set; }
        public virtual Pet Pet { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Active { get; set; } = true;
        public bool Removed { get; set; } = false;
    }

    public class Pet
    {
        [Key]
        public int PetId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public int? ProfilePictureId { get; set; }
        public PetPicture? ProfilePicture { get; set; }
        public virtual ICollection<PetPicture> Pictures { get; set; }
        public DateTime? BirthDate { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool Active { get; set; } = true;
    }

    public class MatchingRecord
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("User")]
        public required int UserId { get; set; }
        public virtual User User { get; set; }
        [ForeignKey("Pet")]
        public int PetId { get; set; }
        public virtual Pet Pet { get; set; }
        // set to Pet's modified time or creation time of modified time is null
        public DateTime PetVersionTime { get; set; } // TODO implement more robust snapshot data model
        public required string SnapshotJsonData { get; set; } // json string of pet data at the time of matching
        public required int Rating { get; set; } // -1: dislike, 0: neutral, 1: like

        public required DateTime CreatedAt { get; set; }
        // for swiping back and reconsidering the rating choice
        public DateTime? ModifiedAt { get; set; }
    }

    //public class PetHistory
    //{
    //    [Key]
    //    public required int Id { get; set; }
    //    [ForeignKey("Pet")]
    //    public required int PetId { get; set; }
    //    public virtual Pet Pet { get; set; }
    //    public DateTime CreatedAt { get; set; }
    //}

    public class AppDbContext : DbContext
    {
        public AppDbContext() { }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // TODO create database if not exist
                // TODO update migration schema
                // TODO update database schema while keeping old data
                optionsBuilder.UseSqlite("Data Source=shioko.sqlite");
                // for sql server
                // IConfigurationRoot configuration = new ConfigurationBuilder()
                //     .SetBasePath(Directory.GetCurrentDirectory())
                //     .AddJsonFile("appsettings.json")
                //     .Build();
                // var connectionString = configuration.GetConnectionString("Default");
                // optionsBuilder.UseSqlServer(connectionString);
            }
        }
        public DbSet<User> Users { get; set; }
        public DbSet<AuthProviders> AuthProviders { get; set; }
        public DbSet<Pet> Pets { get; set; }
        public DbSet<PetPicture> PetPictures { get; set; }
        public DbSet<MatchingRecord> MatchingRecords { get; set; }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasMany(u => u.Pets)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.AuthProviders)
                .WithOne(ap => ap.User)
                .HasForeignKey(ap => ap.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Pet>()
                .HasMany(p => p.Pictures)
                .WithOne(pp => pp.Pet)
                .HasForeignKey(pp => pp.PetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Pet>()
                .HasOne(p => p.ProfilePicture)
                .WithMany()
                .HasForeignKey(p => p.ProfilePictureId)
                .OnDelete(DeleteBehavior.SetNull);

            // modelBuilder.Entity<User>()
            //     .Property(u => u.CreatedAt)
            //     .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // modelBuilder.Entity<AuthProviders>()
            //     .Property(ap => ap.CreatedAt)
            //     .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // modelBuilder.Entity<Pet>()
            //     .Property(p => p.CreatedAt)
            //     .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // modelBuilder.Entity<PetPicture>()
            //     .Property(pp => pp.CreatedAt)
            //     .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
