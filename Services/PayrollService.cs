using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using EnterprisePayrollSystem.Helpers;
using EnterprisePayrollSystem.Models;
using EnterprisePayrollSystem.Repositories;

namespace EnterprisePayrollSystem.Services;

/// <summary>
/// Service layer class responsible for managing payroll operations.
/// Enforces the business rule that payroll records are immutable historical data (no updates or deletes).
/// </summary>
public class PayrollService
{
    private readonly EmployeeRepository _employeeRepository;
    private readonly PayrollRepository _payrollRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollService"/> class.
    /// </summary>
    // DEPENDENCY INJECTION: Both repositories injected via constructor
    public PayrollService(EmployeeRepository employeeRepository, PayrollRepository payrollRepository)
    {
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _payrollRepository = payrollRepository ?? throw new ArgumentNullException(nameof(payrollRepository));
    }

    /// <summary>
    /// Retrieves all historical payroll records for a specific employee.
    /// </summary>
    /// <param name="employeeId">The ID of the employee.</param>
    /// <returns>A list of payroll records, or an empty list if none exist.</returns>
    public List<Payroll> GetPayrollsByEmployee(int employeeId)
    {
        // VALIDATION LAYER: check ID is positive
        if (employeeId <= 0)
        {
            throw new InvalidEmployeeDataException("Employee ID must be positive.");
        }

        return _payrollRepository.GetPayrollsByEmployee(employeeId);
    }

    /// <summary>
    /// Generates a new payroll record for an employee for a specific pay period, polymorphically
    /// calculating pay, and persists it to the database.
    /// </summary>
    /// <param name="employeeId">The ID of the employee.</param>
    /// <param name="payPeriod">The pay period month.</param>
    /// <returns>The newly generated PayrollId.</returns>
    public int GenerateAndInsertPayroll(int employeeId, DateTime payPeriod)
    {
        // IMMUTABLE RECORD: no updates or deletes, only generation and insertion
        
        // VALIDATION LAYER: check ID
        if (employeeId <= 0)
        {
            throw new InvalidEmployeeDataException("Employee ID must be positive.");
        }

        // VALIDATION LAYER: check pay period is valid
        if (payPeriod == DateTime.MinValue || payPeriod == DateTime.MaxValue)
        {
            throw new InvalidEmployeeDataException("Pay period cannot be MinValue or MaxValue.");
        }

        // VALIDATION LAYER: check pay period is not in the future (starts after the current month)
        var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        if (payPeriod > currentMonthStart)
        {
            throw new InvalidEmployeeDataException("Pay period cannot be in the future.");
        }

        // COMPOSITION: uses EmployeeRepository to validate employee existence
        var employee = _employeeRepository.GetEmployeeById(employeeId);
        if (employee == null)
        {
            throw new EmployeeNotFoundException(employeeId);
        }

        // POLYMORPHIC SALARY: Payroll.GenerateFor() calls employee.CalculateGrossSalary()
        var payroll = Payroll.GenerateFor(employee, payPeriod);

        try
        {
            return _payrollRepository.InsertPayroll(payroll);
        }
        catch (SqlException ex)
        {
            // ERROR MAPPING: SQL error 2627 (UNIQUE) → DuplicatePayrollException
            if (ex.Number == 2627)
            {
                throw new DuplicatePayrollException(employeeId, payPeriod);
            }
            throw;
        }
    }

    /// <summary>
    /// Persists a pre-built Payroll object directly to the database.
    /// Useful for migrations or bulk operations.
    /// </summary>
    /// <param name="payroll">The payroll object to persist.</param>
    /// <returns>The newly generated PayrollId.</returns>
    public int InsertPayroll(Payroll payroll)
    {
        if (payroll == null)
        {
            throw new ArgumentNullException(nameof(payroll));
        }

        if (payroll.EmployeeId <= 0)
        {
            throw new InvalidEmployeeDataException("Payroll employee ID must be positive.");
        }

        if (payroll.GrossSalary <= 0)
        {
            throw new InvalidEmployeeDataException("Payroll gross salary must be positive.");
        }

        try
        {
            return _payrollRepository.InsertPayroll(payroll);
        }
        catch (SqlException ex)
        {
            // ERROR MAPPING: SQL error 2627 (UNIQUE) → DuplicatePayrollException
            if (ex.Number == 2627)
            {
                throw new DuplicatePayrollException(payroll.EmployeeId, payroll.PayPeriod);
            }
            throw;
        }
    }
}
