-- CREATE Customer Table
CREATE TABLE Customers (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CustomerName VARCHAR(100),
    Country VARCHAR(50),
    City VARCHAR(50)
);

--CREATE Order TAble
CREATE TABLE Orders (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CustomerId INT NOT NULL,
    OrderDate DATE,
    Amount DECIMAL(10,2),
    Status VARCHAR(50),
    CONSTRAINT FK_Orders_Customers
        FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
);

--Insert Customer Data 
INSERT INTO Customers (CustomerName, Country, City) VALUES
('John Smith','USA','New York'),
('Emma Johnson','USA','Chicago'),
('Michael Brown','Canada','Toronto'),
('Sophia Davis','Canada','Vancouver'),
('William Miller','UK','London'),
('Olivia Wilson','UK','Manchester'),
('James Moore','India','Mumbai'),
('Ava Taylor','India','Delhi'),
('Benjamin Anderson','Australia','Sydney'),
('Isabella Thomas','Australia','Melbourne'),
('Daniel Jackson','Germany','Berlin'),
('Mia White','Germany','Munich'),
('Matthew Harris','France','Paris'),
('Charlotte Martin','France','Lyon'),
('David Thompson','Japan','Tokyo'),
('Amelia Garcia','Japan','Osaka'),
('Joseph Martinez','Brazil','Rio'),
('Harper Robinson','Brazil','Sao Paulo'),
('Christopher Clark','Mexico','Mexico City'),
('Evelyn Rodriguez','Mexico','Guadalajara');


--INSERT Order Data
INSERT INTO Orders (CustomerId, OrderDate, Amount, Status)
SELECT
 FLOOR(1+RAND()*20),
 DATE_ADD('2023-01-01', INTERVAL FLOOR(RAND()*730) DAY),
 ROUND(100+RAND()*4900,2),
 ELT(FLOOR(1+RAND()*4),'Pending','Processing','Completed','Cancelled')
FROM (
 SELECT 1
 FROM information_schema.columns a
 CROSS JOIN information_schema.columns b
 LIMIT 500
)t;

