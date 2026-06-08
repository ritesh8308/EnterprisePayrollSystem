using System;
using System.Globalization;
using EnterprisePayrollSystem.Services;
using EnterprisePayrollSystem.Models;

namespace EnterprisePayrollSystem.Helpers;

/// <summary>
/// Top-level orchestration layer responsible for displaying menus, capturing user input,
/// calling services, catching domain exceptions, and displaying user-friendly messages.
/// </summary>
// ORCHESTRATION LAYER: coordinates services and presentation
public class MenuHelper
{
    private readonly EmployeeService _employeeService;
    private readonly PayrollService _payrollService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MenuHelper"/> class.
    /// </summary>
    /// <param name="employeeService">The employee service instance.</param>
    /// <param name="payrollService">The payroll service instance.</param>
    // DEPENDENCY INJECTION: Both services injected via constructor
    public MenuHelper(EmployeeService employeeService, PayrollService payrollService)
    {
        _employeeService = employeeService ?? throw new ArgumentNullException(nameof(employeeService));
        _payrollService = payrollService ?? throw new ArgumentNullException(nameof(payrollService));
    }

    /// <summary>
    /// Runs the interactive menu loop.
    /// </summary>
    public void Run()
    {
        bool running = true;
        while (running)
        {
            DisplayMainMenu();
            string choice = Console.ReadLine()?.Trim() ?? "";
            
            try
            {
                running = ProcessMenuChoice(choice);
            }
            catch (Exception ex)
            {
                DisplayError($"Unexpected error: {ex.Message}");
                Pause();
            }
        }
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Goodbye!");
        Console.ResetColor();
    }

