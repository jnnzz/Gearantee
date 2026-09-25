# Project Milestones

## Campus Equipment Borrowing & Reservation System

This plan covers the complete first version of the system using ASP.NET Core MVC with Razor Views, Microsoft SQL Server, Entity Framework Core's SQL Server provider, ASP.NET Core Identity, Tailwind CSS, MailKit, and school SMTP.

**Tracking:** change `[ ]` to `[x]` as each task is completed.

## Milestone Overview

| Milestone | Focus | Main deliverable |
| --- | --- | --- |
| 1 | Project foundation | Running ASP.NET Core MVC project connected to SQL Server |
| 2 | Authentication and access | Secure sign-in, password recovery, and role-based dashboards |
| 3 | Master data | Managed users, borrower profiles, categories, and equipment items |
| 4 | Reservations | Searchable catalog, availability checking, and reservation requests |
| 5 | Custodian workflow | Approval, rejection, release, and return/condition processing |
| 6 | Calendar and reporting | Availability calendar and filterable operational reports |
| 7 | Quality assurance | Tested, secure, responsive, and documented system |
| 8 | Deployment and handover | Production-ready deployment and administrator handover |

---

## Milestone 1 — Project Foundation

**Goal:** Create the application structure and database connection.

### Tasks

- [x] Confirm the ASP.NET Core MVC project structure with controllers and Razor Views.
- [x] Create/configure the Microsoft SQL Server development database with a non-secret LocalDB connection that can be overridden through user secrets or environment variables.
- [ ] Optionally use SQL Server Management Studio (SSMS) to administer and inspect SQL Server; SSMS is not the database engine.
- [x] Use Entity Framework Core with `Microsoft.EntityFrameworkCore.SqlServer`.
- [x] Add the initial entities and create/apply migrations based on the canonical ERD.
- [ ] Configure Tailwind CSS, its Razor content scanning, development watch command, and minified production build.
- [ ] Establish the shared layout, navigation, Tailwind styling, error pages, and basic logging.
- [ ] Create development/production configuration separation.

### Completion criteria

- The application runs locally.
- Entity Framework Core can create/update the SQL Server schema through migrations.
- The compiled Tailwind stylesheet is generated under `wwwroot` and loaded by the shared layout.
- The database contains the initial tables required by the ERD.
- No credentials are stored in source control.

---

## Milestone 2 — Authentication and Access Control

**Goal:** Allow secure access and ensure each person sees only their authorized functions.

### Tasks

- [x] Configure ASP.NET Core Identity with SQL Server.
- [x] Seed the three roles and documented role-permission assignments.
- [x] Build Identity-backed registration, login, and logout pages.
- [ ] Build role-based dashboards and navigation.
- [ ] Add account activation/deactivation behavior.
- [ ] Implement Forgot Password using Identity reset tokens, MailKit, and school SMTP.
- [ ] Apply password policy, sign-in lockout, HTTPS, and authorization rules.

### Completion criteria

- Users can log in and log out securely.
- Borrowers, custodians, and administrators land on the correct dashboard.
- Unauthorized pages/actions are blocked.
- An active user can reset their password through an expiring email reset link.

---

## Milestone 3 — Master Data Management

**Goal:** Make the users and inventory ready for real borrowing transactions.

### Tasks

- [ ] Build User and Role Management for administrators.
- [ ] Build Borrower Profile Management, including school ID, department, contact information, and eligibility.
- [ ] Build Equipment Category Management.
- [ ] Build Equipment Item Management with item code, name/model, serial number, condition, status, and storage location.
- [ ] Add item filters, search, validation, active/inactive controls, and item status updates.
- [ ] Seed representative development data for each role and equipment category.

### Completion criteria

- Administrators can maintain users, borrower profiles, categories, and equipment items.
- Item codes and relevant identity fields are unique.
- Inactive/ineligible borrowers cannot make new requests.
- Equipment marked unavailable or under maintenance cannot be reserved.

---

## Milestone 4 — Equipment Discovery and Reservations

**Goal:** Let borrowers find equipment and submit valid reservation requests.

### Tasks

- [ ] Build the borrower equipment catalog with search and category/status filters.
- [ ] Show equipment details and current availability.
- [ ] Build the reservation form: item, requested release date/time, requested return date/time, and purpose.
- [ ] Validate date/time ranges and borrower eligibility.
- [ ] Implement reservation status: Pending, Approved, Rejected, Cancelled, and Expired.
- [ ] Implement overlap detection for approved reservations and active loans of the same item.
- [ ] Build the borrower’s My Reservations and Active Loans pages.
- [ ] Allow borrowers to cancel eligible pending reservations.

