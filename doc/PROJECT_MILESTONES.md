# Project Milestones

## Campus Equipment Borrowing & Reservation System

This plan covers the complete first version of the system using ASP.NET Core MVC with Razor Views, Microsoft SQL Server, Entity Framework Core's SQL Server provider, ASP.NET Core Identity, Tailwind CSS, and Brevo transactional email for password-reset OTP delivery.

**Tracking:** change `[ ]` to `[x]` as each task is completed.

## How to update this file

Update this file **in the same PR** that finishes or starts a feature.

1. Find the feature's row in the WBS Feature Tracker (match the WBS number).
2. Set **Status** to one of: `Not Started` · `In Progress` · `In Review` · `Done` · `Blocked`.
   - `In Progress` → fill **Actual Start**.
   - `In Review` → add the PR link (for example, `#12`).
   - `Done` → fill **Actual End** with the owner-confirmed completion date (use the merge date when no earlier completion date is confirmed) and keep the PR link.
3. Tick the related `[ ]` → `[x]` item(s) in the matching Milestone section only when *all* WBS rows for that item are `Done`.
4. Recalculate the Progress summary counts.
5. Add one line to the Change log.
6. Mirror Actual Start / Actual End in the WBS Google Sheet.

Dates use `M/D/YYYY` (the same format as the WBS sheet). Never change Baseline dates here — change them in the WBS sheet first, then copy them here.

## Progress summary

### By status

The tracker contains 44 leaf features. Group headings such as 2.00 and 4.00 are not counted.

| Status | Count |
| --- | --- |
| Done | 5 |
| In Review | 21 |
| In Progress | 0 |
| Not Started | 18 |
| Blocked | 0 |

### By member

Shared rows count for each assigned member.

| Member | Assigned | Done |
| --- | --- | --- |
| Member 1 — Gesim | 6 (1.00, 20–23, 56) | 1 |
| Member 2 — Bansag | 13 (1.00, 9–12, 25–28, 52–55) | 1 |
| Member 3 — Laroco | 12 (1.00, 3, 5–7, 14–18, 49–50) | 5 |
| Member 4 — Cancencia | 7 (30–32, 39–42) | 0 |
| Member 5 — Cataraja | 9 (34–37, 44–47, 56) | 0 |

## WBS Feature Tracker

