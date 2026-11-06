# Campus Equipment Borrowing & Reservation System
## System Workflow

---

## 1. Authentication Workflow

```mermaid
flowchart TD
    A[User Opens System] --> B[Login Page]
    B --> C{Enter Credentials}
    C -->|Valid| D[Authenticate User]
    C -->|Invalid| E[Display Login Error]
    E --> B

    D --> F{Identify User Role}

    F -->|Borrower| G[Borrower Dashboard]
    F -->|Custodian| H[Custodian Dashboard]
    F -->|Administrator| I[Administrator Dashboard]
```

### Password Reset

The system uses **ASP.NET Core Identity** for password recovery and Brevo for OTP delivery.

```mermaid
flowchart TD
    A[User Selects Forgot Password] --> B[Enter Registered Email]
    B --> C{Email Exists?}

    C -->|No| D[Display Generic Response]
    C -->|Yes| E[Generate and hash six-digit OTP]

    E --> F[Send OTP through Brevo]
    F --> G[User enters OTP]
    G --> H{OTP valid and within limits?}

    H -->|No| I[Display Generic Error]
    H -->|Yes| J[Consume OTP and create Identity reset token]

    J --> K[Enter New Password]
    K --> L[Validate Password]
    L -->|Invalid| K
    L -->|Valid| M[Update Password]
    M --> N[Password Reset Complete]
```

> **Database Note:** No custom `PASSWORD_RESET`, `OTP_CODE`, or `PASSWORD_RESET_TOKEN` table is required. The reset process is handled by ASP.NET Core Identity.

---

## 2. Borrower Workflow

The borrower workflow covers searching equipment, submitting reservations, monitoring reservation status, cancelling reservations, receiving equipment, and returning equipment.

```mermaid
flowchart TD
    A[Borrower Login] --> B[Borrower Dashboard]
    B --> C[Search Available Equipment]

    C --> D[Filter Equipment]
    D --> E[View Equipment Details]

    E --> F[Select Equipment]
    F --> G[Select Borrowing Date & Time]
    G --> H[Enter Purpose]

    H --> I{Schedule Conflict?}

    I -->|Yes| J[Display Conflict]
    J --> C

    I -->|No| K[Submit Reservation]

    K --> L[Reservation Status: Pending]
    L --> M[Wait for Custodian Review]

    M --> N{Reservation Decision}

    N -->|Rejected| O[View Rejection Reason]
    O --> P[End]

    N -->|Approved| Q[Reservation Status: Approved]

    Q --> R[Wait for Equipment Release]
    R --> S[Equipment Released]
    S --> T[Equipment Status: Borrowed]

    T --> U[Use Equipment]
    U --> V[Return Equipment]

    V --> W[Equipment Inspection]
    W --> X{Equipment Condition}

    X -->|Good| Y[Equipment Status: Available]
    X -->|Damaged| Z[Equipment Status: Under Maintenance]

    Y --> AA[Borrowing Transaction Completed]
    Z --> AA

    AA --> AB[View Borrowing History]
```

---

## 3. Equipment Search & Availability Workflow

```mermaid
flowchart TD
    A[Borrower Opens Equipment Catalog] --> B[Search Equipment]

    B --> C[Filter by Category]
    C --> D[Filter by Name / Model]
    D --> E[Select Desired Date & Time]

    E --> F[Check Equipment Status]
    F --> G[Check Existing Reservations]
    G --> H[Check Active Borrowing Transactions]

    H --> I{Available?}

    I -->|Yes| J[Display as Available]
    I -->|No| K[Display as Unavailable]

    J --> L[Select Equipment]
    K --> B
```

The availability check should consider both the equipment's current status and conflicting reservation schedules.

> **Version 1 data rule:** Each reservation references exactly one physical equipment item. A borrower who needs several items submits one reservation per item.

---

## 4. Reservation Workflow

```mermaid
flowchart TD
    A[Select Equipment] --> B[Enter Reservation Details]

    B --> C[Select Start Date & Time]
    C --> D[Select End Date & Time]
    D --> E[Enter Purpose]

    E --> F[Validate Reservation]

    F --> G{Borrower Eligible?}

    G -->|No| H[Reject Submission]
    H --> I[Display Eligibility Message]

    G -->|Yes| J{Equipment Available?}

    J -->|No| K[Display Schedule Conflict]
    K --> B

    J -->|Yes| L[Create Reservation]
    L --> M[Status: Pending]

    M --> N[Notify Custodian]
```

---

## 5. Reservation Approval Workflow

