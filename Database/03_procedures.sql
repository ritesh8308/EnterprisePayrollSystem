/*
===============================================================================
PROJECT: Enterprise Payroll System
FILE: 03_procedures.sql
PURPOSE: Creates the T-SQL stored procedures for database interactions.
EXECUTION ORDER: Run this script third, after 02_seed.sql. Can be re-run anytime.

NAMING CONVENTION:
- prefix: usp_<Action><Entity> (e.g. usp_GetAllEmployees, usp_InsertFullTimeEmployee)
  "usp" stands for User Stored Procedure. This is the industry-standard naming 
  convention to differentiate them from system-defined procedures (which start 
  with "sp_"). This prefix avoids the master database catalog search lookup 
  penalty that SQL Server incurs for "sp_" prefixes.

EXCEPTION-HANDLING DESIGN (THROW; vs RAISERROR):
- All write procedures utilize a standard CATCH block with `THROW;`.
- Unlike RAISERROR, which intercepts and maps errors to general user-defined 
  error 50000, `THROW;` preserves the original SQL error code (e.g., error 547 
  for Foreign Key violation, error 2627 for Unique constraint conflict).
- This enables the application data-access layer (ADO.NET) to catch the specific 
  exception codes and map them cleanly to domain-specific C# exceptions.
===============================================================================
*/

USE PayrollDB;
GO

