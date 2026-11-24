# E-Library and Resource Circulation System — Design Specification

Course 72ITSE41704 · Team: Nguyen Gia Thanh (leader), Nguyen Tran Hoang Nhan, Do Quoc Bao

This document records the design that the code in `/src` implements. It is the source for
Chapter 3 (sections 3.2 and 3.3) of the Final Project Report. All diagrams are in
`docs/design/diagrams` as Mermaid source (`.mmd`) and rendered images (`.png`).

---

## 1. Architecture

### 1.1 Layers

The system uses a **three-layer architecture** with dependencies pointing one way only
(Figure D-1, `architecture.png`):

| Layer | Project | Responsibility | Knows about |
|---|---|---|---|
| Presentation | `ELibrary.WinForms` (net8.0-windows) | Windows Forms screens, input collection, showing results and error messages. Composition root (creates services). | Business, (DataAccess only in `Program.cs` to wire it up) |
| Business logic | `ELibrary.Business` (net8.0) | Application services for every use case, authorisation (role checks), input validation, DTOs, repository interfaces. | Domain |
| Domain | `ELibrary.Domain` (net8.0) | Entities with behaviour, business rules BR1–BR7, the library calendar, exceptions. No UI, no SQL. | nothing |
| Data access | `ELibrary.DataAccess` (net8.0) | EF Core `DbContext`, table mappings, repository and unit-of-work implementations, seed data, SQL Server backup/restore. | Business (interfaces), Domain |

The business layer **defines** the repository interfaces (`IRepository<T>`, `IUnitOfWork`) and
the data-access layer **implements** them (Dependency Inversion). Because of this, every
business rule can be unit-tested with an in-memory fake unit of work — no window and no
database (`tests/ELibrary.Tests`).

**Rule:** form event handlers only (1) read the controls, (2) call one service method, (3)
display the result or the error. No business rule and no query is written in a form.

### 1.2 Design patterns used (and why)

| Pattern | Where | Problem it solves |
|---|---|---|
| Repository + Unit of Work | `IRepository<T>`, `IUnitOfWork` / `EfRepository<T>`, `EfUnitOfWork` | Isolates persistence; one `SaveChanges()` per use case makes checkout, return and payment atomic (NFR-07); allows in-memory fakes in tests. |
| Strategy | `IReport` (+ 4 report classes), `IReportExporter` (+ `ExcelReportExporter`, `CsvReportExporter`) | Reports and export formats differ in behaviour. Adding a new report or format = adding one class; nothing else changes. |
| Information Expert (GRASP) | `Loan.IsOverdue`, `MembershipType.CalculateDueDate`, `Member.EnsureCanBorrow`, `FinePolicy.CalculateFine`, `Reservation.PlaceHold` … | Entities decide about their own data (avoids an anaemic domain model). |
| Factory | `IUnitOfWorkFactory` | Each use case gets a fresh, short-lived `DbContext` (avoids stale data in a long-running desktop app). |

### 1.3 Cross-cutting decisions

| Concern | Decision |
|---|---|
| Authentication | `AuthService.Login` verifies PBKDF2-SHA256 hashes (100,000 iterations, 16-byte random salt, 32-byte hash; NFR-03). User names are matched case-insensitively and a wrong user name or password gives the same message. 5 consecutive failures lock the account for 5 minutes (NFR-05). `PasswordPolicy`: 8–100 characters, at least one letter and one digit, not equal to the user name. |
| Authorisation | Every public service method starts with `_session.Require(...)` (NFR-04). Member-facing methods additionally check that the member only touches his/her own data. |
| Time | All services read the date from `IClock`, so tests can fix "today". |
| Errors | `LibraryException` hierarchy: `ValidationException` (bad input), `BusinessRuleException` (BR violated, carries the rule id), `NotFoundException`, `AccessDeniedException`, `AuthenticationFailedException`, `DataAccessException` (wraps EF/SQL errors). The UI shows `LibraryException` messages as warnings; anything else is logged and shown as a generic error by a global handler — the application never crashes on bad input (NFR-06). |
| Transactions | One `SaveChanges()` per use case (EF wraps it in a transaction). Registration of a member with a login account uses `ExecuteInTransaction`. |
| Money | `decimal`, VND, no fractional part. |
| Dates | `DateOnly` for calendar dates (due date, card expiry, holiday, hold expiry), `DateTime` for time stamps. |
| Database creation | `EnsureCreated()` + seed on first run; `--export-schema` writes the generated SQL script to `docs/database`. |