    /// <summary>
    /// Displays the main menu options to the console.
    /// </summary>
    private void DisplayMainMenu()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  MAIN MENU");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("1. View All Employees");
        Console.WriteLine("2. View Specific Employee");
        Console.WriteLine("3. Insert Full-Time Employee");
        Console.WriteLine("4. Insert Part-Time Employee");
        Console.WriteLine("5. Insert Contract Employee");
        Console.WriteLine("6. Delete Employee");
        Console.WriteLine("7. View Payrolls for Employee");
        Console.WriteLine("8. Generate Payroll for Employee");
        Console.WriteLine("9. Exit");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.Write("Enter choice (1-9): ");
    }

    /// <summary>
    /// Dispatches control depending on the user menu choice.
    /// </summary>
    private bool ProcessMenuChoice(string choice)
    {
        switch (choice)
        {
            case "1": MenuViewAllEmployees(); return true;
            case "2": MenuViewSpecificEmployee(); return true;
            case "3": MenuInsertFullTimeEmployee(); return true;
            case "4": MenuInsertPartTimeEmployee(); return true;
            case "5": MenuInsertContractEmployee(); return true;
            case "6": MenuDeleteEmployee(); return true;
            case "7": MenuViewPayrollsForEmployee(); return true;
            case "8": MenuGeneratePayroll(); return true;
            case "9": return false;  // Exit
            default:
                DisplayError("Invalid choice. Please enter 1-9.");
                Pause();
                return true;
        }
    }

    /// <summary>
    /// Fetches all employees from the service and prints details.
    /// </summary>
    private void MenuViewAllEmployees()
    {
        Console.WriteLine();
        DisplaySuccess("--- All Employees ---");
        var employees = _employeeService.GetAllEmployees();

        if (employees.Count == 0)
        {
            DisplayWarning("No employees registered in the system.");
        }
        else
        {
            foreach (var emp in employees)
            {
                Console.WriteLine($"  ID: {emp.EmployeeId} | Name: {emp.FullName} | Dept: {emp.Department} | Type: {emp.EmployeeType} | Gross Annual: {emp.CalculateGrossSalary():C}");
            }
        }
        Pause();
    }

    /// <summary>
    /// Fetches and displays a specific employee by ID.
    /// </summary>
    private void MenuViewSpecificEmployee()
    {
        Console.WriteLine();
        Console.WriteLine("--- View Specific Employee ---");
        int id = PromptForInt("Enter Employee ID: ");

        try
        {
            // EXCEPTION HANDLING: domain exceptions caught and displayed user-friendly
            var emp = _employeeService.GetEmployeeById(id);
            DisplaySuccess("Employee Details:");
            Console.WriteLine($"  {emp.GetEmployeeInfo()}");
            Console.WriteLine($"  Email: {emp.Email}");
        }
        catch (EmployeeNotFoundException ex)
        {
            DisplayError(ex.Message);
        }
        catch (InvalidEmployeeDataException ex)
        {
            DisplayError(ex.Message);
        }
        Pause();
    }

    /// <summary>
    /// Prompts for details to insert a new full-time employee with inline field validation.
    /// </summary>
    private void MenuInsertFullTimeEmployee()
    {
        Console.WriteLine();
        Console.WriteLine("--- Insert Full-Time Employee ---");
        
        // VALIDATE each field IMMEDIATELY with retry loops
        string fullName;
        while (true)
        {
            fullName = PromptForString("Enter Full Name: ");
            if (!string.IsNullOrEmpty(fullName))
                break;
            DisplayError("Full name cannot be empty.");
        }
        
        string email;
        while (true)
        {
            email = PromptForString("Enter Email: ");
            if (email.Contains('@'))
                break;
            DisplayError("Email must contain '@'.");
        }
        
        string department;
        while (true)
        {
            department = PromptForString("Enter Department: ");
            if (!string.IsNullOrEmpty(department))
                break;
            DisplayError("Department cannot be empty.");
        }
        
        DateTime hireDate = PromptForDate("Enter Hire Date (yyyy-MM-dd): ");
        decimal monthlySalary = PromptForDecimal("Enter Monthly Salary: ");

        try
        {
            int newId = _employeeService.InsertFullTimeEmployee(fullName, email, department, hireDate, monthlySalary);
            DisplaySuccess($"Employee inserted with ID: {newId}");
        }
        catch (InvalidEmployeeDataException ex)
        {
            DisplayError(ex.Message);
        }
        catch (DuplicateEmailException ex)
        {
            DisplayError(ex.Message);
        }
        Pause();
    }

    /// <summary>
    /// Prompts for details to insert a new part-time employee with inline field validation.
    /// </summary>
    private void MenuInsertPartTimeEmployee()
    {
        Console.WriteLine();
        Console.WriteLine("--- Insert Part-Time Employee ---");
        
        // VALIDATE each field IMMEDIATELY with retry loops
        string fullName;
        while (true)
        {
            fullName = PromptForString("Enter Full Name: ");
            if (!string.IsNullOrEmpty(fullName))
                break;
            DisplayError("Full name cannot be empty.");
        }
        
        string email;
        while (true)
        {
            email = PromptForString("Enter Email: ");
            if (email.Contains('@'))
                break;
            DisplayError("Email must contain '@'.");
        }
        
        string department;
        while (true)
        {
            department = PromptForString("Enter Department: ");
            if (!string.IsNullOrEmpty(department))
                break;
            DisplayError("Department cannot be empty.");
        }
        
        DateTime hireDate = PromptForDate("Enter Hire Date (yyyy-MM-dd): ");
        decimal hourlyRate = PromptForDecimal("Enter Hourly Rate: ");
        int hoursWorkedPerMonth = PromptForInt("Enter Hours Worked Per Month: ");

        try
        {
            int newId = _employeeService.InsertPartTimeEmployee(fullName, email, department, hireDate, hourlyRate, hoursWorkedPerMonth);
            DisplaySuccess($"Employee inserted with ID: {newId}");
        }
        catch (InvalidEmployeeDataException ex)
        {
            DisplayError(ex.Message);
        }
        catch (DuplicateEmailException ex)
        {
            DisplayError(ex.Message);
        }
        Pause();
    }

    /// <summary>
    /// Prompts for details to insert a new contract employee with inline field validation.
    /// </summary>
    private void MenuInsertContractEmployee()
    {
        Console.WriteLine();
        Console.WriteLine("--- Insert Contract Employee ---");
        
        // VALIDATE each field IMMEDIATELY with retry loops
        string fullName;
        while (true)
        {
            fullName = PromptForString("Enter Full Name: ");
            if (!string.IsNullOrEmpty(fullName))
                break;
            DisplayError("Full name cannot be empty.");
        }
        
        string email;
        while (true)
        {
            email = PromptForString("Enter Email: ");
            if (email.Contains('@'))
                break;
            DisplayError("Email must contain '@'.");
        }
        
        string department;
        while (true)
        {
            department = PromptForString("Enter Department: ");
            if (!string.IsNullOrEmpty(department))
                break;
            DisplayError("Department cannot be empty.");
        }
           
        // DATE VALIDATION: hire date must be <= today
        DateTime hireDate;
        while (true)
        {
            hireDate = PromptForDate("Enter Hire Date (yyyy-MM-dd): ");
            if (hireDate <= DateTime.UtcNow.Date)
                break;
            DisplayError("Hire date cannot be in the future.");
        }
        
        decimal contractAmount = PromptForDecimal("Enter Contract Amount: ");
        
        // DATE VALIDATION: contract end date must be > hire date
        DateTime contractEndDate;
        while (true)
        {
            contractEndDate = PromptForDate("Enter Contract End Date (yyyy-MM-dd): ");
            if (contractEndDate > hireDate)
                break;
            DisplayError("Contract end date must be after hire date.");
        }
        
        try
        {
            int newId = _employeeService.InsertContractEmployee(fullName, email, department, hireDate, contractAmount, contractEndDate);
            DisplaySuccess($"Employee inserted with ID: {newId}");
        }
        catch (InvalidEmployeeDataException ex)
        {
            DisplayError(ex.Message);
        }
        catch (DuplicateEmailException ex)
        {
            DisplayError(ex.Message);
        }
        Pause();
    }

    /// <summary>
    /// Prompts for ID to delete an employee.
    /// </summary>
    private void MenuDeleteEmployee()
    {
        Console.WriteLine();
        Console.WriteLine("--- Delete Employee ---");
        int id = PromptForInt("Enter Employee ID: ");

        try
        {
            _employeeService.DeleteEmployee(id);
            DisplaySuccess("Employee deleted successfully.");
        }
        catch (EmployeeHasPayrollsException ex)
        {
            DisplayError(ex.Message);
        }
        catch (InvalidEmployeeDataException ex)
        {
            DisplayError(ex.Message);
        }
        Pause();
    }

    /// <summary>
    /// Prompts for ID to view historical payrolls for an employee.
    /// </summary>
    private void MenuViewPayrollsForEmployee()
    {
        Console.WriteLine();
        int id = PromptForInt("Enter Employee ID: ");

        try
        {
            var payrolls = _payrollService.GetPayrollsByEmployee(id);
            DisplaySuccess($"--- Payrolls for Employee {id} ---");
            if (payrolls.Count == 0)
            {
                DisplayWarning("No payrolls found.");
            }
            else
            {
                foreach (var p in payrolls)
                {
                    Console.WriteLine($"  Period: {p.PayPeriod:yyyy-MM} | Gross: {p.GrossSalary:C} | Tax: {p.TaxDeduction:C} | Insurance: {p.HealthInsuranceDeduction:C} | Net: {p.NetSalary:C} | Generated: {p.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
                }
            }
        }
        catch (InvalidEmployeeDataException ex)
        {
            DisplayError(ex.Message);
        }
        Pause();
    }

    /// <summary>
    /// Prompts for ID and pay period to generate a new payroll.
    /// </summary>
    private void MenuGeneratePayroll()
    {
        Console.WriteLine();
        Console.WriteLine("--- Generate Payroll ---");
        int id = PromptForInt("Enter Employee ID: ");
        DateTime payPeriod = PromptForDate("Enter Pay Period (yyyy-MM-dd): ");

        try
        {
            int newId = _payrollService.GenerateAndInsertPayroll(id, payPeriod);
            DisplaySuccess($"Payroll generated with ID: {newId}");
        }
        catch (EmployeeNotFoundException ex)
        {
            DisplayError(ex.Message);
        }
        catch (DuplicatePayrollException ex)
        {
            DisplayError(ex.Message);
        }
        catch (InvalidEmployeeDataException ex)
        {
            DisplayError(ex.Message);
        }
        Pause();
    }

    /// <summary>
    /// Prompts for a string input (no validation — used in retry loops within handlers).
    /// </summary>
    private string PromptForString(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? "";
    }

    /// <summary>
    /// Prompts for an integer with retry loop until valid input is provided.
    /// </summary>
    // INPUT VALIDATION: int parsing with retry loop
    private int PromptForInt(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string input = Console.ReadLine()?.Trim() ?? "";
            if (int.TryParse(input, out int result))
            {
                return result;
            }
            DisplayError("Invalid. Enter a number.");
        }
    }

    /// <summary>
    /// Prompts for a decimal with retry loop until valid input is provided.
    /// </summary>
    // INPUT VALIDATION: decimal parsing with retry loop
    private decimal PromptForDecimal(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string input = Console.ReadLine()?.Trim() ?? "";
            if (decimal.TryParse(input, out decimal result))
            {
                return result;
            }
            DisplayError("Invalid. Enter a valid decimal number.");
        }
    }

    /// <summary>
    /// Prompts for a date with retry loop until valid input in yyyy-MM-dd format is provided.
    /// </summary>
    // INPUT VALIDATION: strict date parsing with yyyy-MM-dd format and retry loop
    private DateTime PromptForDate(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string input = Console.ReadLine()?.Trim() ?? "";
            if (DateTime.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
            DisplayError("Invalid format. Use yyyy-MM-dd (e.g., 2026-01-15).");
        }
    }

    /// <summary>
    /// Displays a success message in green.
    /// </summary>
    // COLOR-CODED OUTPUT: green for success, red for error, yellow for warning
    private void DisplaySuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    /// <summary>
    /// Displays an error message in red with a cross emoji.
    /// </summary>
    private void DisplayError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Displays a warning message in yellow with a warning emoji.
    /// </summary>
    private void DisplayWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠️  {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Pauses execution and waits for user to press Enter.
    /// </summary>
    private void Pause()
    {
        Console.WriteLine("\nPress Enter to continue...");
        Console.ReadLine();
    }
}