/*
===============================================================================
1. usp_GetAllEmployees (READ — TPT JOIN)
===============================================================================
*/
IF OBJECT_ID('dbo.usp_GetAllEmployees', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetAllEmployees;
GO

CREATE PROCEDURE dbo.usp_GetAllEmployees
AS
BEGIN
    SET NOCOUNT ON; -- Suppresses "(1 row affected)" messages to optimize ADO.NET reading

    BEGIN TRY
        SELECT 
            e.EmployeeId,
            e.FullName,
            e.Email,
            e.Department,
            e.HireDate,
            e.EmployeeType,
            ft.MonthlySalary,
            pt.HourlyRate,
            pt.HoursWorkedPerMonth,
            ct.ContractAmount,
            ct.ContractEndDate
        FROM dbo.Employees e
        LEFT JOIN dbo.FullTimeEmployees ft ON e.EmployeeId = ft.EmployeeId
        LEFT JOIN dbo.PartTimeEmployees pt ON e.EmployeeId = pt.EmployeeId
        LEFT JOIN dbo.ContractEmployees ct ON e.EmployeeId = ct.EmployeeId
        ORDER BY e.Department, e.FullName;
    END TRY
    BEGIN CATCH
        THROW; -- Rethrow error preserving code, severity, state, and message
    END CATCH
END
GO

/*
===============================================================================
2. usp_GetEmployeeById (READ — TPT JOIN)
===============================================================================
*/
IF OBJECT_ID('dbo.usp_GetEmployeeById', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetEmployeeById;
GO

CREATE PROCEDURE dbo.usp_GetEmployeeById
    @EmployeeId INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT 
            e.EmployeeId,
            e.FullName,
            e.Email,
            e.Department,
            e.HireDate,
            e.EmployeeType,
            ft.MonthlySalary,
            pt.HourlyRate,
            pt.HoursWorkedPerMonth,
            ct.ContractAmount,
            ct.ContractEndDate
        FROM dbo.Employees e
        LEFT JOIN dbo.FullTimeEmployees ft ON e.EmployeeId = ft.EmployeeId
        LEFT JOIN dbo.PartTimeEmployees pt ON e.EmployeeId = pt.EmployeeId
        LEFT JOIN dbo.ContractEmployees ct ON e.EmployeeId = ct.EmployeeId
        WHERE e.EmployeeId = @EmployeeId;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END
GO

/*
===============================================================================
3. usp_InsertFullTimeEmployee (WRITE — Transactional)
===============================================================================
*/
IF OBJECT_ID('dbo.usp_InsertFullTimeEmployee', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_InsertFullTimeEmployee;
GO

CREATE PROCEDURE dbo.usp_InsertFullTimeEmployee
    @FullName       NVARCHAR(150),
    @Email          NVARCHAR(255),
    @Department     NVARCHAR(100),
    @HireDate       DATE,
    @MonthlySalary  DECIMAL(18, 2),
    @NewEmployeeId  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Insert into base shared table
        INSERT INTO dbo.Employees (FullName, Email, Department, HireDate, EmployeeType)
        VALUES (@FullName, @Email, @Department, @HireDate, 'FullTime');

        -- 2. Capture the newly generated EmployeeId safe from trigger pollution
        SET @NewEmployeeId = SCOPE_IDENTITY();

        -- 3. Insert specific subclass salary characteristics
        INSERT INTO dbo.FullTimeEmployees (EmployeeId, MonthlySalary)
        VALUES (@NewEmployeeId, @MonthlySalary);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Safely roll back active transactions on exception
        IF @@TRANCOUNT > 0 
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/*
===============================================================================
4. usp_InsertPartTimeEmployee (WRITE — Transactional)
===============================================================================
*/
IF OBJECT_ID('dbo.usp_InsertPartTimeEmployee', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_InsertPartTimeEmployee;
GO

CREATE PROCEDURE dbo.usp_InsertPartTimeEmployee
    @FullName            NVARCHAR(150),
    @Email               NVARCHAR(255),
    @Department          NVARCHAR(100),
    @HireDate            DATE,
    @HourlyRate          DECIMAL(18, 2),
    @HoursWorkedPerMonth INT,
    @NewEmployeeId       INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Insert into base shared table
        INSERT INTO dbo.Employees (FullName, Email, Department, HireDate, EmployeeType)
        VALUES (@FullName, @Email, @Department, @HireDate, 'PartTime');

        -- 2. Capture identity in current transaction execution scope
        SET @NewEmployeeId = SCOPE_IDENTITY();

        -- 3. Insert subclass characteristics
        INSERT INTO dbo.PartTimeEmployees (EmployeeId, HourlyRate, HoursWorkedPerMonth)
        VALUES (@NewEmployeeId, @HourlyRate, @HoursWorkedPerMonth);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/*
===============================================================================
5. usp_InsertContractEmployee (WRITE — Transactional)
===============================================================================
*/
IF OBJECT_ID('dbo.usp_InsertContractEmployee', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_InsertContractEmployee;
GO

CREATE PROCEDURE dbo.usp_InsertContractEmployee
    @FullName         NVARCHAR(150),
    @Email            NVARCHAR(255),
    @Department       NVARCHAR(100),
    @HireDate         DATE,
    @ContractAmount   DECIMAL(18, 2),
    @ContractEndDate  DATE,
    @NewEmployeeId    INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Insert into base shared table
        INSERT INTO dbo.Employees (FullName, Email, Department, HireDate, EmployeeType)
        VALUES (@FullName, @Email, @Department, @HireDate, 'Contract');

        -- 2. Capture identity
        SET @NewEmployeeId = SCOPE_IDENTITY();

        -- 3. Insert subclass characteristics
        INSERT INTO dbo.ContractEmployees (EmployeeId, ContractAmount, ContractEndDate)
        VALUES (@NewEmployeeId, @ContractAmount, @ContractEndDate);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/*
===============================================================================
6. usp_DeleteEmployee (WRITE — Transactional)
===============================================================================
*/
IF OBJECT_ID('dbo.usp_DeleteEmployee', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_DeleteEmployee;
GO

CREATE PROCEDURE dbo.usp_DeleteEmployee
    @EmployeeId INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        /*
        -----------------------------------------------------------------------
        AUDIT SYSTEM CRITICAL INTEGRITY CHECK:
        
        If the employee has payroll history, this DELETE will fail with 
        error 547 (FOREIGN KEY constraint conflict) due to the NO ACTION 
        FK on Payrolls.EmployeeId. This is INTENTIONAL — payroll records 
        must be preserved for audit/legal reasons. The application layer 
        catches this SqlException and surfaces a domain error.

        If there is no payroll history, deleting from dbo.Employees will 
        trigger the ON DELETE CASCADE constraints on the subclass tables 
        (FullTimeEmployees, PartTimeEmployees, ContractEmployees) to clean 
        them up automatically. No manual deletion in those tables is needed.
        -----------------------------------------------------------------------
        */
        DELETE FROM dbo.Employees 
        WHERE EmployeeId = @EmployeeId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/*
===============================================================================
7. usp_InsertPayroll (WRITE — Transactional)
===============================================================================
*/
IF OBJECT_ID('dbo.usp_InsertPayroll', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_InsertPayroll;
GO

CREATE PROCEDURE dbo.usp_InsertPayroll
    @EmployeeId               INT,
    @PayPeriod                DATE,
    @GrossSalary              DECIMAL(18, 2),
    @TaxDeduction             DECIMAL(18, 2),
    @HealthInsuranceDeduction DECIMAL(18, 2),
    @NetSalary                DECIMAL(18, 2),
    @NewPayrollId             INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        /*
        Note: The UNIQUE (EmployeeId, PayPeriod) constraint on Payrolls 
        will automatically trigger a duplicate key exception (error 2627) 
        if we attempt to create two payroll lines for the same employee in 
        the same calendar period.
        */
        INSERT INTO dbo.Payrolls (
            EmployeeId, 
            PayPeriod, 
            GrossSalary, 
            TaxDeduction, 
            HealthInsuranceDeduction, 
            NetSalary
        )
        VALUES (
            @EmployeeId, 
            @PayPeriod, 
            @GrossSalary, 
            @TaxDeduction, 
            @HealthInsuranceDeduction, 
            @NetSalary
        );

        SET @NewPayrollId = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/*
===============================================================================
8. usp_GetPayrollsByEmployee (READ)
===============================================================================
*/
IF OBJECT_ID('dbo.usp_GetPayrollsByEmployee', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetPayrollsByEmployee;
GO

CREATE PROCEDURE dbo.usp_GetPayrollsByEmployee
    @EmployeeId INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT 
            PayrollId,
            EmployeeId,
            PayPeriod,
            GrossSalary,
            TaxDeduction,
            HealthInsuranceDeduction,
            NetSalary,
            GeneratedAt
        FROM dbo.Payrolls
        WHERE EmployeeId = @EmployeeId
        ORDER BY PayPeriod DESC; -- Most recent entries first
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END
GO

-- Verification Queries
PRINT '=== Stored Procedures Created ===';
SELECT name AS ProcedureName
FROM sys.procedures
WHERE name LIKE 'usp_%'
ORDER BY name;
GO
