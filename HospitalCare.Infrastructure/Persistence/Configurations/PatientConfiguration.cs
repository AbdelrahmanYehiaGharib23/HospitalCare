using System;
using System.Collections.Generic;
using System.Text;
using HospitalCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalCare.Infrastructure.Persistence.Configurations
{
    public class PatientConfiguration : BaseEntityConfiguration<Patient>
    {
        public override void Configure(EntityTypeBuilder<Patient> builder)
        {
            builder.ToTable("Patients");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(x => x.Phone)
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(x => x.Allergies)
                .HasMaxLength(2000);

            builder.Property(x => x.ChronicConditions)
                .HasMaxLength(2000);

            builder.HasIndex(x => x.Email)
                .IsUnique();

            builder.HasMany(x => x.Appointments)
                .WithOne(x => x.Patient)
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            base.Configure(builder);
        }
    }
}