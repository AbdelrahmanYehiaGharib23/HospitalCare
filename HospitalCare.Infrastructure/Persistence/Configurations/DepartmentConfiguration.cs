using HospitalCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalCare.Infrastructure.Persistence.Configurations
{
    public class DepartmentConfiguration : BaseEntityConfiguration<Department>
    {
        public override void Configure(EntityTypeBuilder<Department> builder)
        {
            builder.ToTable("Departments");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(x => x.IsActive)
                .HasDefaultValue(true);

            builder.HasMany(x => x.Doctors)
                .WithOne(x => x.Department)
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            base.Configure(builder);
        }
    }
}