---

## 2. Domain model

### 2.1 Classes (17 entities + 1 domain service)

Figure D-2 (`class-diagram.png`) shows associations and multiplicities. This follows the
initial domain model of the proposal (Figure 1) with one addition, the `TitleAuthor` join
entity that realises the many-to-many Title–Author association.

| Package | Class | Key behaviour (operations that carry rules) |
|---|---|---|
| Users | `Role` | `Code` (Administrator, Librarian, Member) |
| | `UserAccount` | `IsLockedOut(now)`, `RegisterFailedLogin(now)`, `RegisterSuccessfulLogin(now)`, `Lock()`, `Unlock()`, `ChangePasswordHash()`, `ChangeFullName()`, `ChangeStaffRole()` |
| | `MembershipType` | `Update(…)`, `CalculateDueDate(start, calendar)` (BR1), `EnsureBelowLoanLimit(activeLoans)` (BR1); `MaxRenewals` is used by `Loan.Renew` (BR7) |
| | `Member` | `IsCardValid(today)`, `GetBorrowingBlock(today, outstandingFines, policy)` (the reason, or null) and `EnsureCanBorrow(…)` (BR2), `UpdateDetails(…)`, `ExtendCard(newExpiry)`, `LinkAccount(accountId)`, `Deactivate()` / `Reactivate()` |
| Catalogue | `Title`, `Author`, `TitleAuthor`, `Publisher`, `Category` | `Title.Update(...)`, `Title.Deactivate()` |
| | `EResource` | URL must be absolute http/https |
| | `Copy` | `CheckOut()`, `CheckOutHeldCopy()`, `CheckIn()`, `PlaceOnHold()`, `ReleaseHold()`, `ChangeStatus()`, `ChangeLocation()` — guards legal status transitions |
| Circulation | `Loan` | `IsOpen`, `IsOverdue(today)`, `EnsureCanBeRenewed(maxRenewals, today)` and `Renew(newDueDate, maxRenewals, today)` (BR7), `Close(returnedAt, staffId)` |
| | `Reservation` | `IsActive`, `PlaceHold(copyId, expiresOn)`, `IsHeldFor(copyId)`, `IsHoldExpired(today)`, `Fulfil(now)`, `Cancel(now)`, `Expire(now)` (BR4–BR6) |
| Fines | `FinePolicy` | `CalculateFine(chargeableDays)` = min(days × rate, cap) (BR3), `IsAboveDebtThreshold(amount)` (BR2) |
| | `Holiday` | date + description |
| | `Fine` | `Balance`, `IsSettled`, `ApplyPayment(amount, …)` → `Payment` |
| | `Payment` | amount, time, staff who received it |
| Domain service | `LibraryCalendar` | `IsOpeningDay(d)`, `FirstOpeningDayOnOrAfter(d)`, `AddOpeningDays(d, n)`, `CountChargeableDays(due, returned)` — Sundays and holidays excluded (BR1, BR3, BR5) |

Entities expose **no public setters**: state changes only through the methods above, which
guard the invariants (for example, a copy that is `OnLoan` cannot be checked out again, a
payment cannot exceed the balance). Each entity also has a private parameterless constructor for
EF Core. Note: EF Core 8 materialises an entity through the bindable constructor with the most
parameters, so the same invariants are checked again when rows are loaded from the database.

### 2.2 Enumerations

* `RoleCode`: Administrator = 1, Librarian = 2, Member = 3
* `CopyStatus`: Available, OnLoan, OnHold, UnderRepair, Lost, Withdrawn
* `ReservationStatus`: Waiting, OnHold, Fulfilled, Cancelled, Expired

