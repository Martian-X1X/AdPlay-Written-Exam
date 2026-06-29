SELECT
    o.Id, o.CustomerId, o.ProductId, o.OrderDate, o.Amount,
    c.Name AS CustomerName, c.Country,
    p.Name AS ProductName
FROM Orders o
JOIN Customers c ON c.Id = o.CustomerId
JOIN Products p ON p.Id = o.ProductId
WHERE o.OrderDate >= '2026-06-01'
  AND o.OrderDate <  '2026-07-01'
  AND c.Country = 'BD'
ORDER BY o.OrderDate DESC;

-- Date range instead of YEAR()/MONTH()
-- SELECT * replaced with explicit columns


--Indexes to Create

-- Speeds up the date-range filter + ORDER BY (same column covers both)
CREATE INDEX idx_orders_orderdate ON Orders (OrderDate);

-- Speeds up the Orders → Customers join
CREATE INDEX idx_orders_customerid ON Orders (CustomerId);

-- Speeds up the Orders → Products join
CREATE INDEX idx_orders_productid ON Orders (ProductId);

-- Speeds up filtering Customers by Country
CREATE INDEX idx_customers_country ON Customers (Country);

-- If OrderDate range-filtering and the join to Customers both matter, we could combine into a composite index
CREATE INDEX idx_orders_date_customer ON Orders (OrderDate, CustomerId);