namespace Dsw2026Tpi.Application.Dtos;

public record LoginAdminModel
{
    public record Request(string Email, string Password);
    public record Response(string? Token, string? Role);
}

public record LoginPatientModel
{
    // Nombre y apellido son opcionales para no romper los logins existentes.
    public record Request(string Email, long Dni, string? FirstName = null, string? LastName = null);
    public record Response(string? Token, string? Role);
}
