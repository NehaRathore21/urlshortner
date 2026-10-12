using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Data.Configurations;

public sealed class ClickEventConfiguration : IEntityTypeConfiguration<ClickEvent>
{
    public void Configure(EntityTypeBuilder<ClickEvent> builder)
    {
        builder.ToTable("click_events");
        builder.HasKey(clickEvent => clickEvent.Id);
        builder.Property(clickEvent => clickEvent.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(clickEvent => clickEvent.ShortUrlId).HasColumnName("short_url_id").IsRequired();
        builder.Property(clickEvent => clickEvent.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("timestamp with time zone");
        builder.HasIndex(clickEvent => new { clickEvent.ShortUrlId, clickEvent.OccurredAtUtc });
        builder.HasOne(clickEvent => clickEvent.ShortUrl)
            .WithMany()
            .HasForeignKey(clickEvent => clickEvent.ShortUrlId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}