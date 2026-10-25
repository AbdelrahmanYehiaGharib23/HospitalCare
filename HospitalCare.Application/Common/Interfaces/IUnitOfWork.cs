
using HospitalCare.Domain.Entities;

namespace HospitalCare.Application.Common.Interfaces
{
    public interface IUnitOfWork
    {
        IGenericRepository<Department> Departments { get; }

        IGenericRepository<Doctor> Doctors { get; }

        IGenericRepository<Patient> Patients { get; }

        IGenericRepository<Appointment> Appointments { get; }

        IGenericRepository<AppointmentType> AppointmentTypes { get; }

        IGenericRepository<DoctorSchedule> DoctorSchedules { get; }

        IGenericRepository<PaymentTransaction> PaymentTransactions { get; }

        Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default);

        Task<TResult> ExecuteSerializableTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default);
    }
}

