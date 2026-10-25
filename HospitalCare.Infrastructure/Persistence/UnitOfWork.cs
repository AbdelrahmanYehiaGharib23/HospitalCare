using System.Data;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Domain.Entities;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using HospitalCare.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HospitalCare.Infrastructure.Persistence
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private const int MaximumTransactionAttempts = 3;
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
        // Serializable locks and the unique index protect allocation; retry deadlocks and uniqueness races.
        public async Task<TResult> ExecuteSerializableTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            for (var attempt = 1; attempt <= MaximumTransactionAttempts; attempt++)
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken);

                try
                {
                    var result = await operation(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch (Exception exception) when (
                    attempt < MaximumTransactionAttempts &&
                    IsRetryableSqlException(exception))
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _context.ChangeTracker.Clear();
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(50 * attempt),
                        cancellationToken);
                }
            }

            throw new InvalidOperationException(
                "The serializable transaction did not complete.");
        }

        private static bool IsRetryableSqlException(Exception exception)
        {
            for (var current = exception;
                 current is not null;
                 current = current.InnerException)
            {
                if (current is SqlException sqlException &&
                    sqlException.Errors
                        .Cast<SqlError>()
                        .Any(error => error.Number is 1205 or 2601 or 2627))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
