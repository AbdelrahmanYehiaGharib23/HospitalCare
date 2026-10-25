using System;
using System.Collections.Generic;
using System.Text;
using HospitalCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalCare.Infrastructure.Persistence.Configurations
{
    public sealed class AppointmentTypeConfiguration
     : IEntityTypeConfiguration<AppointmentType>
    {
        public  void Configure(EntityTypeBuilder<AppointmentType> builder)
        {
            builder.ToTable("AppointmentTypes",
                tableBuilder =>
                {
                    tableBuilder.HasCheckConstraint(
                        "CK_AppointmentTypes_DurationMinutes",
                        "[DurationMinutes] > 0");

                    tableBuilder.HasCheckConstraint(
                        "CK_AppointmentTypes_BookingFee",
                        "[BookingFee] > 0");
                });

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(x => x.DurationMinutes)
                .IsRequired();

            builder.Property(x => x.BookingFee)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.HasMany(x => x.Appointments)
                .WithOne(x => x.AppointmentType)
                .HasForeignKey(x => x.AppointmentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.PaymentTransactions)
                .WithOne(x => x.AppointmentType)
                .HasForeignKey(x => x.AppointmentTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
