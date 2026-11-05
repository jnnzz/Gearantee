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
| Done | 8 |
| In Review | 33 |
| In Progress | 2 |
| Not Started | 1 |
| Blocked | 0 |

### By member

Shared rows count for each assigned member.

| Member | Assigned | Done |
| --- | --- | --- |
| Member 1 — Gesim | 6 (1.00, 20–23, 56) | 1 |
| Member 2 — Bansag | 13 (1.00, 9–12, 25–28, 52–55) | 1 |
| Member 3 — Laroco | 12 (1.00, 3, 5–7, 14–18, 49–50) | 5 |
| Member 4 — Canencia | 7 (30–32, 39–42) | 3 |
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
| 30.00 | View Borrower's Records | Canencia | 9/29/2026 | 10/2/2026 | 3 | 10/3/2026 | 10/17/2026 | Done | #31 | M3 |
| 31.00 | Update Borrower's Records | Canencia | 9/29/2026 | 10/2/2026 | 3 | 10/3/2026 | 10/17/2026 | Done | #31 | M3 |
| 32.00 | Update Eligibility Status | Canencia | 9/29/2026 | 10/1/2026 | 2 | 10/3/2026 | 10/17/2026 | Done | #31 | M3 |
| **33.00** | **Transactions: Equipment Reservation** | | | | | | | | | |
| 34.00 | Search & View Available Items Page | Cataraja | 10/6/2026 | 10/7/2026 | 1 | 10/7/2026 | | In Review | #26 | M4 |
| 35.00 | Create and Submit Reservation Request | Cataraja | 10/6/2026 | 10/8/2026 | 2 | 10/7/2026 | | In Review | #26 | M4 |
| 36.00 | View Reservation Status | Cataraja | 10/6/2026 | 10/8/2026 | 2 | 10/7/2026 | | In Review | #26 | M4 |
| 37.00 | Cancel Reservation | Cataraja | 10/6/2026 | 10/8/2026 | 2 | 10/7/2026 | | In Review | #26 | M4 |
| **38.00** | **Transactions: Approval and Release** | | | | | | | | | |
| 39.00 | View Reservation Requests | Canencia | 10/9/2026 | 10/11/2026 | 2 | 10/17/2026 | | In Review | #33 | M5 |
| 40.00 | Approve Reservation Requests | Canencia | 10/9/2026 | 10/10/2026 | 1 | 10/17/2026 | | In Review | #33 | M5 |
| 41.00 | Reject Reservation Requests | Canencia | 10/9/2026 | 10/10/2026 | 1 | 10/17/2026 | | In Review | #33 | M5 |
| 42.00 | Record Equipment Release | Canencia | 10/9/2026 | 10/11/2026 | 2 | 10/17/2026 | | In Review | #33 | M5 |
| **43.00** | **Transactions: Return and Condition Check** | | | | | | | | | |
| 44.00 | Record Returned Item | Cataraja | 10/11/2026 | 10/13/2026 | 2 | 10/17/2026 | | In Review | #32 | M5 |
| 45.00 | Update Item Condition Status | Cataraja | 10/11/2026 | 10/13/2026 | 2 | 10/17/2026 | | In Review | #32 | M5 |
| 46.00 | Update Item Status | Cataraja | 10/11/2026 | 10/13/2026 | 2 | 10/17/2026 | | In Review | #32 | M5 |
| 47.00 | Identify Late Returns | Cataraja | 10/11/2026 | 10/13/2026 | 2 | 10/17/2026 | | In Review | #32 | M5 |
| **48.00** | **Reports: Availability Calendar** | | | | | | | | | |
| 49.00 | View Availability Calendar Page | Laroco | 10/9/2026 | 10/13/2026 | 4 | 10/14/2026 | | In Progress | | M6 |
| 50.00 | Filter Calendar View | Laroco | 10/10/2026 | 10/12/2026 | 2 | 10/14/2026 | | In Progress | | M6 |
| **51.00** | **Reports: Borrowing History Report** | | | | | | | | | |
| 52.00 | View Borrowing History Page | Bansag | 10/14/2026 | 10/16/2026 | 2 | 10/14/2026 | | In Review | #29 | M6 |
| 53.00 | Search Borrowing History | Bansag | 10/15/2026 | 10/16/2026 | 1 | 10/14/2026 | | In Review | #29 | M6 |
| 54.00 | Filter Borrowing History | Bansag | 10/16/2026 | 10/17/2026 | 1 | 10/14/2026 | | In Review | #29 | M6 |
| 55.00 | Generate Borrowing Report | Bansag | 10/18/2026 | 10/21/2026 | 3 | 10/14/2026 | | In Review | #29 | M6 |
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
| 11/5/2026 | Restored the login password toggle's initial visible Show label and verified both toggle states. | Codex | — |
| 11/5/2026 | Completed the merged PR #36/#37 equipment image follow-up on `fix/equipment-image-recovery`. Hidden image fields now render refreshed server values after validation errors and repeated save attempts. Published image files are retained when an item removes or replaces its URL so shared links and concurrent edits remain valid. Four regression cases failed before the fix; the category/image and Excel HTTP checks now pass (64 passed, 1 opt-in SQL Server concurrency skip), as do all 6 frontend tests. CSS generation and the asset guard passed using a separate output after local file-access failures. Recounted 44 WBS leaf rows: 8 Done, 33 In Review, 2 In Progress, 1 Not Started, 0 Blocked. | Codex | — |
| 11/5/2026 | Implemented the Assignment D return/report module follow-up on `feat/custodian-reporting-refinement`, based on `d70f6da5c648c7b286aee3989d960ac02c034f6e`. Added database-side search across all four return tabs before counts/paging, preserved search through tab/page/details/return flows, and refined custodian queue/return/report controls. Added a separate streaming Open XML XLSX export with the CSV's 17-column order, typed Manila date/number cells, literal text identifiers, column widths, wrapped text, frozen header, row-limit handling and temporary-file cleanup. CSV behavior and transaction writes remain unchanged. Full Release suite: 406 passed, 26 opt-in skipped, 0 failed; focused Return/XLSX/MVC checks passed (50 passed, 1 interactive preview skipped); CSS build and `git diff --check` passed. The dedicated default-Kestrel CSV response regression also passed (1 passed). Figma/browser visual comparison, opening the generated workbook in desktop Excel, SQL Server opt-in checks, and final evidence from the other assignment owners remain outstanding. No WBS rows, owners, dates, statuses, milestone checkboxes or progress counts changed. No PR created or merged. | Codex | — |
| 11/4/2026 | Completed Assignment C code changes and automated verification: full Release suite with LocalDB enabled passed 426, 0 failed, 1 interactive preview skipped; category HTTP/concurrency, storage, client upload-script, and category metadata coverage passed; Tailwind production build/asset guard and 6 UI script tests passed. Added regressions for missing staged-image reselection and browser-side browse/drop validation. Two final live UI checks (drag/drop save-and-reload and responsive inventory screenshots) remain pending because Computer Use was stopped before the editor opened. No WBS rows/statuses/dates changed; PR is pending. | Codex | — |
| 11/4/2026 | Compared the Assignment C dashboard and inventory views against visible Figma A01/A02 references. Matched the dashboard subtitle and inventory heading/caption, changed alert icons to the reference clock treatment, let Needs attention reflow after the required Shortcuts removal, and made category cards fill the available desktop row responsively. Added an accessible Clear selected image action and kept the preview aligned with the actual file input when the picker is cancelled. The focused MVC UI acceptance test passed (1/1); full Release suite passed with 389 passed, 26 skipped, 0 failed. JavaScript syntax and `git diff --check` passed. Verified Assignment C acceptance items 1–9 with focused MVC and HTTP checks, generated production CSS, and live localhost visual checks: the Users page has one filter search and no topbar search; desktop, intermediate/tablet, and narrow-phone CSS viewport layouts were inspected (narrow viewport emulated with browser zoom); there is one H1; and actions, status badges, and keyboard focus are readable/visible in Light and Dark themes. Corrected the dark-theme Edit link and input focus ring. CSS asset build and targeted MVC/search tests passed (1 each). Screenshots were inspected live but were not saved as files. No WBS rows, owners, dates, statuses, milestone checkboxes, or progress counts changed. | Codex | — |
| 11/4/2026 | Aligned the account activation action with shared danger/secondary button styles, replaced the Edit account status badge with the shared badge component, and added a visible theme-aware focus ring around the equipment upload area when its transparent file input is keyboard-focused. Added UI acceptance assertions for the action, badge, and served focus CSS. Targeted UI test and Release suite passed: 389 passed, 26 skipped, 0 failed. Recounted WBS progress: 8 Done, 33 In Review, 2 In Progress, 1 Not Started, 0 Blocked; WBS rows, statuses, owners, dates, milestone checkboxes and counts unchanged. | Codex | — |
| 11/4/2026 | Restored the optional equipment image URL workflow as a secondary form input. Edit forms show the saved URL; HTTPS and same-site path validation remain enforced by the service. Uploaded files take precedence over the URL, and explicit removal clears the image. Added HTTP coverage for create, edit, blank-field preservation, form rendering and upload precedence. Release suite: 389 passed, 26 skipped, 0 failed. Recounted WBS progress: 8 Done, 33 In Review, 2 In Progress, 1 Not Started, 0 Blocked; WBS rows, statuses, owners, dates, milestone checkboxes and counts unchanged. | Codex | — |
| 11/4/2026 | Completed the Assignment C equipment-upload client validation follow-up: require a matching JPEG/PNG extension and MIME type, verify file signatures, decode and enforce server-matched dimensions, and reject multiple dropped files instead of using only the first. Node smoke checks passed and the Release suite passed: 388 passed, 26 skipped, 0 failed. Recounted WBS progress: 8 Done, 33 In Review, 2 In Progress, 1 Not Started, 0 Blocked; no WBS rows, statuses, owners, dates, milestone checkboxes or counts changed. | Codex | — |
| 11/4/2026 | Removed the Admin dashboard Shortcuts card to meet Assignment C; kept the inventory summary and Needs attention panel intact and removed now-unused Admin shortcut styles. Recounted the WBS progress summary: 8 Done, 33 In Review, 2 In Progress, 1 Not Started, 0 Blocked; no WBS rows, statuses, owners, dates, milestone checkboxes, or progress counts changed. | Codex | — |
| 11/4/2026 | Implemented Assignment C admin dashboard, users, borrower profiles, import entry and inventory/category refinements. Added validated staged JPEG/PNG uploads with persistent storage, permission checks, cleanup and regression coverage; metadata edits preserve category activation status. Kept the Admin Shortcuts card at the user's direction to match Figma, despite the assignment's remove-shortcuts bullet. Updated setup guidance and test hosts. Release suite: 388 passed, 26 opt-in tests skipped, 0 failed. WBS rows/statuses, owners, dates, milestone checkboxes and progress counts unchanged. Direct browser comparison against the Figma screens remains unverified because the Windows browser automation service stopped on URL verification. | Codex | — |
| 11/3/2026 | Fixed equipment schedule links so they follow the active calendar date and Day or Week view. | Unassigned | — |
| 11/3/2026 | Fixed detailed calendar block counts so they refresh for the visible range when switching between Week and Day. | Unassigned | — |
| 11/3/2026 | Clarified that both availability calendar views show current scheduling blocks and directed users to borrowing history for completed loans. | Unassigned | — |
| 11/3/2026 | Fixed the Assignment B calendar review findings. The time grid opens past dates, and calendar input and navigation stay within complete weeks supported by the API. | Unassigned | — |
| 11/3/2026 | Implemented Assignment B on `feat/student-calendar-refinement` from Assignment A commit `100b465`. Refined borrower catalog, history, and reservation presentation. Added a shared equipment week with server-side search and retained the detailed booking grid. Targeted checks passed with 123 tests and 11 opt-in skips. See `doc/UI_MIGRATION.md` for browser evidence and limits. | Unassigned | — |
| 11/3/2026 | Applied theme-aware link colors to the password toggle and recovery controls. Stylesheet build and asset guard passed. | Unassigned | — |
| 11/3/2026 | Fixed unreadable login validation messages by using the shared danger background and text colors. Stylesheet build and asset checks passed. | Unassigned | — |
| 11/2/2026 | Fixed the subsequent Ponytail dark-mode contrast finding: Users/Edit School ID uses theme-aware body text, and the shared file-selection button uses the theme-aware link color. Rebuilt CSS/utility guard passed; 6 MVC UI tests passed, 1 interactive skipped. Browser light/dark contrast assertions passed: School ID 9.11:1 / 9.61:1; upload button 6.15:1 / 7.51:1. No business rules or production data changed. Details: `doc/SHARED_UI.md`. | Unassigned | — |
| 11/2/2026 | Replaced the dark Gearantee logo's navy background with genuine transparent alpha at the user's request, keeping the original light asset and shared theme integration. PNG alpha samples and real login browser assertions passed. Restored a generated stylesheet truncated by the earlier line-ending cleanup; rebuilt CSS/utility guard passed and the styled preview was verified. Current asset/prompt: `doc/SHARED_UI.md`. | Unassigned | — |
| 11/2/2026 | Created and integrated the requested dark Gearantee logo on the existing shared UI branch: light lettering/blue artwork on navy, shared authentication/registration/sidebar logo partial, CSS theme selection and removal of the white dark-mode panel. Original light asset preserved. Production CSS/guard, 3 focused MVC tests and 3 JavaScript checks passed; real local login browser verified both themes. Asset/prompt/checks: `doc/SHARED_UI.md`. | Unassigned | — |
| 11/2/2026 | Fixed Assignment A Ponytail finding 1: migrated late-return/403/Error badges and Reject/Cancel buttons from heading-color fills to shared danger styles. Added a real-MVC regression (verified red before fix); 6 UI tests passed, 1 interactive skipped; production CSS/guard passed. Browser light/dark contrast assertions passed (badges 5.86:1 / 7.63:1, hovered buttons 9.21:1). Existing confirmation/security/workflow behavior retained. | Unassigned | — |
| 11/2/2026 | Completed Assignment A shared UI/login implementation on `feat/shared-ui-login-refinement`: Light/Dark/System, Barlow controls, status/required patterns, toasts across roles with local assets, coordinated shell/search changes, login control cleanup and user-authorized campus-equipment SVG. Release: 380 passed, 26 opt-in skipped; 3 JS checks passed; production CSS/utility guard and scoped responsive SQLite browser checks passed. Contract/limits: `doc/SHARED_UI.md`. B/C/D implementation remains separate. | Unassigned | — |
| 11/2/2026 | Started Assignment A shared UI/login refinement on `feat/shared-ui-login-refinement` from `develop@2c1293b`: shared themes/controls/statuses, toast infrastructure, coordinated shell updates and login controls. User authorized creating an SVG illustration instead of supplying the previously pending picture. Baseline Release: 378 passed, 26 opt-in skipped, 0 failed. | Unassigned | — |
| 10/31/2026 | Prepared the post-migration UI refinement [implementation plan and four standalone assignment handoffs](ui-refinement-handoffs/00-IMPLEMENTATION-PLAN.md): A shared UI/toasts/login; B student/calendar; C admin/inventory; D custodian/reporting/acceptance coordination. Each includes its detailed topic instructions; original ten topic files remain references. Recorded the user's confirmed all-status personal history, date-only filters with scheduling times preserved, and XLSX alongside CSV. Planning only; implementation remains Not started, no named assignees or WBS feature statuses changed. | Unassigned | — |
| 10/30/2026 | Addressed the PR #35 Ponytail re-review must-fix finding in `doc/Handoff.md`: `ReservationService.SubmitReservation` and `ReturnRecordService.AddReturnRecord` now build a fresh reservation / return record (and its late-return child) inside each retry attempt, so a rolled-back insert no longer replays a database-generated ID when the execution strategy retries. Added two opt-in SQL Server regressions that fail the first commit, force a retry, and assert exactly one committed reservation and one return (verified red before the fix). Verification: clean build 0 warnings, 404 passed / 1 UI skip / 0 failed with `GEARANTEE_TEST_SQLSERVER=1`. | opencode | — |
| 10/29/2026 | Addressed the PR #35 should-fix review findings in `doc/Handoff.md` (6–9): made the borrower school ID read-only (derived from the user code) on Create/Edit and removed the silently-ignored inputs and validations; restored distinct `SchoolId` values in the `NormalizeUserAndBorrowerSchema` rollback before the unique index; corrected the setup docs (migration required, explicit `--seed` command) and the architecture table inventory (`AspNetUserLogins` removed); removed the shadowed `Succeeded`/`Message` properties so result state is consistent through the `ServiceResult` base. Verification: clean build 0 warnings, 399 passed / 1 UI skip / 0 failed with SQL Server tests enabled. | opencode | — |
| 10/29/2026 | Addressed the PR #35 must-fix review findings in `doc/Handoff.md` (1–5): restored the persistent `RolePermissionSeed` marker so reseeding never restores revoked grants; restored the shared administration application lock across borrower-profile, user-administration, and return writes; guarded `NormalizeUserAndBorrowerSchema` against dropping `SchoolId` values that differ from the account identifier; clear the change tracker per retry attempt in equipment writes; repaired the SQL Server test containers (`IAuditService`/`IUnitOfWork` registration) and the return/revocation lock protocol. Full suite with `GEARANTEE_TEST_SQLSERVER=1`: 396 passed, 1 UI-acceptance skip, 0 failed; clean build 0 warnings. This supersedes the 10/28 Option-1 de-serialization entry, which the review found allowed a deactivated administrator to finish a pending write. | opencode | — |
| 10/29/2026 | Executed the full 5-phase codebase refactor (dead code removal, user schema normalization with EF migration, structural decoupling, controller alignment, persistence encapsulation with IAccountService/IAuditService/IApplicationLockManager/ServiceResult and IUnitOfWork-based services). Clean build, 371 passed, 0 failed, 23 opt-in SQL Server tests skipped. | Unassigned | — |
| 10/28/2026 | Executed Option 1 de-serialization: changed administrative and profile writes to ReadCommitted, cleaned up dead no-ops (AcquireAdministrationReadLock) and stale comments, added an AspNetRoles UPDLOCK guard for concurrent last-admin demotions, eliminated the global 15s sp_getapplock pool bottleneck, and wrapped all user transactions in CreateExecutionStrategy. Full test suite: 371 passed, 0 failed, 23 opt-in tests skipped. | Unassigned | — |
| 10/27/2026 | Completed restoration of the original login lockout policy after review and explicit user approval: 5 failed attempts, then a 15-minute lockout. Authentication matches the Git baseline; the checker rejects the earlier local relaxation and preserves unrelated UI/login-copy/CSV work. Release build/regression: 371 passed, 18 opt-in skipped, 0 failed; 51 view contracts and whitespace checks passed. | Unassigned | — |
| 10/26/2026 | Completed the separately authorized borrowing-history CSV export repair: use asynchronous StreamWriter disposal under Kestrel. Download headers/content and formatting preserved. Dedicated real HTTP export regression and unfiltered Release suite passed: 371 passed, 18 opt-in tests skipped, 0 failed. Resolves the earlier UI acceptance CSV exception; existing WBS ownership/statuses unchanged. | Unassigned | — |
| 10/26/2026 | Completed phases 4–5 Custodian/Admin UI migration and phase 6 UI consistency checks on `feat/ui-design-migration`; existing functionality, fields, authorization and 10-record pagination preserved. Initial regression: 370 passed, 18 opt-in tests skipped; 41 role-page renders and 51 view contracts verified. The CSV exception was initially reported separately and subsequently resolved by the authorized repair above. See `doc/UI_MIGRATION.md`. | Unassigned | — |
| 10/24/2026 | Completed pagination refresh across all nine paginated lists: maximum 10 records, shared responsive pager, record ranges, numbered links and preserved filters. Release suite: 367 passed, 17 opt-in SQL tests skipped; additional rendered navigation test passed. Desktop/mobile browser checks passed using rendered integration-test pages. | Unassigned | — |
| 10/24/2026 | Completed removal of the disabled catalog-search placeholder from the shared header; verified no header search input on the Borrower, Custodian and Administrator dashboards. Working catalog filters and user-account search remain intact. | Unassigned | — |
| 10/24/2026 | Completed the responsive sidebar follow-up: removed the demo role switcher and visible sidebar scrollbar, bounded the card to the viewport and pinned profile/sign-out below a scrollable menu. CSS build and desktop/mobile/landscape browser checks passed; the separate existing `Startup.Auth.cs` edit still triggers the broader UI-only guard. | Unassigned | — |
| 10/24/2026 | Completed configurable standalone Custodian seed support for `--seed` and `--seed-demo`; existing accounts are preserved on rerun. All 15 seed tests passed. Configured local `CUST-001` and verified Custodian sign-in after creation through the running app's administrator workflow; standalone seed integration was unavailable because LocalDB failed to start in the execution environment. | Unassigned | — |
| 10/24/2026 | Completed UI migration phases 0–3 on `feat/ui-design-migration`: baseline, shared foundation, authentication and Borrower pages; presentation only. Release verification: 348 passed, 17 opt-in tests skipped; form contracts and scoped browser checks passed. Phases 4–6 await user instruction. See `doc/UI_MIGRATION.md`. | Unassigned | — |
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
| 11/5/2026 | Recorded the login toggle label repair under Additional work. WBS progress remains 8 Done, 33 In Review, 2 In Progress, 1 Not Started and 0 Blocked. | Codex |
| 11/5/2026 | Completed and verified the merged equipment image review fixes under Additional work; WBS counts remain 8 Done, 33 In Review, 2 In Progress, 1 Not Started and 0 Blocked. | Codex |
| 11/4/2026 | Recorded Assignment C final automated verification and the two still-pending live UI checks under Additional work. Added browser-script and missing-staged-upload regressions. WBS rows, progress counts, dates and owners unchanged; PR is pending. | Codex |
| 11/4/2026 | Replaced the raw account activation button and status badge with shared components/styles, and added theme-aware keyboard focus styling for the equipment upload area. UI acceptance and Release tests passed; WBS progress unchanged. | Codex |
| 11/4/2026 | Restored optional equipment image URL entry and edit behavior, preserving safe URL validation and file-over-URL precedence. Added HTTP coverage; Release suite passed with 389 passed, 26 skipped and 0 failed. WBS progress counts remain unchanged. | Codex |
| 11/4/2026 | Completed browser-side equipment image validation for file extension, MIME type, signature, decoding and dimensions; multi-file drag/drop now shows an error and rejects the selection. Node smoke checks and the Release test suite passed (388 passed, 26 skipped, 0 failed). WBS counts remain unchanged. | Codex |
| 11/4/2026 | Removed the Admin Shortcuts card per Assignment C follow-up and recorded the change under Additional work. Recounted the WBS summary; counts and WBS tracking remain unchanged. | Codex |
| 11/4/2026 | Recorded Assignment C implementation, test results and the pending direct Figma visual comparison under Additional work; WBS rows and progress counts unchanged. | Codex |
| 11/4/2026 | Completed live Light/Dark and responsive Users page checks at desktop, intermediate/tablet, and narrow-phone CSS viewport widths. Updated the Assignment C verification note; screenshots were inspected live but not saved as files. WBS rows and progress unchanged. | Codex |
| 11/3/2026 | Recorded the Assignment B schedule link fix under Additional work. | Codex |
| 11/3/2026 | Recorded the detailed calendar block count fix under Additional work. | Codex |
| 11/3/2026 | Recorded the Assignment B calendar wording fix under Additional work. | Codex |
| 11/3/2026 | Recorded the Assignment B calendar date fixes under Additional work. | Codex |
| 11/3/2026 | Recorded Assignment B under Additional work. The 44 WBS leaf rows still total 8 Done, 33 In Review, 2 In Progress, and 1 Not Started. | Codex |
| 11/3/2026 | Recorded the password control contrast fix under Additional work. | Codex |
| 11/3/2026 | Recorded the login error banner contrast fix under Additional work. | Codex |
| 11/2/2026 | Completed the School ID/upload-button contrast repair under Additional work. Recounted all 44 WBS leaf rows: 8 Done, 33 In Review, 2 In Progress, 1 Not Started. No WBS dates, owners, feature statuses or milestone checkboxes changed. | Codex |
| 11/2/2026 | Completed the transparent dark-logo follow-up under Additional work and restored/verified generated CSS. Recounted 44 WBS leaf rows: 8 Done, 33 In Review, 2 In Progress, 1 Not Started. No WBS dates, owners, statuses or checkboxes changed. | Codex |
| 11/2/2026 | Added the user-requested dark logo under Additional work and verified its theme switching. Recounted the 44 WBS leaf rows: 8 Done, 33 In Review, 2 In Progress, 1 Not Started; no WBS dates, owners, statuses or milestone checkboxes changed. | Codex |
| 11/2/2026 | Resolved the Assignment A contrast review finding and documented the regression/browser checks. Recounted WBS: 8 Done, 33 In Review, 2 In Progress, 1 Not Started; no WBS statuses, owners, actual/baseline dates or checkboxes changed. | Codex |
| 11/2/2026 | Finished and verified Assignment A under Additional work; published shared component/message/theme instructions for B/C/D. Recounted 44 WBS leaf rows: 8 Done, 33 In Review, 2 In Progress, 1 Not Started. No WBS owners, feature statuses, actual/baseline dates or milestone checkboxes changed. | Codex |
| 11/2/2026 | Started the authorized Assignment A implementation under Additional work in the original checkout; preserved planning/design/review files. WBS feature statuses, owners, baseline/actual dates and checkboxes unchanged; counts remain 8 Done, 33 In Review, 2 In Progress, 1 Not Started. | Codex |
| 10/31/2026 | Consolidated the UI refinement plan into four complete files for the user's four-person team. Assigned shared shell/CSS/toast infrastructure to one topic owner, kept module-specific integration/tests with each owner, and assigned final acceptance coordination to the custodian/reporting owner. Retained all ten detailed topic scopes; no implementation, WBS statuses, owners, dates or progress counts changed. | Codex |
| 10/31/2026 | Added the UI refinement plan and ten name-free assignment packets under Additional work, grounded in the current develop checkout and Figma reference. No application code, branches, PRs or feature statuses changed. Recounted WBS: 8 Done, 33 In Review, 2 In Progress, 1 Not Started; owners, actual/baseline dates and milestone checkboxes unchanged. | Codex |
| 10/30/2026 | Finished the PR #35 Ponytail re-review must-fix remediation (Additional work, `doc/Handoff.md` finding 1): reservation and return retries now create their records inside the retry attempt (fresh reservation / return plus late-return child), preventing a rolled-back insert from replaying its generated ID; added two SQL Server retry-after-rollback regressions (verified failing pre-fix). Verification: clean build 0 warnings, 404 passed / 1 UI skip / 0 failed with SQL Server tests enabled. No WBS feature statuses, owners or baseline dates changed. | opencode |
| 10/29/2026 | Finished the PR #35 should-fix review remediation (Additional work, `doc/Handoff.md` findings 6–9): school ID is now derived/read-only on Create and Edit (removed ignored inputs and validations); migration rollback backfills distinct `SchoolId` before its unique index; setup and architecture docs corrected; `ReservationResult`/`ReturnRecordResult` no longer shadow `ServiceResult.Succeeded`/`Message` (base-typed regression tests added). Verification: clean build 0 warnings, 399 passed / 1 UI skip / 0 failed with SQL Server tests enabled. No WBS feature statuses, owners or baseline dates changed. | opencode |
| 10/29/2026 | Finished the PR #35 must-fix review remediation (Additional work, `doc/Handoff.md` findings 1–5): restored `RolePermissionSeed` with a `RestoreRolePermissionSeed` migration and reseed regression test; restored exclusive administration locks on Serializable transactions for profile/user-administration writes plus a shared lock for returns; added a data-safety guard before the `SchoolId` drop; clear tracked state per retry attempt in equipment writes (with regression test); fixed the SQL Server test containers. Verification: clean build 0 warnings, 396 passed / 1 UI skip / 0 failed with SQL Server tests enabled. No WBS feature statuses, owners or baseline dates changed. | opencode |
| 10/29/2026 | Completed the full 5-phase codebase refactor (per `doc/Codebase Refactor Implementation Plan.md`, Additional work): dead code/backup artifacts/unused packages removed; user schema normalized (`IdNumber` alias on UserName, `BorrowerProfile.SchoolId` dropped, `RolePermissionSeed` and `AspNetUserLogins` dropped, migration applied to GearanteeDev); `DomainValues` moved to Resources, `PathManager` to WebApp, `DatabaseSeeder` to Data/Seeding; all 12 controllers inherit `ControllerBase<T>` with centralized PageDate; `IAccountService`, `IAuditService`, `IPasswordResetOtpService`, `IApplicationLockManager`, `ServiceResult` extracted; SQL lock SQL fully encapsulated in Data/Locks and repositories; services depend on `IUnitOfWork` (no concrete DbContext injection or casts). Verification: clean build 0 warnings, 371 passed / 0 failed / 23 opt-in SQL Server tests skipped. No WBS feature statuses, owners or baseline dates changed. | opencode |
| 10/28/2026 | Executed Option 1: deserialized administrative transactions to ReadCommitted, cleaned up dead no-ops and stale comments, guarded last-admin race via AspNetRoles UPDLOCK, and wrapped transactions in SqlServerRetryingExecutionStrategy. Verified full test suite (371 passed, 0 failed). No WBS status, owner, baseline date or progress-count changes. | Antigravity |
| 10/27/2026 | Prepared the Custodian/Admin UI continuation and authorized CSV repair for Git handoff on `feat/ui-design-migration`. Original authentication policy retained; separate login-copy/design/review edits excluded from staging. Release verification remains 371 passed, 18 opt-in skipped; contract checker now validates the staged index without local-edit exemptions. WBS counts, feature statuses, owners, dates and checkboxes unchanged. | Codex |
| 10/27/2026 | Addressed the Ponytail review finding by restoring baseline Identity lockout settings and requiring the complete original authentication file in the continuation checker. Recounted WBS: 8 Done, 33 In Review, 2 In Progress, 1 Not Started; feature statuses, owners, baseline dates and milestone checkboxes unchanged. | Codex |
| 10/26/2026 | Fixed the previously reported CSV export failure after explicit user authorization, using asynchronous writer disposal without changing report rules. Real Kestrel export regression and unfiltered Release suite passed (371 passed, 18 opt-in skipped); 51 view contracts and whitespace checks passed. Recounted WBS: 8 Done, 33 In Review, 2 In Progress, 1 Not Started; other members' rows, owners, baseline dates and milestone checkboxes unchanged. | Codex |
| 10/26/2026 | Completed the authorized Custodian/Admin presentation migration and scoped responsive/keyboard/validation checks in the original checkout. Recounted WBS: 8 Done, 33 In Review, 2 In Progress, 1 Not Started; no feature statuses, owners, baseline dates or milestone checkboxes changed. Recorded the existing CSV export failure separately; user explicitly retained UI-only scope, so unqualified full-system acceptance remains outstanding. | Codex |
| 10/25/2026 | Started the user-authorized UI migration phases 4–6 on `feat/ui-design-migration` in the original checkout. Existing local authentication/login-copy/design edits remain preserved; WBS rows, dates, owners and counts unchanged. | Codex |
| 10/25/2026 | Prepared the scoped UI migration and approved follow-ups for Git handoff on `feat/ui-design-migration`. Release validation: 368 passed, 17 opt-in SQL tests skipped; 22 view/9 pager contracts, asset consistency, EF model drift and NuGet vulnerability checks passed. Separate local authentication/login-copy edits and earlier design/review work remain excluded; WBS rows and counts unchanged. | Codex |
| 10/24/2026 | Completed the user-requested shared pagination UI and 10-record limit for catalog, reservations, review queue, returns, history, users, borrower profiles, equipment and categories. Recounted WBS: 8 Done, 33 In Review, 2 In Progress, 1 Not Started; no feature statuses, owners, baseline dates or milestone checkboxes changed. | Codex |
| 10/24/2026 | Removed the disabled catalog-search placeholder from the shared header at the user's request and verified all three role dashboards in the running app. No WBS status, owner, date or progress-count changes. | Codex |
| 10/24/2026 | Completed the user-requested sidebar cleanup and responsive checks in the running Visual Studio app; preserved navigation permissions and sign-out behavior. WBS counts, owners, dates and milestone checkboxes remain unchanged. | Codex |
| 10/24/2026 | Completed the Custodian seed addition, local setup documentation and 15 seed tests. Verified `CUST-001` login at the running Visual Studio app through the existing account-management workflow; direct LocalDB seed execution remains an environment limitation. No WBS status, owner, baseline date or progress-count changes. | Codex |
| 10/24/2026 | Started the user-requested Custodian seed addition under Additional work. Existing WBS counts, owners, dates and milestone checkboxes remain unchanged. | Codex |
| 10/24/2026 | Relocated the existing UI migration phases 0–3 into the original repository checkout on `feat/ui-design-migration` at the user's request; preserved earlier local design/review work and tracker notes. No WBS statuses, owners, dates, milestone checkboxes or progress counts changed. | Codex |
| 10/24/2026 | Started the approved UI-only migration phases 0–3 under Additional work. Baseline: 348 tests passed, 17 opt-in SQL Server tests skipped. No WBS feature statuses, baseline dates, owners or milestone checkboxes changed. | Codex |
| 10/24/2026 | Completed the scoped UI migration phases 0–3 and recorded verification in `doc/UI_MIGRATION.md`: 348 tests passed, 17 skipped, view contracts preserved, real MVC/SQL-backed Borrower and authentication checks passed. Awaiting user instruction for phases 4–6; WBS counts and feature statuses remain unchanged. | Codex |
| 9/25/2026 | Added WBS Feature Tracker; marked 1.00 and 3.00 Done | Laroco |
| 9/28/2026 | Moved WBS 9.00–12.00 to In Review for PR #19 and aligned password-reset documentation with the OTP/Brevo implementation | Bansag |
| 9/29/2026 | Added SQL-backed dashboards, shared responsive shell, Tailwind build, and demo seeding for WBS 5.00–7.00 | Laroco |
| 9/30/2026 | Addressed PR #21 review findings TEST-01, OPS-01, UX-01, and P3-01–P3-05 | Laroco |
| 10/1/2026 | Removed the Location entity and updated the schema, dashboards, demo seed, and ERD for item location text | Laroco |
| 10/1/2026 | Marked WBS 5.00–7.00 Done using owner-confirmed 9/25 start and 9/28 completion dates; PR #21 merged on 10/1 | Laroco |
| 10/1/2026 | Started WBS 14.00–18.00 implementation for user accounts, role assignment, permissions, and account activation management | Laroco |
| 10/1/2026 | Implemented the user-administration screens, Identity workflows, permission controls, SQL Server safeguards, documentation, and regression tests; awaiting PR review before advancing WBS status | Laroco |
| 10/2/2026 | Moved WBS 14.00–18.00 to In Review for PR #22; addressed REQ-01 with atomic CSV account creation, UX/AUDIT-01 with trusted edit redisplay and no-op activation handling, and ROLE-EDGE-01 with inactive-admin demotion; added regression tests and import instructions | Laroco |
| 10/3/2026 | Started WBS 30.00–32.00 (Borrower Profile Management: view, update, eligibility) | Canencia |
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
| 10/14/2026 | Started WBS 52.00–55.00 implementation for searchable, filterable borrowing history and report generation | Bansag |
| 10/14/2026 | Moved WBS 52.00–55.00 to In Review for PR #29; completed local build/test verification, generated Tailwind CSS, and updated the vulnerable XML cryptography dependency | Bansag |
| 10/14/2026 | Started WBS 49.00–50.00 availability calendar and equipment/category/location filters | Laroco |
| 10/14/2026 | Moved WBS 30.00–32.00 to In Review for PR #31; added borrower-profile service and authorization tests | Canencia |
| 10/17/2026 | Started WBS 39.00–42.00 reservation approval and release: custodian review queue, approve/reject with a required reason, and equipment release recorded under the item lock | Canencia |
| 10/17/2026 | Merged `staging` (PR #31) into the review branch; WBS 30.00–32.00 verified and moved to Done | Canencia |
| 10/17/2026 | Started WBS 44.00–47.00: custodian return desk with On loan/Overdue/Returned/Late tabs, return time and receiver, condition and resulting item status (damaged items never auto-Available), automatic late-return records with whole days late, back-dated returns that require a reason note, and SQL Server race tests | Cataraja |
| 10/17/2026 | Moved WBS 44.00–47.00 to In Review for PR #32; fixed a SQL Server deadlock when two returns are recorded at once and added opt-in SQL Server race tests | Cataraja |
| 10/17/2026 | Addressed PR #32 review AUTH-01 for WBS 44.00–47.00 (still In Review): the return permission re-check now runs inside the return transaction under the shared administration lock (Shared mode, before the equipment lock); added SQL Server revocation race tests | Cataraja |
| 10/17/2026 | Moved WBS 39.00–42.00 to In Review for PR #33: custodian review queue, approve/reject with a required reason, and equipment release recorded under the item lock; 20 new tests | Canencia |
