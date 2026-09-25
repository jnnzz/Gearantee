# Local Development Setup

Follow this guide after cloning or pulling the Gearantee repository.

## 1. Install the prerequisites

Install the following on your computer:

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server Express LocalDB
- Git
- SQL Server Management Studio (SSMS), optional but recommended for viewing the database

This repository uses .NET SDK `9.0.318`. A newer .NET 9 patch may also work because `global.json` allows the latest patch release.

Confirm that .NET is installed:

```powershell
dotnet --version
```

## 2. Clone or pull the project

For a new clone:

```powershell
git clone <repository-url>
cd Gearantee
```

If the project is already cloned:

```powershell
cd <path-to-Gearantee>
git pull
```

Run the remaining commands from the repository root—the directory containing `ASI.Basecode.sln`.

## 3. Start SQL Server LocalDB

```powershell
sqllocaldb start MSSQLLocalDB
```

Confirm that it is running:

```powershell
sqllocaldb info MSSQLLocalDB
```

The output should show `State: Running`.

## 4. Restore the project

Restore the local tools and NuGet packages:

```powershell
dotnet tool restore
dotnet restore .\ASI.Basecode.sln
```

## 5. Create or update the database

Apply the Entity Framework Core migrations:

```powershell
dotnet ef database update `
  --project .\ASI.Basecode.Data\ASI.Basecode.Data.csproj `
  --startup-project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

This creates or updates the local `GearanteeDev` database.

## 6. Configure a development administrator

Each developer stores their administrator credentials locally with .NET User Secrets. Replace the example values with your own credentials:

```powershell
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com" `
  --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj

dotnet user-secrets set "SeedAdmin:UserCode" "ADMIN-001" `
  --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj

dotnet user-secrets set "SeedAdmin:Password" "Admin123!" `
  --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

Use a development password with at least eight characters, an uppercase letter, a lowercase letter, a number, and a special character.

Do not add passwords or other secrets to Git. User Secrets remain on the developer's computer.

Seed the roles, permissions, and optional administrator account:

```powershell
dotnet run --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj -- --seed
```

The seed command is safe to run again. It will not create duplicate roles or permissions.

## 7. Run the website

Set the development environment and local address for the current PowerShell session:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5199"
```

Start the application with automatic rebuilding and hot reload:

```powershell
dotnet watch --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

Open these pages in a browser:

- Website: <http://localhost:5199>
- Login: <http://localhost:5199/Account/Login>
- Registration: <http://localhost:5199/Account/Register>
- Database health check: <http://localhost:5199/health/database>

A successful database health check returns:

```json
{"status":"Healthy"}
```

Press `Ctrl+C` in the terminal to stop the website.

## Daily development workflow

After completing the initial setup, the usual workflow is:

```powershell
cd <path-to-Gearantee>
git pull
sqllocaldb start MSSQLLocalDB
dotnet watch --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

Run the database update command again whenever the repository contains a new Entity Framework Core migration.

## Viewing the database in SSMS

Use these connection settings in SQL Server Management Studio:

- Server name: `(localdb)\MSSQLLocalDB`
- Authentication: Windows Authentication
- Database: `GearanteeDev`

SSMS is an administration tool; SQL Server LocalDB is the database engine.

## Common problems

### The required .NET SDK cannot be found

Install .NET SDK `9.0.318` or a compatible newer .NET 9 patch, then restart the terminal.

### LocalDB does not start

Make sure SQL Server Express LocalDB is installed, then run:

```powershell
sqllocaldb start MSSQLLocalDB
```

### The database health check fails

Confirm that LocalDB is running, then apply the migrations again using the command in step 5.

### Port 5199 is already in use

Stop the other application or choose another port:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5200"
```

Then open <http://localhost:5200>.
