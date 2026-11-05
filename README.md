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
- FullCalendar Standard for daily/weekly availability (no Premium Scheduler)

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
- Node.js 24/npm when rebuilding Tailwind or FullCalendar assets
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

Equipment images are uploaded to a **private** Supabase Storage bucket when it is configured, and displayed through short-lived signed URLs. Create a private bucket (`equipment-images` by default) and store the project URL and `service_role` key locally with User Secrets. The register/edit forms always show the drag/drop upload control; it stays disabled (no uploads attempted) until Supabase is configured. Never commit the `service_role` key:

```powershell
dotnet user-secrets --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj set "Supabase:Url" "https://YOUR-PROJECT.supabase.co"
dotnet user-secrets --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj set "Supabase:ServiceRoleKey" "YOUR_SERVICE_ROLE_KEY"
```

Production connection strings, Brevo credentials, and Supabase credentials belong in protected IIS, Azure, or environment configuration. Use `Brevo__ApiKey`, `Brevo__SenderEmail`, `Supabase__Url`, `Supabase__Bucket`, and `Supabase__ServiceRoleKey` as production environment variable names.

## Database Setup

The canonical schema and feature migrations are committed under `ASI.Basecode.Data/Migrations`. Run this command after the first database setup and whenever you pull a change that adds a migration; it applies all pending migrations:

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

The user-administration migration adds audit and permission-seed tables, and makes normalized Identity email unique. It checks for existing duplicate email addresses before changing the index; if any are found, the migration stops without changing the schema, and those duplicates must be resolved first.

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

It ensures the three roles and eight permissions exist. Default role grants are initialized once and subsequent seed runs preserve permission changes made through Users & Roles. An existing account configured as the bootstrap administrator is not silently re-promoted if its role was removed. The command creates the initial administrator only when these user-secret values are present:

```powershell
dotnet user-secrets set "SeedAdmin:Email" "admin@example.edu" --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
dotnet user-secrets set "SeedAdmin:UserCode" "ADMIN-001" --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
dotnet user-secrets set "SeedAdmin:Password" "use-a-strong-private-password" --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

Do not place the administrator password in `appsettings.json`.

## Tailwind CSS

Tailwind scans MVC Razor Views and JavaScript. The source stylesheet is `ASI.Basecode.WebApp/wwwroot/css/tailwind.src.css`; the compiled, minified `wwwroot/css/app.css` is checked in so the app can run without Node.js.

From `ASI.Basecode.WebApp`, install the locked dependencies once, then run the watcher in a separate terminal while styling:

```powershell
npm ci
npm run css:dev
```

Before submitting calendar, JavaScript, CSS, or Razor class changes, prepare the checked-in frontend assets:

```powershell
npm run assets:build
```

This copies the pinned FullCalendar bundle and its MIT license into `wwwroot/lib/fullcalendar`, then builds the minified Tailwind stylesheet. These generated assets are checked in, so teammates can run the application without Node.js. Bootstrap and the old template CSS have been removed; use Tailwind utility classes and the shared `.input` / `.btn-primary` component classes.

## Preview the dashboards with sample data

The development-only `--seed-demo` command creates idempotent dashboard sample rows and one account (`DEMO-001`) that has Borrower, Custodian, and Administrator roles. Set its password as a local secret first:

```powershell
dotnet user-secrets set "SeedDemo:Password" "choose-a-strong-local-password" --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj -- --seed-demo
```

Sign in with user code `DEMO-001` and the password you selected. Use the development-only role switcher to preview each dashboard. Do not use demo seed data in production.

## Run the Application

```powershell
dotnet restore
dotnet build
dotnet test .\ASI.Basecode.sln
dotnet run --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

For active UI work, run `npm ci` once and `npm run css:dev` in a separate terminal from `ASI.Basecode.WebApp`. The checked-in `app.css` is already available when running the site without Node.js.

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
- Tailwind CSS build, shared responsive shell, and Tailwind-styled authentication pages
- Permission-protected daily/weekly availability calendar with category, item, and location filters
- Borrower equipment catalog and reservation request, status, and cancellation pages
- Searchable borrowing history with filters and CSV export
- Administrator user management with single and atomic bulk CSV account creation, role assignment, permission matrix, and account activation controls (see [CSV import instructions](doc/USER_CSV_IMPORT.md))
- SQL-backed borrower, custodian, and administrator dashboard views
- Development-only role switcher and demo dashboard data seeder

Still in progress:

- Dashboard polish and acceptance against the approved screen-size and role test matrix
- Borrower profile administration and custodian approval, release, and return workflows
