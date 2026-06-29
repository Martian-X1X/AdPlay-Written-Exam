-- Q1: Top 3 Highest Paid Employees from Each Department
-- Constraints: No LIMIT, no multiple nested subqueries, uses Window Functions

SELECT Id, Name, DepartmentId, Salary, JoiningDate
FROM (
    SELECT
        Id, Name, DepartmentId, Salary, JoiningDate,
        DENSE_RANK() OVER (PARTITION BY DepartmentId ORDER BY Salary DESC) AS rnk
    FROM Employee
) ranked
WHERE rnk <= 3
ORDER BY DepartmentId, Salary DESC;