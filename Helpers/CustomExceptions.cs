using System;

namespace EnterprisePayrollSystem.Helpers;

/// <summary>
/// Exception thrown when a requested employee cannot be found in the database.
/// </summary>
public class EmployeeNotFoundException : Exception
{
    public EmployeeNotFoundException(int employeeId)
        : base($"Employee with ID {employeeId} not found.")
    {
    }
}

/// <summary>
/// Exception thrown when attempting to delete an employee that has existing payroll history.
/// </summary>
public class EmployeeHasPayrollsException : Exception
{
    public EmployeeHasPayrollsException(int employeeId)
        : base($"Cannot delete employee {employeeId}: payroll history exists. Audit integrity enforced.")
    {
    }
}

/// <summary>
/// Exception thrown when inserting an employee with an email that is already registered.
/// </summary>
public class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string email)
        : base($"An employee with email '{email}' already exists.")
    {
    }
}

/// <summary>
/// Exception thrown when input validation fails for employee data.
/// </summary>
public class InvalidEmployeeDataException : Exception
{
    public InvalidEmployeeDataException(string message)
        : base(message)
    {
    }
}
