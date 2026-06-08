using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using EnterprisePayrollSystem.Helpers;
using EnterprisePayrollSystem.Models;
using EnterprisePayrollSystem.Repositories;
using EnterprisePayrollSystem.Services;
using Microsoft.Data.SqlClient;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("═══════════════════════════════════════════");
Console.WriteLine("  Enterprise Payroll Management System");
Console.WriteLine("═══════════════════════════════════════════");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Status: Initialized");

Console.ResetColor();
Console.WriteLine("Connecting to: localhost:1433");

Console.WriteLine();
Console.WriteLine("--- Polymorphism Demo ---");

List<Employee> staff = new()
{
    new FullTimeEmployee(1, "Alice Johnson", "alice@corp.com", 
        "Engineering", new DateTime(2020, 3, 15), 6500m),
    new PartTimeEmployee(2, "Bob Singh", "bob@corp.com", 
        "Support", new DateTime(2022, 8, 1), 30m, 100),
    new ContractEmployee(3, "Carol Reyes", "carol@corp.com", 
        "Design", new DateTime(2024, 1, 10), 45000m, 
        DateTime.UtcNow.AddMonths(6))
};

foreach (var emp in staff)
{
    Console.WriteLine(emp);  // calls ToString() → GetEmployeeInfo() polymorphically
}

decimal totalAnnualPayroll = staff.Sum(e => e.CalculateGrossSalary());
Console.WriteLine($"\nTotal Annual Payroll: ${totalAnnualPayroll:N2}");

Console.WriteLine();
Console.WriteLine("--- Payroll Generation Demo ---");

var payPeriod = new DateTime(2026, 5, 1);

foreach (var emp in staff)
{
    var payroll = Payroll.GenerateFor(emp, payPeriod);
    Console.WriteLine(payroll);
}

Console.WriteLine();
Console.WriteLine("--- DatabaseHelper Smoke Test ---");

try
{
    var employees = DatabaseHelper.ExecuteReader("usp_GetAllEmployees");
    Console.WriteLine($"Connected to database. Loaded {employees.Rows.Count} employee rows.");
    
    foreach (DataRow row in employees.Rows)
    {
        Console.WriteLine(
            $"  [{row["EmployeeType"]}] {row["FullName"]} " +
            $"— {row["Department"]} (ID: {row["EmployeeId"]})");
    }
}
catch (SqlException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Database error {ex.Number}: {ex.Message}");
    Console.ResetColor();
}

Console.WriteLine();
Console.WriteLine("--- EmployeeRepository Smoke Test ---");

var repo = new EmployeeRepository();
var allEmployees = repo.GetAllEmployees();

Console.WriteLine($"Loaded {allEmployees.Count} employees from repository.");
foreach (var emp in allEmployees)
{
    Console.WriteLine($"  [{emp.EmployeeType}] {emp.FullName} (ID: {emp.EmployeeId})");
}

Console.WriteLine();
Console.WriteLine("Testing GetEmployeeById(1) [should be Alice]:");
var alice = repo.GetEmployeeById(1);
if (alice != null)
{
    Console.WriteLine($"  Found: {alice.FullName} — {alice.GetEmployeeInfo()}");
}

Console.WriteLine();
Console.WriteLine("Testing GetEmployeeById(999) [should be null]:");
var notFound = repo.GetEmployeeById(999);
if (notFound == null)
{
    Console.WriteLine("  Correctly returned null (employee not found)");
}

Console.WriteLine();
Console.WriteLine("--- PayrollRepository Smoke Test ---");

var payrollRepo = new PayrollRepository();

// Test 1: Get payrolls for Alice (ID: 1)
Console.WriteLine("Payrolls for Alice (Employee ID: 1):");
var alicePayrolls = payrollRepo.GetPayrollsByEmployee(1);
if (alicePayrolls.Count == 0)
{
    Console.WriteLine("  No payrolls found.");
}
else
{
    foreach (var p in alicePayrolls)
    {
        Console.WriteLine($"  Period: {p.PayPeriod:yyyy-MM}, Gross: {p.GrossSalary:C}, Net: {p.NetSalary:C}");
    }
}

// Test 2: Get payrolls for a non-existent employee (ID: 999)
Console.WriteLine();
Console.WriteLine("Payrolls for non-existent employee (ID: 999):");
var notFoundPayrolls = payrollRepo.GetPayrollsByEmployee(999);
Console.WriteLine($"  {(notFoundPayrolls.Count == 0 ? "Correctly returned empty list" : "ERROR: Expected empty list")}");

Console.WriteLine();
Console.WriteLine("--- EmployeeService Smoke Test ---");

var empRepo = new EmployeeRepository();
var empService = new EmployeeService(empRepo);

// Test 1: Get all employees (valid)
Console.WriteLine("Test 1: GetAllEmployees() — valid");
var allEmps = empService.GetAllEmployees();
Console.WriteLine($"  ✓ Loaded {allEmps.Count} employees");

// Test 2: Get specific employee (valid)
Console.WriteLine("Test 2: GetEmployeeById(1) — valid (Alice)");
try
{
    var aliceSvc = empService.GetEmployeeById(1);
    Console.WriteLine($"  ✓ Found: {aliceSvc.FullName}");
}
catch (Exception ex)
{
    Console.WriteLine($"  ✗ Error: {ex.Message}");
}

