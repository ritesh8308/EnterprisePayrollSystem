using System;

namespace EnterprisePayrollSystem.Models;

/// <summary>
/// Represents an immutable historical payroll record. Once generated, it can never be modified.
/// </summary>
// IMMUTABILITY: payroll records are frozen historical data — no public setters anywhere
public sealed class Payroll
{
    // BUSINESS RULE: Define key tax and deduction parameters driving the payroll calculation.
    private static readonly decimal TAX_RATE = 0.20m; // 20% tax deduction
    private static readonly decimal HEALTH_INSURANCE_DEDUCTION = 200m; // flat $200/month

    // IMMUTABILITY: All properties use private setters to guarantee that they cannot be modified after instantiation.
    public int PayrollId { get; private set; } = 0;
    public int EmployeeId { get; private set; }
    public DateTime PayPeriod { get; private set; }
    public decimal GrossSalary { get; private set; }
    public decimal TaxDeduction { get; private set; }
    public decimal HealthInsuranceDeduction { get; private set; }
    public decimal NetSalary { get; private set; }
    public DateTime GeneratedAt { get; private set; }

    /// <summary>
    /// Private constructor to prevent direct instantiation and force callers to use the static factory method or reconstruction constructor.
    /// </summary>
    private Payroll(
        int employeeId,
        DateTime payPeriod,
        decimal grossSalary,
        decimal taxDeduction,
        decimal healthInsuranceDeduction,
        decimal netSalary,
        DateTime generatedAt)
    {
        PayrollId = 0;
        EmployeeId = employeeId;
        PayPeriod = payPeriod;
        GrossSalary = grossSalary;
        TaxDeduction = taxDeduction;
        HealthInsuranceDeduction = healthInsuranceDeduction;
        NetSalary = netSalary;
        GeneratedAt = generatedAt;
    }

    /// <summary>
    /// Public constructor for database reconstruction.
    /// This allows repositories to rebuild Payroll from database rows.
    /// </summary>
    public Payroll(
        int payrollId,
        int employeeId,
        DateTime payPeriod,
        decimal grossSalary,
        decimal taxDeduction,
        decimal healthInsuranceDeduction,
        decimal netSalary,
        DateTime generatedAt)
    {
        PayrollId = payrollId;
        EmployeeId = employeeId;
        PayPeriod = payPeriod;
        GrossSalary = grossSalary;
        TaxDeduction = taxDeduction;
        HealthInsuranceDeduction = healthInsuranceDeduction;
        NetSalary = netSalary;
        GeneratedAt = generatedAt;
    }

    /// <summary>
    /// Factory method that encapsulates the full payroll calculation recipe.
    /// Computes gross salary polymorphically from the employee, applies business-rule deductions,
    /// and returns an immutable Payroll record.
    /// </summary>
    // FACTORY METHOD: Encapsulates the complete object construction sequence and fail-fast validation.
    public static Payroll GenerateFor(Employee employee, DateTime payPeriod)
    {
        if (employee == null)
        {
            throw new ArgumentNullException(nameof(employee), "Employee cannot be null.");
        }

        if (payPeriod == DateTime.MinValue || payPeriod == DateTime.MaxValue)
        {
            throw new ArgumentException("Pay period cannot be MinValue or MaxValue.", nameof(payPeriod));
        }

        // POLYMORPHISM: Calls CalculateGrossSalary() which dynamically dispatches to the correct subclass (FullTimeEmployee, PartTimeEmployee, ContractEmployee) based on the runtime type of 'employee'.
        decimal gross = employee.CalculateGrossSalary();

        decimal tax = gross * TAX_RATE;
        decimal insurance = HEALTH_INSURANCE_DEDUCTION;
        decimal net = gross - tax - insurance;

        return new Payroll(
            employeeId: employee.EmployeeId,
            payPeriod: payPeriod,
            grossSalary: gross,
            taxDeduction: tax,
            healthInsuranceDeduction: insurance,
            netSalary: net,
            generatedAt: DateTime.Now
        );
    }

    /// <summary>
    /// Overrides the standard object ToString method to print a beautifully formatted payroll record.
    /// </summary>
    public override string ToString()
    {
        return $"Payroll[Employee={EmployeeId}, Period={PayPeriod:yyyy-MM}, Gross=${GrossSalary:N2}, Tax=${TaxDeduction:N2}, Insurance=${HealthInsuranceDeduction:N2}, Net=${NetSalary:N2}, Generated={GeneratedAt:yyyy-MM-dd HH:mm:ss}]";
    }
}