```mermaid
flowchart TD
    A[Custodian Login] --> B[Custodian Dashboard]

    B --> C[View Reservation Requests]
    C --> D[Select Reservation]

    D --> E[Review Borrower]
    E --> F[Review Equipment]
    F --> G[Review Date & Time]
    G --> H[Review Purpose]

    H --> I{Decision}

    I -->|Approve| J[Confirm Reservation]
    J --> K[Status: Approved]
    K --> L[Notify Borrower]

    I -->|Reject| M[Enter Rejection Reason]
    M --> N[Status: Rejected]
    N --> O[Notify Borrower]
```

The requirements specify that rejection should include a mandatory explanation visible to the applicant.

---

## 6. Equipment Release Workflow

```mermaid
flowchart TD
    A[Approved Reservation] --> B[Borrower Arrives for Pickup]

    B --> C[Custodian Verifies Reservation]
    C --> D[Verify Borrower]
    D --> E[Verify Equipment]

    E --> F{Valid Reservation?}

    F -->|No| G[Do Not Release]
    F -->|Yes| H[Record Equipment Release]

    H --> I[Record Custodian ID]
    I --> J[Record Release Timestamp]
    J --> K[Set Equipment Status: Borrowed]

    K --> L[Release Equipment to Borrower]
```

The release transaction records the custodian, actual release timestamp, and changes the equipment status to **Borrowed**.

---

## 7. Equipment Return Workflow

```mermaid
flowchart TD
    A[Borrower Returns Equipment] --> B[Custodian Receives Equipment]

    B --> C[Record Return Date & Time]
    C --> D[Inspect Equipment]

    D --> E{Condition}

    E -->|Good| F[Condition: Good]
    E -->|Damaged| G[Condition: Damaged]
    E -->|Needs Inspection| H[Condition: Needs Inspection]

    F --> I[Set Status: Available]

    G --> J[Set Status: Under Maintenance]
    H --> K[Set Status: Under Maintenance]

    I --> L{Returned Late?}
    J --> L
    K --> L

    L -->|No| M[Complete Transaction]
    L -->|Yes| N[Create Late Return Record]

    N --> O[Record Days Late]
    O --> P[Record Penalty / Infraction]
    P --> M
```

The system identifies late returns and records associated penalties or infractions against the borrower's record.

---

## 8. Equipment Status Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Available

    Available --> Available : Future Reservation Approved
    Available --> Borrowed : Equipment Released

    Borrowed --> Available : Returned in Good Condition
    Borrowed --> UnderMaintenance : Damaged / Needs Inspection

    UnderMaintenance --> Available : Repair Completed

    Available --> Archived : Equipment Archived
    UnderMaintenance --> Archived : Equipment Archived
```

### Main Statuses

| Status | Description |
|---|---|
| **Available** | Equipment can be reserved or released. |
| **Borrowed** | Equipment has been released to a borrower. |
| **Under Maintenance** | Equipment is damaged or requires inspection/repair. |
| **Archived** | Equipment is no longer available for new transactions. |

An approved reservation blocks its scheduled time range but does not permanently change the item's current physical status to `Reserved`. The availability service combines the operational status with approved reservation ranges and active releases.

---

## 9. Late Return Workflow

```mermaid
flowchart TD
    A[Equipment Return] --> B[Compare Actual Return Time]
    B --> C[Compare With Expected Return Time]

    C --> D{Past Due?}

    D -->|No| E[Normal Return]
    E --> F[Complete Transaction]

    D -->|Yes| G[Flag Late Return]
    G --> H[Calculate Days Late]
    H --> I[Record Late Return]

    I --> J[Record Penalty / Infraction]
    J --> K[Update Borrower Record]

    K --> L[Complete Transaction]
```

---

## 10. Borrowing History Workflow

```mermaid
flowchart TD
    A[User Opens Borrowing History] --> B[Load Transaction Records]

    B --> C{Search / Filter?}

    C -->|No| D[Display History]
    C -->|Yes| E[Search by Borrower / ID / Item]

    E --> F[Apply Date Filter]
    F --> G[Apply Category Filter]
    G --> H[Apply Status Filter]
    H --> I[Apply Late Return Filter]

    I --> J[Display Filtered Results]

    D --> K{Generate Report?}
    J --> K

    K -->|No| L[End]
    K -->|Yes| M[Generate Borrowing Report]
