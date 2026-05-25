/*
===============================================================================
PROJECT: Enterprise Payroll Mgmt. System

FILE: 01_schema.sql
PURPOSE: Defines the database schema, tables, constraints, and indexes.
EXECUTION ORDER: Run this script first to create the schema, then run 02_seed.sql.

ARCHITECTURAL INTENT:
1. TABLE PER TYPE (TPT) INHERITANCE:
   - Base table: Employees (stores shared fields).
   - Subclass tables: FullTimeEmployees, PartTimeEmployees, ContractEmployees.
   - Each subclass table's Primary Key is also a Foreign Key to Employees(EmployeeId).
   - Keeps type-specific fields fully normalized (no sparse NULL columns).
   - Trade-off: Stronger database integrity and normalization at the cost of 
     requiring JOINs for reads and multi-table transactions for writes.

2. IMMUTABLE PAYROLL RECORDS (NO ACTION FK):
   - Establishes a FOREIGN KEY on Payrolls(EmployeeId) referencing Employees(EmployeeId).
   - Explicitly configured with ON DELETE NO ACTION.
   - Refuses any DELETE on an Employee row that still has historic payroll records.
   - Essential for legal, financial, and taxation audit compliance.
===============================================================================
*/

-- Idempotent database creation
USE master;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'PayrollDB')
BEGIN
    CREATE DATABASE PayrollDB;
END
GO

USE PayrollDB;
GO

