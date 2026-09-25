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
IF DB_ID(N'AsiBasecodeDB') IS NULL
BEGIN
    CREATE DATABASE [AsiBasecodeDB];
END
```

The development connection string is stored in:

```text
ASI.Basecode.WebApp\appsettings.Development.json
```

It points to:

```text
Server=(localdb)\MSSQLLocalDB;Database=AsiBasecodeDB
```

## 3. Build the solution

From the repository root:

```powershell
dotnet build .\ASI.Basecode.sln
```

## 4. Run the website

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

The repository currently has no EF Core `Migrations` folder. Creating `AsiBasecodeDB` creates an empty database only; it does not create the application tables.

If the application reports that a table such as `Users` does not exist, the database schema must first be created through an EF migration or an approved SQL schema script.

Do not run `dotnet ef database update` until migrations have been added to the repository.

## Daily workflow

```powershell
cd <path-to-Gearantee>
sqllocaldb start MSSQLLocalDB
dotnet watch --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

## Common problems

### LocalDB does not start

```powershell
sqllocaldb info MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

### Database cannot be opened

Confirm that the database name is exactly `AsiBasecodeDB` and that the server is:

```text
(localdb)\MSSQLLocalDB
```

### Port 5199 is already in use

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5200"
dotnet watch --project .\ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj
```

Then open `http://localhost:5200/Account/Login`.
