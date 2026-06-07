using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using EnterprisePayrollSystem.Helpers;
using EnterprisePayrollSystem.Models;
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
