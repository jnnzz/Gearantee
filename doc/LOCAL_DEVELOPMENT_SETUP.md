# Local Development Setup

Follow this guide after cloning or pulling the Gearantee repository.

## 1. Install the prerequisites

Install the following on your computer:

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server Express LocalDB
- Git
- SQL Server Management Studio (SSMS), optional but recommended for viewing the database

This repository uses .NET SDK `9.0.318`. A newer .NET 9 patch may also work because `global.json` allows the latest patch release.

Node.js 24 and npm are only needed to rebuild the Tailwind and FullCalendar assets. The generated files are checked in, so Node.js is not required just to run the website.

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

For a separate Custodian login, also configure these User Secrets. Replace the password placeholder with your local development password:

```powershell
dotnet user-secrets set "SeedCustodian:Email" "custodian@example.com" `
  --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
dotnet user-secrets set "SeedCustodian:UserCode" "CUST-001" `
  --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
dotnet user-secrets set "SeedCustodian:Password" "YOUR_DEVELOPMENT_PASSWORD" `
  --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

Seed the roles, permissions, and configured administrator and Custodian accounts:

```powershell
dotnet run --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj -- --seed
```

The seed command is safe to run again. It will not create duplicate roles or permissions.

The Custodian account receives only the Custodian role. All three `SeedCustodian` values are required; without them, its creation is skipped. Reseeding leaves existing accounts, passwords, activation and role changes intact. `--seed-demo` also runs this account seed before adding its existing multi-role demo account and sample data. `UI-BORROWER` belongs to the separate UI preview fixture and is not created by either seed command.

## 7. Run the website

If you change calendar scripts or styles, rebuild the frontend assets from `ASI.Basecode.WebApp`:

```powershell
cd .\ASI.Basecode.WebApp
npm ci
npm run assets:build
cd ..
```

The command refreshes the FullCalendar bundle and license in `wwwroot/lib/fullcalendar` and regenerates the minified Tailwind stylesheet.

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

## Equipment image storage

Equipment images are optional JPEG or PNG uploads. The application validates and decodes each image, then stores it outside `wwwroot` under the configured `EquipmentImages:StoragePath`. The default is `App_Data/EquipmentImages`, relative to the WebApp content root. Temporary uploads used while a form is being corrected are removed after 24 hours.

For a deployed server, set `EquipmentImages__StoragePath` to a directory on persistent storage and grant the application identity read/write access. For example, in PowerShell:

```powershell
$env:EquipmentImages__StoragePath = "D:\GearanteeData\EquipmentImages"
```

On Linux, configure an absolute persistent directory such as `/var/lib/gearantee/equipment-images` using the deployment environment's configuration mechanism. Do not put uploaded files under `wwwroot`; the application serves generated filenames through an authorized image endpoint. Back up the configured image directory alongside the database, and restore both when recovering the system. For multi-instance hosting, all instances must use the same persistent shared storage location.

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
