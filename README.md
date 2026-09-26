# Campus Equipment Borrowing & Reservation System

An ASP.NET Core MVC application for managing school equipment reservations, approvals, releases, returns, borrower eligibility, availability, and operational reports.

## Target Architecture

- ASP.NET Core MVC on .NET 9
- Controllers with Razor Views
- ASP.NET Core Identity for authentication and roles
- Policy-based permissions stored through `Permission` and `RolePermission`
- Microsoft SQL Server with Entity Framework Core
- SQL Server Management Studio (SSMS) as an optional administration tool
- Tailwind CSS with minimal custom CSS
- Brevo transactional email API for password-reset OTP delivery
- FullCalendar for daily/weekly availability

The canonical database design is documented in [the ERD and data dictionary](doc/Campus_Equipment_Borrowing_ERD_Data_Dictionary.md).

## Version 1 Database Rules

- Each reservation contains exactly one equipment item.
- A user who can borrow has one `BorrowerProfile`.
- ASP.NET Core Identity owns passwords, reset tokens, roles, and security metadata.
- Business tables must not duplicate Identity password fields.
- Entity Framework Core migrations are the authoritative schema history.
- SSMS may inspect and administer SQL Server, but manual SSMS changes must not replace migrations.

## Project Structure

- `ASI.Basecode.Data` — EF Core context, entities, repositories, and database-only operations.
- `ASI.Basecode.Services` — business rules and service-layer workflows.
- `ASI.Basecode.Resources` — labels, messages, and shared resources.
- `ASI.Basecode.WebApp` — MVC controllers, Razor Views, authentication configuration, and static assets.
- `doc` — functional requirements, workflows, canonical ERD, milestones, risks, and technology decisions.

## Local Prerequisites

- .NET 9 SDK
- Microsoft SQL Server (Developer or Express is sufficient for local development)
- SSMS, Azure Data Studio, or another SQL client (optional)
- Node.js/npm for the Tailwind CSS build
- Visual Studio 2022 or another compatible editor

The repository includes a `global.json` pin and a local `dotnet-ef` tool manifest. Restore the tool after cloning:

```powershell
dotnet tool restore
```

## Configuration

Keep secrets out of committed JSON files. Use .NET user secrets for local development:

```powershell
dotnet user-secrets --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=GearanteeDev;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj set "Brevo:ApiKey" "YOUR_ACTUAL_BREVO_API_KEY"
dotnet user-secrets --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj set "Brevo:SenderEmail" "your-verified-email@example.com"
```

Production connection strings and Brevo credentials belong in protected IIS, Azure, or environment configuration. Use `Brevo__ApiKey` and `Brevo__SenderEmail` as production environment variable names.

## Database Setup

The initial canonical migration is committed under `ASI.Basecode.Data/Migrations`. Apply it with this one-line command:

```powershell
dotnet ef database update --project .\ASI.Basecode.Data\ASI.Basecode.Data.csproj --startup-project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj --context AsiBasecodeDBContext
```

In Visual Studio Package Manager Console, set `ASI.Basecode.Data` as the Default project and `ASI.Basecode.WebApp` as the Startup Project, then run:

```powershell
Update-Database -Project ASI.Basecode.Data -StartupProject ASI.Basecode.WebApp -Context AsiBasecodeDBContext
```

For future model changes, create and apply a named migration:

```powershell
dotnet ef migrations add AddYourFeatureName --project .\ASI.Basecode.Data\ASI.Basecode.Data.csproj --startup-project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj --context AsiBasecodeDBContext
dotnet ef database update --project .\ASI.Basecode.Data\ASI.Basecode.Data.csproj --startup-project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj --context AsiBasecodeDBContext
```

Review generated migrations before applying them. Back up production SQL Server databases and test restoration before deployment.

The default non-secret development connection targets:

```text
Server=(localdb)\MSSQLLocalDB;Database=GearanteeDev
```

Override it through user secrets when using another SQL Server instance.

## Seed Roles, Permissions, and an Administrator

The seed command is idempotent:

```powershell
dotnet run --no-build `
  --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj `
  -- --seed
```

It always seeds the three roles, eight permissions, and documented role-permission assignments. It creates an administrator only when these user-secret values are present:

```powershell
dotnet user-secrets set "SeedAdmin:Email" "admin@example.edu" --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
dotnet user-secrets set "SeedAdmin:UserCode" "ADMIN-001" --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
dotnet user-secrets set "SeedAdmin:Password" "use-a-strong-private-password" --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

Do not place the administrator password in `appsettings.json`.

## Tailwind CSS

The frontend migration must configure Tailwind to scan MVC Razor Views and JavaScript, compile its source stylesheet to `ASI.Basecode.WebApp/wwwroot/css/app.css`, watch files during development, and generate a minified production build.

Bootstrap references and Bootstrap-only view classes should be removed only after the corresponding views have been converted.

## Run the Application

```powershell
dotnet restore
dotnet build
dotnet run --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

Run the Tailwind development watcher in a separate terminal after its npm scripts have been added.

## Current Implementation Status

Implemented:

- SQL Server/LocalDB connection through EF Core
- Canonical Identity and business entities
- Initial SQL Server migration
- ASP.NET Core Identity registration, login, logout, lockout, and active-account checks
- Role-permission claims and authorization policies
- Idempotent role/permission seeding with optional secure administrator creation
- Database health endpoint at `/health/database`
- Password-reset OTP flow with Brevo email delivery

Still planned:

- Tailwind CSS migration
- Role-specific dashboards and the equipment/reservation workflows