### 2.3 State machines

**Copy** — `Available → OnLoan` (checkout) · `OnLoan → Available | OnHold` (return) ·
`OnHold → OnLoan` (checkout by the holder) · `OnHold → Available | OnHold(next member)`
(hold expired/cancelled) · `Available ↔ UnderRepair | Lost | Withdrawn` (librarian).

**Reservation** — `Waiting → OnHold` (copy assigned) · `OnHold → Fulfilled` (checked out) ·
`Waiting | OnHold → Cancelled` (member) · `OnHold → Expired` (not collected in 3 opening days).

---

## 3. Business rules → where they are enforced

| Rule | Enforced in | Tested by |
|---|---|---|
| BR1 Loan limit & period | `MembershipType.EnsureBelowLoanLimit`, `MembershipType.CalculateDueDate`, called by `CirculationService.CheckOut` | `MemberCirculationFineTests`: `CheckoutAllowsLimitAndRejectsNextCopy`, `CheckoutMovesDueDatePastSundayAndHoliday` |
| BR2 Eligibility | `Member.EnsureCanBorrow` / `GetBorrowingBlock` (card + debt threshold), `FineLedger.GetOutstanding` (unpaid fines + fines accrued to date on overdue loans); used by checkout, renew, reserve | `CheckoutUsesStrictDebtThreshold`, `ExpiryDateIsInclusive`, `AccruingFineCanBlockCheckout`, `RenewalRejectsIneligibleLoan`, `AMemberWhoCannotBorrowCannotReserve` |
| BR3 Overdue fine | `LibraryCalendar.CountChargeableDays` + `FinePolicy.CalculateFine`; `CirculationService.Return` records the `Fine` | `ReturnCountsOpeningDaysAndAppliesCap`, `AccruingFineIsVisibleButPayableAfterReturn`, `PartialPaymentPaysOldestFineFirstAndUnblocksMember` |
| BR4 FIFO queue | `ReservationService.Place` (only if no copy Available; one active reservation per member per title); `HoldAllocator` serves the oldest `Waiting` reservation. DB: filtered unique index on active reservations | `ReservationTests`: `MembersJoinTheQueueInOrderAndOnlyOnce`, `ATitleWithACopyOnTheShelfCannotBeReserved`, `ATitleWithOnlyLostCopiesCannotBeReserved` |
| BR5 Hold expiry | `HoldAllocator.AssignCopy` (expiry = return date + 3 opening days), `ReservationService.ProcessHolds` / `ExpireHoldsAtStartup` | `AReturnedCopyIsHeldForTheFirstMemberForThreeOpeningDays`, `AnUncollectedHoldExpiresAndTheCopyGoesToTheNextMember`, `StartupExpiryReturnsTheCopyToTheShelfWhenNobodyElseWaits` |
| BR6 Held copy protection | `CirculationService.CheckOut` — a copy `OnHold` can only be lent to the member whose reservation holds it | `ReturnAssignsFirstWaitingMemberAndProtectsTheirHold`, `TheHolderCollectsTheCopyAndTheReservationIsFulfilled` |
| BR7 Renewal | `Loan.EnsureCanBeRenewed` / `Loan.Renew` (open, not overdue, under the renewal limit) + `CirculationService.Renew` (no `Waiting` reservation for the title). BR7 is checked before BR2, so an overdue loan is refused as overdue | `MemberCanRenewOwnLoanOnceFromExistingDueDate`, `RenewalRejectsIneligibleLoan` (5 cases) |

**Clarifications made during design** (to be confirmed in the librarian interview,
Appendix A of the proposal):

* BR2 "unpaid fines" = recorded unpaid fines **plus** fines accrued to date on loans that are
  still out and overdue — otherwise a member who never returns a book would never be blocked.
* BR3 a fine is **recorded** when the copy is returned; for loans still out the fine-to-date is
  calculated and shown but not stored.
* Overdue days are counted from the day after the due date up to and including the return
  date; Sundays and holidays are not counted.
* BR5 a hold expires at the end of its expiry date; the check runs when the application starts
  and whenever the librarian opens the Holds screen.
