using Echo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Echo.Infrastructure.Persistence;

public sealed class EchoDbContext : DbContext
{
    public EchoDbContext(DbContextOptions<EchoDbContext> options) : base(options)
    {
    }

    public DbSet<AnonymousUser> AnonymousUsers => Set<AnonymousUser>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Reply> Replies => Set<Reply>();
    public DbSet<Media> Media => Set<Media>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<AnonymousUser>(e =>
        {
            e.ToTable("anonymous_users");
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceKeyHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.DeviceKeyHash).IsUnique();
            e.HasOne(x => x.AvatarMedia)
                .WithMany()
                .HasForeignKey(x => x.AvatarMediaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Media>(e =>
        {
            e.ToTable("media");
            e.HasKey(x => x.Id);
            e.Property(x => x.Path).HasMaxLength(512).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Post>(e =>
        {
            e.ToTable("posts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Location).HasColumnType("geography (Point, 4326)");
            e.HasIndex(x => x.Location).HasMethod("GIST");
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Author)
                .WithMany(u => u.Posts)
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Media)
                .WithMany()
                .HasForeignKey(x => x.MediaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Reply>(e =>
        {
            e.ToTable("replies");
            e.HasKey(x => x.Id);
            e.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Post)
                .WithMany(p => p.Replies)
                .HasForeignKey(x => x.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Author)
                .WithMany(u => u.Replies)
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Media)
                .WithMany()
                .HasForeignKey(x => x.MediaId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
