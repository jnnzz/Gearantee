# Technology Stack

This stack applies to the entire Campus Equipment Borrowing & Reservation System: authentication, authorization, equipment inventory, reservations, approvals, releases, returns, reports, availability calendar, and administration.

## 1. Selected Stack

| Layer | Technology | Purpose |
| --- | --- | --- |
| Backend web framework | ASP.NET Core MVC (.NET 9, C#) | Provides controllers, Razor Views, validation, authorization, and business logic. |
| Authentication and authorization | ASP.NET Core Identity | Manages accounts, password hashing, sign-in, roles, password-reset tokens, lockout, and account security. |
| Database | Microsoft SQL Server | Stores Identity data, borrower profiles, inventory, reservations, releases, returns, permissions, and audit fields. |
| Database administration | SQL Server Management Studio (SSMS), optional | Provides a graphical tool for inspecting and administering SQL Server. SSMS is not the database engine. |
| Database access | Entity Framework Core + `Microsoft.EntityFrameworkCore.SqlServer` | Maps C# entities to SQL Server tables and manages queries and migrations. |
| Email library | MailKit | Sends password-reset messages through SMTP. |
| Email delivery | School SMTP server | Delivers reset messages from an approved school address. |
| User interface | ASP.NET Core MVC with Razor Views | Provides server-rendered pages, forms, tables, dashboards, and role-specific screens. |
| Styling | Tailwind CSS + minimal custom CSS | Provides responsive utility-based styling without Bootstrap. |
| Client-side interaction | Vanilla JavaScript | Supports confirmation dialogs, filters, calendar interaction, and small asynchronous updates. |
| Equipment images | Application file storage + SQL Server image path/URL | Stores image files outside the database while SQL Server stores the relative path or URL. |
| Calendar | FullCalendar JavaScript library | Displays daily and weekly availability using data supplied by ASP.NET Core endpoints. |
| Reporting | EF Core queries + MVC Razor Views | Produces filterable borrowing-history, overdue, availability, and inventory reports. |
| Validation | ASP.NET Core model validation + FluentValidation (optional) | Enforces form and business rules on the server. |
| Logging | ASP.NET Core logging/Serilog | Records technical events while business records retain the responsible user and timestamp. |
| Deployment | IIS on Windows Server or Azure App Service | Hosts the ASP.NET Core application. The final choice depends on school infrastructure. |

## 2. SQL Server and SSMS

Microsoft SQL Server is the database engine used by the application. SSMS is an optional administration client used to inspect tables, run queries, manage backups, and troubleshoot the SQL Server instance.

The application must create and evolve its schema through Entity Framework Core migrations. Manual SSMS changes must not replace migrations because every environment needs a repeatable schema history.

Recommended SQL Server conventions:

- Use `BIT` for Boolean values.
- Use `DATETIME2` for timestamps.
- Use `NVARCHAR(n)` or `NVARCHAR(MAX)` for user-entered text.
- Use `BIGINT IDENTITY(1,1)` for business-entity primary keys unless another key type is explicitly required.
- Use a filtered unique index for optional serial numbers: `WHERE serial_number IS NOT NULL`.
- Store connection strings in user secrets or environment-specific deployment configuration, not source control.

## 3. Tailwind CSS Build Process

Tailwind CSS is compiled during development and production; the browser does not build Tailwind classes itself.

```text
Razor Views and JavaScript files
            │
            │ scanned for Tailwind utility classes
            ▼
Tailwind source CSS
            │
            │ Tailwind CLI/PostCSS build
            ▼
wwwroot/css/app.css
            │
            ▼
Loaded by the shared MVC layout
```

Required setup:

1. Keep the Tailwind source file outside the generated output path.
2. Configure content scanning for `Views/**/*.cshtml`, JavaScript files, and any C# files that contain complete class names.
3. Compile the generated stylesheet to `wwwroot/css/app.css`.
4. Use watch mode during local development.
5. Run a minified production build during publishing or continuous integration.
6. Do not construct Tailwind class names dynamically unless those classes are safelisted.
7. Remove Bootstrap CSS, Bootstrap JavaScript, and Bootstrap-only classes after the corresponding views have been migrated.

Minimal custom CSS may be retained for third-party controls or behavior that is clearer outside utility classes.

## 4. Authentication and Password Recovery

```text
Browser
  │  1. User submits a registered email
  ▼
ASP.NET Core MVC application
  │  2. ASP.NET Core Identity creates a protected, expiring reset token
  ├──────────────────────────────► MailKit ► School SMTP ► User inbox
  │
  ▼
Microsoft SQL Server
  Stores Identity users, password hashes, roles, and security metadata
```

SQL Server stores Identity records but does not send email. MailKit connects to the school SMTP server to deliver the reset link. No custom password-reset or plaintext OTP table is required for the first version.

## 5. Whole-System Architecture

```text
Borrower / Custodian / Administrator browser
                  │
                  ▼
MVC Razor Views + Tailwind CSS + JavaScript + FullCalendar
                  │ HTTPS
                  ▼
ASP.NET Core MVC application
 ├─ ASP.NET Core Identity: accounts, roles, sign-in, password recovery
 ├─ Authorization policies: role and permission enforcement
 ├─ Business services: availability, reservations, approval, release, return
 ├─ Reporting services: history, overdue, availability, inventory
 ├─ Entity Framework Core SQL Server provider
 └─ MailKit: password-reset email delivery
                  │
                  ▼
          Microsoft SQL Server
 Identity | Profiles | Permissions | Categories | Locations | Items
 Reservations | Releases | Returns | Late returns | Audit fields
```

## 6. Technology Use by System Function

| System function | Technologies used | How they support the feature |
| --- | --- | --- |
| Login and role-based dashboards | ASP.NET Core Identity, MVC, SQL Server | Authenticates users, loads roles/permissions, and selects the authorized dashboard. |
| Forgot Password | ASP.NET Core Identity, MailKit, school SMTP, SQL Server | Generates a protected reset token, emails a link, and securely updates the password hash. |
| User and role management | ASP.NET Core Identity, EF Core, SQL Server | Creates/deactivates accounts and assigns roles. |
| Borrower profiles | MVC, EF Core, SQL Server | Maintains school ID, department, contact information, and eligibility separately from authentication data. |
| Equipment management | MVC, EF Core, SQL Server | Maintains categories, locations, physical items, conditions, and statuses. |
| Reservation requests | MVC, server validation, EF Core, SQL Server | Stores one equipment item per reservation after eligibility and schedule validation. |
| Conflict prevention | C# business service, SQL Server transaction/query | Prevents overlapping approved reservations or active loans for an item. |
| Approval and release | Authorization policies, EF Core, SQL Server | Restricts decisions and handover records to authorized staff. |
| Return and condition check | MVC, EF Core, SQL Server | Records return time and condition and updates equipment availability. |
| Availability calendar | FullCalendar, JavaScript, MVC endpoint, SQL Server | Displays reservations, active loans, and maintenance states. |
| Reports | EF Core queries, MVC Razor Views, SQL Server | Provides filterable operational and historical reports. |

## 7. Core Dependencies

| Package/tool | Use |
| --- | --- |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Identity persistence through EF Core. |
| `Microsoft.EntityFrameworkCore.SqlServer` | SQL Server provider for EF Core. |
| `Microsoft.EntityFrameworkCore.Design` | Design-time migration tooling. |
| `MailKit` | SMTP email delivery. |
| Tailwind CSS CLI/PostCSS | Compiles Tailwind source CSS into the application stylesheet. |
| FullCalendar | Equipment availability calendar. |

## 8. Configuration and Secrets

Required settings include:

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | SQL Server connection string. |
| `Smtp:Host` | School SMTP host. |
| `Smtp:Port` | SMTP port, commonly `587` for STARTTLS. |
| `Smtp:Username` | SMTP account name. |
| `Smtp:Password` | Protected SMTP credential. |
| `Smtp:FromAddress` | Approved sender address. |
| `Smtp:FromName` | Sender display name. |
| `Application:BaseUrl` | Public URL used in password-reset links. |

Development secrets belong in .NET user secrets. Production secrets belong in protected environment or hosting configuration. No real credentials or token-signing secrets may be committed to the repository.

## 9. Security Requirements

- Use HTTPS and SMTP TLS/STARTTLS.
- Use ASP.NET Core Identity password hashing; never use reversible password encryption.
- Return the same forgot-password response for known and unknown email addresses.
- Configure reset-token lifetime, sign-in lockout, and request rate limiting.
- Enforce permissions on backend actions, not only in the user interface.
- Record the responsible user and timestamp for approval, rejection, release, return, and administrative changes.
- Apply migrations through a controlled deployment process and maintain tested SQL Server backups.

## 10. Non-Required Services for Version 1

The first version does not require SendGrid, Firebase Authentication, Twilio SMS, Azure Communication Services, or a payment service. MailKit and an approved school SMTP server are sufficient when SMTP access is available.
