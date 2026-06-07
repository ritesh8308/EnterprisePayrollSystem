using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace EnterprisePayrollSystem.Helpers;

/// <summary>
/// Centralized database helper class serving as the sole ADO.NET wrapper in the codebase.
/// Encapsulates connection lifecycle, command preparation, and parameter binding for stored procedures,
/// ensuring resource safety and preventing connection leaks.
/// </summary>
public static class DatabaseHelper
{
    // PRODUCTION NOTE: Hardcoded for local dev only. Production code 
    // would load this from IConfiguration / environment variables / 
    // a secrets manager (Azure Key Vault, AWS Secrets Manager, etc.).
    private const string ConnectionString = 
        "Server=localhost,1433;Database=PayrollDB;User Id=sa;" +
        "Password=YourStrong!Pass123;TrustServerCertificate=True;";

    /// <summary>
    /// Creates a new, unopened SqlConnection instance.
    /// Caller is responsible for opening and disposing the connection.
    /// Typically used for advanced scenarios such as manual C# transaction control.
    /// </summary>
    /// <returns>A new <see cref="SqlConnection"/> instance.</returns>
    public static SqlConnection GetConnection()
    {
        return new SqlConnection(ConnectionString);
    }

    /// <summary>
    /// Executes a stored procedure that returns a result set, materializing the results into a DataTable.
    /// </summary>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">Optional parameters for the stored procedure.</param>
    /// <returns>A <see cref="DataTable"/> containing the materialized result set.</returns>
    public static DataTable ExecuteReader(string procedureName, SqlParameter[]? parameters = null)
    {
        // RESOURCE SAFETY: using statement guarantees Dispose() even on exceptions
        using var connection = new SqlConnection(ConnectionString);
        using var command = new SqlCommand(procedureName, connection)
        {
            // STORED PROC ONLY: CommandType.StoredProcedure forbids ad-hoc SQL injection vectors
            CommandType = CommandType.StoredProcedure
        };

        if (parameters != null && parameters.Length > 0)
        {
            command.Parameters.AddRange(parameters);
        }

        // ERROR PROPAGATION: SqlException flows up unchanged; repository will map error codes
        connection.Open();

        using var reader = command.ExecuteReader();
        var dataTable = new DataTable();
        
        // Materializing into a DataTable rather than streaming a SqlDataReader 
        // keeps the connection lifetime SHORT (open just for the load, then 
        // closed). Repositories work with the resulting in-memory snapshot. 
        // For this project's data scale, this is the right trade-off.
        dataTable.Load(reader);
        
        return dataTable;
    }

    /// <summary>
    /// Executes a stored procedure that performs an insert, update, or delete operation.
    /// </summary>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">Optional parameters for the stored procedure.</param>
    /// <returns>The number of rows affected by the operation.</returns>
    public static int ExecuteNonQuery(string procedureName, SqlParameter[]? parameters = null)
    {
        // RESOURCE SAFETY: using statement guarantees Dispose() even on exceptions
        using var connection = new SqlConnection(ConnectionString);
        using var command = new SqlCommand(procedureName, connection)
        {
            // STORED PROC ONLY: CommandType.StoredProcedure forbids ad-hoc SQL injection vectors
            CommandType = CommandType.StoredProcedure
        };

        if (parameters != null && parameters.Length > 0)
        {
            command.Parameters.AddRange(parameters);
        }

        // ERROR PROPAGATION: SqlException flows up unchanged; repository will map error codes
        connection.Open();
        return command.ExecuteNonQuery();
    }

    /// <summary>
    /// Executes a stored procedure and returns the value of an integer output parameter.
    /// </summary>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">The parameters for the stored procedure, including the output parameter.</param>
    /// <param name="outputParameterName">The name of the output parameter whose value should be retrieved.</param>
    /// <returns>The integer value of the output parameter.</returns>
    /// <exception cref="ArgumentException">Thrown when the output parameter is not found in the command's parameters.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the output parameter's value is DBNull.</exception>
    public static int ExecuteNonQueryWithOutput(
        string procedureName, 
        SqlParameter[] parameters, 
        string outputParameterName)
    {
        // RESOURCE SAFETY: using statement guarantees Dispose() even on exceptions
        using var connection = new SqlConnection(ConnectionString);
        using var command = new SqlCommand(procedureName, connection)
        {
            // STORED PROC ONLY: CommandType.StoredProcedure forbids ad-hoc SQL injection vectors
            CommandType = CommandType.StoredProcedure
        };

        if (parameters != null && parameters.Length > 0)
        {
            command.Parameters.AddRange(parameters);
        }

        // ERROR PROPAGATION: SqlException flows up unchanged; repository will map error codes
        connection.Open();
        command.ExecuteNonQuery();

        // OUTPUT PARAMETER: ParameterDirection.Output retrieves auto-generated IDs
        var outputParam = command.Parameters[outputParameterName];
        if (outputParam == null)
        {
            throw new ArgumentException(
                $"Output parameter '{outputParameterName}' was not found in the parameters collection for procedure '{procedureName}'.", 
                nameof(outputParameterName));
        }

        if (outputParam.Value == null || outputParam.Value == DBNull.Value)
        {
            throw new InvalidOperationException(
                $"Output parameter '{outputParameterName}' in procedure '{procedureName}' was DBNull — procedure did not assign it.");
        }

        return (int)outputParam.Value;
    }
}
