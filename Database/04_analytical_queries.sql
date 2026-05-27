/*
===============================================================================
PROJECT: Enterprise Payroll System
FILE: 04_analytical_queries.sql
PURPOSE: Standalone analytical and reporting queries (The Reporting Cookbook).
EXECUTION CONTEXT: Run these queries individually for ad-hoc analysis.
                   These are read-only reports, not part of the runtime C# 
                   application data access path (which uses stored procedures).

TECHNIQUES DEMONSTRATED:
- Common Table Expressions (CTEs) for modular query isolation
- Window functions (ROW_NUMBER, DENSE_RANK) for partitions and rankings
- Conditional aggregations (SUM with CASE WHEN)
- Polymorphic mappings via UNION ALL across subclass tables (TPT joins)
===============================================================================
*/

USE PayrollDB;
GO

/* =========================================================================
QUERY 1 — Latest Payroll Per Employee
=========================================================================
BUSINESS QUESTION:
"For each employee, show their most recent payroll record. If they have no 
payroll history, still list them with NULL payroll fields."

SQL TECHNIQUES DEMONSTRATED:
- CTE (Common Table Expression) to pre-aggregate and isolate rankings
- ROW_NUMBER() window function for chronological recency filtering
- LEFT OUTER JOIN to preserve parent records with no child data
========================================================================= */

-- CTE to partition and rank payroll records per employee by date
WITH RankedPayrolls AS (
    SELECT 
        PayrollId,
        EmployeeId,
        PayPeriod,
        GrossSalary,
        NetSalary,
        GeneratedAt,
        -- PARTITION BY: creates independent chronological ranking queues per employee
        -- TEACHABLE: We use ROW_NUMBER() rather than RANK/DENSE_RANK here because we 
        -- want EXACTLY one row per employee, even if a database anomaly allowed two 
        -- payroll records to be generated on the identical PayPeriod.
        ROW_NUMBER() OVER (
            PARTITION BY EmployeeId 
            ORDER BY PayPeriod DESC
        ) AS RecencyRank
    FROM dbo.Payrolls
)
SELECT 
    e.EmployeeId,
    e.FullName,
    e.Department,
    e.EmployeeType,
    rp.PayPeriod    AS LatestPayPeriod,
    rp.GrossSalary  AS LatestGrossSalary,
    rp.NetSalary    AS LatestNetSalary
FROM dbo.Employees e
-- TEACHABLE: We use a LEFT JOIN instead of an INNER JOIN here so that employees 
-- who have never been paid (e.g., newly hired employees with no payroll logs) 
-- are still displayed in the report, rendering with NULL salary fields.
LEFT JOIN RankedPayrolls rp 
    ON e.EmployeeId = rp.EmployeeId 
   AND rp.RecencyRank = 1
ORDER BY e.Department, e.FullName;
GO

/* =========================================================================
QUERY 2 — Salary Ranking Within Department
=========================================================================
BUSINESS QUESTION:
"Rank FullTime employees by annual salary within their department. 
Who is the top earner in Engineering vs Design vs Support?"

SQL TECHNIQUES DEMONSTRATED:
- TPT Join (base table Joined to subclass table)
- DENSE_RANK() window function for competitive tie ranking
- Partitioning by categorical attributes
========================================================================= */

-- CTE to perform TPT join and calculate annual salaries for FullTime staff
WITH FullTimeAnnualSalaries AS (
    SELECT 
        e.EmployeeId,
        e.FullName,
        e.Department,
        ft.MonthlySalary,
        ft.MonthlySalary * 12 AS AnnualSalary
    -- TEACHABLE: We use an INNER JOIN here because only FullTime employees are 
    -- stored in the FullTimeEmployees subclass table. This naturally filters out 
    -- PartTime and Contract employees, ensuring an "apples-to-apples" salary audit.
    FROM dbo.Employees e
    INNER JOIN dbo.FullTimeEmployees ft 
        ON e.EmployeeId = ft.EmployeeId
)
SELECT 
    Department,
    FullName,
    AnnualSalary,
    -- TEACHABLE: DENSE_RANK() is ideal for competitive audits. Unlike ROW_NUMBER() 
    -- (which assigns sequential unique numbers and hides ties), DENSE_RANK() allows 
    -- tied employees to share the identical rank position. Unlike RANK() (which skips 
    -- positions after a tie, e.g., 1, 2, 2, 4), DENSE_RANK() keeps numbering contiguous.
    DENSE_RANK() OVER (
        PARTITION BY Department 
        ORDER BY AnnualSalary DESC
    ) AS SalaryRankInDept
