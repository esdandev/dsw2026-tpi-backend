using Dsw2026Tpi.Data.Identity;

namespace Dsw2026Tpi.Application.Dtos;

public static class AppointmentModel
{
    //contiene información administrativa completa
    public record AdministrativeResponse(Guid AppointmentsId, string AppointmentsStatus, AdministrativePatient Patient, AdministrativeDoctor Doctor, DateTime AvailableTime);
    public record SearchAdministrativeResponse(Guid AppointmentsId, string AppointmentsStatus, AdministrativePatient Patient, AdministrativeDoctor Doctor, DateTime AvailableTime);
    public record AdministrativePatient(long Dni, string FullName)
    {
        // Los pacientes registrados antes de que existiera el nombre no lo tienen: se muestra el email.
        public static AdministrativePatient From(ApplicationUser user)
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            return new AdministrativePatient(user.Dni, fullName.Length > 0 ? fullName : user.Email ?? user.Dni.ToString());
        }
    }
    public record AdministrativeDoctor(Guid DoctorId, string Name, AdministrativeSpecialty Specialty);
    public record AdministrativeSpecialty(Guid SpecialtyId, string Name);

    //----------------
    public record PatientRequest(long Dni);
    public record CreateRequest(Guid DoctorId, Guid AvailabilitySlotId, PatientRequest Patient, string Reason);
    public record CreateResponse(Guid Id, string Status, DateTime StartTime, DateTime EndTime);
    public record GetByDateQuery(string Date, int PageSize, int PageIndex);
    public record SearchQuery(Guid? SpecialtyId, Guid? DoctorId, long? Dni, string? Date, int PageSize, int PageIndex);
    public record PatientResponse(Guid Id, Guid DoctorId, string DoctorName, string Reason, string Status, DateTime StartTime, DateTime EndTime);
}