* Payments are allocated to the member's oldest unpaid fines first.

---

## 4. Business layer — services

| Service | Use cases | Main methods | Allowed roles |
|---|---|---|---|
| `AuthService` | UC01 | `Login`, `Logout`, `ChangeOwnPassword` | everyone |
| `AccountService` | UC02 | `GetAccounts`, `CreateStaffAccount`, `UpdateStaffAccount`, `SetLocked`, `ResetPassword` | Admin |
| `SettingsService` | UC03 | `GetMembershipTypes`, `SaveMembershipType`, `GetFinePolicy`, `UpdateFinePolicy`, `GetHolidays`, `AddHoliday`, `RemoveHoliday`, `Backup`, `Restore` | Admin (reads: Librarian too) |
| `CatalogueService` | UC04, UC05 | `Search`, `GetTitleDetails`, `SaveTitle`, `SetTitleActive`, `AddCopy`, `ChangeCopyLocation`, `ChangeCopyStatus`, `AddEResource`, `RemoveEResource`, `GetEResourceUrl`, `GetCategories`, `GetPublishers` | search: all; edit: Librarian |
| `MemberService` | UC06 | `Search`, `GetMembershipTypes`, `GetMember`, `Register`, `Update`, `ExtendCard`, `SetActive`, `ResetPassword`, `GetMyProfile` | Librarian (Member: own profile) |
| `CirculationService` | UC07–UC09 | `GetMemberSummary`, `CheckOut`, `GetReturnPreview` + `Return` (return in two steps: find the loan, then confirm), `Renew`, `GetOpenLoans`, `GetMyLoans` | Librarian (Member: renew/view own) |
| `ReservationService` | UC10, UC11 | `Place`, `Cancel`, `GetMyReservations`, `GetActiveHolds`, `ProcessHolds` and `ExpireHoldsAtStartup` (system); both return a `HoldProcessingResultDto` (holds expired, copies reassigned, copies released) | Member (place/cancel); Librarian (holds, cancel) |
| `FineService` | UC12 | `GetMemberFines`, `GetMyFines`, `RecordPayment` | Librarian (Member: own) |
| `ReportService` | UC13 | `GetAvailableReports`, `Run` | Admin, Librarian |

Internal collaborators (not called by the UI): `HoldAllocator` (assigns a returned or released
copy to the next waiting member), `FineLedger` (outstanding balance and fine-to-date),
`LibraryRules` (loads the `FinePolicy` and builds a `LibraryCalendar` from the holiday table) and
`DtoProjector` (turns entities into DTOs with one query per related table). Figure D-5
(`services-diagram.png`) shows the services, their collaborators and the Strategy interfaces.

Services return **DTOs** (C# records such as `TitleSummaryDto`, `LoanDto`, `ReturnReceiptDto`)
— entities never leave the business layer.

---

## 5. Database design

Figure D-3 (`erd.png`) shows the ERD. One table per entity (17 tables) in SQL Server.

| Table | Important columns / constraints |
|---|---|
| Roles | Id (PK, = RoleCode), Name (unique) |
| UserAccounts | Username (unique), PasswordHash, FullName, RoleId → Roles, IsLocked, FailedLoginAttempts, LockoutEndsAt |
| MembershipTypes | Name (unique), MaxLoans > 0, LoanPeriodDays > 0, MaxRenewals ≥ 0 |
| Members | MemberCode (unique), FullName, Email, Phone, MembershipTypeId → MembershipTypes, CardExpiryDate, IsActive, UserAccountId → UserAccounts (unique, nullable) |
| Publishers / Categories / Authors | Name (unique) |
| Titles | Isbn (unique when not null), Name, PublisherId, CategoryId, PublicationYear, IsActive |
| TitleAuthors | (TitleId, AuthorId) unique |
| EResources | TitleId → Titles, Label, Url |
| Copies | Barcode (unique), TitleId → Titles, Location, Status |
| Loans | CopyId, MemberId, LoanDate, DueDate, ReturnedAt, RenewalCount, IssuedById, ReturnedById; **filtered unique index on CopyId where ReturnedAt IS NULL** (a copy can never have two open loans) |
| Reservations | TitleId, MemberId, ReservedAt, Status, HeldCopyId, HoldExpiresOn; **filtered unique index on (TitleId, MemberId) where Status is Waiting or OnHold** (BR4 at database level) |
| Fines | LoanId (unique), MemberId, OverdueDays, Amount ≥ 0, AmountPaid between 0 and Amount |
| Payments | FineId → Fines, Amount > 0, PaidAt, ReceivedById |
| Holidays | Date (unique), Description |
| FinePolicies | single row: DailyRate, MaxFinePerLoan, DebtThreshold, HoldPeriodDays, ClosedOnSundays |

All foreign keys use `Restrict` on delete — records with history are deactivated, never deleted.

---

## 6. User interface

### 6.1 Navigation

`LoginForm` → `MainForm`. The main window has a sidebar built from the user's role (groups below;
each screen also has a keyboard shortcut, e.g. F2–F8, Ctrl+F for the catalogue). A screen is
created the first time it is opened and kept, so switching back keeps its state; Ctrl+W closes
it. On narrow windows the sidebar collapses to icons. Figure D-4 (`navigation.png`).

