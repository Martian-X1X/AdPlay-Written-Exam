--Customer Order Analysis
--Constraint : Single Query
SELECT
    Id, CustomerId, OrderDate,
    Amount AS CurrentOrderAmount,
    LAG(Amount) OVER (PARTITION BY CustomerId ORDER BY OrderDate) AS PreviousOrderAmount,
    Amount - LAG(Amount) OVER (PARTITION BY CustomerId ORDER BY OrderDate) AS Difference,
    SUM(Amount) OVER (PARTITION BY CustomerId ORDER BY OrderDate) AS RunningTotal
FROM Orders
ORDER BY CustomerId, OrderDate;