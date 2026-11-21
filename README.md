# Trabajo Práctico Integrador
## Desarrollo de Software 2026
## Comisión 3k3
## Integrantes - Legajos

- Escalante Daniel Alejo Baltazar - 60554
- Lisandro Ruiz - 58399
- Mauro Benjamín Ibarra - 60698
- Gutierrez, Maia Federica - 60348

## Stack

- .NET 10 (`net10.0`), ASP.NET Core y Entity Framework Core 10.
- SQL Server (LocalDB en desarrollo).
- ASP.NET Core Identity + JWT, con roles `Administrador` y `Paciente`.
- FluentValidation, Serilog y el middleware de rate limiting de ASP.NET Core.
- Tests: xUnit + NSubstitute (`Dsw2026Tpi.Test`).

Solución en capas: `Api` (controllers, middlewares, configuración), `Application` (servicios, DTOs y validadores), `Domain` (entidades y contratos), `Data` (DbContext, configuraciones EF, migraciones e Identity), `CrossCutting` (`Clock`, excepciones, roles, códigos de error) y `Test`.

## Cómo ejecutar el proyecto localmente

1. Requisitos: .NET 10 SDK, SQL Server (o LocalDB) y un cliente de API (Swagger incluido).
2. Cloná el repositorio y pará en la carpeta raíz.
3. Verificá la herramienta de EF Core 10 (`dotnet ef --version`); si no está instalada, `dotnet tool install --global dotnet-ef`.
4. Configurá los secretos locales con `dotnet user-secrets` (quedan fuera del repositorio):
   ```
   dotnet user-secrets --project Dsw2026Tpi.Api set "ConnectionStrings:DefaultConnection" "<cadena de conexión>"
   dotnet user-secrets --project Dsw2026Tpi.Api set "Jwt:Key" "<clave aleatoria de al menos 32 caracteres>"
   dotnet user-secrets --project Dsw2026Tpi.Api set "Seed:Admin:Email" "admin@utn.com"
   dotnet user-secrets --project Dsw2026Tpi.Api set "Seed:Admin:Password" "Admin123!"
   ```
5. Aplicá las migraciones:
   ```
   dotnet ef database update --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api --context Dsw2026TpiDbContext
   dotnet ef database update --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api --context AuthenticationDbContext
   ```
6. Corré la API:
   `dotnet run --project Dsw2026Tpi.Api`
   Queda en `http://localhost:5278` (y `https://localhost:7075` con el perfil `https`).
7. Abrí `http://localhost:5278/swagger` (o el puerto que indique la consola).
8. Al iniciar sobre una base vacía se crea el administrador configurado en `Seed:Admin:Email` / `Seed:Admin:Password`; si falta alguno, el seed se omite con un warning.

Comandos útiles:

- Build: `dotnet build`
- Tests: `dotnet test`

### Secretos y configuración

- La cadena de conexión, la clave JWT y el admin inicial **no** se versionan: van en `dotnet user-secrets` o en variables de entorno.
- La clave JWT debe tener 32 caracteres o más; si se cambia, los tokens emitidos antes dejan de ser válidos.
- En producción, partí de `appsettings.Production.json.example` (valores vacíos): copialo a `appsettings.Production.json` (ignorado por git) o definí las variables de entorno `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Seed__Admin__Email` y `Seed__Admin__Password`.

## Roles y permisos

Todos los endpoints requieren `Authorization: Bearer <token>`, salvo los dos logins.

- **Administrador** (`ADMINISTRADOR`): gestiona especialidades, médicos y disponibilidades; consulta turnos del día y realiza búsquedas avanzadas.
- **Paciente** (`PACIENTE`): consulta turnos disponibles, reserva, cancela sus turnos y consulta sus turnos activos. No requiere registro previo: en el primer login se crea la cuenta y se le asigna el rol.

El administrador inicial se crea solo por seed; no existe registro público de administradores.

## Rate limiting

Configurado en la sección `RateLimiting` de `appsettings.json` (no está codificado en los controllers):

| Política | Límite | Partición |
|---|---|---|
| `admin-login` | 5 solicitudes/minuto | IP |
| `patient-login` | 10 solicitudes/minuto | IP |
| `appointment-create` | 5 solicitudes/minuto | paciente autenticado |
| `general` | 100 solicitudes/minuto | usuario o IP |