| Sidebar group | Screen | Use case | Admin | Librarian | Member |
|---|---|---|:-:|:-:|:-:|
| Library | Overview (dashboard: totals, overdue items, titles by category, most-reserved titles) | — | ✓ | ✓ | ✓ |
| | Catalogue search (+ Title editor dialog; list or book-cover view) | UC04, UC05, UC10 | view | ✓ edit | ✓ reserve / open link |
| Circulation | Check out | UC07 | | ✓ | |
| | Return (Find loan → Confirm return) | UC08 | | ✓ | |
| | Loans & renewals | UC09 | | ✓ | |
| | Holds | UC11 | | ✓ | |
| | Fines & payments | UC12 | | ✓ | |
| Members | Members (+ Member editor dialog) | UC06 | | ✓ | |
| My library | My loans / My reservations / My fines | UC09, UC10, UC12 | | | ✓ |
| Reports | Reports (+ export to Excel/CSV) | UC13 | ✓ | ✓ | |
| Administration | User accounts | UC02 | ✓ | | |
| | Settings: membership types, fine policy, holidays, backup/restore | UC03, NFR-08 | ✓ | | |
| Sidebar footer | Light / dark theme, Change password, Log out | UC01 | ✓ | ✓ | ✓ |

### 6.2 UI conventions

* One consistent look: every screen derives from `ScreenBase` (page header with section and
  title, status line, standard margins); every list is a read-only `DataGridView` with
  full-row selection, styled by `GridStyle` (header, alternating rows, hover, status badges,
  empty state).
* Design tokens — colours, spacing, radii, sizes, fonts and motion — are defined once in
  `UI/Theme.cs`; screens use the helpers in `Ui` and the themed controls in `UI/`
  (`ThemedButton`, `FormField`, `Badge`, `Card`, `EmptyState`, `Toast`, `MessageDialog`)
  instead of setting colours or fonts themselves. Text and controls meet WCAG AA contrast,
  every control is reachable with the keyboard and shows a focus ring, and animations are
  skipped when Windows animations are turned off.
* Feedback: expected errors (validation, business rules) appear inline — the status line and a
  toast on screens, a banner above the fields in dialogs — so the user's input is kept;
  confirmations use `MessageDialog.Confirm` (destructive actions in red, Cancel focused);
  unexpected errors open the error dialog.
* The checkout screen supports the 4-step flow of NFR-09: member code → Enter → barcode →
  Enter (saves). Rejections show the reason from the business rule, e.g.
  "BR1: Loan limit reached (3/3)."
* Invalid input never reaches the database: the service throws `ValidationException`, the
  screen shows the message and keeps the user's input.

---

## 7. Testing approach

