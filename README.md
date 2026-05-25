# Enterprise Employee & Payroll Management System

A console-based payroll management system built with **.NET 8** and **SQL Server 2022**, demonstrating practical mastery of C#, Object-Oriented Programming, layered architecture, and T-SQL.

> **Status:** 🟡 In active development — domain model and database schema complete, repository and service layers in progress.

---

## 🎯 Project Goals

This project is a focused, 1-day deep-dive intended to solidify and demonstrate:

- **C# / .NET 8** fundamentals in a real, cohesive application
- **Object-Oriented Programming** — Inheritance, Polymorphism, Encapsulation, Abstraction
- **Design patterns** — Factory Method, Repository, Service Layer
- **SQL Server** proficiency — T-SQL, stored procedures, complex queries, normalized schema design
- **Layered architecture** — strict separation of Presentation, Service, Repository, Database
- **SDLC discipline** — version control, containerization, atomic commits, documentation

---

## 🛠️ Tech Stack

| Layer | Technology |
|---|---|
| Language | C# (.NET 8.0) |
| Database | Microsoft SQL Server 2022 |
| Data Access | ADO.NET with `Microsoft.Data.SqlClient` v7.0.1 |
| Containerization | Docker + Docker Compose v2 |
| Development Environment | Google Antigravity IDE (Linux) |
| Version Control | Git + GitHub |
| Host OS | Pop!_OS 24.04 (Ubuntu Noble base) |

---

## 🏗️ Architecture

The application follows a strict layered architecture:

```text
┌─────────────────────────────────┐
│   Presentation Layer (Console)  │  ← User input, menus, display
├─────────────────────────────────┤
│   Service Layer                 │  ← Business logic, validation, custom exceptions
├─────────────────────────────────┤
│   Repository Layer              │  ← Data access via stored procedures
├─────────────────────────────────┤
│   Database Layer (SQL Server)   │  ← Tables, stored procedures, complex queries
└─────────────────────────────────┘
```

Each layer talks only to the one directly beneath it. This enables independent testing, easier debugging, and clean evolution of each concern.

---

## 📁 Project Structure
```text
EnterprisePayrollSystem/
├── Models/                      # Domain entities (built ✅)
│   ├── Employee.cs              # Abstract base
│   ├── FullTimeEmployee.cs      # Sealed subclass
│   ├── PartTimeEmployee.cs      # Sealed subclass
│   ├── ContractEmployee.cs      # Sealed subclass
│   └── Payroll.cs               # Immutable record + factory method
├── Repositories/                # Data access layer (pending)
├── Services/                    # Business logic layer (pending)
├── Helpers/                     # Utilities (pending)
├── Database/                    # SQL scripts (built ✅)
│   ├── 01_schema.sql            # Tables, constraints, indexes
│   └── 02_seed.sql              # Sample test data
├── docker-compose.yml           # SQL Server container definition
├── dev-up.sh / dev-down.sh      # Container lifecycle scripts
├── EnterprisePayrollSystem.csproj
├── Program.cs                   # Entry point + demo
└── README.md                    # This file
```


---

## 🧠 OOP Concepts in Action

| Concept | Implementation |
|---|---|
| **Abstraction** | `Employee` is `abstract`; `CalculateGrossSalary()` is an abstract method — the base defines the contract, subclasses fill in the behavior |
| **Inheritance** | `FullTimeEmployee`, `PartTimeEmployee`, `ContractEmployee` extend `Employee` via `: base(...)` |
| **Polymorphism** | Each subclass overrides `CalculateGrossSalary()` with type-specific math; one `List<Employee>` runs three different calculations transparently |
| **Encapsulation** | All setters are `private` or `protected`; constructor validates inputs fail-fast before any state assignment |
| **Immutability** | `Payroll` records are frozen after creation — no public setters, no mutator methods |
| **Factory Method** | `Payroll.GenerateFor(employee, payPeriod)` encapsulates the full construction recipe; `Payroll`'s constructor is `private` so the factory is the only entry point |
| **Sealed Concrete Classes** | All three Employee subclasses are `sealed` — locks the hierarchy at the right layer and enables JIT devirtualization |

---

## 🗄️ Database Design

The schema uses the **Table Per Type (TPT)** inheritance mapping strategy and **NO ACTION** referential integrity on payroll history.

### Tables

```text
Employees (base)              FullTimeEmployees         PartTimeEmployees       ContractEmployees
─────────────────             ─────────────────         ─────────────────       ─────────────────
EmployeeId (PK)               EmployeeId (PK/FK)        EmployeeId (PK/FK)      EmployeeId (PK/FK)
FullName                      MonthlySalary             HourlyRate              ContractAmount
Email (UNIQUE)                                          HoursWorkedPerMonth     ContractEndDate
Department
HireDate
EmployeeType (CHECK)
CreatedAt / UpdatedAt

Payrolls
─────────────────
PayrollId (PK)
EmployeeId (FK → Employees, NO ACTION)
PayPeriod
GrossSalary / TaxDeduction / HealthInsuranceDeduction / NetSalary
GeneratedAt
UNIQUE (EmployeeId, PayPeriod)
```

### Key Design Decisions

