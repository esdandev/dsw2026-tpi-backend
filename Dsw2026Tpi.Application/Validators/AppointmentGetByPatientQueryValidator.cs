using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.CrossCutting.Helpers;
using FluentValidation;

namespace Dsw2026Tpi.Application.Validators;

public class AppointmentGetByPatientQueryValidator : AbstractValidator<AppointmentModel.GetByPatientQuery>
{
    public AppointmentGetByPatientQueryValidator()
    {
        RuleFor(x => x.Dni)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("es obligatorio")
            .Must(dni => dni!.Value.IsDniValid(7, 8))
            .WithMessage("debe tener 7 u 8 dígitos");
    }
}