Las solicitudes rechazadas devuelven `429 Too Many Requests` con el formato de error habitual (`RATE_LIMIT_EXCEEDED`), se registran en el log y no se encolan.

## Endpoints implementados

### Autenticación (`/api/auth`)
- `POST /api/auth/admin/login` — login de administrador (email + password). Devuelve `{ token, role }`.
- `POST /api/auth/patient/login` — login de paciente (email + dni, opcionalmente `firstName` y `lastName`). Si el paciente no existe, se registra automáticamente.
- `POST /api/auth/logout` — revoca el token actual (requiere token). Devuelve `204 No Content`.

### Especialidades (`/api/specialties`) — requiere token
- `GET /api/specialties` — listado paginado, filtrable por `name`.
- `POST /api/specialties` — alta (admin).
- `PUT /api/specialties/{id}` — modificación (admin).
- `DELETE /api/specialties/{id}` — baja lógica (admin). Devuelve `409 SPECIALITY_IN_USE` si tiene médicos activos.

### Médicos (`/api/doctors`) — requiere token
- `GET /api/doctors` — listado paginado, filtrable por `name` y `specialtyId`.
- `GET /api/doctors/{id}/availabilities` — reglas de disponibilidad del mes actual: `[{ id, day, startTime, endTime }]` con horas `HH:mm`.
- `GET /api/doctors/{id}/slots?from=&to=` — turnos disponibles del médico en el rango de fechas (máximo 31 días).
- `POST /api/doctors` — alta (admin). Body: `{ "name", "licenseNumber", "specialtyId" }`; `licenseNumber` es obligatorio y único.
- `PUT /api/doctors/{id}` — modificación (admin), mismo body que el alta.
- `DELETE /api/doctors/{id}` — baja lógica (admin). Devuelve `409 DOCTOR_HAS_FUTURE_APPOINTMENTS` si tiene turnos reservados futuros.

### Disponibilidades (`/api/availabilities`) — admin
- `POST /api/availabilities` — carga la disponibilidad semanal de un médico y genera los turnos de 30 minutos del resto del mes en los días indicados. No genera turnos vencidos ni en feriados. Body: `{ "doctorId", "days": [{ "day", "startTime", "endTime" }] }`, con `startTime`/`endTime` en `HH:mm` (inicio menor que fin) y `day` aceptando el nombre con o sin tilde (`MIÉRCOLES`/`MIERCOLES`, `SÁBADO`/`SABADO`).
- `PUT /api/availabilities` — mismo body; reemplaza la disponibilidad del mes y regenera los turnos futuros no reservados, conservando los turnos ya reservados.
- Los horarios que se solapan con otra regla del mismo médico en el mes devuelven `409 AVAILABILITY_OVERLAP`.
- Los feriados se leen de `Dsw2026Tpi.Data/Sources/holidays.json`.

### Turnos (`/api/appointments`)
- `POST /api/appointments` — reserva un turno (paciente). Body: `{ "doctorId", "availabilitySlotId", "patient": { "dni" }, "reason" }`, con `availabilitySlotId` tomado de `GET /api/doctors/{id}/slots`.
- `DELETE /api/appointments/{id}` — cancela un turno propio (paciente). Solo si está `BOOKED` y el horario no comenzó; en caso contrario `409 APPOINTMENT_CONFLICT`.
- `PUT /api/appointments/{id}/status` — marca un turno como `ATTENDED` o `NO_SHOW` (admin). Body: `{ "status": "ATTENDED" }`. Solo turnos `BOOKED` que ya comenzaron; en caso contrario `409 APPOINTMENT_CONFLICT`.
- `GET /api/appointments/patient?dni=` — turnos activos y futuros del paciente autenticado. `401` si el DNI no coincide con el token.
- `GET /api/appointments?date=` — turnos de un día (admin), con `pageSize` y `pageIndex`.
- `GET /api/appointments/search?...` — búsqueda combinada por especialidad, médico, dni y fecha (admin), con `pageSize` y `pageIndex`.

## Contrato de errores

