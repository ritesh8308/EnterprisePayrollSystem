# Enterprise Employee & Payroll Management System

A console-based payroll management system built with **.NET 8** and **SQL Server 2022**, designed to demonstrate practical mastery of C#, Object-Oriented Programming, layered architecture, and T-SQL.

> **Status:** 🟡 In active development — infrastructure complete, application layer in progress.

---

## 🎯 Project Goals

This project is a focused, 1-day deep-dive intended to solidify and demonstrate:

- **C# / .NET 8** fundamentals in a real, cohesive application
- **Object-Oriented Programming** — Inheritance, Polymorphism, Encapsulation, Abstraction
- **SQL** proficiency — queries, joins, aggregate functions, stored procedures, CTEs, window functions
- **Layered architecture** — separation of concerns across Presentation, Service, Repository, and Database layers
- **Software Development Lifecycle (SDLC)** thinking — version control, containerization, documentation, error handling

---

## 🛠️ Tech Stack

| Layer | Technology |
|---|---|
| Language | C# (.NET 8.0) |
| Database | Microsoft SQL Server 2022 |
| Data Access | ADO.NET with `Microsoft.Data.SqlClient` |
| Containerization | Docker + Docker Compose |
| Development Environment | Google Antigravity (Linux, Pop!_OS 24.04) |
| Version Control | Git |

---

## 🏗️ Architecture

The application follows a strict layered architecture:

```
┌─────────────────────────────────┐
│   Presentation Layer (Console)  │  ← User input, menus, display
├─────────────────────────────────┤
│   Service Layer                 │  ← Business logic, validation
├─────────────────────────────────┤
│   Repository Layer              │  ← Data access via stored procedures
├─────────────────────────────────┤
│   Database Layer (SQL Server)   │  ← Tables, stored procedures, queries
└─────────────────────────────────┘
```

Each layer only talks to the one directly beneath it. This makes the codebase testable, maintainable, and easy to evolve.

---

## 📁 Project Structure

```
EnterprisePayrollSystem/
├── Models/              # Domain entities (Employee hierarchy, Payroll)
├── Repositories/        # Data access classes + interfaces
├── Services/            # Business logic layer
├── Helpers/             # Database helper, menu utilities
├── Database/            # SQL scripts (schema, procedures, queries)
├── docker-compose.yml   # SQL Server container definition
├── Program.cs           # Application entry point
└── README.md            # This file
```

---

## 🧠 OOP Concepts in Action

| Concept | Implementation |
|---|---|
| **Abstraction** | `Employee` abstract base class with abstract `CalculateGrossSalary()` |
| **Inheritance** | `FullTimeEmployee`, `PartTimeEmployee`, `ContractEmployee` extend `Employee` |
| **Polymorphism** | Each subclass overrides `CalculateGrossSalary()` with its own logic |
| **Encapsulation** | Private setters, validation in service layer, no direct field access |

---

## 🗄️ Database Design

Two primary tables:

- **Employees** — stores all employee types in a single table using an `EmployeeType` discriminator column
- **Payrolls** — historical payroll records with foreign key to `Employees`

Demonstrates:
- Stored procedures for all CRUD operations
- Complex queries using `JOIN`, `GROUP BY`, `CASE WHEN`, CTEs, and window functions (`ROW_NUMBER`)
- Aggregate reporting (department salary summaries)
- Cascade delete relationships

---

## 🚀 Getting Started

### Prerequisites

- Docker + Docker Compose (v2)
- .NET 8 SDK
- A SQL client (Azure Data Studio, DBeaver, or the VS Code/Antigravity `mssql` extension)

### 1. Start the database

```bash
docker compose up -d
```

This spins up a SQL Server 2022 container on `localhost:1433` with a persistent named volume.

### 2. Verify the database is reachable

```bash
docker exec -it payroll-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong!Pass123' -C -Q "SELECT @@VERSION"
```

### 3. Run the schema and stored procedures

Execute the following files against the SQL Server instance (in order):

```
Database/schema.sql
Database/procedures.sql
```

### 4. Build and run the application

```bash
dotnet build
dotnet run
```

### 5. Stop the database when done

```bash
docker compose down
```

Data persists in the `sqldata` Docker volume between sessions.

---

## 🔐 Configuration

Database connection details are defined in `docker-compose.yml`:

| Setting | Value |
|---|---|
| Server | `localhost,1433` |
| Username | `sa` |
| Password | `YourStrong!Pass123` |
| Database | `PayrollDB` (created via `schema.sql`) |

> ⚠️ The password is intentionally hardcoded for local development. In a production setting, this would live in environment variables or a secrets manager.

---

## 📊 Build Roadmap

The project is being built in 15 sequential phases:

- [x] **Phase 0:** Docker + SQL Server infrastructure
- [ ] **Phase 1:** Project scaffold and folder structure
- [ ] **Phase 2:** OOP models — `Employee` hierarchy
- [ ] **Phase 3:** `Payroll` model and factory method
- [ ] **Phase 4:** SQL schema and seed data
- [ ] **Phase 5:** Stored procedures
- [ ] **Phase 6:** Complex reporting queries
- [ ] **Phase 7:** Database helper class
- [ ] **Phase 8:** Employee repository (with polymorphic mapping)
- [ ] **Phase 9:** Payroll repository
- [ ] **Phase 10:** Employee service (validation + business logic)
- [ ] **Phase 11:** Payroll service (salary generation)
- [ ] **Phase 12:** Console menu system
- [ ] **Phase 13:** `Program.cs` wiring and error handling
- [ ] **Phase 14:** Final polish, logging, and documentation

---

## 📚 Lessons Learned

This project is also a learning log. Key takeaways captured during development:

- Bash special characters (`!`, `$`) require single-quoting to survive shell expansion
- Docker installs from `apt` (`docker.io`) lag behind official Docker repos — use `docker-ce` for production-grade installs
- "Container running" ≠ "service working correctly" — always verify with an actual query
- Compose v2 (`docker compose`) replaced the Python-based v1 (`docker-compose`)
- Systemd manages background services on modern Linux — `start`, `enable`, `status` are the trio you'll use forever

---

## 📄 License

MIT — built for personal learning and portfolio use.
