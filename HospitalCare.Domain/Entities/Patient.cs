namespace HospitalCare.Domain.Entities
{
    public class Patient : BaseEntity
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public DateOnly DateOfBirth { get; set; }

        public string? Allergies { get; set; }

        public string? ChronicConditions { get; set; }

        public ICollection<Appointment> Appointments { get; set; } =
            new List<Appointment>();
    }
}
