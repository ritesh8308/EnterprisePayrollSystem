using System;

namespace EnterprisePayrollSystem.Models;

/// <summary>
/// Represents a concrete full-time salaried employee.
/// Demonstrates INHERITANCE (inherits properties and behavior from Employee) 
/// and POLYMORPHISM (customizes salary computation and info formatting).
/// </summary>
// OOP DESIGN: sealed because this is a concrete leaf type — extend Employee directly for new types.
// This prevents further uncontrolled inheritance and allows JIT compiler devirtualization optimizations.
public sealed class FullTimeEmployee : Employee
{
    /// <summary>
    /// Gets the monthly salary of the full-time employee.
    /// </summary>
    // ENCAPSULATION: Read-only property with private setter to restrict mutations.
    public decimal MonthlySalary { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FullTimeEmployee"/> class.
    /// </summary>
    public FullTimeEmployee(
        int employeeId,
        string fullName,
        string email,
        string department,
        DateTime hireDate,
        decimal monthlySalary)
        // INHERITANCE: Calls base constructor first, ensuring base validations run and base state is initialized.
        : base(employeeId, fullName, email, department, hireDate, "FullTime")
    {
        // ENCAPSULATION: Validate subclass-specific invariants after base initialization (fail-fast).
        if (monthlySalary <= 0)
        {
            throw new ArgumentException("Monthly salary must be greater than zero.", nameof(monthlySalary));
        }

        // ENCAPSULATION: Assign subclass-specific properties once validations clear.
        MonthlySalary = monthlySalary;
    }

    /// <summary>
    /// Calculates the gross annual salary of the full-time employee.
    /// </summary>
    // POLYMORPHISM: Overrides the abstract method defined in the Employee base class.
    public override decimal CalculateGrossSalary() => MonthlySalary * 12;

    /// <summary>
    /// Appends full-time specific salary information to the base employee info.
    /// </summary>
    // POLYMORPHISM: Overrides the virtual base method, augmenting it with full-time details.
    public override string GetEmployeeInfo()
    {
        return $"{base.GetEmployeeInfo()} | Monthly: ${MonthlySalary:N2} | Annual: ${CalculateGrossSalary():N2}";
    }
}
