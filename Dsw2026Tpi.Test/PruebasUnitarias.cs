using Dsw2026Tpi.Api.Controllers;
using Dsw2026Tpi.Api.Middlewares;
using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.Application.Validators;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;
using Xunit;
using Dsw2026Tpi.CrossCutting.Identity;
using System.Text.Json;
using Dsw2026Tpi.CrossCutting.Resources;

namespace Dsw2026Tpi.Test.Services;

public class PruebasUnitarias
{
    private readonly IPersistence _persistence;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppointmentService _appointmentTest;

    public PruebasUnitarias()
    {
        _persistence = Substitute.For<IPersistence>();
        _persistence.ExecuteInTransaction(Arg.Any<Func<Task<AppointmentModel.CreateResponse>>>())
            .Returns(call => call.Arg<Func<Task<AppointmentModel.CreateResponse>>>()());
        _persistence.ExecuteInTransaction(Arg.Any<Func<Task>>())
            .Returns(call => call.Arg<Func<Task>>()());

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

    [Fact]
    public async Task Create_GeneraSlotsDe30MinParaElRestoDelMesExcluyendoFeriados()
    {
        var persistence = Substitute.For<IPersistence>();
        var holidays = Substitute.For<IHolidayProvider>();
        holidays.IsNonWorkingDay(Arg.Any<DateOnly>())
            .Returns(call => call.Arg<DateOnly>().DayOfWeek == DayOfWeek.Monday);
        var service = CrearServicioDeDisponibilidad(persistence, holidays);

        var doctor = CrearDoctor();
        persistence.GetById<Doctor>(doctor.Id).Returns(doctor);

        var slots = new List<AvailabilitySlot>();
        persistence.AddRange(Arg.Do<IEnumerable<AvailabilitySlot>>(list => slots.AddRange(list)));

        var days = new[] { "LUNES", "MARTES", "MIERCOLES", "JUEVES", "VIERNES", "SABADO", "DOMINGO" }
            .Select(day => (day, "09:00", "10:00"))
            .ToArray();

        await service.Create(CrearRequestDeDisponibilidad(doctor, days));

        var today = Clock.Today;
        var lastDay = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var now = Clock.Now;
        var expectedSlots = Enumerable.Range(0, (lastDay - today).Days + 1)
            .Select(offset => today.AddDays(offset))
            .Where(date => date.DayOfWeek != DayOfWeek.Monday)
            .SelectMany(date => new[] { date.AddHours(9), date.AddHours(9).AddMinutes(30) })
            .Count(start => Clock.FromArgentina(start) > now);

        Assert.Equal(expectedSlots, slots.Count);
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromMinutes(30), slot.End - slot.Start));
        Assert.DoesNotContain(slots, slot => Clock.ToArgentina(slot.Start).DayOfWeek == DayOfWeek.Monday);
        await persistence.Received(1).AddRange(Arg.Any<IEnumerable<AvailabilitySlot>>());
    }

    [Fact]
    public async Task Create_AceptaDiasConTildeYNormalizaElNombre()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = CrearServicioDeDisponibilidad(persistence, Substitute.For<IHolidayProvider>());

        var doctor = CrearDoctor();
        persistence.GetById<Doctor>(doctor.Id).Returns(doctor);

        var rules = new List<AvailabilityRule>();
        persistence.Add(Arg.Any<AvailabilityRule>())
            .Returns(call =>
            {
                rules.Add(call.Arg<AvailabilityRule>());
                return Task.FromResult(call.Arg<AvailabilityRule>());
            });

        await service.Create(CrearRequestDeDisponibilidad(
            doctor, ("MIÉRCOLES", "09:00", "10:00"), ("SÁBADO", "09:00", "10:00")));

        var rule = Assert.Single(rules);
        Assert.Equal("MIERCOLES,SABADO", rule.DaysOfWeekCsv);
    }

    [Fact]
    public async Task Create_CuandoLaHoraDeInicioNoEsMenorALaDeFin_LanzaValidationException()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = CrearServicioDeDisponibilidad(persistence, Substitute.For<IHolidayProvider>());

        var doctor = CrearDoctor();
        persistence.GetById<Doctor>(doctor.Id).Returns(doctor);

        var act = () => service.Create(CrearRequestDeDisponibilidad(doctor, ("LUNES", "10:00", "09:00")));

        await Assert.ThrowsAsync<ValidationException>(act);
    }

    [Fact]
    public async Task Create_CuandoSeSolapaConUnaReglaExistente_LanzaBusinessRuleException()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = CrearServicioDeDisponibilidad(persistence, Substitute.For<IHolidayProvider>());

        var doctor = CrearDoctor();
        persistence.GetById<Doctor>(doctor.Id).Returns(doctor);

        var existente = new AvailabilityRule
        {
            DoctorId = doctor.Id,
            DaysOfWeekCsv = "LUNES",
            StartTime = new TimeSpan(9, 30, 0),
            EndTime = new TimeSpan(11, 0, 0),
            EffectiveFrom = Clock.Now,
            IsActive = true
        };

        persistence.GetFiltered<AvailabilityRule>(
                Arg.Any<Expression<Func<AvailabilityRule, bool>>>(), Arg.Any<string[]>())
            .Returns(new[] { existente });

        var act = () => service.Create(CrearRequestDeDisponibilidad(doctor, ("LUNES", "09:00", "10:00")));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("AVAILABILITY_OVERLAP", ex.Error.ErrorCode);
    }

    [Fact]
    public async Task Update_ConservaElSlotReservadoYEliminaElDisponibleObsoleto()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = CrearServicioDeDisponibilidad(persistence, Substitute.For<IHolidayProvider>());

        var doctor = CrearDoctor();
        persistence.GetById<Doctor>(doctor.Id).Returns(doctor);

        var today = Clock.Today;
        var lastDay = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var reservado = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = doctor.Id,
            Start = Clock.FromArgentina(lastDay.AddHours(8)),
            End = Clock.FromArgentina(lastDay.AddHours(8).AddMinutes(30)),
            Status = SlotStatus.BOOKED
        };
        var obsoleto = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = doctor.Id,
            Start = Clock.FromArgentina(lastDay.AddHours(8).AddMinutes(30)),
            End = Clock.FromArgentina(lastDay.AddHours(9)),
            Status = SlotStatus.AVAILABLE
        };

        persistence.GetFiltered<AvailabilitySlot>(
                Arg.Any<Expression<Func<AvailabilitySlot, bool>>>(), Arg.Any<string[]>())
            .Returns(new[] { reservado, obsoleto });

        var days = new[] { "LUNES", "MARTES", "MIERCOLES", "JUEVES", "VIERNES", "SABADO", "DOMINGO" }
            .Select(day => (day, "09:00", "10:00"))
            .ToArray();

        await service.Update(CrearRequestDeDisponibilidad(doctor, days));

        await persistence.Received(1).Delete(Arg.Is<AvailabilitySlot>(slot => slot.Id == obsoleto.Id));
        await persistence.DidNotReceive().Delete(Arg.Is<AvailabilitySlot>(slot => slot.Id == reservado.Id));
        Assert.Equal(SlotStatus.BOOKED, reservado.Status);
    }

    [Fact]
    public async Task Create_NoGeneraSlotsVencidosDelDiaActual()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = CrearServicioDeDisponibilidad(persistence, Substitute.For<IHolidayProvider>());

        var doctor = CrearDoctor();
        persistence.GetById<Doctor>(doctor.Id).Returns(doctor);

        var slots = new List<AvailabilitySlot>();
        persistence.AddRange(Arg.Do<IEnumerable<AvailabilitySlot>>(list => slots.AddRange(list)));

        var days = new[] { "LUNES", "MARTES", "MIERCOLES", "JUEVES", "VIERNES", "SABADO", "DOMINGO" }
            .Select(day => (day, "00:00", "23:30"))
            .ToArray();

        var antes = Clock.Now;
        await service.Create(CrearRequestDeDisponibilidad(doctor, days));

        Assert.All(slots, slot => Assert.True(slot.Start > antes, $"slot vencido generado: {slot.Start:O}"));
    }

    [Fact]
    public async Task AppointmentCreate_CuandoElSlotYaPaso_LanzaValidationException()
    {
        var request = new AppointmentModel.CreateRequest(
            Guid.NewGuid(), Guid.NewGuid(), new AppointmentModel.PatientRequest(40123456), "Control de rutina");
        var doctor = new Doctor("Dr. Juan Pérez", "MP1234", new Speciality("Cardiología", "Especialidad del corazón"), request.DoctorId);
        var slot = new AvailabilitySlot
        {
            Id = request.AvailabilitySlotId,
            DoctorId = request.DoctorId,
            Start = Clock.Now.AddMinutes(-30),
            End = Clock.Now,
            Status = SlotStatus.AVAILABLE
        };

        _persistence.GetById<Doctor>(request.DoctorId).Returns(doctor);
        _persistence.GetById<AvailabilitySlot>(request.AvailabilitySlotId).Returns(slot);

        var act = () => _appointmentTest.Create(request, "paciente@email.com");

        await Assert.ThrowsAsync<ValidationException>(act);
    }

    [Fact]
    public async Task Update_CuandoElTurnoEstaCancelado_EntoncesBorraElSlotEnVezDeBloquearlo()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = CrearServicioDeDisponibilidad(persistence, Substitute.For<IHolidayProvider>());

        var doctor = CrearDoctor();
        persistence.GetById<Doctor>(doctor.Id).Returns(doctor);

        var today = Clock.Today;
        var lastDay = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var slot = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = doctor.Id,
            Start = Clock.FromArgentina(lastDay.AddHours(8)),
            End = Clock.FromArgentina(lastDay.AddHours(8).AddMinutes(30)),
            Status = SlotStatus.AVAILABLE
        };
        var cancelado = new Appointment(slot, "paciente", "Control de rutina");
        cancelado.Cancel();

        persistence.GetFiltered<AvailabilitySlot>(
                Arg.Any<Expression<Func<AvailabilitySlot, bool>>>(), Arg.Any<string[]>())
            .Returns(new[] { slot });
        persistence.GetFiltered<Appointment>(
                Arg.Any<Expression<Func<Appointment, bool>>>(), Arg.Any<string[]>())
            .Returns(new[] { cancelado });

        var days = new[] { "LUNES", "MARTES", "MIERCOLES", "JUEVES", "VIERNES", "SABADO", "DOMINGO" }
            .Select(day => (day, "09:00", "10:00"))
            .ToArray();

        await service.Update(CrearRequestDeDisponibilidad(doctor, days));

        await persistence.Received(1).Delete(Arg.Is<AvailabilitySlot>(s => s.Id == slot.Id));
        await persistence.DidNotReceive().Update(Arg.Is<AvailabilitySlot>(s => s.Id == slot.Id));
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void SpecialityRequestValidator_ValidaLongitudDeDescripcion(int length, bool valida)
    {
        var validator = new SpecialityRequestValidator();
        var request = new SpecialityModel.Request("Cardiología", new string('a', length));

        var result = validator.Validate(request);

        Assert.Equal(valida, result.IsValid);
    }

    [Fact]
    public void DayRequestValidator_CuandoElDiaEstaVacio_DevuelveMensajeEnEspanol()
    {
        var validator = new DayRequestValidator();
        var result = validator.Validate(new AvailabilityModel.DayRequest
        {
            Day = "",
            StartTime = "09:00",
            EndTime = "10:00"
        });

        Assert.False(result.IsValid);
        Assert.Equal("se requiere el día de la semana", Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("9:00")]
    [InlineData("24:00")]
    [InlineData("09:60")]
    public void DayRequestValidator_CuandoLaHoraNoEsHHmm_EsInvalida(string hora)
    {
        var validator = new DayRequestValidator();
        var result = validator.Validate(new AvailabilityModel.DayRequest
        {
            Day = "LUNES",
            StartTime = hora,
            EndTime = "10:00"
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task SpecialityService_Create_RecortaNombreYDescripcion()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = new SpecialityService(persistence);
        Speciality? creada = null;
        persistence.Add(Arg.Do<Speciality>(speciality => creada = speciality));

        await service.Create(new SpecialityModel.Request("  Cardiología  ", "  Especialidad del corazón  "));

        Assert.Equal("Cardiología", creada!.Name);
        Assert.Equal("Especialidad del corazón", creada.Description);
    }

    [Fact]
    public async Task SpecialityService_Delete_CuandoTieneMedicosActivos_LanzaConflictException()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = new SpecialityService(persistence);
        var speciality = new Speciality("Cardiología", "Especialidad del corazón");
        var doctor = new Doctor("Dr. Test", "MP-1", speciality);

        persistence.GetById<Speciality>(speciality.Id).Returns(speciality);
        persistence.GetFiltered<Doctor>(
                Arg.Any<Expression<Func<Doctor, bool>>>(), Arg.Any<string[]>())
            .Returns(new[] { doctor });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.Delete(speciality.Id));

        Assert.Equal("SPECIALITY_IN_USE", ex.Error.ErrorCode);
    }

    [Fact]
    public async Task SpecialityService_Delete_SinMedicosActivos_MarcaComoEliminada()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = new SpecialityService(persistence);
        var speciality = new Speciality("Cardiología", "Especialidad del corazón");

        persistence.GetById<Speciality>(speciality.Id).Returns(speciality);
        persistence.GetFiltered<Doctor>(
                Arg.Any<Expression<Func<Doctor, bool>>>(), Arg.Any<string[]>())
            .Returns(Enumerable.Empty<Doctor>());

        await service.Delete(speciality.Id);

        Assert.True(speciality.Deleted);
        await persistence.Received(1).Update(speciality);
    }

    [Fact]
    public async Task DoctorService_Create_RecortaNombreYMatricula()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = new DoctorService(persistence);
        var speciality = new Speciality("Cardiología", "Especialidad del corazón");
        persistence.GetById<Speciality>(speciality.Id).Returns(speciality);

        Doctor? creado = null;
        persistence.Add(Arg.Do<Doctor>(doctor => creado = doctor));

        await service.Create(new DoctorModel.Request("  Dr. Test  ", "  MP-1  ", speciality.Id));

        Assert.Equal("Dr. Test", creado!.Name);
        Assert.Equal("MP-1", creado.LicenseNumber);
    }

    [Fact]
    public async Task DoctorService_Create_CuandoLaEspecialidadNoExiste_UsaSpecialityComoEntidad()
    {
        var persistence = Substitute.For<IPersistence>();
        var service = new DoctorService(persistence);
        persistence.GetById<Speciality>(Arg.Any<Guid>()).Returns((Speciality?)null);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.Create(new DoctorModel.Request("Dr. Test", "MP-1", Guid.NewGuid())));

        Assert.Contains("Speciality", ex.Error.Message);
        Assert.DoesNotContain("SpecialityId", ex.Error.Message);
    }
    [Fact]
    public async Task AppointmentCreate_DosReservasSimultaneas_UnaDevuelve201YOtra409()
    {
        // Arrange
        var doctorId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var speciality = new Speciality("Cardiología", "Especialidad del corazón");
        var doctor = new Doctor("Dr. Juan Pérez", "MP1234", speciality, doctorId);

        var slotStart = DateTime.UtcNow.AddDays(1);
        var slotEnd = slotStart.AddMinutes(30);

        var paciente1 = new ApplicationUser { Id = "p1", UserName = "p1@email.com", Email = "p1@email.com", Dni = 40111111 };
        var paciente2 = new ApplicationUser { Id = "p2", UserName = "p2@email.com", Email = "p2@email.com", Dni = 40222222 };

        var persistence = Substitute.For<IPersistence>();
        persistence.ExecuteInTransaction(Arg.Any<Func<Task<AppointmentModel.CreateResponse>>>())
            .Returns(call => call.Arg<Func<Task<AppointmentModel.CreateResponse>>>()());
        persistence.GetById<Doctor>(doctorId).Returns(doctor);

        // Simula dos lecturas concurrentes del mismo slot disponible (como dos DbContext en paralelo)
        var ambasLecturasListas = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contadorLecturas = 0;
        persistence.GetById<AvailabilitySlot>(slotId).Returns(async _ =>
        {
            var copiaSlot = new AvailabilitySlot
            {
                Id = slotId,
                DoctorId = doctorId,
                Start = slotStart,
                End = slotEnd,
                Status = SlotStatus.AVAILABLE
            };

            if (Interlocked.Increment(ref contadorLecturas) == 2)
                ambasLecturasListas.TrySetResult();

            await ambasLecturasListas.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return copiaSlot;
        });

        // Solo el primer Update pasa; el segundo falla por RowVersion (DbUpdateConcurrencyException)
        var yaReservado = 0;
        persistence.Update(Arg.Any<AvailabilitySlot>()).Returns(call =>
        {
            if (Interlocked.Exchange(ref yaReservado, 1) == 1)
                return Task.FromException<AvailabilitySlot>(
                    new DbUpdateConcurrencyException("Conflicto de concurrencia en AvailabilitySlot"));

            return Task.FromResult(call.Arg<AvailabilitySlot>());
        });

        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        userManager.FindByNameAsync(paciente1.UserName!).Returns(paciente1);
        userManager.FindByNameAsync(paciente2.UserName!).Returns(paciente2);

        var service = new AppointmentService(persistence, userManager);
        var middlewareLogger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();

        async Task<int> EjecutarReservaHttp(ApplicationUser paciente)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, paciente.UserName!)], "TestAuth"));

            var controller = new AppointmentController(
                service,
                new AppointmentCreateRequestValidator(),
                new AppointmentGetByDateQueryValidator(),
                new AppointmentSearchQueryValidator(),
                new AppointmentGetByPatientQueryValidator())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var request = new AppointmentModel.CreateRequest(
                doctorId,
                slotId,
                new AppointmentModel.PatientRequest(paciente.Dni),
                "Control de rutina");

            var middleware = new ExceptionHandlingMiddleware(async ctx =>
            {
                var actionResult = await controller.Create(request);
                if (actionResult is ObjectResult objectResult && objectResult.StatusCode.HasValue)
                    ctx.Response.StatusCode = objectResult.StatusCode.Value;
            }, middlewareLogger);

            await middleware.InvokeAsync(httpContext);
            return httpContext.Response.StatusCode;
        }

        // Act
        var statusCodes = await Task.WhenAll(
            EjecutarReservaHttp(paciente1),
            EjecutarReservaHttp(paciente2));

        // Assert
        Assert.Contains(StatusCodes.Status201Created, statusCodes);
        Assert.Contains(StatusCodes.Status409Conflict, statusCodes);
        await persistence.Received(1).Add(Arg.Any<Appointment>());
    }

    [Fact]
    public async Task AppointmentCancel_CuandoHayConflictoDeConcurrencia_LanzaConflictException()
    {
        // Arrange
        var slot = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            Start = DateTime.UtcNow.AddDays(1),
            End = DateTime.UtcNow.AddDays(1).AddMinutes(30),
            Status = SlotStatus.BOOKED,
            BookedCount = 1
        };
        var patient = new ApplicationUser { Id = "patient-1", UserName = "paciente@email.com", Dni = 40123456 };
        var appointment = new Appointment(slot, patient.Id, "Control de rutina");

        _persistence.GetById<Appointment>(appointment.Id, nameof(Appointment.AvailabilitySlot)).Returns(appointment);
        _userManager.FindByNameAsync(patient.UserName!).Returns(patient);
        _persistence.Update(Arg.Any<AvailabilitySlot>())
            .Returns(Task.FromException<AvailabilitySlot>(new DbUpdateConcurrencyException("Conflicto de concurrencia")));

        // Act
        var act = () => _appointmentTest.Cancel(appointment.Id, patient.UserName!);

        // Assert
        await Assert.ThrowsAsync<ConflictException>(act);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_CuandoRecibeDbUpdateConcurrencyException_Devuelve409()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new DbUpdateConcurrencyException("Conflicto de concurrencia"),
            logger);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);
    }

    private static AvailabilityService CrearServicioDeDisponibilidad(
        IPersistence persistence, IHolidayProvider holidays)
    {
        persistence.GetFiltered<AvailabilityRule>(
                Arg.Any<Expression<Func<AvailabilityRule, bool>>>(), Arg.Any<string[]>())
            .Returns(Enumerable.Empty<AvailabilityRule>());

        persistence.ExecuteInTransaction(Arg.Any<Func<Task<List<AvailabilityModel.Response>>>>())
            .Returns(call => call.Arg<Func<Task<List<AvailabilityModel.Response>>>>()());

        return new AvailabilityService(persistence, holidays);
    }

    private static AvailabilityModel.Request CrearRequestDeDisponibilidad(
        Doctor doctor, params (string Day, string StartTime, string EndTime)[] days) =>
        new()
        {
            DoctorId = doctor.Id.ToString(),
            Days = days.Select(day => new AvailabilityModel.DayRequest
            {
                Day = day.Day,
                StartTime = day.StartTime,
                EndTime = day.EndTime
            })
        };

    private static Doctor CrearDoctor() =>
        new("Dra. Ana López", "MP1234", new Speciality("Clínica", "Clínica médica"));
    [Fact]
    public async Task AppointmentGetByDate_CuandoHayTurnoHuerfanoSinPaciente_NoLanzaKeyNotFoundException()
    {
        // Arrange
        var speciality = new Speciality("Cardiología", "Especialidad del corazón");
        var doctor = new Doctor("Dr. Juan Pérez", "MP1234", speciality);
        var slot = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = doctor.Id,
            Doctor = doctor,
            Start = Clock.FromArgentina(new DateTime(2026, 11, 20, 10, 0, 0)),
            End = Clock.FromArgentina(new DateTime(2026, 11, 20, 10, 30, 0)),
            Status = SlotStatus.BOOKED
        };
        var appointment = new Appointment(slot, "usuario-inexistente", "Control de rutina");

        _persistence.Paginate<Appointment, DateTime>(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<Expression<Func<Appointment, bool>>>(),
                Arg.Any<Expression<Func<Appointment, DateTime>>>(),
                Arg.Any<string[]>())
            .Returns(new Pagination<Appointment>(10, 0, 1, [appointment]));

        _userManager.Users.Returns(new TestAsyncEnumerable<ApplicationUser>(Array.Empty<ApplicationUser>()));

        // Act
        var result = await _appointmentTest.GetByDate(new DateOnly(2026, 11, 20), 10, 0);

        // Assert
        var item = Assert.Single(result.Data);
        Assert.Equal(0, item.Patient.Dni);
        Assert.Equal("Paciente desconocido", item.Patient.FullName);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("abcd", false)]
    [InlineData("abcde", true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void AppointmentCreateRequestValidator_ValidaLongitudDeReason(object input, bool valido)
    {
        var reason = input is int length ? new string('a', length) : (string)input;
        var validator = new AppointmentCreateRequestValidator();
        var request = new AppointmentModel.CreateRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AppointmentModel.PatientRequest(40123456),
            reason);

        var result = validator.Validate(request);

        Assert.Equal(valido, result.IsValid);
    }
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1234567, false)]
    [InlineData(123456, false)]
    [InlineData(1234567, true)]
    [InlineData(12345678, true)]
    [InlineData(123456789, false)]
    public void LoginPatientValidator_ValidaCantidadDeDigitosDelDni(long dni, bool valido)
    {
        var validator = new LoginPatientValidator();
        var request = new LoginPatientModel.Request("paciente@email.com", dni);

        var result = validator.Validate(request);

        Assert.Equal(valido, result.IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(123456, false)]
    [InlineData(1234567, true)]
    [InlineData(12345678, true)]
    [InlineData(123456789, true)]
    [InlineData(1234567890, true)]
    [InlineData(12345678901, false)]
    public void AppointmentCreateRequestValidator_ValidaCantidadDeDigitosDelDni(long dni, bool valido)
    {
        var validator = new AppointmentCreateRequestValidator();
        var request = new AppointmentModel.CreateRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AppointmentModel.PatientRequest(dni),
            "Control de rutina");

        var result = validator.Validate(request);

        Assert.Equal(valido, result.IsValid);
    }
    [Fact]
    public async Task AvailabilityController_Create_Devuelve201SinHeaderLocation()
    {
        var service = Substitute.For<IAvailabilityService>();
        var doctorId = Guid.NewGuid();
        service.Create(Arg.Any<AvailabilityModel.Request>())
            .Returns([new AvailabilityModel.Response { Id = Guid.NewGuid(), DoctorId = doctorId }]);

        var controller = new AvailabilityController(service, new AvailabilityRequestValidator());
        var request = new AvailabilityModel.Request
        {
            DoctorId = doctorId.ToString(),
            Days = [new AvailabilityModel.DayRequest { Day = "LUNES", StartTime = "09:00", EndTime = "12:00" }]
        };

        var result = await controller.Create(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
    }
    [Theory]
    [InlineData(null, false)]
    [InlineData(0L, false)]
    [InlineData(123456L, false)]
    [InlineData(1234567L, true)]
    [InlineData(12345678L, true)]
    [InlineData(123456789L, false)]
    public void AppointmentGetByPatientQueryValidator_ValidaDniObligatorioYEntre7Y8Digitos(long? dni, bool valido)
    {
        var validator = new AppointmentGetByPatientQueryValidator();
        var result = validator.Validate(new AppointmentModel.GetByPatientQuery(dni));

        Assert.Equal(valido, result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(123L)]
    [InlineData(123456789L)]
    public async Task AppointmentController_GetByPatient_CuandoDniFaltaOEsInvalido_LanzaValidationExceptionYNoLlamaAlServicio(long? dni)
    {
        var service = Substitute.For<IAppointmentService>();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "paciente@email.com")], "TestAuth"))
        };
        var controller = new AppointmentController(
            service,
            new AppointmentCreateRequestValidator(),
            new AppointmentGetByDateQueryValidator(),
            new AppointmentSearchQueryValidator(),
            new AppointmentGetByPatientQueryValidator())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        await Assert.ThrowsAsync<ValidationException>(() => controller.GetByPatient(dni));

        await service.DidNotReceive().GetByPatient(Arg.Any<long>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AppointmentService_GetByPatient_CuandoDniNoCoincideConUsuarioAutenticado_LanzaAuthenticationException()
    {
        var patient = new ApplicationUser { Id = "p1", UserName = "paciente@email.com", Dni = 40123456 };
        _userManager.FindByNameAsync("paciente@email.com").Returns(patient);

        var act = () => _appointmentTest.GetByPatient(40999999, "paciente@email.com");

        await Assert.ThrowsAsync<AuthenticationException>(act);
    }

    [Fact]
    public async Task AppointmentService_GetByPatient_DevuelveSoloTurnosBookedActivosSinPaginar()
    {
        var patient = new ApplicationUser { Id = "p1", UserName = "paciente@email.com", Dni = 40123456 };
        var doctor = CrearDoctor();
        var slotFuturo = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = doctor.Id,
            Doctor = doctor,
            Start = Clock.Now.AddDays(1),
            End = Clock.Now.AddDays(1).AddMinutes(30),
            Status = SlotStatus.BOOKED
        };
        var slotPasado = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = doctor.Id,
            Doctor = doctor,
            Start = Clock.Now.AddMinutes(-30),
            End = Clock.Now,
            Status = SlotStatus.BOOKED
        };

        var turnoActivo = new Appointment(slotFuturo, patient.Id, "Control de rutina");
        var turnoCancelado = new Appointment(slotFuturo, patient.Id, "Cancelado");
        turnoCancelado.Cancel();
        var turnoAtendido = new Appointment(slotFuturo, patient.Id, "Atendido");
        turnoAtendido.MarkAttended();
        var turnoPasado = new Appointment(slotPasado, patient.Id, "Turno viejo");
        var turnoOtroPaciente = new Appointment(slotFuturo, "otro-paciente", "Otro");

        _userManager.FindByNameAsync("paciente@email.com").Returns(patient);

        Expression<Func<Appointment, bool>>? filtroCapturado = null;
        _persistence.GetFiltered<Appointment>(
                Arg.Any<Expression<Func<Appointment, bool>>>(),
                Arg.Any<string[]>())
            .Returns(call =>
            {
                filtroCapturado = call.Arg<Expression<Func<Appointment, bool>>>();
                return Task.FromResult<IEnumerable<Appointment>?>(new[] { turnoActivo });
            });

        var result = (await _appointmentTest.GetByPatient(patient.Dni, "paciente@email.com")).ToList();

        var item = Assert.Single(result);
        Assert.Equal(turnoActivo.Id, item.Id);
        Assert.Equal("BOOKED", item.Status);

        var predicate = filtroCapturado!.Compile();
        Assert.True(predicate(turnoActivo));
        Assert.False(predicate(turnoCancelado));
        Assert.False(predicate(turnoAtendido));
        Assert.False(predicate(turnoPasado));
        Assert.False(predicate(turnoOtroPaciente));
    }
    [Fact]
    public async Task LoginPatient_CuandoUsuarioEstaEliminado_LanzaAuthenticationExceptionYLogueaWarningSinDniCompleto()
    {
        var (service, userManager, logger) = CrearAuthenticationService();
        var user = new ApplicationUser
        {
            UserName = "paciente@email.com",
            Email = "paciente@email.com",
            Dni = 40123456,
            EmailConfirmed = true,
            Deleted = true
        };
        userManager.FindByEmailAsync("paciente@email.com").Returns(user);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginPatient(new LoginPatientModel.Request("paciente@email.com", 40123456)));

        Assert.Equal("AUTHENTICATION_FAILED", ex.Error.ErrorCode);
        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.DoesNotContain("40123456", log.Message);
    }

    [Fact]
    public async Task LoginPatient_CuandoEmailNoEstaConfirmado_LanzaAuthenticationExceptionYLogueaWarningSinDniCompleto()
    {
        var (service, userManager, logger) = CrearAuthenticationService();
        var user = new ApplicationUser
        {
            UserName = "paciente@email.com",
            Email = "paciente@email.com",
            Dni = 40123456,
            EmailConfirmed = false,
            Deleted = false
        };
        userManager.FindByEmailAsync("paciente@email.com").Returns(user);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginPatient(new LoginPatientModel.Request("paciente@email.com", 40123456)));

        Assert.Equal("AUTHENTICATION_FAILED", ex.Error.ErrorCode);
        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.DoesNotContain("40123456", log.Message);
    }

    [Fact]
    public async Task LoginPatient_CuandoEmailExisteConOtroDni_LanzaAuthenticationExceptionYLogueaWarningSinDniCompleto()
    {
        var (service, userManager, logger) = CrearAuthenticationService();
        var user = new ApplicationUser
        {
            UserName = "paciente@email.com",
            Email = "paciente@email.com",
            Dni = 40123456,
            EmailConfirmed = true,
            Deleted = false
        };
        userManager.FindByEmailAsync("paciente@email.com").Returns(user);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginPatient(new LoginPatientModel.Request("paciente@email.com", 40999999)));

        Assert.Equal("AUTHENTICATION_FAILED", ex.Error.ErrorCode);
        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.DoesNotContain("40999999", log.Message);
        Assert.DoesNotContain("40123456", log.Message);
    }

    [Fact]
    public async Task LoginPatient_CuandoDniPerteneceAOtroEmail_LanzaConflictExceptionYLogueaWarningSinDniCompleto()
    {
        var (service, userManager, logger) = CrearAuthenticationService();
        var otroPaciente = new ApplicationUser
        {
            UserName = "otro@email.com",
            Email = "otro@email.com",
            Dni = 40123456,
            EmailConfirmed = true,
            Deleted = false
        };
        userManager.FindByEmailAsync("nuevo@email.com").Returns((ApplicationUser?)null);
        userManager.Users.Returns(new TestAsyncEnumerable<ApplicationUser>([otroPaciente]));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.LoginPatient(new LoginPatientModel.Request("nuevo@email.com", 40123456)));

        Assert.Equal("PATIENT_DNI_CONFLICT", ex.Error.ErrorCode);
        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.DoesNotContain("40123456", log.Message);
        await userManager.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task LoginPatient_CuandoUsuarioNoTieneRolPaciente_LanzaAuthenticationExceptionYNoActualizaNombre()
    {
        var (service, userManager, logger) = CrearAuthenticationService();
        var admin = new ApplicationUser
        {
            UserName = "admin@email.com",
            Email = "admin@email.com",
            Dni = 99999999,
            EmailConfirmed = true,
            Deleted = false
        };
        userManager.FindByEmailAsync("admin@email.com").Returns(admin);
        userManager.IsInRoleAsync(admin, Roles.Patient).Returns(false);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginPatient(new LoginPatientModel.Request("admin@email.com", 99999999, "Nombre", "Nuevo")));

        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.DoesNotContain("99999999", log.Message);
        await userManager.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task LoginPatient_CuandoAutoRegistra_SeteaEmailConfirmedTrueYDeletedFalse()
    {
        var (service, userManager, logger) = CrearAuthenticationService();
        userManager.FindByEmailAsync("nuevo@email.com").Returns((ApplicationUser?)null);
        userManager.Users.Returns(new TestAsyncEnumerable<ApplicationUser>(Array.Empty<ApplicationUser>()));

        ApplicationUser? creado = null;
        userManager.CreateAsync(Arg.Do<ApplicationUser>(u => creado = u))
            .Returns(IdentityResult.Failed());

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginPatient(new LoginPatientModel.Request("nuevo@email.com", 40123456, "  Ana  ", "  Gómez  ")));

        Assert.NotNull(creado);
        Assert.True(creado!.EmailConfirmed);
        Assert.False(creado.Deleted);
        Assert.Equal(40123456, creado.Dni);
        Assert.Equal("Ana", creado.FirstName);
        Assert.Equal("Gómez", creado.LastName);
        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.DoesNotContain("40123456", log.Message);
    }
    [Fact]
    public void ErrorCodes_ContieneLosCodigosUnificadosEnResx()
    {
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.DOCTOR_INACTIVE));
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.AVAILABILITY_OVERLAP));
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.RATE_LIMIT_EXCEEDED));
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.DOCTOR_LICENSE_CONFLICT));
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.PATIENT_DNI_CONFLICT));
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.UNHANDLED_ERROR));
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_CuandoOcurreErrorNoControlado_Devuelve500ConFormatoUnificado()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Detalle interno sensible"),
            logger);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal("application/json", httpContext.Response.ContentType);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        Assert.DoesNotContain("Detalle interno sensible", body);

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal(nameof(ErrorCodes.UNHANDLED_ERROR), root.GetProperty("errorCode").GetString());
        Assert.Equal(ErrorCodes.UNHANDLED_ERROR, root.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("details").ValueKind);
        Assert.Equal(0, root.GetProperty("details").GetArrayLength());
    }
    private static (AuthenticationService Service, UserManager<ApplicationUser> UserManager, TestLogger<AuthenticationService> Logger)
        CrearAuthenticationService()
    {
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        var signInService = Substitute.For<ISignInService>();
        var roleManager = Substitute.For<RoleManager<IdentityRole>>(
            Substitute.For<IRoleStore<IdentityRole>>(), null, null, null, null);
        var logger = new TestLogger<AuthenticationService>();

        var service = new AuthenticationService(userManager, signInService, roleManager, null!, logger);
        return (service, userManager, logger);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
    private sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    private sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);
        public object? Execute(Expression expression) => inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
                .MakeGenericMethod(expectedResultType)
                .Invoke(inner, [expression]);

            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, [executionResult])!;
        }
    }

    private sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());
        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}