### Completion criteria

- Borrowers can submit and review their own reservation requests.
- The system prevents conflicting reservations for the same equipment item.
- Invalid dates, unavailable items, and ineligible borrowers are rejected with clear messages.
- New requests appear in the custodian approval queue.

---

## Milestone 5 — Approval, Release, and Return Workflow

**Goal:** Complete the controlled equipment handover and return lifecycle.

### Tasks

- [ ] Build a pending-reservation queue for custodians.
- [ ] Allow custodians to approve or reject requests and record rejection reasons.
- [ ] Record approval reviewer and timestamp.
- [ ] Build the equipment release screen and record the actual release date/time and release notes.
- [ ] Change item/loan status when equipment is released.
- [ ] Build the return and condition-check screen.
- [ ] Record actual return time, condition, notes, and receiving custodian.
- [ ] Set the resulting equipment status to Available, Needs Inspection, or Under Maintenance.
- [ ] Identify overdue items based on the requested return schedule.

### Completion criteria

- Only custodians/administrators can approve, release, or receive returns.
- Every approval, rejection, release, and return retains responsible user and timestamp.
- A released item is unavailable until it is returned and cleared for use.
- Damaged items are not automatically made available.

---

## Milestone 6 — Availability Calendar and Reports

**Goal:** Give users visibility into schedules and give staff operational accountability.

### Tasks

- [ ] Add FullCalendar daily/weekly equipment availability views.
- [ ] Display reserved, borrowed, available, and maintenance states clearly.
- [ ] Add calendar filters for item and category.
- [ ] Build Borrowing History Report with filters for borrower, item/category, date range, status, and late return.
- [ ] Build an overdue-items view.
- [ ] Build an inventory/availability summary for administrators.
- [ ] Add server-side report validation and pagination where necessary.

### Completion criteria

- Users can identify when equipment is available before requesting it.
- Authorized staff can filter and review complete borrowing history.
- Reports correctly reflect reservation, release, return, and item-status records.

---

## Milestone 7 — Quality Assurance and Documentation

**Goal:** Ensure the system is reliable, secure, usable, and ready for release.

### Tasks

- [ ] Test all user-role permissions and navigation restrictions.
- [ ] Test reservation conflicts, cancellation, approval/rejection, release, return, damage, and overdue scenarios.
- [ ] Test password reset expiration, reuse prevention, and unknown-email handling.
- [ ] Validate all forms and server-side business rules.
- [ ] Test desktop and mobile responsiveness.
- [ ] Verify that the Tailwind production build includes every class used by Razor Views and JavaScript.
- [ ] Review error handling, logging, security headers, HTTPS, and secret management.
- [ ] Finalize technical documentation, ERD, setup guide, and user guide.

### Completion criteria

- All priority test cases pass.
- No role can perform an unauthorized action.
- Core workflows have been tested from beginning to end.
- Documentation matches the implemented system.

---

## Milestone 8 — Deployment and Handover

**Goal:** Publish the system and prepare the responsible school staff to operate it.

### Tasks

- [ ] Configure the production SQL Server database and run migrations through a controlled deployment process.
- [ ] Configure production environment variables, SMTP credentials, HTTPS certificate, and application URL.
- [ ] Deploy to the approved IIS server or Azure App Service environment.
- [ ] Create the initial administrator account securely.
- [ ] Configure database backup and restore procedures.
- [ ] Test a SQL Server backup restoration before handover.
- [ ] Perform production smoke tests: sign-in, reset password, create reservation, approve, release, return, calendar, and reports.
- [ ] Deliver the administrator setup guide and user quick-start guide.

### Completion criteria

- The production website is reachable over HTTPS.
- Password-reset email works through the approved school SMTP server.
- The school administrator can manage users, equipment, and transactions.
- Backup and recovery responsibilities are defined.

## Suggested Implementation Order

```text
Foundation
    → Authentication and roles
        → Users, profiles, categories, equipment
            → Reservation and conflict validation
                → Approval, release, and return
                    → Calendar and reports
                        → Testing, deployment, and handover
```

## Definition of Done for Version 1

The first version is complete when borrowers can securely reserve one available equipment item per reservation; custodians can approve, release, and receive it; administrators can manage people and inventory; and authorized users can view availability and transaction history. All core actions must be protected by Identity roles/permissions, stored in Microsoft SQL Server, styled with the compiled Tailwind CSS output, and usable through the deployed ASP.NET Core MVC application.