```

The borrowing history includes reservation, release, return, cancelled, and overdue records. Search covers borrower name, user code, school ID, item name, and item code. Filters include borrower, item/category, scheduled borrowing-start date range, status, and late-return status. Results are paginated and displayed in Philippine time. Authorized users can download a CSV containing all matching filtered records; the download uses the same access scope and filters as the page.

---

## 11. Availability Calendar Workflow

```mermaid
flowchart TD
    A[Open Availability Calendar] --> B[Select Day / Week]

    B --> C[Filter by Category, Equipment, and Location]

    C --> D[Load Matching Equipment and Schedule]

    D --> E[Check Approved Reservations and Active Loans]
    E --> F[Display Reserved, Borrowed, and Overdue Times]
    F --> G[Show Current Maintenance / Unavailable State]

    G --> H{Borrower selects an open future interval?}

    H -->|No| I[Continue Reviewing Schedule]
    H -->|Yes| J[Open Reservation Form for Selected Item and Time]
    J --> K[Recheck Eligibility and Conflicts Before Saving]
```

The calendar is read-only; the reservation service remains responsible for final eligibility and conflict checks. Equipment location is filtered as free text stored on each item. The calendar does not show borrower identity or request purpose. Maintenance and unavailable items have no recorded schedule end date, so the calendar shows their current state without implying a repair completion time.

---

## 12. Administrator Workflow

```mermaid
flowchart TD
    A[Administrator Login] --> B[Administrator Dashboard]

    B --> C[User Management]
    B --> D[Equipment Management]
    B --> E[Category Management]
    B --> F[Role & Permission Management]

    %% User Management
    C --> C1[View User Accounts]
    C1 --> C2[Create Account]
    C1 --> C3[Bulk Account Creation]
    C1 --> C4[Update User]
    C1 --> C5[Assign / Update Role]
    C1 --> C6[Deactivate User]

    %% Equipment Management
    D --> D1[View Equipment]
    D1 --> D2[Register Equipment]
    D1 --> D3[Update Equipment]
    D1 --> D4[Archive Equipment]

    %% Category Management
    E --> E1[View Categories]
    E1 --> E2[Create Category]
    E1 --> E3[Update Category]
    E1 --> E4[Deactivate Category]

    %% Permissions
    F --> F1[View Roles]
    F1 --> F2[Configure Permissions]
    F2 --> F3[Save Role Access]
```

The administrator functions include account creation, bulk CSV account creation, role assignment, permission configuration, user deactivation, and equipment/category management.

Bulk account creation is available from **Users & Roles → Import CSV** under WBS 15.00. An authorized active administrator uploads a UTF-8 CSV containing at most 100 accounts (1 MiB). All rows must pass validation; the complete batch, including role assignments, borrower profiles, and audit records, commits in one transaction or rolls back together. New borrower profiles start ineligible. Uploads contain initial passwords and are processed in memory without retaining the original file or including passwords in errors/audit records. See [CSV import instructions](USER_CSV_IMPORT.md) for the template and operational details.

---

## 13. Complete System Workflow

The following represents the **high-level workflow of the entire system**:

```mermaid
flowchart TD

    START([Start]) --> LOGIN[Login]

    LOGIN --> AUTH{Authenticated?}

    AUTH -->|No| LOGIN
    AUTH -->|Yes| ROLE{User Role}

    %% =========================
    %% BORROWER
    %% =========================

    ROLE -->|Borrower| BD[Borrower Dashboard]

    BD --> SEARCH[Search Available Equipment]
    SEARCH --> SELECT[Select Equipment]
    SELECT --> RESERVE[Create Reservation]

    RESERVE --> CHECK{Available & Eligible?}

    CHECK -->|No| SEARCH
    CHECK -->|Yes| PENDING[Reservation Pending]

    PENDING --> REVIEW[Custodian Reviews]

    REVIEW --> DECISION{Approved?}

    DECISION -->|No| REJECTED[Reservation Rejected]
    REJECTED --> BD

    DECISION -->|Yes| APPROVED[Reservation Approved]
    APPROVED --> RELEASE[Equipment Release]

    RELEASE --> BORROWED[Equipment Borrowed]
    BORROWED --> RETURN[Equipment Returned]

    RETURN --> INSPECT[Inspect Condition]

    INSPECT --> CONDITION{Condition}

    CONDITION -->|Good| AVAILABLE[Equipment Available]
    CONDITION -->|Damaged| MAINT[Under Maintenance]
    CONDITION -->|Needs Inspection| MAINT

    AVAILABLE --> HISTORY[Borrowing History]
    MAINT --> HISTORY

    RETURN --> LATE{Late Return?}
    LATE -->|Yes| PENALTY[Record Penalty / Infraction]
    LATE -->|No| HISTORY
    PENALTY --> HISTORY

    %% =========================
    %% CUSTODIAN
    %% =========================

    ROLE -->|Custodian| CD[Custodian Dashboard]

    CD --> REQUESTS[View Reservation Requests]
    REQUESTS --> REVIEW

    CD --> RELEASE
    CD --> RETURN

    %% =========================
    %% ADMIN
    %% =========================

    ROLE -->|Administrator| AD[Administrator Dashboard]

    AD --> USERS[Manage Users]
    AD --> EQUIPMENT[Manage Equipment]
    AD --> CATEGORIES[Manage Categories]
    AD --> ROLES[Manage Roles & Permissions]
    AD --> REPORTS[View Reports]

    %% =========================
    %% PASSWORD RESET
    %% =========================

    LOGIN --> FORGOT[Forgot Password]
    FORGOT --> EMAIL[Enter Registered Email]
    EMAIL --> TOKEN[ASP.NET Core Identity Reset URL]
    TOKEN --> NEWPASS[Set New Password]
    NEWPASS --> LOGIN
