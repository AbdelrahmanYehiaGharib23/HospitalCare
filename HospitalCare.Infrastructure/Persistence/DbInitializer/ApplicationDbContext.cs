using System.Reflection;
using HospitalCare.Domain.Entities;
using HospitalCare.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HospitalCare.Infrastructure.Persistence.DbInitializer
{

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var assembly = Assembly.GetExecutingAssembly();

            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        public DbSet<Department> Departments => Set<Department>();
        public DbSet<Doctor> Doctors => Set<Doctor>();
        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();
        public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
        public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    }
}
