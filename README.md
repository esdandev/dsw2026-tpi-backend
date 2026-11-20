# Trabajo Práctico Integrador
## Desarrollo de Software 2026
## Comisión 3k3
## Integrantes - Legajos

- Escalante Daniel Alejo Baltazar - 60554
- Lisandro Ruiz - 58399
- Mauro Benjamín Ibarra - 60698
- Gutierrez, Maia Federica - 60348

## Cómo ejecutar el proyecto localmente

1. Requisitos: .NET 10 SDK, SQL Server (o LocalDB) y un cliente de API (Swagger incluido).
2. Cloná el repositorio y pará en la carpeta raíz.
3. Configurá los secretos locales con `dotnet user-secrets` (quedan fuera del repositorio):
   dotnet user-secrets --project Dsw2026Tpi.Api set "ConnectionStrings:DefaultConnection" "<cadena de conexión>"
   dotnet user-secrets --project Dsw2026Tpi.Api set "Jwt:Key" "<clave aleatoria de al menos 32 caracteres>"
   dotnet user-secrets --project Dsw2026Tpi.Api set "Seed:Admin:Email" "admin@utn.com"
   dotnet user-secrets --project Dsw2026Tpi.Api set "Seed:Admin:Password" "Admin123!"
4. Aplicá las migraciones:
   dotnet ef database update --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api --context Dsw2026TpiDbContext
   dotnet ef database update --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api --context AuthenticationDbContext
5. Corré la API:
   dotnet run --project Dsw2026Tpi.Api
6. Abrí `https://localhost:7075/swagger` (o el puerto que indique la consola).
7. Al iniciar sobre una base vacía se crea el administrador configurado en `Seed:Admin:Email` / `Seed:Admin:Password`; si falta alguno, el seed se omite con un warning.

En producción, partí de `appsettings.Production.json.example` (valores vacíos): copialo a `appsettings.Production.json` (ignorado por git) o definí las variables de entorno `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Seed__Admin__Email` y `Seed__Admin__Password`.

## Endpoints implementados

### Autenticación (`/api/auth`)
- `POST /api/auth/admin/login` — login de administrador (email + password).
- `POST /api/auth/patient/login` — login de paciente (email + dni, opcionalmente `firstName` y `lastName`). Si el paciente no existe, se registra automáticamente.

### Especialidades (`/api/specialties`) — requiere token
- `GET /api/specialties` — listado paginado, filtrable por `name`.
- `POST /api/specialties` — alta (admin).
- `PUT /api/specialties/{id}` — modificación (admin).
- `DELETE /api/specialties/{id}` — baja lógica (admin).

### Médicos (`/api/doctors`) — requiere token
- `GET /api/doctors` — listado paginado, filtrable por `name` y `specialtyId`.
- `GET /api/doctors/{id}/availabilities` — reglas de disponibilidad del mes actual.
- `GET /api/doctors/{id}/slots?from=&to=` — turnos disponibles del médico en el rango de fechas.
- `POST /api/doctors` — alta (admin). Body: `{ "name", "licenseNumber", "specialtyId" }`; `licenseNumber` es obligatorio y único.
- `PUT /api/doctors/{id}` — modificación (admin), mismo body que el alta.
- `DELETE /api/doctors/{id}` — baja lógica (admin).

### Disponibilidades (`/api/availabilities`) — admin
- `POST /api/availabilities` — carga la disponibilidad semanal de un médico y genera los turnos de 30 minutos del resto del mes en los días indicados. No genera turnos vencidos ni en feriados. Body: `{ "doctorId", "days": [{ "day", "startTime", "endTime" }] }`, con `startTime`/`endTime` en `HH:mm` (inicio menor que fin) y `day` aceptando el nombre con o sin tilde (`MIÉRCOLES`/`MIERCOLES`, `SÁBADO`/`SABADO`).
- `PUT /api/availabilities` — mismo body; reemplaza la disponibilidad del mes y regenera los turnos futuros no reservados, conservando los turnos ya reservados.
- Los horarios que se solapan con otra regla del mismo médico en el mes devuelven `409 AVAILABILITY_OVERLAP`.

### Turnos (`/api/appointments`)
- `POST /api/appointments` — reserva un turno (paciente). Body: `{ "doctorId", "availabilitySlotId", "patient": { "dni" }, "reason" }`, con `availabilitySlotId` tomado de `GET /api/doctors/{id}/slots`.
- `DELETE /api/appointments/{id}` — cancela un turno propio (paciente).
- `GET /api/appointments/patient?dni=` — turnos activos y futuros del paciente autenticado.
- `GET /api/appointments?date=` — turnos de un día (admin), con `pageSize` y `pageIndex`.
- `GET /api/appointments/search?...` — búsqueda combinada por especialidad, médico, dni y fecha (admin), con `pageSize` y `pageIndex`.

## Notas
- Todos los endpoints (salvo login) requieren `Authorization: Bearer <token>`.
- Los listados paginados (`/api/specialties`, `/api/doctors`, `/api/appointments`, `/api/appointments/search`) devuelven `{ pageSize, pageIndex, total, data }`; `total` cuenta los registros activos que cumplen el filtro, no los de la página.
- Los turnos vistos por un administrador muestran `patient.fullName` con el nombre y apellido cargados en el login. Si el paciente no los tiene (cuentas creadas antes de agregar esos campos), se muestra su email.
- Los errores siguen el formato `{ errorCode, message, details? }`, con `details[].field` en camelCase (ej. `pageSize`, `licenseNumber`).
