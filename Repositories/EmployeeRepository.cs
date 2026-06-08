using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EnterprisePayrollSystem.Helpers;
using EnterprisePayrollSystem.Models;

namespace EnterprisePayrollSystem.Repositories;

/// <summary>
/// TPT-aware repository for managing Employee domain objects.
/// Acts as the data access layer translating database rows into strongly-typed 
/// polymorphic objects (FullTimeEmployee, PartTimeEmployee, ContractEmployee).
/// </summary>
public class EmployeeRepository
{
    /// <summary>
    /// Retrieves all employees from the database.
    /// </summary>
    /// <returns>A list of polymorphic <see cref="Employee"/> objects. Returns empty list if no employees exist.</returns>
    public List<Employee> GetAllEmployees()
    {
        // ERROR PROPAGATION: SqlException bubbles up unchanged
        var dataTable = DatabaseHelper.ExecuteReader("usp_GetAllEmployees");
        var employees = new List<Employee>();

        foreach (DataRow row in dataTable.Rows)
        {
            employees.Add(MapRowToEmployee(row));
        }

        return employees;
    }

    /// <summary>
    /// Retrieves a single employee by their ID.
    /// </summary>
    /// <param name="employeeId">The ID of the employee to retrieve.</param>
    /// <returns>The polymorphic <see cref="Employee"/> if found; otherwise, null.</returns>
    public Employee? GetEmployeeById(int employeeId)
    {
        var parameters = new[]
        {
            new SqlParameter("@EmployeeId", employeeId)
        };

        // ERROR PROPAGATION: SqlException bubbles up unchanged
        var dataTable = DatabaseHelper.ExecuteReader("usp_GetEmployeeById", parameters);

        // NULL HANDLING: when to return null (not found) vs throwing (corrupt data)
        if (dataTable.Rows.Count == 0)
        {
            return null;
        }

        return MapRowToEmployee(dataTable.Rows[0]);
    }

    /// <summary>
    /// Inserts a new full-time employee.
    /// </summary>
    /// <param name="employee">The full-time employee domain object to insert.</param>
    /// <returns>The newly generated EmployeeId.</returns>
    public int InsertFullTimeEmployee(FullTimeEmployee employee)
    {
        var parameters = new[]
        {
            new SqlParameter("@FullName", employee.FullName),
            new SqlParameter("@Email", employee.Email),
            new SqlParameter("@Department", employee.Department),
            new SqlParameter("@HireDate", employee.HireDate),
            new SqlParameter("@MonthlySalary", employee.MonthlySalary),
            new SqlParameter("@NewEmployeeId", SqlDbType.Int) { Direction = ParameterDirection.Output }
        };

        // ERROR PROPAGATION: SqlException bubbles up unchanged
        return DatabaseHelper.ExecuteNonQueryWithOutput("usp_InsertFullTimeEmployee", parameters, "@NewEmployeeId");
    }

    /// <summary>
    /// Inserts a new part-time employee.
    /// </summary>
    /// <param name="employee">The part-time employee domain object to insert.</param>
    /// <returns>The newly generated EmployeeId.</returns>
    public int InsertPartTimeEmployee(PartTimeEmployee employee)
    {
        var parameters = new[]
        {
            new SqlParameter("@FullName", employee.FullName),
            new SqlParameter("@Email", employee.Email),
            new SqlParameter("@Department", employee.Department),
            new SqlParameter("@HireDate", employee.HireDate),
            new SqlParameter("@HourlyRate", employee.HourlyRate),
            new SqlParameter("@HoursWorkedPerMonth", employee.HoursWorkedPerMonth),
            new SqlParameter("@NewEmployeeId", SqlDbType.Int) { Direction = ParameterDirection.Output }
        };

        // ERROR PROPAGATION: SqlException bubbles up unchanged
        return DatabaseHelper.ExecuteNonQueryWithOutput("usp_InsertPartTimeEmployee", parameters, "@NewEmployeeId");
    }

