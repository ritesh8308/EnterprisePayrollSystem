using System;

namespace EnterprisePayrollSystem.Models;

/// <summary>
/// Represents a concrete contract employee paid a flat total contract amount.
/// Demonstrates INHERITANCE (inherits properties and behavior from Employee) 
/// and POLYMORPHISM (customizes salary computation based on a flat contract sum).
/// </summary>
// OOP DESIGN: sealed because this is a concrete leaf type — extend Employee directly for new types.
// This prevents further uncontrolled inheritance and allows JIT compiler devirtualization optimizations.
public sealed class ContractEmployee : Employee
{
    /// <summary>
    /// Gets the flat rate contract payment amount.
    /// </summary>
    // ENCAPSULATION: Read-only property with private setter to restrict mutations.
    public decimal ContractAmount { get; private set; }

    /// <summary>
    /// Gets the end date of the employee's contract.
    /// </summary>
    // ENCAPSULATION: Read-only property with private setter to restrict mutations.
    public DateTime ContractEndDate { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractEmployee"/> class.
    /// </summary>
    public ContractEmployee(
        int employeeId,
        string fullName,
        string email,
        string department,
        DateTime hireDate,
        decimal contractAmount,
        DateTime contractEndDate)
        // INHERITANCE: Calls base constructor first, ensuring base validations run and base state is initialized.
        : base(employeeId, fullName, email, department, hireDate, "Contract")
    {
        // ENCAPSULATION: Validate subclass-specific invariants after base initialization (fail-fast).
        if (contractAmount <= 0)
        {
            throw new ArgumentException("Contract amount must be greater than zero.", nameof(contractAmount));
        }

        // ENCAPSULATION: Validate contract end date is in the future.
        // TIMEZONE CAVEAT: Comparing directly to DateTime.UtcNow.Date can cause false positives due to timezone offsets.
        // In a production-grade enterprise application, DateTimeOffset or DateOnly should be used.
        if (contractEndDate.Date <= DateTime.UtcNow.Date)
        {
            throw new ArgumentException("Contract end date must be in the future.", nameof(contractEndDate));
        }

        // ENCAPSULATION: Assign subclass-specific properties once validations clear.
        ContractAmount = contractAmount;
        ContractEndDate = contractEndDate;
    }

    /// <summary>
    /// Calculates the gross salary of the contract employee (returns flat contract amount).
    /// </summary>
    // POLYMORPHISM: Overrides the abstract method defined in the Employee base class.
    public override decimal CalculateGrossSalary() => ContractAmount;

    /// <summary>
    /// Appends contract-specific amount and end date details to the base employee info.
    /// </summary>
    // POLYMORPHISM: Overrides the virtual base method, augmenting it with contract details.
    public override string GetEmployeeInfo()
    {
        return $"{base.GetEmployeeInfo()} | Contract: ${ContractAmount:N2} | Ends: {ContractEndDate:yyyy-MM-dd}";
    }
}
