/*
===============================================================================
PROJECT: Enterprise Payroll System
FILE: 02_seed.sql
PURPOSE: Seeds sample employee profiles, subclass details, and payroll history.
EXECUTION ORDER: Run this script second, after 01_schema.sql has successfully executed.

ARCHITECTURAL INTENT:
1. DUAL-INSERT TPT WRITES:
   - Under Table Per Type (TPT), an employee's data is split across two tables.
   - Inserting an employee requires:
     (a) Inserting the shared base columns into the Employees table.
     (b) Capturing the newly generated ID immediately.
     (c) Inserting the type-specific columns into the respective subclass table.
   - Using SCOPE_IDENTITY() is critical. Unlike @@IDENTITY, it is restricted to
     the current local execution scope and prevents pollution from database triggers.
   - Wrap the operations in transactional blocks (BEGIN/COMMIT TRANSACTION) to
     guarantee atomicity: either both tables are written, or neither is.
===============================================================================
*/

USE PayrollDB;
GO

-- 1. Idempotent Data Truncation (Order is critical due to NO ACTION and FK relations)
-- Payrolls must be deleted first because it references Employees under ON DELETE NO ACTION
DELETE FROM dbo.Payrolls;
DELETE FROM dbo.FullTimeEmployees;
DELETE FROM dbo.PartTimeEmployees;
DELETE FROM dbo.ContractEmployees;
DELETE FROM dbo.Employees;

-- Reset identity counters back to zero
IF IDENT_CURRENT('dbo.Employees') > 1
    DBCC CHECKIDENT ('dbo.Employees', RESEED, 0);

IF IDENT_CURRENT('dbo.Payrolls') > 1
    DBCC CHECKIDENT ('dbo.Payrolls', RESEED, 0);
GO

-- 2. Seed Employee Profiles (TPT Transactions)
BEGIN TRANSACTION;

BEGIN TRY
    -- Variable to hold the captured Identity value across steps
    DECLARE @NewId INT;

    -- =========================================================================
    -- EMPLOYEE 1: Alice Johnson (FullTime)
    -- =========================================================================
    INSERT INTO dbo.Employees (FullName, Email, Department, HireDate, EmployeeType)
    VALUES ('Alice Johnson', 'alice.johnson@corp.com', 'Engineering', '2020-03-15', 'FullTime');
    
    -- Capture the auto-generated identity safe from trigger pollution
    SET @NewId = SCOPE_IDENTITY();
    
    INSERT INTO dbo.FullTimeEmployees (EmployeeId, MonthlySalary)
    VALUES (@NewId, 6500.00);

    -- =========================================================================
    -- EMPLOYEE 2: Daniel Park (FullTime)
    -- =========================================================================
    INSERT INTO dbo.Employees (FullName, Email, Department, HireDate, EmployeeType)
    VALUES ('Daniel Park', 'daniel.park@corp.com', 'Engineering', '2019-07-22', 'FullTime');
    
    SET @NewId = SCOPE_IDENTITY();
    
    INSERT INTO dbo.FullTimeEmployees (EmployeeId, MonthlySalary)
    VALUES (@NewId, 7800.00);

    -- =========================================================================
    -- EMPLOYEE 3: Bob Singh (PartTime)
    -- =========================================================================
    INSERT INTO dbo.Employees (FullName, Email, Department, HireDate, EmployeeType)
    VALUES ('Bob Singh', 'bob.singh@corp.com', 'Support', '2022-08-01', 'PartTime');
    
    SET @NewId = SCOPE_IDENTITY();
    
    INSERT INTO dbo.PartTimeEmployees (EmployeeId, HourlyRate, HoursWorkedPerMonth)
    VALUES (@NewId, 30.00, 100);

    -- =========================================================================
    -- EMPLOYEE 4: Priya Mehta (PartTime)
    -- =========================================================================
    INSERT INTO dbo.Employees (FullName, Email, Department, HireDate, EmployeeType)
    VALUES ('Priya Mehta', 'priya.mehta@corp.com', 'Support', '2023-02-14', 'PartTime');
    
    SET @NewId = SCOPE_IDENTITY();
    
    INSERT INTO dbo.PartTimeEmployees (EmployeeId, HourlyRate, HoursWorkedPerMonth)
    VALUES (@NewId, 35.00, 120);

    -- =========================================================================
    -- EMPLOYEE 5: Carol Reyes (Contract)
    -- =========================================================================
    INSERT INTO dbo.Employees (FullName, Email, Department, HireDate, EmployeeType)
    VALUES ('Carol Reyes', 'carol.reyes@corp.com', 'Design', '2024-01-10', 'Contract');
    
    SET @NewId = SCOPE_IDENTITY();
    
    INSERT INTO dbo.ContractEmployees (EmployeeId, ContractAmount, ContractEndDate)
    VALUES (@NewId, 45000.00, '2026-11-23');

    -- =========================================================================
    -- EMPLOYEE 6: Marco Bianchi (Contract)
    -- =========================================================================
    INSERT INTO dbo.Employees (FullName, Email, Department, HireDate, EmployeeType)
    VALUES ('Marco Bianchi', 'marco.bianchi@corp.com', 'Design', '2025-06-01', 'Contract');
    
    SET @NewId = SCOPE_IDENTITY();
    
    INSERT INTO dbo.ContractEmployees (EmployeeId, ContractAmount, ContractEndDate)
    VALUES (@NewId, 62000.00, '2027-05-31');

    -- Commit transaction if all inserts succeed
    COMMIT TRANSACTION;
    PRINT 'Seed data for Employees successfully committed.';