- **TPT over Table-Per-Hierarchy (TPH):** Chose stronger normalization over query simplicity. No NULL columns in the base table. Every subclass column is `NOT NULL` with `CHECK` constraints — the database itself enforces type-specific invariants.
- **NO ACTION on Payrolls FK:** Deleting an employee with payroll history is **forbidden at the database level**. Payroll records are legally and operationally immutable; real HR systems behave this way for audit and tax-compliance reasons. The application layer must handle this constraint violation explicitly.
- **CASCADE on subclass FKs:** When an employee IS deletable (no payroll history), their type-specific row in the subclass table dies with them — this is intra-employee data, not historical record.
- **DECIMAL(18,2) for money:** Never FLOAT/REAL. Floating-point produces rounding errors unacceptable for financial calculations.
- **NVARCHAR over VARCHAR:** Unicode support for international employee names.
- **`SCOPE_IDENTITY()` for TPT inserts:** Session-scoped, trigger-safe identity capture — not `@@IDENTITY` or `IDENT_CURRENT`.
- **Idempotent scripts:** Both schema and seed re-run cleanly; uses `IF NOT EXISTS`, `OBJECT_ID` checks, and `DBCC CHECKIDENT` resets.

---

## 🚀 Getting Started

### Prerequisites

- Docker + Docker Compose v2
- .NET 8 SDK
- A SQL client (Azure Data Studio, DBeaver, or `sqlcmd` via container exec)

### 1. Start the database

```bash
./dev-up.sh
# or directly:
docker compose up -d
```

This spins up a SQL Server 2022 container on `localhost:1433` with a persistent named volume `sqldata`.

### 2. Verify the database is reachable

```bash
docker exec -it payroll-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong!Pass123' -C -Q "SELECT @@VERSION"
```

### 3. Apply schema and seed data

```bash
docker cp Database/01_schema.sql payroll-sql:/tmp/01_schema.sql
docker cp Database/02_seed.sql payroll-sql:/tmp/02_seed.sql

docker exec -it payroll-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong!Pass123' -C -i /tmp/01_schema.sql

docker exec -it payroll-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong!Pass123' -C -i /tmp/02_seed.sql
```

### 4. Build and run the application

```bash
dotnet build
dotnet run
```

Currently outputs a colored banner plus polymorphism and payroll-generation demos.

### 5. Stop the database when done

```bash
./dev-down.sh
# or directly:
docker compose down
```

Data persists in the `sqldata` volume between sessions.

---

## 🔐 Configuration

Database connection details are defined in `docker-compose.yml`:

| Setting | Value |
|---|---|
| Server | `localhost,1433` |
| Username | `sa` |
| Password | `YourStrong!Pass123` |
| Database | `PayrollDB` |

> ⚠️ The password is intentionally hardcoded for local development. Production would use environment variables or a secrets manager.

Connection string used by the application (when wired in later prompts):
Server=localhost,1433;Database=PayrollDB;User Id=sa;Password=YourStrong!Pass123;TrustServerCertificate=True;


---

## 📊 Build Roadmap

The project is being built in 15 sequential phases.

### ✅ Completed

- [x] **Phase 0** — Docker + SQL Server 2022 infrastructure
- [x] **Phase 1** — Project scaffold and folder structure
- [x] **Phase 2** — Abstract `Employee` base class with encapsulation and validation
- [x] **Phase 3** — Concrete subclasses (`FullTime`, `PartTime`, `Contract`) — inheritance + polymorphism
- [x] **Phase 4** — Immutable `Payroll` record with static factory method
- [x] **Phase 5** — SQL schema (TPT) and seed data with audit-grade integrity

### ⬜ In Progress / Pending

- [ ] **Phase 6** — Stored procedures (CRUD + reporting)
- [ ] **Phase 7** — Complex queries (CTE, window functions, GROUP BY)
- [ ] **Phase 8** — `DatabaseHelper` class (ADO.NET wrapper)
- [ ] **Phase 9** — `EmployeeRepository` with TPT-aware polymorphic mapping
- [ ] **Phase 10** — `PayrollRepository`
- [ ] **Phase 11** — `EmployeeService` (validation + business logic + FK exception handling)
- [ ] **Phase 12** — `PayrollService` (salary generation)
- [ ] **Phase 13** — Console menu system
- [ ] **Phase 14** — `Program.cs` wiring + try/catch + logging
- [ ] **Phase 15** — Final polish, README updates

---

## 📚 Lessons Captured During Development

- Bash special characters (`!`, `$`) require single-quoting to survive shell expansion
- Docker installs from `apt` (`docker.io`) lag behind official Docker repos — use `docker-ce`
- Compose v2 (`docker compose`) replaced the Python-based v1 (`docker-compose`)
- "Container running" ≠ "service working" — always verify with an actual query
- Systemd manages background services: `start`, `enable`, `status` is the trio you'll use forever
- `protected` constructor is the only access level that allows subclass `base(...)` while blocking external instantiation
- Fail-fast validation (validate all → then assign) prevents partial-state objects on exceptions
- `sealed` on concrete leaf classes signals design intent and enables JIT devirtualization
- Immutable records (`private` constructor + factory method) prevent calculation drift across a codebase
- `DateTime.UtcNow.Date` is timezone-fragile in production; `DateTimeOffset` or `DateOnly` are more robust
- Email validation via `Contains('@')` is naive; production uses `System.Net.Mail.MailAddress` or rigorous regex
- TPT requires multi-table transactional inserts via `SCOPE_IDENTITY()` for atomicity
- `ON DELETE NO ACTION` on financial-history FKs enforces audit integrity at the database level
- `DECIMAL(18, 2)` is the only correct type for money — `FLOAT`/`REAL` cause base-2 rounding errors

---

## 📄 License

MIT — built for personal learning and portfolio use.
