# E-Library and Resource Circulation System

Application Programming Project (72ITSE41704) · Class group 261_72ITSE41704_01 · Van Lang University
Lecturer: Dr. Nguyen Tri Hai · Topic 6 of the course catalogue

A Windows desktop application that digitises the circulation of a small library: catalogue and
e-resource links, members, check-out, return, renewal, a FIFO reservation queue with hold expiry,
overdue fines with a cap and holiday exclusions, payments and reports.

Main features of the final version:

* Role-based sidebar (Administrator, Librarian, Member) with keyboard shortcuts and a light / dark theme
* Overview dashboard: totals, overdue items, titles by category, most-reserved titles
* Catalogue with list and book-cover views, copies and e-resource links
* Check out, two-step return (find the loan, then confirm), renewal, FIFO reservations and holds
* Fines that accrue per opening day (Sundays and holidays excluded, capped), partial payments
* Reports with export to Excel and CSV; database backup and restore

## Team

| Member | Student ID | Role and main modules |
|---|---|---|
| Nguyen Gia Thanh | 2474802010463 | Team leader · Catalogue (M2) · Reports (M7) · database design |
| Nguyen Tran Hoang Nhan | 2474802010272 | Members (M3) · Loan & return (M4) · Fines (M6) |
| Do Quoc Bao | 2474802015084 | Accounts & settings (M1) · Reservations & holds (M5) · test coordination |

## Technology stack

| Layer | Technology |
|---|---|
| Language / runtime | C# 12, .NET 8 |
| User interface | Windows Forms (`ELibrary.WinForms`) |
| Business logic | `ELibrary.Business` (services) + `ELibrary.Domain` (entities, rules BR1–BR7) |
| Data access | Entity Framework Core 8 + SQL Server (`ELibrary.DataAccess`) |
| Database | Microsoft SQL Server Express 2019 or later |
| Tests | NUnit 4 (`tests/ELibrary.Tests`) — business rules tested without UI or database |
| Libraries | ClosedXML 0.104 (Excel export, MIT licence) |

## Repository structure

```
/src
  ELibrary.Domain       entities with behaviour, LibraryCalendar, exceptions
  ELibrary.Business     services (one per use-case group), repository interfaces, DTOs, reports
  ELibrary.DataAccess   EF Core DbContext, mappings, repositories, sample-data seeder, backup
  ELibrary.WinForms     login, main window, screens, dialogs, Excel export
                        UI/ = theme, sidebar, grid style, themed controls, toasts
/tests
  ELibrary.Tests        NUnit tests with an in-memory unit of work and a fixed clock
/docs
  design/               DESIGN.md + UML, ERD and sequence diagrams (Mermaid source + PNG)
  database/             create_database.sql, generated from the EF Core mappings
RUN_GUIDE.md            how to install, configure, run and test
```

## Build and run (short version)

1. Install the .NET 8 SDK, Visual Studio 2022 (".NET desktop development") and SQL Server Express.
2. Check the connection string in `src/ELibrary.WinForms/appsettings.json`.
3. Open `ELibrary.sln`, set **ELibrary.WinForms** as the start-up project and press **F5**.
   On the first run the database `ELibraryDb` is created and filled with sample data.

The full procedure, troubleshooting and the test instructions are in [RUN_GUIDE.md](RUN_GUIDE.md).

## Sample accounts

| Role | User name | Password |
|---|---|---|
| Administrator | `admin` | `Admin@123` |
| Librarian | `librarian1`, `librarian2` | `Librarian@123` |
| Member | `M0001` … `M0020` | `Member@123` |

## Tests

```
dotnet test
```

110 test cases (NUnit), no SQL Server needed. They cover BR1–BR7, role checks (NFR-04), login
lock-out (NFR-05), password hashing and password policy (NFR-03), settings and holidays, and input
validation (NFR-06). Catalogue, reports and the user interface are checked with the manual
acceptance tests in [RUN_GUIDE.md](RUN_GUIDE.md#6-sample-data--what-to-try).

## Status

| Module | Use cases | Status |
|---|---|---|
| M1 Accounts & settings | UC01–UC03 | Implemented |
| M2 Catalogue | UC04, UC05 | Implemented |
| M3 Members | UC06 | Implemented |
| M4 Loan & return | UC07–UC09 | Implemented |
| M5 Reservations | UC10, UC11 | Implemented |
| M6 Fines | UC12 | Implemented |
| M7 Reports | UC13 (+ Excel/CSV export) | Implemented |
| Extra (beyond the proposal) | Overview dashboard, book-cover view, light/dark theme | Implemented |
| Stretch goals | Barcode/QR scanning, recommendations | Not started (a USB barcode scanner works as a keyboard on the Check out and Return screens) |