    /// <summary>
    /// Inserts a new contract employee.
    /// </summary>
    /// <param name="employee">The contract employee domain object to insert.</param>
    /// <returns>The newly generated EmployeeId.</returns>
    public int InsertContractEmployee(ContractEmployee employee)
    {
        var parameters = new[]
        {
            new SqlParameter("@FullName", employee.FullName),
            new SqlParameter("@Email", employee.Email),
            new SqlParameter("@Department", employee.Department),
            new SqlParameter("@HireDate", employee.HireDate),
            new SqlParameter("@ContractAmount", employee.ContractAmount),
            new SqlParameter("@ContractEndDate", employee.ContractEndDate),
            new SqlParameter("@NewEmployeeId", SqlDbType.Int) { Direction = ParameterDirection.Output }
        };

        // ERROR PROPAGATION: SqlException bubbles up unchanged
        return DatabaseHelper.ExecuteNonQueryWithOutput("usp_InsertContractEmployee", parameters, "@NewEmployeeId");
    }

    /// <summary>
    /// Deletes an employee by their ID.
    /// </summary>
    /// <param name="employeeId">The ID of the employee to delete.</param>
    public void DeleteEmployee(int employeeId)
    {
        var parameters = new[]
        {
            new SqlParameter("@EmployeeId", employeeId)
        };

        // ERROR PROPAGATION: SqlException bubbles up unchanged
        DatabaseHelper.ExecuteNonQuery("usp_DeleteEmployee", parameters);
    }

    /// <summary>
    /// Maps a DataRow to a polymorphic Employee domain object.
    /// </summary>
    private Employee MapRowToEmployee(DataRow row)
    {
        // DISCRIMINATOR PATTERN: switch on EmployeeType to determine subclass
        string type = (string)row["EmployeeType"];

        // SHARED COLUMNS: EmployeeId, FullName, Email, Department, HireDate appear in all branches
        int employeeId = (int)row["EmployeeId"];
        string fullName = (string)row["FullName"];
        string email = (string)row["Email"];
        string department = (string)row["Department"];
        DateTime hireDate = (DateTime)row["HireDate"];

        // POLYMORPHIC INSTANTIATION: construct the right subclass
        return type switch
        {
            // TYPE-SPECIFIC COLUMNS: only read the columns that exist for that subclass
            "FullTime" => new FullTimeEmployee(
                employeeId: employeeId,
                fullName: fullName,
                email: email,
                department: department,
                hireDate: hireDate,
                monthlySalary: row["MonthlySalary"] == DBNull.Value 
                    ? throw new InvalidOperationException("MonthlySalary cannot be null for a FullTime employee.") 
                    : (decimal)row["MonthlySalary"]
            ),

            "PartTime" => new PartTimeEmployee(
                employeeId: employeeId,
                fullName: fullName,
                email: email,
                department: department,
                hireDate: hireDate,
                hourlyRate: row["HourlyRate"] == DBNull.Value 
                    ? throw new InvalidOperationException("HourlyRate cannot be null for a PartTime employee.") 
                    : (decimal)row["HourlyRate"],
                hoursWorkedPerMonth: row["HoursWorkedPerMonth"] == DBNull.Value 
                    ? throw new InvalidOperationException("HoursWorkedPerMonth cannot be null for a PartTime employee.") 
                    : (int)row["HoursWorkedPerMonth"]
            ),

            "Contract" => new ContractEmployee(
                employeeId: employeeId,
                fullName: fullName,
                email: email,
                department: department,
                hireDate: hireDate,
                contractAmount: row["ContractAmount"] == DBNull.Value 
                    ? throw new InvalidOperationException("ContractAmount cannot be null for a Contract employee.") 
                    : (decimal)row["ContractAmount"],
                contractEndDate: row["ContractEndDate"] == DBNull.Value 
                    ? throw new InvalidOperationException("ContractEndDate cannot be null for a Contract employee.") 
                    : (DateTime)row["ContractEndDate"]
            ),

            _ => throw new InvalidOperationException(
                $"Unknown employee type '{type}' for employee ID {employeeId}")
        };
    }
}