FROM FullTimeAnnualSalaries
ORDER BY Department, SalaryRankInDept;
GO

/* =========================================================================
QUERY 3 — Salary Band Categorization
=========================================================================
BUSINESS QUESTION:
"Categorize all employees into salary bands. How many employees 
fall into each band across the company?"

SQL TECHNIQUES DEMONSTRATED:
- Polymorphic UNION ALL across subclass tables (TPT reconstruction)
- CASE WHEN conditional categorization
- GROUP BY aggregate reporting
========================================================================= */

-- CTE to polymorphically compile annual salaries across all TPT subclasses
WITH EmployeeAnnualSalaries AS (
    -- FullTime: Monthly Salary × 12
    SELECT e.EmployeeId, e.FullName, e.Department, 
           ft.MonthlySalary * 12 AS AnnualSalary, 
           'FullTime' AS EmployeeType
    FROM dbo.Employees e
    INNER JOIN dbo.FullTimeEmployees ft ON e.EmployeeId = ft.EmployeeId
    
    -- TEACHABLE: We use UNION ALL instead of UNION. UNION performs a costly sorting 
    -- and duplicate-checking pass across the entire set. Because our TPT tables are 
    -- mutually exclusive by design, duplicates are physically impossible. UNION ALL 
    -- combines these sets instantly in a single, high-performance operation.
    UNION ALL
    
    -- PartTime: Hourly Rate × Hours worked × 12
    SELECT e.EmployeeId, e.FullName, e.Department, 
           pt.HourlyRate * pt.HoursWorkedPerMonth * 12 AS AnnualSalary,
           'PartTime'
    FROM dbo.Employees e
    INNER JOIN dbo.PartTimeEmployees pt ON e.EmployeeId = pt.EmployeeId
    
    UNION ALL
    
    -- Contract: Flat contract amount (no multiplier)
    SELECT e.EmployeeId, e.FullName, e.Department, 
           ct.ContractAmount AS AnnualSalary,
           'Contract'
    FROM dbo.Employees e
    INNER JOIN dbo.ContractEmployees ct ON e.EmployeeId = ct.EmployeeId
),
BandedEmployees AS (
    SELECT 
        EmployeeId,
        FullName,
        Department,
        EmployeeType,
        AnnualSalary,
        -- TEACHABLE: Order matters. WHEN clauses are evaluated top-down and 
        -- short-circuit on first match. If we wrote >=40000 before >=80000, 
        -- a $90k earner would match 'Mid Earner' and never reach 'High Earner'.
        -- Always order from MOST RESTRICTIVE to LEAST RESTRICTIVE.
        CASE 
            WHEN AnnualSalary >= 80000 THEN 'High Earner ($80k+)'
            WHEN AnnualSalary >= 40000 THEN 'Mid Earner ($40k–$79k)'
            ELSE 'Entry Level (<$40k)'
        END AS SalaryBand
    FROM EmployeeAnnualSalaries
)
-- Perform aggregation based on the calculated salary bands
SELECT 
    SalaryBand,
    COUNT(*) AS EmployeeCount,
    MIN(AnnualSalary) AS BandMin,
    MAX(AnnualSalary) AS BandMax,
    AVG(AnnualSalary) AS BandAvg
FROM BandedEmployees
GROUP BY SalaryBand
ORDER BY BandMin DESC;
GO

/* =========================================================================
QUERY 4 — Department Aggregate Report
=========================================================================
BUSINESS QUESTION:
"Give me a department-by-department summary: how many employees, 
total annual payroll cost, average salary, and breakdown by employee type."

SQL TECHNIQUES DEMONSTRATED:
- UNION ALL TPT polymorphic salary calculations
- GROUP BY categorical summaries with multiple aggregated calculations
- Conditional aggregation via SUM(CASE WHEN...)
========================================================================= */