--------------------------------------------------------------------------------------------
-------------------------------------``````MAIN Procedure`````````--------------------------
--------------------------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_SearchOrders;

DELIMITER $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `hr_demo`.`sp_SearchOrders`(
    IN pCustomerName VARCHAR(100),
    IN pDateFrom DATE,
    IN pDateTo DATE,
    IN pMinAmount DECIMAL(10,2),
    IN pMaxAmount DECIMAL(10,2),
    IN pStatus VARCHAR(50),
    IN pCountry VARCHAR(50),
    IN pCity VARCHAR(50),
    IN pSortBy VARCHAR(30),
    IN pSortDirection VARCHAR(4),
    IN pPage INT,
    IN pPageSize INT
)
BEGIN

    DECLARE vSQL TEXT;
    DECLARE vOffset INT;

    IF pPageSize IS NULL OR pPageSize <= 0 THEN
        SET pPageSize = 20;
    END IF;

    SET vOffset = (IFNULL(pPage,1)-1) * pPageSize;

    IF vOffset < 0 THEN
        SET vOffset = 0;
    END IF;

    SET vSQL =
    'SELECT
        o.Id, c.CustomerName, c.Country, c.City, o.OrderDate, o.Amount, o.Status
     FROM Orders o
     INNER JOIN Customers c
        ON c.Id = o.CustomerId
     WHERE 1=1';

    IF pCustomerName IS NOT NULL AND pCustomerName <> '' THEN
        SET vSQL = CONCAT(vSQL,
        ' AND c.CustomerName LIKE ',
        QUOTE(CONCAT('%', pCustomerName, '%')));
    END IF;

    IF pDateFrom IS NOT NULL THEN
        SET vSQL = CONCAT(vSQL,
        ' AND o.OrderDate >= ',
        QUOTE(pDateFrom));
    END IF;

    IF pDateTo IS NOT NULL THEN
        SET vSQL = CONCAT(vSQL,
        ' AND o.OrderDate <= ',
        QUOTE(pDateTo));
    END IF;

    IF pMinAmount IS NOT NULL THEN
        SET vSQL = CONCAT(vSQL,
        ' AND o.Amount >= ',
        QUOTE(pMinAmount));
    END IF;

    IF pMaxAmount IS NOT NULL THEN
        SET vSQL = CONCAT(vSQL,
        ' AND o.Amount <= ',
        QUOTE(pMaxAmount));
    END IF;

    IF pStatus IS NOT NULL AND pStatus <> '' THEN
        SET vSQL = CONCAT(vSQL,
        ' AND o.Status = ',
        QUOTE(pStatus));
    END IF;

    IF pCountry IS NOT NULL AND pCountry <> '' THEN
        SET vSQL = CONCAT(vSQL,
        ' AND c.Country = ',
        QUOTE(pCountry));
    END IF;

    IF pCity IS NOT NULL AND pCity <> '' THEN
        SET vSQL = CONCAT(vSQL,
        ' AND c.City = ',
        QUOTE(pCity));
    END IF;

    -- Fixed: explicit NULL check before whitelist check
    IF pSortBy IS NULL OR pSortBy NOT IN ('CustomerName','OrderDate','Amount','Status') THEN
        SET pSortBy = 'OrderDate';
    END IF;

    IF pSortDirection IS NULL OR UPPER(pSortDirection) NOT IN ('ASC','DESC') THEN
        SET pSortDirection = 'DESC';
    END IF;

    SET vSQL =
    CONCAT(
        vSQL,
        ' ORDER BY ',
        CASE pSortBy
            WHEN 'CustomerName' THEN 'c.CustomerName'
            WHEN 'Amount' THEN 'o.Amount'
            WHEN 'Status' THEN 'o.Status'
            ELSE 'o.OrderDate'
        END,
        ' ',
        UPPER(pSortDirection),
        ' LIMIT ', pPageSize,
        ' OFFSET ', vOffset
    );

    SET @sql = vSQL;

    PREPARE stmt FROM @sql;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;

END$$

DELIMITER ;

--------------------------------------------------------------------------------------------------------------
-- Example:
CALL sp_SearchOrders(NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'OrderDate','DESC',1,10);
--First page
CALL sp_SearchOrders(NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'OrderDate','DESC',1,2);

--Customer Search
CALL sp_SearchOrders('John',NULL,NULL,NULL,NULL,NULL,NULL,NULL,'CustomerName','ASC',1,20);

--Completed Orders
CALL sp_SearchOrders(NULL,NULL,NULL,NULL,NULL,'Completed',NULL,NULL,'OrderDate','DESC',1,20);

--Country Search
CALL sp_SearchOrders(NULL,NULL,NULL,NULL,NULL,NULL,'USA',NULL,'CustomerName','ASC',1,20);

--Date Range
CALL sp_SearchOrders(NULL,'2024-01-01','2024-12-31',NULL,NULL,NULL,NULL,NULL,'OrderDate','DESC',1,20);

--Amount Range
CALL sp_SearchOrders(NULL,NULL,NULL,1000,5000,NULL,NULL,NULL,'Amount','DESC',1,20);

--------------------------------------------------------------------------------------------------------------------------
--INDEXES
CREATE INDEX idx_customers_name ON Customers(CustomerName);
CREATE INDEX idx_customers_country_city ON Customers(Country, City);  -- covers Country-only lookups too

CREATE INDEX idx_orders_customer ON Orders(CustomerId);
CREATE INDEX idx_orders_orderdate ON Orders(OrderDate);
CREATE INDEX idx_orders_amount ON Orders(Amount);
CREATE INDEX idx_orders_status_date ON Orders(Status, OrderDate);  -- covers Status-only lookups too

----------------------------------------------------------------------------------------------------------------------------------------