# Campus Equipment Borrowing & Reservation System

## 1. Project Overview

Campus Equipment is a web-based system for managing school-owned equipment such as laptops, projectors, cameras, speakers, and laboratory tools. It gives students and faculty a way to check availability and request equipment, while custodians and administrators manage approvals, releases, returns, records, and access.

The initial release is intentionally focused on the core borrowing workflow and does not require advanced third-party integrations.

### 1.1 Implementation Architecture

- **Web architecture:** ASP.NET Core MVC with controllers and Razor Views.
- **Database:** Microsoft SQL Server accessed through Entity Framework Core and `Microsoft.EntityFrameworkCore.SqlServer`.
- **Database administration:** SQL Server Management Studio (SSMS) may be used to inspect and administer SQL Server, but SSMS is not the database engine.
- **Authentication:** ASP.NET Core Identity with Identity roles and policy-based permissions.
- **Password recovery:** Six-digit OTP delivery through Brevo, with Identity generating the final password-reset token after OTP verification.
- **Styling:** Tailwind CSS compiled into a production stylesheet under `wwwroot`, plus minimal custom CSS where required.
- **Reservation scope:** One physical equipment item per reservation in Version 1.

## 2. User Roles

| Role | Main purpose | Primary access |
| --- | --- | --- |
| Borrower | Student or faculty member requesting equipment | Browse availability, submit and view reservations, view own borrowing history |
| Custodian | Staff member responsible for equipment handover and return checks | Review requests, approve/reject, release items, process returns and condition checks |
| Administrator | System manager | Manage users, roles, categories, equipment records, and reports |

## 3. Functional Breakdown

### 3.1 Authentication

#### User Login
- Users sign in with their School ID (the canonical account UserName) and password. Email login is not supported.
- The system validates credentials before granting access.
- Unauthenticated users cannot use protected system features.

#### Role-Based Dashboard
- After sign-in, the system opens the dashboard appropriate to the user’s role.
- Borrowers see available equipment, their requests, and their current borrowings.
- Custodians see pending approval, release, and return tasks.
- Administrators see management and reporting functions.

#### Password Reset Request
- A user can request a six-digit OTP using their School ID. The server sends it to that account’s registered email without exposing the email in the response.
- The OTP is hashed and stored in the existing ASP.NET Core Identity user-token table for ten minutes, with a maximum of five checked attempts.
- OTP attempts are reserved atomically so concurrent requests cannot bypass the attempt limit.
- Requests are rate-limited and resend requests use a per-account cooldown and hourly issuance cap.
- The response must not reveal whether the address exists.
- After successful OTP verification, ASP.NET Core Identity creates a short-lived reset token that is submitted in the reset form rather than placed in the URL.

### 3.2 Master Data Management

#### Equipment Category Management
- Administrators can create, view, edit, and deactivate equipment categories.
- Example categories: Laptop, Projector, Camera, Speaker, Laboratory Tool.
- Every equipment item belongs to one category so inventory can be organized and filtered consistently.

#### Equipment Item Management
- Administrators can register and maintain each borrowable item.
- Each record should include: item code, category, name/model, serial number, catalog image, condition, current status, and storage/assigned location.
- The borrower catalog displays the item's catalog image when one has been uploaded; the image is optional and a default placeholder is shown when no image exists.
- An item can be marked unavailable when under maintenance, lost, retired, or otherwise not borrowable.

#### Borrower Profile Management
- Administrators maintain borrower records for students and faculty.
- Profile details include name, ID number, department, contact number, email address, and eligibility status.
- Ineligible or inactive borrowers cannot place new requests.

### 3.3 Borrowing Transactions

#### Equipment Reservation
- Borrowers search and filter available equipment by category, name, and date.
- They select one item, intended borrowing date/time, return date/time, and purpose of use.
- The system creates a reservation request and prevents conflicting reservations for the same item and time period.
- A borrower who needs multiple items submits one reservation per item.
- Borrowers can view the status of their submitted requests.

#### Approval and Release
- Custodians review pending reservations and approve or reject each request.
- A rejection should store a reason visible to the borrower.
- For approved requests, the custodian records the actual release date/time and marks the item as released/borrowed.
- Releasing an item updates its current availability so it cannot be reserved by another borrower during the active loan.

#### Return and Condition Check
- Custodians record the actual return date/time when an item is returned.
- They inspect the item and record its returned condition.
- The item status is updated to available, under maintenance, or another appropriate status.
- Late returns and damaged items are flagged for follow-up and reporting.

### 3.4 Reports and Visibility

#### Borrowing History Report
- Users with `history.view` can review reservation and loan history; borrower-role users are restricted to their own records.
- Search covers borrower name, user code, school ID, item name, and item code. Filters include borrower, item text, category, borrowing-start date range, status, and late-return status.
- The paginated page shows requested, scheduled, actual release, and actual return times in Philippine time, along with return condition and late days when recorded.
- Authorized users can export all matching filtered records to CSV. The export uses the same server-side filters as the page and protects text values from spreadsheet formula injection.
- Authorized users can also download all matching records as an XLSX workbook. It keeps the CSV's 17-column order, stores identifiers and user values as literal text, writes Manila timestamps as formatted date cells, and uses explicit column widths, wrapped long text, and a frozen header row. Borrower-role exports remain scoped to the signed-in borrower.

