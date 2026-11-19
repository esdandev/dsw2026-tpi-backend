using System.Linq.Expressions;
using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Dsw2026Tpi.Test.Services;

public class PruebasUnitarias
{
    private readonly IPersistence _persistence;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppointmentService _appointmentTest;

    public PruebasUnitarias()
    {
        _persistence = Substitute.For<IPersistence>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        _appointmentTest = new AppointmentService(_persistence, _userManager); //Objeto de prueba
    }

    [Fact]
    public void Clock_ConvierteEntreArgentinaYUtc_SinPerderLaHoraLocal()
    {
        var local = new DateTime(2026, 11, 16, 10, 0, 0);
        var utc = Clock.FromArgentina(local);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(13, utc.Hour);
        Assert.Equal(local, Clock.ToArgentina(utc));
    }

    [Fact]
    public async Task AppointmentCreate_CuandoElDoctorNoExiste_EntoncesLanzaEntityNotFoundException()
    {
        // Arrange
        var request = new AppointmentModel.CreateRequest(
            DoctorId: Guid.NewGuid(),
            AvailabilitySlotId: Guid.NewGuid(),
            Patient: new(40123456),
            Reason: "Control de rutina");

        _persistence.GetById<Doctor>(request.DoctorId).Returns((Doctor?)null); //aqui hace como que el doctor no existe en la bd, asi que devuelve null.

        // Act
        var act = () => _appointmentTest.Create(request, "paciente@email.com"); //Prueba el create por el camino del doctor is null

        // Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(act); //Aqui espera a q llegue la excepcion en el "act" 
    }

    [Fact]
    public async Task AppointmentCreate_CuandoElDoctorEstaEliminado_EntoncesLanzaEntityNotFoundException()
    {
        // Arrange
        var request = new AppointmentModel.CreateRequest(
           DoctorId: Guid.NewGuid(),
           AvailabilitySlotId: Guid.NewGuid(),
           Patient: new(40123456),
           Reason: "Control de rutina");

        var speciality = new Speciality("Cardiología", "Especialidad del corazón");
        var doctor = new Doctor("Dr. Juan Pérez", "MP1234", speciality, request.DoctorId);
        doctor.MarkAsDeleted(); // el doctor existe, pero está eliminado lógicamente

        _persistence.GetById<Doctor>(request.DoctorId).Returns(doctor);

        // Act
        var act = () => _appointmentTest.Create(request, "paciente@email.com");

        // Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(act);
    }

 

    [Fact]
    public async Task AppointmentCreate_CuandoElSlotYaEstaReservado_EntoncesLanzaConflictException()
    {
        // Arrange
        var request = new AppointmentModel.CreateRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AppointmentModel.PatientRequest(40123456),
            "Control de rutina");

        var speciality = new Speciality(
            "Cardiología",
            "Especialidad médica");

        var doctor = new Doctor(
            "Juan Pérez",
            "MP12345",
            speciality,
            request.DoctorId);

        var slot = new AvailabilitySlot
        {
            Id = request.AvailabilitySlotId,
            DoctorId = request.DoctorId,
            Status = SlotStatus.BOOKED,
            Start = DateTime.UtcNow.AddHours(1),
            End = DateTime.UtcNow.AddHours(2)
        };

        _persistence.GetById<Doctor>(request.DoctorId)
            .Returns(doctor);

        _persistence.GetById<AvailabilitySlot>(request.AvailabilitySlotId)
            .Returns(slot);

        // Act
        var act = () => _appointmentTest.Create(request, "paciente@email.com");