* **Unit tests (NUnit)** on the business layer with an in-memory unit of work (`MemoryLibrary`)
  and a fixed clock — **110 test cases** in `tests/ELibrary.Tests`:

  | Area | Test class | Cases |
  |---|---|---:|
  | Members, check out, return, renewal, fines (BR1–BR3, BR6, BR7) | `Circulation/MemberCirculationFineTests` | 30 |
  | Reservations and holds (BR4–BR6) | `Reservations/ReservationTests` | 12 |
  | Log-in, lock-out, accounts, roles (UC01, UC02, NFR-04, NFR-05) | `Accounts/AuthAndAccountTests` | 20 |
  | Membership types, fine policy, holidays, backup (UC03, NFR-08) | `Accounts/SettingsTests` | 13 |
  | Password hashing (NFR-03) | `Security/Pbkdf2PasswordHasherTests` | 17 |
  | Password policy | `Security/PasswordPolicyTests` | 18 |

  Catalogue search, title management and reports are covered by the manual acceptance tests.
* **Regression tests for defects found in testing** — e.g. renewing an overdue loan with a fine
  above the limit must report BR7 (`RenewalRejectsIneligibleLoan`, case "overdue with a fine above
  the limit"); the test fails on the code before the fix.
* **Sample data** is produced by the services themselves (`SampleDataSeeder`), which exercises the
  whole stack against SQL Server on the first start.
* **Manual acceptance tests** per use case (test-case table in the report: ID, purpose,
  precondition, input, expected, actual, pass/fail).
* **NFR checks**: search timing on generated data (NFR-01), restart test (NFR-07),
  backup → delete → restore (NFR-08).

---

## 8. Diagram index

| Figure | File | Shows |
|---|---|---|
| D-1 | `architecture.png` | Layers and projects |
| D-2 | `class-diagram.png` | Domain classes, operations, associations and multiplicities |
| D-3 | `erd.png` | Tables, keys and relationships |
| D-4 | `navigation.png` | Screens and navigation |
| D-5 | `services-diagram.png` | Business services, internal collaborators, Repository and Strategy interfaces |
| D-6 | `seq-checkout.png` | UC07 Check out with alternative flows (BR1, BR2, BR6) |
| D-7 | `seq-return.png` | UC08 Return including UC12 and extended by UC11 |
| D-8 | `seq-reserve.png` | UC10 Place reservation (BR2, BR4) |

## 9. Traceability (FR → UC → service method)

| FR | UC | Implemented by |
|---|---|---|
| FR-01 | UC01 | `AuthService.Login` |
| FR-02 | UC02 | `AccountService.*` |
| FR-03 | UC03 | `SettingsService.*` |
| FR-04 | UC04 | `CatalogueService.SaveTitle / SetTitleActive / AddCopy / ChangeCopyStatus / ChangeCopyLocation / AddEResource / RemoveEResource` |
| FR-05 | UC05 | `CatalogueService.Search` |
| FR-06 | UC05 | `CatalogueService.GetEResourceUrl` |
| FR-07 | UC06 | `MemberService.Register / Update / ExtendCard / SetActive / ResetPassword` |
| FR-08 | UC09, UC10, UC12 | `CirculationService.GetMyLoans`, `ReservationService.GetMyReservations`, `FineService.GetMyFines` |
| FR-09 | UC07 | `CirculationService.CheckOut` |
| FR-10 | UC08 | `CirculationService.Return` |
| FR-11 | UC09 | `CirculationService.Renew` |
| FR-12 | UC10 | `ReservationService.Place / Cancel` |
| FR-13 | UC11 | `HoldAllocator.AssignCopy`, `ReservationService.GetActiveHolds` |
| FR-14 | UC11 | `ReservationService.ProcessHolds`, `ReservationService.ExpireHoldsAtStartup` |
| FR-15 | UC12 | `FineLedger`, `FinePolicy.CalculateFine`, `LibraryCalendar.CountChargeableDays`, `FineService.GetMemberFines` |
| FR-16 | UC12 | `FineService.RecordPayment` |
| FR-17 | UC13 | `ReportService.Run` + 4 `IReport` classes |
| FR-18 | UC13 | `IReportExporter` (Excel, CSV) |
