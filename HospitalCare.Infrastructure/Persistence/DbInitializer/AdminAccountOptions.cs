namespace HospitalCare.Infrastructure.Persistence.DbInitializer;

public sealed class AdminAccountOptions
{
    public const string SectionName = "SeedAdmin";

    public string? Email { get; set; }

    public string? Password { get; set; }
}