-- Safe Drop Tables in reverse-dependency order (FK dependencies)
IF OBJECT_ID('dbo.Payrolls', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Payrolls;
END
GO

IF OBJECT_ID('dbo.FullTimeEmployees', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.FullTimeEmployees;
END
GO

IF OBJECT_ID('dbo.PartTimeEmployees', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.PartTimeEmployees;
END
GO

IF OBJECT_ID('dbo.ContractEmployees', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ContractEmployees;
END
GO

IF OBJECT_ID('dbo.Employees', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Employees;
END
GO

/*
===============================================================================
BASE TABLE: Employees
===============================================================================
*/
CREATE TABLE dbo.Employees (
    -- INT with IDENTITY for auto-incrementing surrogate keys
    EmployeeId    INT             PRIMARY KEY IDENTITY(1,1),
    
    -- NVARCHAR supports international/Unicode character encodings (names like Priyā or Marco)
    FullName      NVARCHAR(150)   NOT NULL,
    
    -- UNIQUE constraint guarantees email distinctness at the database layer
    Email         NVARCHAR(255)   NOT NULL UNIQUE,
    
    Department    NVARCHAR(100)   NOT NULL,
    
    -- DATE stores date only (no time portion) to accurately track the calendar hire date
    HireDate      DATE            NOT NULL,
    
    -- Discriminator column to route data access to subclass tables in C# layer
    EmployeeType  NVARCHAR(20)    NOT NULL 
        CONSTRAINT CK_Employees_EmployeeType 
        CHECK (EmployeeType IN ('FullTime', 'PartTime', 'Contract')),
        
    -- Audit columns are industry standard for logging record lifecycle timestamps
    CreatedAt     DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

/*
===============================================================================
SUBCLASS TABLE: FullTimeEmployees
===============================================================================
*/
CREATE TABLE dbo.FullTimeEmployees (
    -- TPT MARKER: EmployeeId is BOTH the Primary Key and a Foreign Key to the base table
    EmployeeId       INT             NOT NULL PRIMARY KEY,
    
    -- DECIMAL(18,2) is mandatory for financial data to prevent float rounding errors
    MonthlySalary    DECIMAL(18, 2)  NOT NULL 
        CONSTRAINT CK_FullTimeEmployees_MonthlySalary 
        CHECK (MonthlySalary > 0),
    
    -- Cascade deletion on the subclass profile row if the main Employee is deleted
    CONSTRAINT FK_FullTimeEmployees_Employees
        FOREIGN KEY (EmployeeId) 
        REFERENCES dbo.Employees(EmployeeId)
        ON DELETE CASCADE
);
GO

/*
===============================================================================
SUBCLASS TABLE: PartTimeEmployees
===============================================================================
*/
CREATE TABLE dbo.PartTimeEmployees (
    EmployeeId            INT             NOT NULL PRIMARY KEY,
    
    HourlyRate            DECIMAL(18, 2)  NOT NULL 
        CONSTRAINT CK_PartTimeEmployees_HourlyRate 
        CHECK (HourlyRate > 0),
        
    HoursWorkedPerMonth   INT             NOT NULL 
        CONSTRAINT CK_PartTimeEmployees_HoursWorkedPerMonth 
        CHECK (HoursWorkedPerMonth > 0 AND HoursWorkedPerMonth <= 200),
    
    CONSTRAINT FK_PartTimeEmployees_Employees
        FOREIGN KEY (EmployeeId) 
        REFERENCES dbo.Employees(EmployeeId)
        ON DELETE CASCADE
);
GO

/*
===============================================================================
SUBCLASS TABLE: ContractEmployees
===============================================================================
*/
CREATE TABLE dbo.ContractEmployees (
    EmployeeId         INT             NOT NULL PRIMARY KEY,
    
    ContractAmount     DECIMAL(18, 2)  NOT NULL 
        CONSTRAINT CK_ContractEmployees_ContractAmount 
        CHECK (ContractAmount > 0),
        
    ContractEndDate    DATE            NOT NULL,
    
    CONSTRAINT FK_ContractEmployees_Employees
        FOREIGN KEY (EmployeeId) 
        REFERENCES dbo.Employees(EmployeeId)
        ON DELETE CASCADE
);
GO

/*
===============================================================================
HISTORICAL TRANSACTION TABLE: Payrolls
===============================================================================
*/
CREATE TABLE dbo.Payrolls (
    PayrollId                   INT             PRIMARY KEY IDENTITY(1,1),
    EmployeeId                  INT             NOT NULL,
    PayPeriod                   DATE            NOT NULL,
    
    GrossSalary                 DECIMAL(18, 2)  NOT NULL 
        CONSTRAINT CK_Payrolls_GrossSalary 
        CHECK (GrossSalary >= 0),
        
    TaxDeduction                DECIMAL(18, 2)  NOT NULL 
        CONSTRAINT CK_Payrolls_TaxDeduction 
        CHECK (TaxDeduction >= 0),
        
    HealthInsuranceDeduction    DECIMAL(18, 2)  NOT NULL 
        CONSTRAINT CK_Payrolls_HealthInsuranceDeduction 
        CHECK (HealthInsuranceDeduction >= 0),
        
    NetSalary                   DECIMAL(18, 2)  NOT NULL,
    GeneratedAt                 DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    
    /*
    ---------------------------------------------------------------------------
    BUSINESS RULE / AUDIT INTEGRITY:
    ON DELETE NO ACTION (explicitly configured) means SQL Server refuses any 
    DELETE on an Employee row that still has active Payroll records.
    
    Rationale: payroll records are legally and operationally immutable. 
    An employee who leaves the company must NOT cause loss of their 
    payment history (audits, taxes, disputes can occur years later).
    
    Implication for the application layer: EmployeeService.Delete() 
    must either:
      (a) refuse to delete employees with payroll history, OR
      (b) implement soft-delete (IsActive flag, never physical delete)
    
    This codebase uses option (a) for audit traceability.
    ---------------------------------------------------------------------------
    */
    CONSTRAINT FK_Payrolls_Employees 
        FOREIGN KEY (EmployeeId) 
        REFERENCES dbo.Employees(EmployeeId)
        ON DELETE NO ACTION, -- INTENTIONAL: payroll history is immutable
    
    -- Prevents double generation of payroll logs for the same employee in the same period
    CONSTRAINT UQ_Payroll_PerEmployeePerPeriod 
        UNIQUE (EmployeeId, PayPeriod)
);
GO

/*
===============================================================================
PERFORMANCE INDEXES
===============================================================================
*/
-- Optimizes queries filtering or grouping employees by department
CREATE INDEX IX_Employees_Department ON dbo.Employees(Department);

-- Optimizes conditional JOINS in TPT polymorphic queries
CREATE INDEX IX_Employees_EmployeeType ON dbo.Employees(EmployeeType);

-- Optimizes JOIN searches between Employees and historical payroll records
CREATE INDEX IX_Payrolls_EmployeeId ON dbo.Payrolls(EmployeeId);

-- Optimizes payroll ledger analysis over chronological intervals
CREATE INDEX IX_Payrolls_PayPeriod ON dbo.Payrolls(PayPeriod);
GO
