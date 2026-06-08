using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using EnterprisePayrollSystem.Helpers;
using EnterprisePayrollSystem.Models;
using EnterprisePayrollSystem.Repositories;

namespace EnterprisePayrollSystem.Services;

/// <summary>
/// Service layer class responsible for validating inputs, orchestrating repository calls,
/// and translating database constraint violations into meaningful domain exceptions.
/// </summary>
public class EmployeeService
{
    private readonly EmployeeRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmployeeService"/> class with constructor injection.
    /// </summary>
    /// <param name="repository">The employee repository instance.</param>
    // DEPENDENCY INJECTION: Repository injected via constructor
    public EmployeeService(EmployeeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Retrieves all employees in the system.
    /// </summary>
    /// <returns>A list of all employees.</returns>
    // REPOSITORY ABSTRACTION: service doesn't know about SQL or DataRows; only domain objects
    public List<Employee> GetAllEmployees()
    {
        // No validation needed for retrieving all records.
        // Exception propagates unchanged if it's an unexpected DB issue.
        return _repository.GetAllEmployees();
    }

    /// <summary>
    /// Retrieves a single employee by their ID.
    /// </summary>
    /// <param name="employeeId">The ID of the employee to retrieve.</param>
    /// <returns>The employee instance if found.</returns>
    /// <exception cref="InvalidEmployeeDataException">Thrown when employeeId is not positive.</exception>
    /// <exception cref="EmployeeNotFoundException">Thrown when the employee does not exist.</exception>
    public Employee GetEmployeeById(int employeeId)
    {
        // FAIL-FAST VALIDATION: validate ID before repository call
        if (employeeId <= 0)
        {
            throw new InvalidEmployeeDataException("Employee ID must be positive.");
        }

        var employee = _repository.GetEmployeeById(employeeId);
        if (employee == null)
        {
            throw new EmployeeNotFoundException(employeeId);
        }

        return employee;
    }

    /// <summary>
    /// Registers a new full-time employee with input validation.
    /// </summary>
    public int InsertFullTimeEmployee(
        string fullName, 
        string email, 
        string department, 
        DateTime hireDate, 
        decimal monthlySalary)
    {
        // FAIL-FAST VALIDATION: all business rules checked synchronously
        ValidateBaseEmployeeData(fullName, email, department, hireDate);

        if (monthlySalary <= 0)
        {
            throw new InvalidEmployeeDataException("Monthly salary must be greater than zero.");
        }

        var emp = new FullTimeEmployee(
            employeeId: 0, 
            fullName: fullName, 
            email: email, 
            department: department, 
            hireDate: hireDate, 
            monthlySalary: monthlySalary);

        try
        {
            return _repository.InsertFullTimeEmployee(emp);
        }
        catch (SqlException ex)
        {
            // ERROR MAPPING: SQL error 2627 (UNIQUE violation) → domain exception DuplicateEmailException
            // EXCEPTION PROPAGATION: only domain exceptions escape; SqlException caught and mapped
            if (ex.Number == 2627)
            {
                throw new DuplicateEmailException(email);
            }
            throw;
        }
    }

    /// <summary>
    /// Registers a new part-time employee with input validation.
    /// </summary>
    public int InsertPartTimeEmployee(
        string fullName, 
        string email, 
        string department, 
        DateTime hireDate, 
        decimal hourlyRate, 
        int hoursWorkedPerMonth)
    {
        // FAIL-FAST VALIDATION: all business rules checked synchronously
        ValidateBaseEmployeeData(fullName, email, department, hireDate);

        if (hourlyRate <= 0)
        {
            throw new InvalidEmployeeDataException("Hourly rate must be greater than zero.");
        }

        if (hoursWorkedPerMonth < 0 || hoursWorkedPerMonth > 200)
        {
            throw new InvalidEmployeeDataException("Hours per month must be between 0 and 200.");
        }

        var emp = new PartTimeEmployee(
            employeeId: 0, 
            fullName: fullName, 
            email: email, 
            department: department, 
            hireDate: hireDate, 
            hourlyRate: hourlyRate, 
            hoursWorkedPerMonth: hoursWorkedPerMonth);

        try
        {
            return _repository.InsertPartTimeEmployee(emp);
        }
        catch (SqlException ex)
        {
            // ERROR MAPPING: SQL error 2627 (UNIQUE violation) → domain exception DuplicateEmailException
            if (ex.Number == 2627)
            {
                throw new DuplicateEmailException(email);
            }
            throw;
        }
    }

    /// <summary>
    /// Registers a new contract employee with input validation.
    /// </summary>
    public int InsertContractEmployee(
        string fullName, 
        string email, 
        string department, 
        DateTime hireDate, 
        decimal contractAmount, 
        DateTime contractEndDate)
    {
        // FAIL-FAST VALIDATION: all business rules checked synchronously
        ValidateBaseEmployeeData(fullName, email, department, hireDate);

        if (contractAmount <= 0)
        {
            throw new InvalidEmployeeDataException("Contract amount must be greater than zero.");
        }

        if (contractEndDate <= hireDate)
        {
            throw new InvalidEmployeeDataException("Contract end date must be after hire date.");
        }

        var emp = new ContractEmployee(
            employeeId: 0, 
            fullName: fullName, 
            email: email, 
            department: department, 
            hireDate: hireDate, 
            contractAmount: contractAmount, 
            contractEndDate: contractEndDate);

        try
        {
            return _repository.InsertContractEmployee(emp);
        }
        catch (SqlException ex)
        {
            // ERROR MAPPING: SQL error 2627 (UNIQUE violation) → domain exception DuplicateEmailException
            if (ex.Number == 2627)
            {
                throw new DuplicateEmailException(email);
            }
            throw;
        }
    }

    /// <summary>
    /// Deletes an employee by their ID.
    /// </summary>
    /// <param name="employeeId">The ID of the employee to delete.</param>
    public void DeleteEmployee(int employeeId)
    {
        // FAIL-FAST VALIDATION: check ID before repository call
        if (employeeId <= 0)
        {
            throw new InvalidEmployeeDataException("Employee ID must be positive.");
        }

        try
        {
            _repository.DeleteEmployee(employeeId);
        }
        catch (SqlException ex)
        {
            // ERROR MAPPING: SQL error 547 (FOREIGN KEY violation) → domain exception EmployeeHasPayrollsException
            // EXCEPTION PROPAGATION: only domain exceptions escape; SqlException caught and mapped
            if (ex.Number == 547)
            {
                throw new EmployeeHasPayrollsException(employeeId);
            }
            throw;
        }
    }

    /// <summary>
    /// Shared helper to validate base employee attributes.
    /// </summary>
    private static void ValidateBaseEmployeeData(string fullName, string email, string department, DateTime hireDate)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvalidEmployeeDataException("Full name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new InvalidEmployeeDataException("Email must be valid (contain '@').");
        }

        if (string.IsNullOrWhiteSpace(department))
        {
            throw new InvalidEmployeeDataException("Department cannot be empty.");
        }

        if (hireDate.Date > DateTime.UtcNow.Date)
        {
            throw new InvalidEmployeeDataException("Hire date cannot be in the future.");
        }
    }
}
