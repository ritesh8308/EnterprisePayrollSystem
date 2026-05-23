using System;

namespace EnterprisePayrollSystem.Models;

/// <summary>
/// Represents the abstract base class for all employee types within the Enterprise Payroll System.
/// This class serves as the architectural cornerstone of the system, demonstrating:
/// 1. ABSTRACTION: Defined as an abstract class that cannot be instantiated directly and establishes a common contract.
/// 2. ENCAPSULATION: Enforces read-only immutability from outside, validates internal state invariants upon construction.
/// 3. INHERITANCE: Serves as a base class for specialized subclasses (e.g., FullTime, PartTime, Contract).
/// 4. POLYMORPHISM: Defines abstract and virtual methods that subclasses implement or override dynamically.
/// </summary>
// ABSTRACTION: Declaring the class as abstract prevents direct instantiation using the 'new' keyword.
public abstract class Employee
{
    // ENCAPSULATION: Public get allows read access; private set blocks external modifications.
    public int EmployeeId { get; private set; }
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string Department { get; private set; }
    public DateTime HireDate { get; private set; }

    // ENCAPSULATION: Protected set allows inherited subclasses to set/identify their discriminator type,
    // which will later map to the database schema, while keeping it read-only for public consumers.
    public string EmployeeType { get; protected set; }

    /// <summary>
    /// Protected constructor ensures this base class can only be instantiated through its concrete subclasses.
    /// </summary>
    protected Employee(
        int employeeId,
        string fullName,
        string email,
        string department,
        DateTime hireDate,
        string employeeType)
    {
        // ENCAPSULATION: Explicit validation in constructor protects object invariants and state integrity.
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Full name cannot be null, empty, or whitespace.", nameof(fullName));
        }


/****************************************************************************************************************************************************************************************************************************************************************************************************************************************************************************************
        // ENCAPSULATION: Ensure basic structural correctness of email address.
        // TEACHABLE TRADE-OFF: A simple Contains('@') check is highly naive. In a real-world enterprise system,
        // you should parse the email using System.Net.Mail.MailAddress or validate it via rigorous Regular Expressions (Regex).
****************************************************************************************************************************************************************************************************************************************************************************************************************************************************************************************/        
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ArgumentException("Email must not be null/empty and must contain a valid '@' character.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(department))
        {
            throw new ArgumentException("Department cannot be null, empty, or whitespace.", nameof(department));
        }


/****************************************************************************************************************************************************************************************************************************************************************************************************************************************************************************************
        // ENCAPSULATION: Validate hire date is not in the future.
        // TIMEZONE CAVEAT: Comparing directly to DateTime.UtcNow.Date can cause false positives due to timezone offsets
        // (e.g., local time is tomorrow but UTC is still today). In production, DateTimeOffset or DateOnly should be used.
****************************************************************************************************************************************************************************************************************************************************************************************************************************************************************************************/      
        if (hireDate.Date > DateTime.UtcNow.Date)
        {
            throw new ArgumentException("Hire date cannot be in the future.", nameof(hireDate));
        }

        if (string.IsNullOrWhiteSpace(employeeType))
        {
            throw new ArgumentException("Employee type cannot be null, empty, or whitespace.", nameof(employeeType));
        }

        // Assign validated parameters to encapsulate values
        EmployeeId = employeeId;
        FullName = fullName;
        Email = email;
        Department = department;
        HireDate = hireDate;
        EmployeeType = employeeType;
    }

    /// <summary>
    /// Computes the gross annual salary of the employee.
    /// This is the abstraction contract — Employee defines THAT it is calculated; subclasses define HOW.
    /// </summary>
    // ABSTRACTION: Abstract method has no body here; subclasses MUST override and implement it.
    public abstract decimal CalculateGrossSalary();


    /// <summary>
    /// Returns a formatted summary of base employee information.
    /// This is marked virtual to allow subclasses to override and append type-specific characteristics.
    /// </summary>
    // POLYMORPHISM-READY: Virtual method provides a default implementation that subclasses CAN customize/extend.
    public virtual string GetEmployeeInfo()
    {
        return $"[{EmployeeType}] {FullName} (ID: {EmployeeId}) — {Department} | Hired: {HireDate:yyyy-MM-dd}";
    }

    /// <summary>
    /// Overrides the standard object ToString method to dynamically dispatch info formatting.
    /// </summary>
    // POLYMORPHISM: Standard override ensures dynamic binding to the specific subclass implementation at runtime.
    public override string ToString() => GetEmployeeInfo();
}