| WBS | Feature | Owner | Baseline Start | Baseline End | Days | Actual Start | Actual End | Status | PR | Milestone |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1.00 | Project Architecture & Setup | Gesim, Bansag, Laroco | 9/19/2026 | 9/23/2026 | 4 | 9/20/2026 | 9/24/2026 | Done | — | M1 |
| **2.00** | **Authentication: User Login** | | | | | | | | | |
| 3.00 | User Login Page | Laroco | 9/24/2026 | 9/25/2026 | 1 | 9/25/2026 | 9/25/2026 | Done | #6 | M2 |
| **4.00** | **Authentication: Role-Based Dashboard** | | | | | | | | | |
| 5.00 | View Borrower's Dashboard Page | Laroco | 9/25/2026 | 9/28/2026 | 3 | 9/25/2026 | 9/28/2026 | Done | #21 | M2 |
| 6.00 | View Custodian's Dashboard Page | Laroco | 9/25/2026 | 9/28/2026 | 3 | 9/25/2026 | 9/28/2026 | Done | #21 | M2 |
| 7.00 | View Administrator's Dashboard Page | Laroco | 9/25/2026 | 9/28/2026 | 3 | 9/25/2026 | 9/28/2026 | Done | #21 | M2 |
| **8.00** | **Authentication: Password Reset Request** | | | | | | | | | |
| 9.00 | Request Password Reset Page | Bansag | 9/25/2026 | 9/28/2026 | 3 | 9/25/2026 | | In Review | #19 | M2 |
| 10.00 | Receive OTP in Email | Bansag | 9/25/2026 | 9/28/2026 | 3 | 9/25/2026 | | In Review | #19 | M2 |
| 11.00 | OTP Verification Page | Bansag | 9/25/2026 | 9/28/2026 | 3 | 9/25/2026 | | In Review | #19 | M2 |
| 12.00 | Set New Password | Bansag | 9/25/2026 | 9/28/2026 | 3 | 9/25/2026 | | In Review | #19 | M2 |
| **13.00** | **Administration: User and Role Management** | | | | | | | | | |
| 14.00 | View User Accounts Page | Laroco | 9/29/2026 | 10/2/2026 | 3 | 10/1/2026 | | In Review | #22 | M3 |
| 15.00 | Create Accounts | Laroco | 9/29/2026 | 10/2/2026 | 3 | 10/1/2026 | | In Review | #22 | M3 |
| 16.00 | Assign & Update Roles | Laroco | 9/29/2026 | 10/2/2026 | 3 | 10/1/2026 | | In Review | #22 | M3 |
| 17.00 | Configure Role Access Permissions | Laroco | 9/29/2026 | 10/2/2026 | 3 | 10/1/2026 | | In Review | #22 | M3 |
| 18.00 | Deactivate Users | Laroco | 9/29/2026 | 10/2/2026 | 3 | 10/1/2026 | | In Review | #22 | M3 |
| **19.00** | **Master Data: Equipment Category Management** | | | | | | | | | |
| 20.00 | Create Category | Gesim | 9/29/2026 | 10/2/2026 | 3 | 10/4/2026 |  | In Review | #25 | M3 |
| 21.00 | Read Category | Gesim | 9/29/2026 | 10/2/2026 | 3 | 10/4/2026 |  | In Review | #25 | M3 |
| 22.00 | Update Category | Gesim | 9/29/2026 | 10/2/2026 | 3 | 10/4/2026 |  | In Review | #25 | M3 |
| 23.00 | Delete/Deactivate Category | Gesim | 9/29/2026 | 10/2/2026 | 3 | 10/4/2026 |  | In Review | #25 | M3 |
| **24.00** | **Master Data: Equipment Item Management** | | | | | | | | | |
| 25.00 | View Equipment Items | Bansag | 10/3/2026 | 10/6/2026 | 3 | 10/9/2026 | | In Review | #27 | M3 |
| 26.00 | Register Equipment Item | Bansag | 10/3/2026 | 10/6/2026 | 3 | 10/9/2026 | | In Review | #27 | M3 |
| 27.00 | Update Equipment Item | Bansag | 10/3/2026 | 10/6/2026 | 3 | 10/9/2026 | | In Review | #27 | M3 |
| 28.00 | Delete Equipment Items | Bansag | 10/3/2026 | 10/6/2026 | 3 | 10/9/2026 | | In Review | #27 | M3 |
| **29.00** | **Master Data: Borrower Profile Management** | | | | | | | | | |
| 30.00 | View Borrower's Records | Cancencia | 9/29/2026 | 10/2/2026 | 3 | | | Not Started | | M3 |
| 31.00 | Update Borrower's Records | Cancencia | 9/29/2026 | 10/2/2026 | 3 | | | Not Started | | M3 |
| 32.00 | Update Eligibility Status | Cancencia | 9/29/2026 | 10/1/2026 | 2 | | | Not Started | | M3 |
| **33.00** | **Transactions: Equipment Reservation** | | | | | | | | | |
| 34.00 | Search & View Available Items Page | Cataraja | 10/6/2026 | 10/7/2026 | 1 | 10/7/2026 | | In Review | #26 | M4 |
| 35.00 | Create and Submit Reservation Request | Cataraja | 10/6/2026 | 10/8/2026 | 2 | 10/7/2026 | | In Review | #26 | M4 |
| 36.00 | View Reservation Status | Cataraja | 10/6/2026 | 10/8/2026 | 2 | 10/7/2026 | | In Review | #26 | M4 |
| 37.00 | Cancel Reservation | Cataraja | 10/6/2026 | 10/8/2026 | 2 | 10/7/2026 | | In Review | #26 | M4 |
| **38.00** | **Transactions: Approval and Release** | | | | | | | | | |
| 39.00 | View Reservation Requests | Cancencia | 10/9/2026 | 10/11/2026 | 2 | | | Not Started | | M5 |
| 40.00 | Approve Reservation Requests | Cancencia | 10/9/2026 | 10/10/2026 | 1 | | | Not Started | | M5 |
| 41.00 | Reject Reservation Requests | Cancencia | 10/9/2026 | 10/10/2026 | 1 | | | Not Started | | M5 |
| 42.00 | Record Equipment Release | Cancencia | 10/9/2026 | 10/11/2026 | 2 | | | Not Started | | M5 |
| **43.00** | **Transactions: Return and Condition Check** | | | | | | | | | |
| 44.00 | Record Returned Item | Cataraja | 10/11/2026 | 10/13/2026 | 2 | | | Not Started | | M5 |
| 45.00 | Update Item Condition Status | Cataraja | 10/11/2026 | 10/13/2026 | 2 | | | Not Started | | M5 |
| 46.00 | Update Item Status | Cataraja | 10/11/2026 | 10/13/2026 | 2 | | | Not Started | | M5 |
| 47.00 | Identify Late Returns | Cataraja | 10/11/2026 | 10/13/2026 | 2 | | | Not Started | | M5 |
| **48.00** | **Reports: Availability Calendar** | | | | | | | | | |
| 49.00 | View Availability Calendar Page | Laroco | 10/9/2026 | 10/13/2026 | 4 | | | Not Started | | M6 |
| 50.00 | Filter Calendar View | Laroco | 10/10/2026 | 10/12/2026 | 2 | | | Not Started | | M6 |
| **51.00** | **Reports: Borrowing History Report** | | | | | | | | | |
| 52.00 | View Borrowing History Page | Bansag | 10/14/2026 | 10/16/2026 | 2 | | | Not Started | | M6 |
| 53.00 | Search Borrowing History | Bansag | 10/15/2026 | 10/16/2026 | 1 | | | Not Started | | M6 |
| 54.00 | Filter Borrowing History | Bansag | 10/16/2026 | 10/17/2026 | 1 | | | Not Started | | M6 |
| 55.00 | Generate Borrowing Report | Bansag | 10/18/2026 | 10/21/2026 | 3 | | | Not Started | | M6 |
| 56.00 | Testing and Debugging | Gesim, Cataraja | 10/22/2026 | 10/26/2026 | 4 | | | Not Started | | M7 |

