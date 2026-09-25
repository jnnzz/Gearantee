# Canonical Entity Relationship Diagram

This is the simplified visual reference for the Version 1 Campus Equipment Borrowing & Reservation System. The authoritative field definitions and SQL Server constraints are in `Campus_Equipment_Borrowing_ERD_Data_Dictionary.md`.

## Design Decisions

- ASP.NET Core Identity manages users, password hashes, roles, sign-in, and reset tokens.
- `ApplicationUser` extends Identity with application-specific account fields.
- `BorrowerProfile` stores school identity, department, contact, and borrowing eligibility.
- Each reservation contains exactly one equipment item.
- A borrower requests multiple items by creating multiple reservations.
- SQL Server is the database engine; SSMS is an optional administration tool.

```mermaid
erDiagram
    IDENTITY_ROLE ||--o{ USER_ROLE : contains
    APPLICATION_USER ||--o{ USER_ROLE : receives
    IDENTITY_ROLE ||--o{ ROLE_PERMISSION : grants
    PERMISSION ||--o{ ROLE_PERMISSION : assigned_through

    APPLICATION_USER ||--o| BORROWER_PROFILE : has
    EQUIPMENT_CATEGORY ||--o{ EQUIPMENT_ITEM : classifies
    LOCATION ||--o{ EQUIPMENT_ITEM : stores

    BORROWER_PROFILE ||--o{ RESERVATION : creates
    EQUIPMENT_ITEM ||--o{ RESERVATION : is_requested_in
    APPLICATION_USER ||--o{ RESERVATION : reviews

    RESERVATION ||--o| RELEASE_RECORD : results_in
    APPLICATION_USER ||--o{ RELEASE_RECORD : releases
    RELEASE_RECORD ||--o| RETURN_RECORD : ends_with
    APPLICATION_USER ||--o{ RETURN_RECORD : receives
    RETURN_RECORD ||--o| LATE_RETURN : may_create

    APPLICATION_USER {
        nvarchar user_id PK
        nvarchar user_code UK
        nvarchar first_name
        nvarchar last_name
        bit is_active
    }

    IDENTITY_ROLE {
        nvarchar role_id PK
        nvarchar role_name UK
    }

    USER_ROLE {
        nvarchar user_id PK, FK
        nvarchar role_id PK, FK
    }

    PERMISSION {
        bigint permission_id PK
        nvarchar permission_name UK
        nvarchar description
    }

    ROLE_PERMISSION {
        nvarchar role_id PK, FK
        bigint permission_id PK, FK
    }

    BORROWER_PROFILE {
        bigint borrower_profile_id PK
        nvarchar user_id FK, UK
        nvarchar school_id UK
        nvarchar department
        nvarchar contact_number
        bit is_eligible
    }

    EQUIPMENT_CATEGORY {
        bigint category_id PK
        nvarchar category_code UK
        nvarchar category_name UK
        bit is_active
    }

    LOCATION {
        bigint location_id PK
        nvarchar location_name UK
        bit is_active
    }

    EQUIPMENT_ITEM {
        bigint equipment_id PK
        bigint category_id FK
        bigint location_id FK
        nvarchar item_code UK
        nvarchar item_name
        nvarchar serial_number UK
        nvarchar condition_status
        nvarchar item_status
        bit is_archived
    }

    RESERVATION {
        bigint reservation_id PK
        bigint borrower_profile_id FK
        bigint equipment_id FK
        nvarchar reviewed_by_user_id FK
        datetime2 reservation_start
        datetime2 reservation_end
        nvarchar purpose
        nvarchar status
        nvarchar rejection_reason
        datetime2 reviewed_at
    }

    RELEASE_RECORD {
        bigint release_record_id PK
        bigint reservation_id FK, UK
        nvarchar released_by_user_id FK
        datetime2 actual_release_at
        nvarchar notes
    }

    RETURN_RECORD {
        bigint return_record_id PK
        bigint release_record_id FK, UK
        nvarchar received_by_user_id FK
        datetime2 actual_return_at
        nvarchar returned_condition
        nvarchar resulting_item_status
    }

    LATE_RETURN {
        bigint late_return_id PK
        bigint return_record_id FK, UK
        datetime2 due_at
        datetime2 returned_at
        int days_late
        nvarchar penalty_status
    }
```

## Cardinality Summary

| Relationship | Cardinality | Meaning |
| --- | --- | --- |
| Application User → Borrower Profile | 1:0..1 | Only borrowing accounts need a borrower profile. |
| Role → Permission | M:N | `ROLE_PERMISSION` assigns capabilities to Identity roles. |
| Category → Equipment Item | 1:M | Each item belongs to one category. |
| Location → Equipment Item | 1:M | Each item has one current location. |
| Borrower Profile → Reservation | 1:M | A borrower can submit many requests. |
| Equipment Item → Reservation | 1:M | An item can appear in many historical reservations, but approved times cannot overlap. |
| Reservation → Release Record | 1:0..1 | Only a fulfilled approved reservation is released. |
| Release Record → Return Record | 1:0..1 | A release receives one return when the loan ends. |
| Return Record → Late Return | 1:0..1 | A late-return record exists only when the item was returned past its due time. |

## Important Rules

- `AspNetUsers` and the other standard Identity tables remain part of the physical database even when not fully shown here.
- No custom password, password-reset, or OTP columns/tables are required.
- Reservation start must be earlier than reservation end.
- Approval must recheck schedule overlap in a SQL Server transaction.
- Optional serial numbers require a filtered unique index.
- Historical transaction rows must not be cascade-deleted.