        // Assert
        await Assert.ThrowsAsync<ConflictException>(act);
    }

    [Fact]
    public async Task AppointmentCreate_CuandoTodoEsValido_EntoncesCreaElTurnoConEstadoBooked()
    {
        // Arrange
        var request = new AppointmentModel.CreateRequest(
            DoctorId: Guid.NewGuid(),
            AvailabilitySlotId: Guid.NewGuid(),
            Patient: new(40123456),
            Reason: "Control de rutina");

        var speciality = new Speciality("Cardiología", "Especialidad del corazón");
        var doctor = new Doctor("Dr. Juan Pérez", "MP1234", speciality, request.DoctorId);

        var slot = new AvailabilitySlot
        {
            Id = request.AvailabilitySlotId,
            DoctorId = request.DoctorId,
            Start = DateTime.UtcNow.AddDays(1),
            End = DateTime.UtcNow.AddDays(1).AddMinutes(30),
            Status = SlotStatus.AVAILABLE
        };

        var patient = new ApplicationUser
        {
            UserName = "paciente@email.com",
            Email = "paciente@email.com",
            Dni = 40123456
        };

        _persistence.GetById<Doctor>(request.DoctorId).Returns(doctor);
        _persistence.GetById<AvailabilitySlot>(request.AvailabilitySlotId).Returns(slot);
        _userManager.FindByNameAsync("paciente@email.com").Returns(patient);

        // Act
        var result = await _appointmentTest.Create(request, "paciente@email.com");

        // Assert
        Assert.Equal("BOOKED", result.Status);
        Assert.Equal(SlotStatus.BOOKED, slot.Status);
    }

    [Fact]
    public void AdministrativePatient_CuandoTieneNombreYApellido_EntoncesDevuelveElNombreCompleto()
    {
        var patient = new ApplicationUser
        {
            Dni = 40123456,
            Email = "paciente@email.com",
            FirstName = "Ana",
            LastName = "Gómez"
        };

        var result = AppointmentModel.AdministrativePatient.From(patient);

        Assert.Equal(40123456, result.Dni);
        Assert.Equal("Ana Gómez", result.FullName);
    }

    [Fact]
    public void AdministrativePatient_CuandoNoTieneNombre_EntoncesDevuelveElEmail()
    {
        var patient = new ApplicationUser
        {
            Dni = 40123456,
            Email = "paciente@email.com"
        };

        var result = AppointmentModel.AdministrativePatient.From(patient);

        Assert.Equal("paciente@email.com", result.FullName);
    }

    [Fact]
    public async Task GetAvailabilities_CuandoElMedicoNoExiste_EntoncesLanzaEntityNotFoundException()
    {
        var service = new DoctorService(_persistence);
        _persistence.GetById<Doctor>(Arg.Any<Guid>()).Returns((Doctor?)null);

        var act = () => service.GetAvailabilities(Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(act);
    }

    [Fact]
    public async Task GetAvailabilities_CuandoElMedicoNoTieneReglasDelMes_EntoncesDevuelveVacio()
    {
        var doctor = CrearDoctor();
        var service = new DoctorService(_persistence);

        _persistence.GetById<Doctor>(doctor.Id).Returns(doctor);
        _persistence.GetFiltered<AvailabilityRule>(
                Arg.Any<Expression<Func<AvailabilityRule, bool>>>(), Arg.Any<string[]>())
            .Returns(Enumerable.Empty<AvailabilityRule>());

        var result = await service.GetAvailabilities(doctor.Id);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAvailabilities_DevuelveUnaEntradaPorDiaConIdYHoraDe24Horas()
    {
        var doctor = CrearDoctor();
        var service = new DoctorService(_persistence);
        var manana = new AvailabilityRule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctor.Id,
            DaysOfWeekCsv = "LUNES,MIERCOLES",
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            IsActive = true,
            EffectiveFrom = Clock.Now
        };
        var tarde = new AvailabilityRule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctor.Id,
            DaysOfWeekCsv = "JUEVES",
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            IsActive = true,
            EffectiveFrom = Clock.Now
        };

        _persistence.GetById<Doctor>(doctor.Id).Returns(doctor);
        _persistence.GetFiltered<AvailabilityRule>(
                Arg.Any<Expression<Func<AvailabilityRule, bool>>>(), Arg.Any<string[]>())
            .Returns(new[] { tarde, manana });

        var result = (await service.GetAvailabilities(doctor.Id)).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(manana.Id, result[0].Id);
        Assert.Equal("LUNES", result[0].Day);
        Assert.Equal("09:00", result[0].StartTime);
        Assert.Equal("12:00", result[0].EndTime);
        Assert.Equal("MIÉRCOLES", result[1].Day);
        Assert.Equal(tarde.Id, result[2].Id);
        Assert.Equal("14:00", result[2].StartTime);
        Assert.Equal("18:00", result[2].EndTime);
    }

    private static Doctor CrearDoctor() =>
        new("Dra. Ana López", "MP1234", new Speciality("Clínica", "Clínica médica"));
}