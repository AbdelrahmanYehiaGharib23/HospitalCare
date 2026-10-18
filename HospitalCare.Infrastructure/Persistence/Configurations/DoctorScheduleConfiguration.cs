using System;
using System.Collections.Generic;
using System.Text;
using HospitalCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalCare.Infrastructure.Persistence.Configurations
{
    public sealed class DoctorScheduleConfiguration
     : IEntityTypeConfiguration<DoctorSchedule>
    {
        public void Configure(EntityTypeBuilder<DoctorSchedule> builder)
        {
            builder.ToTable("DoctorSchedules");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.DayOfWeek)
                .IsRequired();

            builder.Property(x => x.StartTime)
                .IsRequired();

            builder.Property(x => x.EndTime)
                .IsRequired();

            builder.ToTable("DoctorSchedules",
               tableBuilder =>
               {
                   tableBuilder.HasCheckConstraint(
                       "CK_DoctorSchedules_EndTime_After_StartTime",
                       "[EndTime] > [StartTime]");
               });

            builder.HasOne(x => x.Doctor)
                .WithMany(x => x.Schedules)
                .HasForeignKey(x => x.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new
            {
                x.DoctorId,
                x.DayOfWeek
            });
        }
    }
    }
