# How to Run Gearantee Locally

## Prerequisites

- .NET 9 SDK/runtime for Windows x64 (`net9.0` is the project target; .NET 10 alone is not sufficient to run it)
- SQL Server Express LocalDB
- SQL Server Management Studio (optional, for database administration)

The current application uses **SQL Server LocalDB**, not PostgreSQL.

## 1. Start LocalDB

Open PowerShell and run:

```powershell
sqllocaldb start MSSQLLocalDB
```

If the instance does not exist, create it first:

```powershell
sqllocaldb create MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

## 2. Create the development database

Connect in SQL Server Management Studio using:

```text
Server name: (localdb)\MSSQLLocalDB
Authentication: Windows Authentication
Database: <default>
```

Run this query once:

```sql
IF DB_ID(N'GearanteeDev') IS NULL
BEGIN
    CREATE DATABASE [GearanteeDev];
END
```

The development connection string is stored in:

```text
ASI.Basecode.WebApp\appsettings.Development.json
```

It points to:

```text
Server=(localdb)\MSSQLLocalDB;Database=GearanteeDev
```

## 3. Apply the EF Core database schema

The repository contains the canonical schema and feature migrations. Creating `GearanteeDev` only creates an empty database, so apply all pending migrations to create/update the Identity and application tables.

If you cloned the repository, restore the local EF CLI tool once before running the commands:

```powershell
dotnet tool restore
```

From the repository root, run this as one command:

```powershell
dotnet ef database update --project ".\ASI.Basecode.Data\ASI.Basecode.Data.csproj" --startup-project ".\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --context AsiBasecodeDBContext
```

The same operation in Visual Studio Package Manager Console is:

```powershell
Update-Database -Project ASI.Basecode.Data -StartupProject ASI.Basecode.WebApp -Context AsiBasecodeDBContext
```

Set `ASI.Basecode.WebApp` as the Startup Project and `ASI.Basecode.Data` as the Package Manager Console Default project. Keep all parameters on the same command line.

When you add or change an EF Core model, create a named migration and then apply it:

```powershell
dotnet ef migrations add AddYourFeatureName --project ".\ASI.Basecode.Data\ASI.Basecode.Data.csproj" --startup-project ".\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --context AsiBasecodeDBContext
dotnet ef database update --project ".\ASI.Basecode.Data\ASI.Basecode.Data.csproj" --startup-project ".\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --context AsiBasecodeDBContext
```

Do not create another initial migration when the existing migration has already been applied.

### Existing databases: schema normalization (October 2026 refactor)

If your local `GearanteeDev` was created before the backend architecture refactor, run the command from section 3 once after pulling. The `NormalizeUserAndBorrowerSchema` migration:

- Drops the `BorrowerProfile.SchoolId` column (borrower identity now lives only in `AspNetUsers.UserName`, exposed as `IdNumber`)
- Drops the unused `AspNetUserLogins` table
- Retains the `RolePermissionSeed` table (it is no longer dropped): the initialization markers survive the upgrade, including roles left with zero grants, so reseeding never restores revoked grants

**Warning:** dropping `SchoolId` is destructive. The migration stops with an error if any borrower's `SchoolId` differs from the linked account identifier (`AspNetUsers.UserName`) or a profile has no linked account; reconcile those rows first (the error message includes the inspection query). Otherwise any values stored in `BorrowerProfile.SchoolId` are permanently deleted. Registration and administration code now derives the borrower's displayed school ID from the account's user code, so no backfill is needed once the identifiers match. Apply this migration **before** running the updated application: new borrower-profile inserts omit the old required `SchoolId` column, so leaving the schema unmigrated causes those writes to fail.

Seeding does **not** run automatically at startup. After migrating, run it explicitly:

```powershell
dotnet run --no-build --project ".\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" -- --seed
```

Use `--seed-demo` instead when you want the development dashboard sample data (Development only).

## 4. Configure password-reset email for local development

The password-reset flow sends a six-digit OTP through Brevo. Keep the API key and verified sender email outside the repository by using .NET User Secrets:

```powershell
dotnet user-secrets --project ".\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" set "Brevo:ApiKey" "YOUR_ACTUAL_BREVO_API_KEY"
dotnet user-secrets --project ".\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" set "Brevo:SenderEmail" "your-verified-email@example.com"
```

The sender email must be registered and verified in Brevo. Restart the application after changing User Secrets.

## 5. Build the solution

From the repository root:

```powershell
dotnet build .\ASI.Basecode.sln
```

## 6. Run the automated tests

Run the OTP state and concurrency regression tests before opening a pull request:

```powershell
dotnet test .\ASI.Basecode.sln
```

The tests cover OTP state persistence, the five-attempt limit under concurrent requests, and single-use consumption.

## 7. Run the website

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5199"

dotnet watch --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

Open:

```text
http://localhost:5199/Account/Login
```

Press `Ctrl+C` to stop the application.

## Important database note

The repository includes an EF Core `Migrations` folder. Creating `GearanteeDev` creates an empty database only; apply the committed migration using the command in section 3 to create the application tables.

If the application reports that a table such as `Users` does not exist, the database schema must first be created through an EF migration or an approved SQL schema script.

## Daily workflow

```powershell
cd <path-to-Gearantee>
sqllocaldb start MSSQLLocalDB
dotnet watch --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

## Common problems

For bulk user creation, sign in as an active administrator with user-management permission, open **Users & Roles → Import CSV**, and follow the [CSV import instructions](USER_CSV_IMPORT.md). No additional migration is required for the CSV feature beyond the existing user-administration migration.

### LocalDB does not start

```powershell
sqllocaldb info MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

### Database cannot be opened

Confirm that the database name is exactly `GearanteeDev` and that the server is:

```text
(localdb)\MSSQLLocalDB
```

### Port 5199 is already in use

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5200"
dotnet watch --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

Then open `http://localhost:5200/Account/Login`.
