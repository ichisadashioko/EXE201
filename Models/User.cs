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
        public required ICollection<Pet> Pets { get; set; }
        public required ICollection<AuthProviders> AuthProviders { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class PROVIDER_TYPE
    {
        public const string LOCAL = "LOCAL";
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
        public required User User { get; set; }
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
        public required Pet Pet { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Pet
    {
        [Key]
        public int PetId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public int? ProfilePictureId { get; set; }
        public PetPicture? ProfilePicture { get; set; }
        public required ICollection<PetPicture> Pictures { get; set; }
        public DateTime? BirthDate { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
        public required User User { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }

    public class AppDbContext : DbContext
    {
        public AppDbContext() { }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
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
