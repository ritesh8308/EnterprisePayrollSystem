using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EnterprisePayrollSystem.Helpers;
using EnterprisePayrollSystem.Models;

namespace EnterprisePayrollSystem.Repositories;

/// <summary>
/// Repository class responsible for data access operations on immutable Payroll entities.
/// Translates database result sets into strongly-typed Payroll domain objects.
/// </summary>
public class PayrollRepository
{
    /// <summary>
    /// Retrieves all historical payroll records generated for a specific employee.
    /// Results are returned ordered by PayPeriod descending (most recent first).
    /// </summary>
    /// <param name="employeeId">The ID of the employee whose payrolls to retrieve.</param>
    /// <returns>A list of <see cref="Payroll"/> records. Returns empty list if none exist.</returns>
    public List<Payroll> GetPayrollsByEmployee(int employeeId)
    {
        var parameters = new[]
        {
            new SqlParameter("@EmployeeId", employeeId)
        };

        // ERROR PROPAGATION: SqlException bubbles up unchanged
        var dataTable = DatabaseHelper.ExecuteReader("usp_GetPayrollsByEmployee", parameters);
        var payrolls = new List<Payroll>();

        foreach (DataRow row in dataTable.Rows)
        {
            payrolls.Add(MapRowToPayroll(row));
        }

        return payrolls;
    }

    /// <summary>
    /// Inserts a new payroll record into the database.
    /// </summary>
    /// <param name="payroll">The immutable payroll record to persist.</param>
    /// <returns>The newly generated PayrollId.</returns>
    public int InsertPayroll(Payroll payroll)
    {
        var parameters = new[]
        {
            new SqlParameter("@EmployeeId", payroll.EmployeeId),
            new SqlParameter("@PayPeriod", payroll.PayPeriod),
            new SqlParameter("@GrossSalary", payroll.GrossSalary),
            new SqlParameter("@TaxDeduction", payroll.TaxDeduction),
            new SqlParameter("@HealthInsuranceDeduction", payroll.HealthInsuranceDeduction),
            new SqlParameter("@NetSalary", payroll.NetSalary),
            new SqlParameter("@NewPayrollId", SqlDbType.Int) { Direction = ParameterDirection.Output }
        };

        // ERROR PROPAGATION: SqlException bubbles up unchanged
        return DatabaseHelper.ExecuteNonQueryWithOutput("usp_InsertPayroll", parameters, "@NewPayrollId");
    }

    /// <summary>
    /// Maps a single DataRow from the database into an immutable Payroll instance.
    /// </summary>
    private Payroll MapRowToPayroll(DataRow row)
    {
        // DEFENSIVE CASTING: explicit (decimal) and (DateTime) casts from DataRow
        // SEALED ENTITY MAPPING: no discriminator pattern needed as Payroll has no subclasses
        // IMMUTABLE RECORD: Payroll is populated directly via the reconstruction constructor and remains read-only
        return new Payroll(
            payrollId: (int)row["PayrollId"],
            employeeId: (int)row["EmployeeId"],
            payPeriod: (DateTime)row["PayPeriod"],
            grossSalary: (decimal)row["GrossSalary"],
            taxDeduction: (decimal)row["TaxDeduction"],
            healthInsuranceDeduction: (decimal)row["HealthInsuranceDeduction"],
            netSalary: (decimal)row["NetSalary"],
            generatedAt: (DateTime)row["GeneratedAt"]
        );
    }
}
