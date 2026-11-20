using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Dsw2026Tpi.Api.Configurations;
using Microsoft.AspNetCore.RateLimiting;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;

namespace Dsw2026Tpi.Api.Controllers;

[Route("api/auth")]
public class AuthenticationController : AppController
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IValidator<LoginAdminModel.Request> _loginAdminValidator;
    private readonly IValidator<LoginPatientModel.Request> _loginPatientValidator;

    public AuthenticationController(IAuthenticationService authenticationService, IValidator<LoginAdminModel.Request> loginAdminValidator, IValidator<LoginPatientModel.Request> loginPatientValidator) 
    {
        _authenticationService = authenticationService;
        _loginAdminValidator = loginAdminValidator;
        _loginPatientValidator = loginPatientValidator;
    }

    [HttpPost("admin/login")]
    [EnableRateLimiting(RateLimitPolicies.AdminLogin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginAdminModel.Request request)
    {
        var validation = await _loginAdminValidator.ValidateAsync(request);
        Invalidez(validation);

        var result = await _authenticationService.LoginAdmin(request);
        return Ok(result);
    }

    [HttpPost("patient/login")]
    [EnableRateLimiting(RateLimitPolicies.PatientLogin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LoginPatient([FromBody] LoginPatientModel.Request request)
    {
        var validation = await _loginPatientValidator.ValidateAsync(request);
        Invalidez(validation);

        var result = await _authenticationService.LoginPatient(request);
        return Ok(result);
    }
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var expClaim = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

        if (string.IsNullOrWhiteSpace(jti) || !long.TryParse(expClaim, out var expUnixSeconds))
            throw new AuthenticationException();

        var expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds).UtcDateTime;

        await _authenticationService.Logout(jti, expiresAtUtc);
        return NoContent();
    }
}