END TRY
BEGIN CATCH
    -- Rollback everything if an error occurs to ensure database remains clean
    ROLLBACK TRANSACTION;
    PRINT 'Transaction rolled back due to error: ' + ERROR_MESSAGE();
END CATCH;
GO

-- 3. Seed Payroll History Logs
-- Historical payment details for Alice (Id 1) and Bob (Id 3)
INSERT INTO dbo.Payrolls (EmployeeId, PayPeriod, GrossSalary, TaxDeduction, HealthInsuranceDeduction, NetSalary)
VALUES
    (1, '2026-03-01', 78000.00, 15600.00, 200.00, 62200.00),
    (1, '2026-04-01', 78000.00, 15600.00, 200.00, 62200.00),
    (3, '2026-03-01', 36000.00,  7200.00, 200.00, 28600.00),
    (3, '2026-04-01', 36000.00,  7200.00, 200.00, 28600.00);
GO

-- 4. Audit & Seed Verification Queries
PRINT '=== Seed Verification: Row Counts ===';

SELECT 'Employees' AS TableName, COUNT(*) AS RowCount FROM dbo.Employees
UNION ALL
SELECT 'FullTimeEmployees',   COUNT(*) FROM dbo.FullTimeEmployees
UNION ALL
SELECT 'PartTimeEmployees',   COUNT(*) FROM dbo.PartTimeEmployees
UNION ALL
SELECT 'ContractEmployees',   COUNT(*) FROM dbo.ContractEmployees
UNION ALL
SELECT 'Payrolls',            COUNT(*) FROM dbo.Payrolls;
GO

PRINT '=== Seed Verification: TPT Integrity (Orphan Detection) ===';

/*
Verify TPT relational integrity: every base employee row must join exactly 
to a corresponding child subclass row. If an ORPHAN is detected, it flags 
a logical data-entry bug where subclass mapping was missed.
*/
SELECT 
    e.EmployeeId, 
    e.FullName, 
    e.EmployeeType AS DeclaredType,
    CASE 
        WHEN ft.EmployeeId IS NOT NULL THEN 'FullTime'
        WHEN pt.EmployeeId IS NOT NULL THEN 'PartTime'
        WHEN ct.EmployeeId IS NOT NULL THEN 'Contract'
        ELSE 'ORPHAN — bug!'
    END AS DetectedType
FROM dbo.Employees e
LEFT JOIN dbo.FullTimeEmployees ft ON e.EmployeeId = ft.EmployeeId
LEFT JOIN dbo.PartTimeEmployees pt ON e.EmployeeId = pt.EmployeeId
LEFT JOIN dbo.ContractEmployees ct ON e.EmployeeId = ct.EmployeeId
ORDER BY e.EmployeeId;
GO