> Feature names correct spelling in the WBS sheet, including “Receive,” “Equipment,” and “items.” Correct the sheet as well, or use its spelling here if the team decides an exact match is required.

## Milestone Overview

| Milestone | Focus | Main deliverable | WBS items |
| --- | --- | --- | --- |
| 1 | Project foundation | Running ASP.NET Core MVC project connected to SQL Server | 1.00 |
| 2 | Authentication and access | Secure sign-in, password recovery, and role-based dashboards | 3.00, 5.00–7.00, 9.00–12.00 |
| 3 | Master data | Managed users, borrower profiles, categories, and equipment items | 14.00–18.00, 20.00–23.00, 25.00–28.00, 30.00–32.00 |
| 4 | Reservations | Searchable catalog, availability checking, and reservation requests | 34.00–37.00 |
| 5 | Custodian workflow | Approval, rejection, release, and return/condition processing | 39.00–42.00, 44.00–47.00 |
| 6 | Calendar and reporting | Availability calendar and filterable operational reports | 49.00–50.00, 52.00–55.00 |
| 7 | Quality assurance | Tested, secure, responsive, and documented system | 56.00 |
| 8 | Deployment and handover | Production-ready deployment and administrator handover | No WBS rows yet — add when scheduled |

---

## Milestone 1 — Project Foundation

**WBS:** 1.00

**Goal:** Create the application structure and database connection.

### Tasks

