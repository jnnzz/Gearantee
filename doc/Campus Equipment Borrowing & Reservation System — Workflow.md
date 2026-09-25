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

The system uses **ASP.NET Core Identity** for password recovery.

```mermaid
flowchart TD
    A[User Selects Forgot Password] --> B[Enter Registered Email]
    B --> C{Email Exists?}

    C -->|No| D[Display Generic Response]
    C -->|Yes| E[ASP.NET Core Identity Generates Reset Token]

    E --> F[Send Password Reset URL]
    F --> G[User Opens Reset URL]
    G --> H{Token Valid?}

    H -->|No| I[Display Expired or Invalid Link]
    H -->|Yes| J[Enter New Password]

    J --> K[Validate Password]
    K -->|Invalid| J
    K -->|Valid| L[Update Password]
    L --> M[Password Reset Complete]
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

The borrowing history includes completed, cancelled, and overdue transactions and supports searching, filtering, and report generation.

---

## 11. Availability Calendar Workflow

```mermaid
flowchart TD
    A[Open Availability Calendar] --> B[Select Date / Week]

    B --> C[Load Equipment Schedule]

    C --> D[Check Reservations]
    D --> E[Check Active Borrowings]
    E --> F[Check Maintenance Status]

    F --> G[Display Timeline]

    G --> H{Apply Filters?}

    H -->|No| I[Display All Equipment]
    H -->|Yes| J[Filter by Category]
    J --> K[Filter by Equipment]
    K --> L[Filter by Location]

    L --> M[Display Filtered Timeline]
```

The calendar provides a daily/weekly view of equipment availability, reservations, active borrowing, and maintenance status.

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

**Important:** The password-reset URL/token mechanism is an authentication implementation detail and does **not** introduce a custom entity into the business ERD.

ASP.NET Core Identity stores the account in `AspNetUsers` through the `ApplicationUser` model. School-specific borrowing fields and eligibility are stored separately in `BORROWER_PROFILE`.
