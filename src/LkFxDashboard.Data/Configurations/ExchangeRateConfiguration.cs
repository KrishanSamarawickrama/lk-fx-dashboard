using LkFxDashboard.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LkFxDashboard.Data.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.BaseCurrency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.TargetCurrency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.BuyingRate)
            .HasPrecision(18, 6);

        builder.Property(e => e.SellingRate)
            .HasPrecision(18, 6);

        builder.Property(e => e.Source)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(e => new { e.BaseCurrency, e.TargetCurrency, e.Source, e.RateDate })
            .HasDatabaseName("IX_ExchangeRate_Currency_Source_Date");

        builder.HasIndex(e => e.RateDate)
            .HasDatabaseName("IX_ExchangeRate_RateDate");
    }
}
