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

-- 1. Composite index on Orders: covers the date-range WHERE filter, the ORDER BY
CREATE INDEX idx_orders_date_customer_product 
ON Orders (OrderDate, CustomerId, ProductId);

-- 2. Composite index on Customers: covers the Country filter AND lets the join back to Orders.CustomerId
CREATE INDEX idx_customers_country_id 
ON Customers (Country, Id);

-- 3. Products PK is already indexed (assuming Id is PK), so the JOIN Products p ON p.Id = o.ProductId needs no extra index  PK lookups are already O(log n).