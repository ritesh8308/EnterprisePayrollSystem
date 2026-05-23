using System;

namespace EnterprisePayrollSystem.Models;

/// <summary>
/// Represents a concrete part-time hourly employee.
/// Demonstrates INHERITANCE (reusing properties and state from Employee) 
/// and POLYMORPHISM (customizes salary computation based on hourly rate).
/// </summary>
// OOP DESIGN: sealed because this is a concrete leaf type — extend Employee directly for new types.
// This prevents further uncontrolled inheritance and allows JIT compiler devirtualization optimizations.
public sealed class PartTimeEmployee : Employee
{
    /// <summary>
    /// Gets the hourly pay rate of the part-time employee.
    /// </summary>
    // ENCAPSULATION: Read-only property with private setter to restrict mutations.
    public decimal HourlyRate { get; private set; }

    /// <summary>
    /// Gets the number of hours worked per month by the part-time employee.
    /// </summary>
    // ENCAPSULATION: Read-only property with private setter to restrict mutations.
    public int HoursWorkedPerMonth { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PartTimeEmployee"/> class.
    /// </summary>
    public PartTimeEmployee(
        int employeeId,
        string fullName,
        string email,
        string department,
        DateTime hireDate,
        decimal hourlyRate,
        int hoursWorkedPerMonth)
        // INHERITANCE: Calls base constructor first, ensuring base validations run and base state is initialized.
        : base(employeeId, fullName, email, department, hireDate, "PartTime")
    {
        // ENCAPSULATION: Validate subclass-specific invariants after base initialization (fail-fast).
        if (hourlyRate <= 0)
        {
            throw new ArgumentException("Hourly rate must be greater than zero.", nameof(hourlyRate));
        }

        if (hoursWorkedPerMonth <= 0 || hoursWorkedPerMonth > 200)
        {
            throw new ArgumentException("Hours worked per month must be greater than zero and less than or equal to 200.", nameof(hoursWorkedPerMonth));
        }

        // ENCAPSULATION: Assign subclass-specific properties once validations clear.
        HourlyRate = hourlyRate;
        HoursWorkedPerMonth = hoursWorkedPerMonth;
    }

    /// <summary>
    /// Calculates the gross annual salary of the part-time employee.
    /// </summary>
    // POLYMORPHISM: Overrides the abstract method defined in the Employee base class.
    public override decimal CalculateGrossSalary() => HourlyRate * HoursWorkedPerMonth * 12;

    /// <summary>
    /// Appends part-time specific rate and hour details to the base employee info.
    /// </summary>
    // POLYMORPHISM: Overrides the virtual base method, augmenting it with part-time details.
    public override string GetEmployeeInfo()
    {
        return $"{base.GetEmployeeInfo()} | Rate: ${HourlyRate:N2}/hr × {HoursWorkedPerMonth}hrs/mo | Annual: ${CalculateGrossSalary():N2}";
    }
}