Todas las respuestas de error siguen el formato:

```json
{
  "errorCode": "APPOINTMENT_CONFLICT",
  "message": "Slot already booked",
  "details": [
    { "field": "dateTime", "issue": "slot_inavailable" }
  ]
}
```

`details` es optativo y `field` va en camelCase. Los errores de servidor devuelven `500` con `UNHANDLED_ERROR`.

Códigos principales:

| HTTP | Códigos |
|---|---|
| 400 | `VALIDATION_ERROR` |
| 401 | `AUTHENTICATION_FAILED` |
| 403 | `AUTHORIZATION_FAILED` |
| 404 | `ENTITY_NOTFOUND` |
| 409 | `PATIENT_DNI_CONFLICT`, `SPECIALITY_NAME_CONFLICT`, `SPECIALITY_IN_USE`, `DOCTOR_LICENSE_CONFLICT`, `DOCTOR_HAS_FUTURE_APPOINTMENTS`, `DOCTOR_INACTIVE`, `AVAILABILITY_OVERLAP`, `AVAILABILITY_SLOT_CONFLICT`, `APPOINTMENT_SLOT_CONFLICT`, `APPOINTMENT_CONFLICT`, `DATA_INTEGRITY_CONFLICT` |
| 429 | `RATE_LIMIT_EXCEEDED` |
| 500 | `UNHANDLED_ERROR` |

## Decisiones de diseño

- **Paginación por servidor:** los listados (`/api/specialties`, `/api/doctors`, `/api/appointments`, `/api/appointments/search`) devuelven `{ pageSize, pageIndex, total, data }`; `total` cuenta los registros activos que cumplen el filtro, no los de la página.
- **Ventana mensual:** la disponibilidad se genera para el resto del mes calendario en curso (el ER mensual y el módulo de disponibilidades mandan sobre la "semana siguiente" de CU02).
- **`availabilitySlotId`:** el contrato de reserva se mantiene igual al enunciado; el id del slot se obtiene de `GET /api/doctors/{id}/slots`.
- **Endpoint extra de slots:** `GET /api/doctors/{id}/slots` no está en el PDF, pero el frontend lo necesita para mostrar los horarios reservables; no reemplaza a `availabilities`.
- **`licenseNumber`:** obligatorio, con máximo 50 caracteres y único (índice único en base).
- **Baja lógica:** especialidades y médicos se marcan `deleted`; no se eliminan físicamente y no se listan.
- **Horas:** se persisten en UTC y se devuelven en UTC; las horas de disponibilidad viajan como `HH:mm` en hora de Argentina (`America/Argentina/Buenos_Aires`).
- **Concurrencia:** la reserva y la cancelación se ejecutan en una transacción; dos reservas simultáneas del mismo slot devuelven `409`, no `500`.

## Seguridad

- CORS configurable en `Cors:AllowedOrigins` (en desarrollo permite `http://localhost:5173`, entre otros). Métodos `GET/POST/PUT/DELETE` y headers `Authorization` y `Content-Type`.
- Headers de seguridad en todas las respuestas: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` y `Permissions-Policy`. El `Content-Security-Policy` se agrega solo en Production (Swagger usa estilos y scripts inline).
- `ForwardedHeaders` deshabilitado por defecto; si se habilita, solo confía en los proxies configurados en `ForwardedHeaders:KnownProxies`.
- Los intentos fallidos de login de administrador se registran como `Warning` y la cuenta se bloquea a los 5 intentos durante 15 minutos.
- Los tokens son revocables: `POST /api/auth/logout` invalida el `jti` hasta su expiración.

## Notas

- Todos los endpoints (salvo login) requieren `Authorization: Bearer <token>`.
- `/health-check` requiere token y está sujeto a la política general de rate limiting.
- Los listados paginados devuelven `total` calculado sobre los registros activos que cumplen el filtro.
- Los turnos vistos por un administrador muestran `patient.fullName` con el nombre y apellido cargados en el login. Si el paciente no los tiene (cuentas creadas antes de agregar esos campos), se muestra su email.
- Los errores siguen el formato `{ errorCode, message, details? }`, con `details[].field` en camelCase (ej. `pageSize`, `licenseNumber`).
