// Q12. DEPARTMENT STATISTICS — LINQ only, with Median
 
// Input model
public class Employee
{
    public string  Department { get; set; }
    public decimal Salary     { get; set; }
}
 
// Output model — one row per department
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
            .GroupBy(e => e.Department)  // group all employees by their department
            .Select(g =>
            {
                // Sort salaries so we can find median
                var sorted = g.Select(e => e.Salary).OrderBy(s => s).ToList();
                var count  = sorted.Count;
 
                // Median = middle value when sorted
                // Even count  - average of two middle values  
                // Odd count   - exact middle 
                var median = count % 2 == 0
                    ? (sorted[count / 2 - 1] + sorted[count / 2]) / 2m
                    : sorted[count / 2];
 
                return new DepartmentStats
                {
                    Department    = g.Key,
                    HighestSalary = g.Max(e => e.Salary),
                    LowestSalary  = g.Min(e => e.Salary),
                    AverageSalary = g.Average(e => e.Salary),
                    MedianSalary  = median
                };
            })
            .ToList();
 
        return result;
    }
}
 
// ---- USAGE -------------------------------------------------
// var employees = new List<Employee>
// {
//     new() { Department="IT", Salary=1000 },
//     new() { Department="IT", Salary=2000 },
//     new() { Department="IT", Salary=3000 },
//     new() { Department="HR", Salary=500  },
//     new() { Department="HR", Salary=700  },
// };
// var stats = new EmployeeService().GetDepartmentStats(employees);
// Result:
//   IT → Highest:3000, Lowest:1000, Avg:2000, Median:2000
//   HR → Highest:700,  Lowest:500,  Avg:600,  Median:600
 