- [x] Confirm the ASP.NET Core MVC project structure with controllers and Razor Views.
- [x] Create/configure the Microsoft SQL Server development database with a non-secret LocalDB connection that can be overridden through user secrets or environment variables.
- [ ] Optionally use SQL Server Management Studio (SSMS) to administer and inspect SQL Server; SSMS is not the database engine.
- [x] Use Entity Framework Core with `Microsoft.EntityFrameworkCore.SqlServer`.
- [x] Add the initial entities and create/apply migrations based on the canonical ERD.
- [x] Configure Tailwind CSS, its Razor content scanning, development watch command, and minified production build.
- [x] Establish the shared layout, navigation, Tailwind styling, error pages, and basic logging.
- [ ] Create development/production configuration separation.
- [x] Add GitHub Actions CI (build, EF migration check, vulnerable-package check, and CodeQL) and protect `main` (PR #6).

### Completion criteria

- The application runs locally.
- Entity Framework Core can create/update the SQL Server schema through migrations.
- The compiled Tailwind stylesheet is generated under `wwwroot` and loaded by the shared layout.
- The database contains the initial tables required by the ERD.
- No credentials are stored in source control.

---

## Milestone 2 — Authentication and Access Control

**WBS:** 3.00, 5.00–7.00, 9.00–12.00

**Goal:** Allow secure access and ensure each person sees only their authorized functions.

### Tasks

- [x] Configure ASP.NET Core Identity with SQL Server.
- [x] Seed the three roles and documented role-permission assignments.
- [x] Build Identity-backed registration, login, and logout pages.
- [x] Build role-based dashboards and navigation.
- [ ] Add account activation/deactivation behavior.
- [ ] Implement Forgot Password using Identity reset tokens, Brevo OTP delivery, and verified sender secrets.
- [x] Apply password policy, sign-in lockout, HTTPS redirect, and global authorization rules; apply per-page permission rules as each feature lands (PR #6).

### Completion criteria

- Users can log in and log out securely.
- Borrowers, custodians, and administrators land on the correct dashboard.
- Unauthorized pages/actions are blocked.
- An active user can reset their password through an expiring six-digit email OTP and a protected reset form.

---

## Milestone 3 — Master Data Management

**WBS:** 14.00–18.00, 20.00–23.00, 25.00–28.00, 30.00–32.00

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

**WBS:** 34.00–37.00

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

**WBS:** 39.00–42.00, 44.00–47.00

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

**WBS:** 49.00–50.00, 52.00–55.00

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

**WBS:** 56.00

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

**WBS:** No WBS rows yet — add when scheduled.

**Goal:** Publish the system and prepare the responsible school staff to operate it.

### Tasks

- [ ] Configure the production SQL Server database and run migrations through a controlled deployment process.
- [ ] Configure production environment variables, Brevo credentials, HTTPS certificate, and application URL.
- [ ] Deploy to the approved IIS server or Azure App Service environment.
- [ ] Create the initial administrator account securely.
- [ ] Configure database backup and restore procedures.
- [ ] Test a SQL Server backup restoration before handover.
- [ ] Perform production smoke tests: sign-in, reset password, create reservation, approve, release, return, calendar, and reports.
- [ ] Deliver the administrator setup guide and user quick-start guide.

### Completion criteria

- The production website is reachable over HTTPS.
- Password-reset OTP email works through Brevo using a verified sender.
- The school administrator can manage users, equipment, and transactions.
- Backup and recovery responsibilities are defined.

## Additional work (not in WBS)

| Date | Work | Owner | PR |
| --- | --- | --- | --- |
| 9/25/2026 | GitHub Actions CI + branch protection on `main` | Laroco | #6 |
| 9/25/2026 | Patched vulnerable build dependencies | Laroco | #6 |
| 9/24/2026 | How to run the system guide (`doc/Instruction_On_How_To_Run.md`) | Bansag | #1, #5 |
| 9/28/2026 | Enabled CI checks for all branches | Laroco | — |
| 9/29/2026 | Removed Bootstrap/template styling and added Tailwind build assets | Laroco | — |
| 9/30/2026 | Addressed dashboard PR review findings and added regression tests | Laroco | #21 |
| 10/1/2026 | Replaced the equipment location lookup with required `VARCHAR(200)` item text and a data-copy migration | Laroco | — |
| 10/10/2026 | Moved password-reset, Brevo email, user-administration, and CSV import services plus their shared service models from `ASI.Basecode.WebApp` to `ASI.Basecode.Services` | Bansag | — |

## Open questions

- **OTP vs reset link (WBS 10.00–11.00).** Resolved for PR #19: the system uses a six-digit OTP delivered through Brevo, stores only its hash in `AspNetUserTokens`, and creates the protected Identity reset token only after successful OTP verification.
- **Milestone 8 (Deployment)** has no WBS rows. Decide whether it is in scope for this term.

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

## Change log

| Date | Change | By |
| --- | --- | --- |
| 9/25/2026 | Added WBS Feature Tracker; marked 1.00 and 3.00 Done | Laroco |
| 9/28/2026 | Moved WBS 9.00–12.00 to In Review for PR #19 and aligned password-reset documentation with the OTP/Brevo implementation | Bansag |
| 9/29/2026 | Added SQL-backed dashboards, shared responsive shell, Tailwind build, and demo seeding for WBS 5.00–7.00 | Laroco |
| 9/30/2026 | Addressed PR #21 review findings TEST-01, OPS-01, UX-01, and P3-01–P3-05 | Laroco |
| 10/1/2026 | Removed the Location entity and updated the schema, dashboards, demo seed, and ERD for item location text | Laroco |
| 10/1/2026 | Marked WBS 5.00–7.00 Done using owner-confirmed 9/25 start and 9/28 completion dates; PR #21 merged on 10/1 | Laroco |
| 10/1/2026 | Started WBS 14.00–18.00 implementation for user accounts, role assignment, permissions, and account activation management | Laroco |
| 10/1/2026 | Implemented the user-administration screens, Identity workflows, permission controls, SQL Server safeguards, documentation, and regression tests; awaiting PR review before advancing WBS status | Laroco |
| 10/2/2026 | Moved WBS 14.00–18.00 to In Review for PR #22; addressed REQ-01 with atomic CSV account creation, UX/AUDIT-01 with trusted edit redisplay and no-op activation handling, and ROLE-EDGE-01 with inactive-admin demotion; added regression tests and import instructions | Laroco |
| 10/4/2026 | Implemented equipment category creation (WBS 20.00) with duplicate checking, admin authorization, Tailwind views, and unit/authorization tests | Gesim |
| 10/5/2026 | Moved WBS 20.00–23.00 to In Review for PR #25 per review handoff (SEC-01/SEC-02/STATE-01 pending correction; cleared premature Done/Actual End) | Gesim |
| 10/6/2026 | Addressed PR #25 review handoff findings SEC-01, SEC-02, STATE-01, UX-01, LIST-01, and UX-02; added regression tests; clarified opt-in SQL Server tests and qualified inactive-category item registration | Gesim |
| 10/9/2026 | Started WBS 25.00–28.00 equipment-item management implementation | Bansag |
| 10/9/2026 | Implemented equipment-item inventory listing, search and filters, registration, editing, and safe archive/restore; web app build passes | Bansag |
| 10/9/2026 | Addressed PR #27 CI findings by registering the item service in the category test host, using encoded form values for equipment pagination, and removing user-supplied item codes from logs | Bansag |
| 10/9/2026 | Addressed PR #27 review feedback with optional catalog-image persistence, serializable inventory lock checks, and equipment-item service/HTTP regression tests | Bansag |
| 10/9/2026 | Addressed PR #27 concurrency and search findings with a shared SQL Server item lock, an opt-in inventory/reservation race regression test, and collation-aware search queries | Bansag |
| 10/9/2026 | Addressed PR #27 CONC-01/TEST-02 by locking and revalidating the item inside production reservation submission, preventing cross-item borrower range-lock deadlocks, and testing the real service against LocalDB | Bansag |
| 10/7/2026 | Started WBS 34.00: borrower equipment catalog with search, category and free-window filters, item details with booked times, and the shared reservation availability rule | Cataraja |
| 10/7/2026 | Started WBS 35.00: borrower reservation request form with eligibility, availability, and duplicate-request checks | Cataraja |
| 10/7/2026 | Started WBS 36.00: My Reservations list with status tabs and a reservation detail page; stale pending requests are marked Expired when viewed | Cataraja |
| 10/7/2026 | Started WBS 37.00: borrowers cancel their own pending or unreleased approved reservations, with an atomic status check | Cataraja |
| 10/8/2026 | WBS 37.00: replaced the browser confirm with a reusable confirmation modal (`Components/_ConfirmDialog` + `confirm-dialog.js`) before cancelling a reservation | Cataraja |
| 10/8/2026 | Moved WBS 34.00–37.00 to In Review for PR #26: borrower catalog, reservation requests, My Reservations status, and cancel with confirmation modal; 69 new tests | Cataraja |
| 10/9/2026 | Addressed PR #26 review for WBS 34.00–37.00 (still In Review): merged `staging` (PR #25) and resolved conflicts; RES-01 serializes duplicate-check + insert per borrower/item with an app lock; RES-02/PERF-05 expire stale requests with one conditional UPDATE; AUTH-03 opens the catalog to any role with `equipment.browse`; AUTH-04 keeps history and cancel open without `reservation.create`; added SQL Server race and role-matrix tests | Cataraja |
