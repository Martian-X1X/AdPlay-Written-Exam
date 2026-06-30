// Q12. DEPARTMENT STATISTICS pure LINQ

// Input model
public class Employee
{
    public string  Department { get; set; }
    public decimal Salary     { get; set; }
}

// Output model one row per department
public class DepartmentStats
{
    public string  Department    { get; set; }
    public decimal HighestSalary { get; set; }
    public decimal LowestSalary  { get; set; }
    public decimal AverageSalary { get; set; }
    public decimal MedianSalary  { get; set; }
}

public class EmployeeService
{
    public List<DepartmentStats> GetDepartmentStats(List<Employee> employees)
    {
        var result = employees
            .GroupBy(e => e.Department)
            .Select(g => new DepartmentStats
            {
                Department    = g.Key,
                HighestSalary = g.Max(e => e.Salary),
                LowestSalary  = g.Min(e => e.Salary),
                AverageSalary = g.Average(e => e.Salary),

                // Median
                // - Order the salaries
                // - Skip down to the middle (count-1)/2, take the 1 or 2
                //   middle elements depending on odd/even count
                // - Average them for odd count this just averages a
                //   single value (itself), for even count it averages
                //   the two true middle values. Same formula handles both.
                MedianSalary = g
                    .Select(e => e.Salary)
                    .OrderBy(s => s)
                    .Skip((g.Count() - 1) / 2)
                    .Take(2 - g.Count() % 2)
                    .Average()
            })
            .ToList();

        return result;
    }
}

// ---- USAGE -------------------------------------------------
var employees = new List<Employee>
{
    new() { Department="IT", Salary=1000 },
    new() { Department="IT", Salary=2000 },
    new() { Department="IT", Salary=3000 },
    new() { Department="HR", Salary=500  },
    new() { Department="HR", Salary=700  },
};

var stats = new EmployeeService().GetDepartmentStats(employees);
// Result:
//   IT  Highest:3000, Lowest:1000, Avg:2000, Median:2000
//   HR  Highest:700,  Lowest:500,  Avg:600,  Median:600