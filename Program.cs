using System;
using System.Data;
using EnterprisePayrollSystem.Helpers;
using EnterprisePayrollSystem.Repositories;
using EnterprisePayrollSystem.Services;
using Microsoft.Data.SqlClient;

/*
===============================================================================
ENTERPRISE PAYROLL MANAGEMENT SYSTEM — Main Entry Point
===============================================================================
This is the top-level orchestration layer. It:
1. Displays the application banner
2. Verifies database connectivity
3. Launches the interactive menu system

ARCHITECTURE:
- Database Layer       → SQL Server 2022 with stored procedures
- Data Access Layer   → Repositories (EmployeeRepository, PayrollRepository)
- Business Logic      → Services (EmployeeService, PayrollService)
- Presentation Layer  → MenuHelper (interactive console UI)

All layers are dependency-injected and loosely coupled.
===============================================================================
*/

// Display banner
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("═══════════════════════════════════════════");
Console.WriteLine("  Enterprise Payroll Management System");
Console.WriteLine("═══════════════════════════════════════════");
Console.ResetColor();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Status: Initialized");
Console.ResetColor();

// Verify database connectivity
Console.WriteLine("Connecting to: localhost:1433");

try
{
    var testData = DatabaseHelper.ExecuteReader("usp_GetAllEmployees");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✓ Database connected. Found {testData.Rows.Count} employees.");
    Console.ResetColor();
}
catch (SqlException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"✗ Database connection failed: {ex.Message}");
    Console.WriteLine("Please ensure SQL Server is running and accessible.");
    Console.ResetColor();
    return;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"✗ Unexpected error: {ex.Message}");
    Console.ResetColor();
    return;
}

Console.WriteLine();

// Instantiate dependency injection chain
var employeeRepository = new EmployeeRepository();
var payrollRepository = new PayrollRepository();
var employeeService = new EmployeeService(employeeRepository);
var payrollService = new PayrollService(employeeRepository, payrollRepository);
var menu = new MenuHelper(employeeService, payrollService);

// Launch interactive menu
Console.WriteLine("Starting interactive menu...");
Console.WriteLine();

menu.Run();