#### Availability Calendar
- Users with `equipment.browse` can view a daily or weekly calendar filtered by category, equipment item, and its free-text location.
- The calendar displays approved reservations, active loans, and overdue loans in Philippine time; it shows current maintenance/unavailable state in the equipment list without inventing a repair date.
- Select an equipment item to see its schedule. Borrowers with `reservation.create` can select an open future interval to continue to the existing reservation form; the reservation service rechecks eligibility and conflicts before saving.
- Calendar results contain schedule and equipment details only, never another borrower's identity or request purpose.

### 3.5 Administration

#### User and Role Management
- Administrators create, edit, activate/deactivate, and assign roles to user accounts.
- Roles control which dashboard, pages, actions, and data each user can access.
- Deactivating an account prevents login while retaining transaction history for audit purposes.
- The Users & Roles page provides searchable, filterable, paginated accounts and a role-to-permission matrix.
- New accounts use ASP.NET Core Identity; borrower-profile fields are stored separately and borrower eligibility starts disabled.
- Current database authority is checked on administrative writes; self-deactivation, self-demotion from Administrator, and removal of the last active Administrator are blocked.
- Account, role, permission, and activation changes are recorded with actor and timestamp; secrets are excluded from the audit record.

## 4. Core Workflow

1. An administrator configures users, borrower profiles, equipment categories, and equipment items.
2. A borrower logs in, checks the availability calendar, and submits a reservation request.
3. A custodian reviews the request and approves or rejects it.
4. When the borrower collects an approved item, the custodian records the release.
5. When the item is returned, the custodian records the return and condition check.
6. The system updates the item’s availability and preserves the full transaction history for reports.

## 5. Main Statuses

| Area | Suggested statuses |
| --- | --- |
| Reservation | Pending, Approved, Rejected, Cancelled, Expired |
| Loan | Awaiting Release, Borrowed, Returned, Overdue |
| Equipment operational state | Available, Borrowed, Under Maintenance, Unavailable, Archived |
| Schedule availability | Calculated from approved reservation ranges and active releases rather than stored as a permanent item status |
| Return condition | Good, Damaged, Needs Inspection, Under Repair |

## 6. Key Business Rules

- Only active, eligible borrowers may create reservations.
- A reservation requires an available item and a valid return date/time after its release date/time.
- One item cannot have overlapping approved reservations or active loans.
- Inventory edits/archives and reservation creation/approval must coordinate through the same per-equipment SQL Server lock. Each workflow must use a `SERIALIZABLE` transaction, acquire the equipment-row lock before checking reservation state, then validate and write before committing. This prevents either workflow from validating against stale inventory state.
- Only custodians and administrators can approve, reject, release, or complete a return.
- Equipment marked as unavailable or under maintenance cannot be reserved or released.
- Every approval, rejection, release, return, and status change should retain the acting user and timestamp for audit history.
- A returned item with a damaged condition must not automatically become available until its status is reviewed.

## 7. Initial Data Entities

| Entity | Purpose | Important fields |
| --- | --- | --- |
| Application User (ASP.NET Core Identity) | Login and account control | Identity user ID, user code, name, email, active status; Identity owns the password hash and security fields |
| Borrower Profile | Student/faculty borrowing record | Borrower profile ID, Identity user ID, school ID, department, contact details, eligibility |
| Identity Role / User Role | Role assignment | Identity role ID/name and user-role association |
| Permission / Role Permission | Fine-grained authorization | Permission name and role-permission association |
| Administration Audit Event | Administrative accountability | Actor, target account/role, action, safe before/after details, UTC timestamp |
| Role Permission Seed | Preserve configured access | Identity role whose initial permissions have been provisioned |
| Equipment Category | Equipment grouping | Category ID, name, active status |
| Equipment Item | Individual trackable item | Item ID/code, category, free-text `VARCHAR(200)` location, name/model, serial number, image URL/path, condition, operational status |
| Reservation | Requested schedule for exactly one item | Reservation ID, borrower profile, equipment item, purpose, requested release/return dates, status, reviewer |
| Release Record | Confirmation of handover | Release ID, reservation, custodian, actual release time, notes |
| Return Record | Confirmation of return and inspection | Return ID, release/reservation, custodian, actual return time, condition, notes |
| Late Return | Recorded overdue outcome | Late-return ID, return record, due/returned times, days late, penalty status |

## 8. Suggested Screens

| Screen | Users | Purpose |
| --- | --- | --- |
| Login / Password Reset | All users | Authenticate and regain account access |
| Borrower Dashboard | Borrower | View available items, current requests, and active loans |
| Custodian Dashboard | Custodian | Act on pending requests, releases, and returns |
| Administrator Dashboard | Administrator | Monitor inventory, users, and reporting shortcuts |
| Equipment Catalog | Borrower, Custodian, Administrator | Search and view item availability |
| Reservation Form | Borrower | Submit an equipment request |
| Approval & Release Queue | Custodian, Administrator | Approve/reject requests and record handover |
| Return & Condition Check | Custodian, Administrator | Record return and item condition |
| Categories and Equipment | Administrator | Maintain inventory master data |
| Borrower Profiles | Administrator | Maintain borrower records and eligibility |
| Users and Roles | Administrator | Maintain accounts and permissions |
| Reports / Availability Calendar | Authorized users | Review history and scheduling availability |

## 9. Scope for the First Website Version

The first website should implement these functions with ASP.NET Core MVC and Razor Views, a responsive Tailwind CSS interface, role/permission-protected access, and a SQL Server-backed audit trail. Out of scope unless later requested: online payment/penalties, external school-information-system synchronization, barcode scanning, SMS notifications, and multi-campus inventory transfers. Password-reset OTP delivery through Brevo remains in scope.
