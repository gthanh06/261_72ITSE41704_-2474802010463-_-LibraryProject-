# Database script

`create_database.sql` is generated from the EF Core mappings, so it always matches the code.
To (re)generate it after building the solution in Release mode:

```
cd src/ELibrary.WinForms/bin/Release/net8.0-windows
ELibrary.exe --export-schema ..\..\..\..\..\docs\database\create_database.sql
```

The script creates 17 tables with primary keys, foreign keys (all `ON DELETE NO ACTION`), unique
indexes (including the filtered indexes `UX_Loans_OpenLoanPerCopy` and
`UX_Reservations_ActivePerMemberAndTitle`) and check constraints. See `docs/design/DESIGN.md`,
section 5, and the ERD in `docs/design/diagrams/erd.png`.