```

---

## 14. Core Business Process

The most important business process can be summarized as:

```text
Borrower
   │
   ▼
Search Equipment
   │
   ▼
Create Reservation
   │
   ▼
Reservation Pending
   │
   ▼
Custodian Review
   │
   ├─────────────── Reject ──────► End
   │
   ▼
Approve Reservation
   │
   ▼
Equipment Release
   │
   ▼
Equipment Borrowed
   │
   ▼
Equipment Return
   │
   ▼
Condition Inspection
   │
   ├── Good ─────────────► Available
   │
   └── Damaged ──────────► Under Maintenance
   │
   ▼
Check Late Return
   │
   ├── Not Late ─────────► Complete
   │
   └── Late ─────────────► Record Penalty
                              │
                              ▼
                           Complete
```

## 15. Actors

| Actor | Main Responsibilities |
|---|---|
| **Borrower** | Search equipment, submit reservations, view reservation status, cancel reservations, borrow and return equipment, view borrowing history. |
| **Custodian** | Review reservations, approve/reject requests, release equipment, receive returns, inspect equipment, and record late returns. |
| **Administrator** | Manage users, roles, permissions, equipment, categories, and system administration. |

## 16. Authentication Architecture

```text
                    ┌──────────────────────┐
                    │  APPLICATION USER    │
                    └──────────┬───────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │ ASP.NET Core Identity│
                    └──────────┬───────────┘
                               │
                ┌──────────────┴──────────────┐
                │                             │
                ▼                             ▼
          Login / Roles               Password Reset
                │                             │
                │                    Cryptographic Token
                │                             │
                │                             ▼
                │                       Reset URL
                │                             │
                ▼                             ▼
        Application Access              New Password
```

**Important:** The password-reset OTP/token mechanism is an authentication implementation detail and does **not** introduce a custom entity into the business ERD.

ASP.NET Core Identity stores the account in `AspNetUsers` through the `ApplicationUser` model. School-specific borrowing fields and eligibility are stored separately in `BORROWER_PROFILE`.


## Workspace refinement — 11/6/2026

New reservation requests use full Philippine calendar days. Start must be tomorrow or later; End may equal Start and includes the whole selected day. Store Start at Manila midnight and End at midnight after the inclusive End date, converted to UTC, with IsFullDay=true. Conflict checks retain half-open intervals [start, end). Existing timed reservations remain unchanged (IsFullDay=false). Releases and actual returns retain timestamps. Scheduled-date labels show the inclusive End for full-day records; overdue and late-return calculations continue using the stored deadline.

My borrowing history supports reservation-code, item-code and item-name search within the signed-in borrower's records, combined with status tabs and 10-row pagination. Custodian Quick Access links open Equipment and Reports under existing permissions.

The calendar uses equipment rows with Week/Day navigation, equipment search and an optional category filter. Location, equipment and view dropdowns are removed. Available future days link to date-only requests for authorized borrowers; submission still rechecks eligibility and conflicts. Existing events stay private; borrower identity is not exposed. Maintenance is current state with no promised completion date.

Equipment and Categories use separate tabs. Delete soft-deletes equipment through the existing archive flag, preserves history and existing transaction restrictions, and retains Restore. New item codes are server-assigned as CATEGORY-001-ASU: a persistent per-category counter, at least three digits, plus the first three letters of the item name uppercased and padded with X if needed. Allocation and insertion share a serializable transaction and category-row lock; the first allocation advances past existing matching codes including archived items. Existing item codes stay unchanged on edits.

Login has no appearance control. Authenticated pages offer a Light/Dark icon toggle; legacy System preferences default to Light. Reports retain both date filters and CSV export, remove the separate item filter and Excel download control. Existing Excel endpoint remains available for compatibility.