// Test 3: Get non-existent employee (should raise EmployeeNotFoundException)
Console.WriteLine("Test 3: GetEmployeeById(999) — invalid (not found)");
try
{
    var notFoundSvc = empService.GetEmployeeById(999);
    Console.WriteLine($"  ✗ ERROR: Should have thrown EmployeeNotFoundException");
}
catch (EmployeeNotFoundException ex)
{
    Console.WriteLine($"  ✓ Correctly raised: {ex.Message}");
}

// Test 4: Try to insert with duplicate email (should raise DuplicateEmailException)
Console.WriteLine("Test 4: InsertFullTimeEmployee with duplicate email");
try
{
    // Alice's email is alice.johnson@corp.com (from seed data)
    empService.InsertFullTimeEmployee("Dummy Name", "alice.johnson@corp.com", "HR", DateTime.Now, 5000m);
    Console.WriteLine($"  ✗ ERROR: Should have thrown DuplicateEmailException");
}
catch (DuplicateEmailException ex)
{
    Console.WriteLine($"  ✓ Correctly raised: {ex.Message}");
}

// Test 5: Try to insert with invalid email (should raise InvalidEmployeeDataException)
Console.WriteLine("Test 5: InsertFullTimeEmployee with invalid email");
try
{
    empService.InsertFullTimeEmployee("Test User", "not-an-email", "HR", DateTime.Now, 5000m);
    Console.WriteLine($"  ✗ ERROR: Should have thrown InvalidEmployeeDataException");
}
catch (InvalidEmployeeDataException ex)
{
    Console.WriteLine($"  ✓ Correctly raised: {ex.Message}");
}

// Test 6: Try to delete an employee with payrolls (should raise EmployeeHasPayrollsException)
Console.WriteLine("Test 6: DeleteEmployee(1) — should fail (Alice has payrolls)");
try
{
    empService.DeleteEmployee(1);
    Console.WriteLine($"  ✗ ERROR: Should have thrown EmployeeHasPayrollsException");
}
catch (EmployeeHasPayrollsException ex)
{
    Console.WriteLine($"  ✓ Correctly raised: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("--- PayrollService Smoke Test ---");

var empRepoForService = new EmployeeRepository();
var payrollRepoForService = new PayrollRepository();
var payrollService = new PayrollService(empRepoForService, payrollRepoForService);

// Test 1: Get payrolls for Alice (valid)
Console.WriteLine("Test 1: GetPayrollsByEmployee(1) — valid (Alice)");
var alicePayrollsSvc = payrollService.GetPayrollsByEmployee(1);
Console.WriteLine($"  ✓ Found {alicePayrollsSvc.Count} payroll(s) for Alice");

// Test 2: Get payrolls for non-existent employee (should return empty list)
Console.WriteLine("Test 2: GetPayrollsByEmployee(999) — valid (not found, empty list)");
var notFoundPayrollsSvc = payrollService.GetPayrollsByEmployee(999);
Console.WriteLine($"  ✓ Correctly returned empty list ({notFoundPayrollsSvc.Count} payroll(s))");

var testPayPeriod = new DateTime(2026, 1, 1);  // December 2026 — unlikely to exist

// Test 3
Console.WriteLine($"Test 3: GenerateAndInsertPayroll(1, {testPayPeriod:yyyy-MM}) — valid (new period)");
try
{
    var newPayrollId = payrollService.GenerateAndInsertPayroll(1, testPayPeriod);
    Console.WriteLine($"  ✓ Generated and inserted payroll with ID: {newPayrollId}");
}
catch (Exception ex)
{
    Console.WriteLine($"  ✗ Error: {ex.Message}");
}

// Test 4: Generate payroll for non-existent employee (should raise EmployeeNotFoundException)
Console.WriteLine("Test 4: GenerateAndInsertPayroll(999, 2026-02) — invalid (employee not found)");
try
{
    payrollService.GenerateAndInsertPayroll(999, new DateTime(2026, 2, 1));
    Console.WriteLine($"  ✗ ERROR: Should have thrown EmployeeNotFoundException");
}
catch (EmployeeNotFoundException ex)
{
    Console.WriteLine($"  ✓ Correctly raised: {ex.Message}");
}

// Test 5: Try to generate duplicate payroll (should raise DuplicatePayrollException)
Console.WriteLine($"Test 5: GenerateAndInsertPayroll(1, {testPayPeriod:yyyy-MM}) — invalid (duplicate)");
try
{
    payrollService.GenerateAndInsertPayroll(1, testPayPeriod);
    Console.WriteLine($"  ✗ ERROR: Should have thrown DuplicatePayrollException");
}
catch (DuplicatePayrollException ex)
{
    Console.WriteLine($"  ✓ Correctly raised: {ex.Message}");
}

// Test 6: Try to generate payroll with invalid pay period (should raise InvalidEmployeeDataException)
Console.WriteLine("Test 6: GenerateAndInsertPayroll(1, MinValue) — invalid (bad period)");
try
{
    payrollService.GenerateAndInsertPayroll(1, DateTime.MinValue);
    Console.WriteLine($"  ✗ ERROR: Should have thrown InvalidEmployeeDataException");
}
catch (InvalidEmployeeDataException ex)
{
    Console.WriteLine($"  ✓ Correctly raised: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("Starting interactive menu...");
Console.WriteLine();

var finalEmployeeRepo = new EmployeeRepository();
var finalPayrollRepo = new PayrollRepository();
var finalEmployeeService = new EmployeeService(finalEmployeeRepo);
var finalPayrollServiceInstance = new PayrollService(finalEmployeeRepo, finalPayrollRepo);
var finalMenu = new MenuHelper(finalEmployeeService, finalPayrollServiceInstance);

finalMenu.Run();
