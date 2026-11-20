using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Dsw2026Tpi.CrossCutting.Resources;

namespace Dsw2026Tpi.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISignInService _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly JwtService _jwtService;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(UserManager<ApplicationUser> userManager,ISignInService signInManager,RoleManager<IdentityRole> roleManager,JwtService jwtService,ITokenRevocationService tokenRevocationService,ILogger<AuthenticationService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _tokenRevocationService = tokenRevocationService;
        _logger = logger;
    }
    public async Task<LoginAdminModel.Response> LoginAdmin(LoginAdminModel.Request request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            _logger.LogWarning("Intento de login administrativo con usuario inexistente. Email: {Email}", request.Email);
            throw new AuthenticationException();
        }

        EnsureUserIsActiveAndConfirmed(user, "administrador", request.Email);

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Intento de login administrativo sobre cuenta bloqueada. Email: {Email}", request.Email);
            throw new AuthenticationException();
        }

        var passwordIsValid = await _signInManager.CheckPassword(user, request.Password);

        if (!passwordIsValid)
        {
            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Cuenta de administrador bloqueada por intentos fallidos. Email: {Email}", request.Email);
            }
            else
            {
                _logger.LogWarning("Intento de login administrativo fallido por contraseña incorrecta. Email: {Email}", request.Email);
            }

            throw new AuthenticationException();
        }

        var isAdministrator = await _userManager.IsInRoleAsync(user, Roles.Administrator);

        if (!isAdministrator)
        {
            _logger.LogWarning("Intento de login administrativo de un usuario sin rol Administrador. Email: {Email}", request.Email);
            throw new AuthenticationException();
        }

        var token = _jwtService.GenerateToken(user.UserName!, Roles.Administrator);
        return new LoginAdminModel.Response(token, Roles.Administrator.ToUpperInvariant());
    }
    public async Task<LoginPatientModel.Response> LoginPatient(LoginPatientModel.Request request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        var firstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();

        // Si no existe, se registra automáticamente como paciente.
        if (user is null)
        {
            var dniInUse = await _userManager.Users.AnyAsync(u => u.Dni == request.Dni);

            if (dniInUse)
            {
                _logger.LogWarning(
                    "Intento de login de paciente con DNI asociado a otro email. Email: {Email}, Dni: {MaskedDni}",
                    request.Email,
                    MaskDni(request.Dni));
                throw new ConflictException(nameof(ErrorCodes.PATIENT_DNI_CONFLICT), ErrorCodes.PATIENT_DNI_CONFLICT);
            }

            var now = DateTime.UtcNow;
            user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true,
                Dni = request.Dni,
                FirstName = firstName,
                LastName = lastName,
                Deleted = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            var createResult = await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                _logger.LogWarning(
                    "No se pudo auto-registrar el paciente. Email: {Email}, Dni: {MaskedDni}",
                    request.Email,
                    MaskDni(request.Dni));
                throw new AuthenticationException();
            }

            var roleResult = await _userManager.AddToRoleAsync(user, Roles.Patient);

            if (!roleResult.Succeeded)
            {
                _logger.LogWarning(
                    "No se pudo asignar el rol Paciente al usuario auto-registrado. Email: {Email}, Dni: {MaskedDni}",
                    request.Email,
                    MaskDni(request.Dni));
                throw new AuthenticationException();
            }
        }
        else
        {
            EnsureUserIsActiveAndConfirmed(user, "paciente", request.Email, request.Dni);

            if (user.Dni != request.Dni)
            {
                _logger.LogWarning(
                    "Intento de login de paciente con DNI incorrecto. Email: {Email}, Dni: {MaskedDni}",
                    request.Email,
                    MaskDni(request.Dni));
                throw new AuthenticationException();
            }

            var isPatient = await _userManager.IsInRoleAsync(user, Roles.Patient);

            if (!isPatient)
            {
                _logger.LogWarning(
                    "Intento de login de paciente con un usuario que no posee el rol Paciente. Email: {Email}, Dni: {MaskedDni}",
                    request.Email,
                    MaskDni(request.Dni));
                throw new AuthenticationException();
            }

            // Los pacientes registrados antes de que existiera el nombre lo completan en el próximo login.
            var updatedFirstName = firstName ?? user.FirstName;
            var updatedLastName = lastName ?? user.LastName;

            if (updatedFirstName != user.FirstName || updatedLastName != user.LastName)
            {
                user.FirstName = updatedFirstName;
                user.LastName = updatedLastName;
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }
        }

        var token = _jwtService.GenerateToken(user.UserName!, Roles.Patient);

        return new LoginPatientModel.Response(token, Roles.Patient.ToUpperInvariant());
    }
    public async Task Logout(string jti, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(jti))
            throw new AuthenticationException();

        await _tokenRevocationService.RevokeAsync(jti, expiresAtUtc);
        _logger.LogInformation("Token revocado mediante logout. Jti: {Jti}", jti);
    }
    private void EnsureUserIsActiveAndConfirmed(ApplicationUser user, string loginType, string email, long? dni = null)
    {
        if (user.Deleted)
        {
            if (dni.HasValue)
            {
                _logger.LogWarning(
                    "Intento de login de {LoginType} con usuario eliminado. Email: {Email}, Dni: {MaskedDni}",
                    loginType,
                    email,
                    MaskDni(dni.Value));
            }
            else
            {
                _logger.LogWarning(
                    "Intento de login de {LoginType} con usuario eliminado. Email: {Email}",
                    loginType,
                    email);
            }

            throw new AuthenticationException();
        }

        if (!user.EmailConfirmed)
        {
            if (dni.HasValue)
            {
                _logger.LogWarning(
                    "Intento de login de {LoginType} con correo no confirmado. Email: {Email}, Dni: {MaskedDni}",
                    loginType,
                    email,
                    MaskDni(dni.Value));
            }
            else
            {
                _logger.LogWarning(
                    "Intento de login de {LoginType} con correo no confirmado. Email: {Email}",
                    loginType,
                    email);
            }

            throw new AuthenticationException();
        }
    }
    private static string MaskDni(long dni)
    {
        var digits = dni.ToString();
        return digits.Length <= 3
            ? new string('*', digits.Length)
            : $"{new string('*', digits.Length - 3)}{digits[^3..]}";
    }
}
