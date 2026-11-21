using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Domain.Entities;
using FluentValidation;

namespace Dsw2026Tpi.Application.Validators;

public class AppointmentUpdateStatusValidator : AbstractValidator<AppointmentModel.UpdateStatusRequest>
{
    private static readonly string[] AllowedStatuses =
    [
        nameof(AppointmentStatus.ATTENDED),
        nameof(AppointmentStatus.NO_SHOW)
    ];

    public AppointmentUpdateStatusValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("es obligatorio")
            .Must(status => AllowedStatuses.Contains(status.ToUpperInvariant()))
            .WithMessage("debe ser ATTENDED o NO_SHOW");
    }
}
