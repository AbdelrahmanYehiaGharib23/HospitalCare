using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Domain.Entities;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using HospitalCare.Infrastructure.Repositories;

namespace HospitalCare.Infrastructure.Persistence
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        private readonly Lazy<IGenericRepository<Department>> _departments;
        private readonly Lazy<IGenericRepository<Doctor>> _doctors;
        private readonly Lazy<IGenericRepository<Patient>> _patients;
        private readonly Lazy<IGenericRepository<Appointment>> _appointments;
        private readonly Lazy<IGenericRepository<AppointmentType>> _appointmentTypes;
        private readonly Lazy<IGenericRepository<DoctorSchedule>> _doctorSchedules;
        private readonly Lazy<IGenericRepository<PaymentTransaction>> _paymentTransactions;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;

            _departments = new Lazy<IGenericRepository<Department>>(
                () => new GenericRepository<Department>(_context));

            _doctors = new Lazy<IGenericRepository<Doctor>>(
                () => new GenericRepository<Doctor>(_context));

            _patients = new Lazy<IGenericRepository<Patient>>(
                () => new GenericRepository<Patient>(_context));

            _appointments = new Lazy<IGenericRepository<Appointment>>(
                () => new GenericRepository<Appointment>(_context));

            _appointmentTypes = new Lazy<IGenericRepository<AppointmentType>>(
                () => new GenericRepository<AppointmentType>(_context));

            _doctorSchedules = new Lazy<IGenericRepository<DoctorSchedule>>(
                () => new GenericRepository<DoctorSchedule>(_context));

            _paymentTransactions = new Lazy<IGenericRepository<PaymentTransaction>>(
                () => new GenericRepository<PaymentTransaction>(_context));
        }

        public IGenericRepository<Department> Departments =>
            _departments.Value;

        public IGenericRepository<Doctor> Doctors =>
            _doctors.Value;

        public IGenericRepository<Patient> Patients =>
            _patients.Value;

        public IGenericRepository<Appointment> Appointments =>
            _appointments.Value;

        public IGenericRepository<AppointmentType> AppointmentTypes =>
            _appointmentTypes.Value;

        public IGenericRepository<DoctorSchedule> DoctorSchedules =>
            _doctorSchedules.Value;

        public IGenericRepository<PaymentTransaction> PaymentTransactions =>
            _paymentTransactions.Value;

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
