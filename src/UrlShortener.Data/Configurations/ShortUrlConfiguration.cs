using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Data.Configurations;

public sealed class ShortUrlConfiguration : IEntityTypeConfiguration<ShortUrl>
{
    public void Configure(EntityTypeBuilder<ShortUrl> builder)
    {
        builder.ToTable("short_urls");
        builder.HasKey(shortUrl => shortUrl.Id);
        builder.Property(shortUrl => shortUrl.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(shortUrl => shortUrl.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.HasIndex(shortUrl => shortUrl.Code).IsUnique();
        builder.Property(shortUrl => shortUrl.OriginalUrl).HasColumnName("original_url").HasMaxLength(2048).IsRequired();
        builder.Property(shortUrl => shortUrl.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(shortUrl => shortUrl.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(shortUrl => shortUrl.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Ignore(shortUrl => shortUrl.ClickEvents);
    }
}