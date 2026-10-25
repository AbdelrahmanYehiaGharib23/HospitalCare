using HospitalCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalCare.Infrastructure.Persistence.Configurations
{
    public class AppointmentConfiguration : BaseEntityConfiguration<Appointment>
    {
        public override void Configure(EntityTypeBuilder<Appointment> builder)
        {
            builder.ToTable("Appointments");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.Notes)
                .HasMaxLength(2000);

            builder.Property(x => x.QueuePosition)
                .IsRequired();

            builder.ToTable("Appointments",
    tableBuilder =>
    {
        tableBuilder.HasCheckConstraint(
            "CK_Appointments_QueuePosition",
            "[QueuePosition] > 0");
    });

            builder.HasOne(x => x.Doctor)
                .WithMany(x => x.Appointments)
                .HasForeignKey(x => x.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Patient)
                .WithMany(x => x.Appointments)
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.AppointmentType)
                .WithMany(x => x.Appointments)
                .HasForeignKey(x => x.AppointmentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.PaymentTransaction)
                .WithOne(x => x.Appointment)
                .HasForeignKey<Appointment>(
                    x => x.PaymentTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new
            {
                x.DoctorId,
                x.AppointmentDate,
                x.QueuePosition
            }).IsUnique();

            builder.HasIndex(x => new
            {
                x.PatientId,
                x.AppointmentDate
            });
            base.Configure(builder);
        }
    }
}