-- CTE compiling all polymorphic annual salaries
WITH EmployeeAnnualSalaries AS (
    SELECT e.EmployeeId, e.Department, 
           ft.MonthlySalary * 12 AS AnnualSalary, 
           'FullTime' AS EmployeeType
    FROM dbo.Employees e
    INNER JOIN dbo.FullTimeEmployees ft ON e.EmployeeId = ft.EmployeeId
    
    UNION ALL
    
    SELECT e.EmployeeId, e.Department, 
           pt.HourlyRate * pt.HoursWorkedPerMonth * 12, 'PartTime'
    FROM dbo.Employees e
    INNER JOIN dbo.PartTimeEmployees pt ON e.EmployeeId = pt.EmployeeId
    
    UNION ALL
    
    SELECT e.EmployeeId, e.Department, 
           ct.ContractAmount, 'Contract'
    FROM dbo.Employees e
    INNER JOIN dbo.ContractEmployees ct ON e.EmployeeId = ct.EmployeeId
)
SELECT 
    Department,
    COUNT(*) AS TotalHeadcount,
    SUM(AnnualSalary) AS TotalAnnualPayroll,
    AVG(AnnualSalary) AS AvgAnnualSalary,
    MIN(AnnualSalary) AS MinAnnualSalary,
    MAX(AnnualSalary) AS MaxAnnualSalary,
    
    -- TEACHABLE: Conditional Aggregation. SUM(CASE WHEN [Condition] THEN 1 ELSE 0 END) 
    -- is the senior-standard idiom for counting specific subsets of a category within 
    -- a single scan. A junior approach might involve performing three costly JOINs or 
    -- subqueries; this method aggregates all columns in a single, highly efficient pass.
    SUM(CASE WHEN EmployeeType = 'FullTime' THEN 1 ELSE 0 END) AS FullTimeCount,
    SUM(CASE WHEN EmployeeType = 'PartTime' THEN 1 ELSE 0 END) AS PartTimeCount,
    SUM(CASE WHEN EmployeeType = 'Contract' THEN 1 ELSE 0 END) AS ContractCount
FROM EmployeeAnnualSalaries
GROUP BY Department
-- Executive standard: Sort by largest department payroll cost first
ORDER BY TotalAnnualPayroll DESC;
GO

/* =========================================================================
QUERY 5 — Combined Dashboard: Top Earner Per Department
=========================================================================
BUSINESS QUESTION:
"Show me the top earner in each department, along with that 
department's total headcount and total annual payroll."

SQL TECHNIQUES DEMONSTRATED:
- Chained CTEs (using a comma separator to connect multiple named queries)
- CTE-to-CTE joining
- ROW_NUMBER() window function for categorical top-spot isolation
========================================================================= */

-- CTE 1: Reconstruct unified annual salaries
WITH AllEmployeeSalaries AS (
    SELECT e.EmployeeId, e.FullName, e.Department, 
           ft.MonthlySalary * 12 AS AnnualSalary
    FROM dbo.Employees e
    INNER JOIN dbo.FullTimeEmployees ft ON e.EmployeeId = ft.EmployeeId
    
    UNION ALL
    
    SELECT e.EmployeeId, e.FullName, e.Department, 
           pt.HourlyRate * pt.HoursWorkedPerMonth * 12
    FROM dbo.Employees e
    INNER JOIN dbo.PartTimeEmployees pt ON e.EmployeeId = pt.EmployeeId
    
    UNION ALL
    
    SELECT e.EmployeeId, e.FullName, e.Department, ct.ContractAmount
    FROM dbo.Employees e
    INNER JOIN dbo.ContractEmployees ct ON e.EmployeeId = ct.EmployeeId
),

-- CTE 2: Aggregate statistical metrics per department
-- TEACHABLE: Chaining CTEs. We are applying the DRY (Don't Repeat Yourself) principle 
-- to SQL. By breaking complex dashboards into distinct named blocks (one for base salaries, 
-- one for department averages, one for employee rankings), the SQL remains clean and readable.
DepartmentStats AS (
    SELECT 
        Department,
        COUNT(*) AS Headcount,
        SUM(AnnualSalary) AS TotalAnnualPayroll
    FROM AllEmployeeSalaries
    GROUP BY Department
),

-- CTE 3: Rank employees chronologically/financially within their department
TopEarnerPerDept AS (
    SELECT 
        EmployeeId,
        FullName,
        Department,
        AnnualSalary,
        ROW_NUMBER() OVER (
            PARTITION BY Department 
            ORDER BY AnnualSalary DESC
        ) AS DeptRank
    FROM AllEmployeeSalaries
)

-- Final Select: Combines structural department averages with individual top-earners
SELECT 
    ds.Department,
    ds.Headcount,
    ds.TotalAnnualPayroll,
    te.FullName        AS TopEarnerName,
    te.AnnualSalary    AS TopEarnerSalary
FROM DepartmentStats ds
INNER JOIN TopEarnerPerDept te 
    ON ds.Department = te.Department 
   AND te.DeptRank = 1
ORDER BY ds.TotalAnnualPayroll DESC;
GO

PRINT '=== Analytical Reporting Cookbook Queries Verified ===';
GO
