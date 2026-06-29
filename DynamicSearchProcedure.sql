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

    SET vOffset = (IFNULL(pPage,1)-1) * IFNULL(pPageSize,20);

    IF vOffset < 0 THEN
        SET vOffset = 0;
    END IF;

    IF pPageSize IS NULL OR pPageSize <=0 THEN
        SET pPageSize = 20;
    END IF;

    SET vSQL =
    'SELECT
        o.Id,
        c.CustomerName,
        c.Country,
        c.City,
        o.OrderDate,
        o.Amount,
        o.Status
     FROM Orders o
     INNER JOIN Customers c
        ON c.Id=o.CustomerId
     WHERE 1=1';

    IF pCustomerName IS NOT NULL AND pCustomerName <> '' THEN
        SET vSQL = CONCAT(vSQL,
        ' AND c.CustomerName LIKE ',
        QUOTE(CONCAT('%',pCustomerName,'%')));
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
        pMinAmount);
    END IF;

    IF pMaxAmount IS NOT NULL THEN
        SET vSQL = CONCAT(vSQL,
        ' AND o.Amount <= ',
        pMaxAmount);
    END IF;

    IF pStatus IS NOT NULL AND pStatus <> '' THEN
        SET vSQL = CONCAT(vSQL,
        ' AND o.Status=',
        QUOTE(pStatus));
    END IF;

    IF pCountry IS NOT NULL AND pCountry <> '' THEN
        SET vSQL = CONCAT(vSQL,
        ' AND c.Country=',
        QUOTE(pCountry));
    END IF;

    IF pCity IS NOT NULL AND pCity <> '' THEN
        SET vSQL = CONCAT(vSQL,
        ' AND c.City=',
        QUOTE(pCity));
    END IF;

    IF pSortBy NOT IN ('CustomerName','OrderDate','Amount','Status') THEN
        SET pSortBy='OrderDate';
    END IF;

    IF UPPER(pSortDirection) NOT IN ('ASC','DESC') THEN
        SET pSortDirection='DESC';
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
        ' LIMIT ',
        pPageSize,
        ' OFFSET ',
        vOffset
    );

SET @sql = vSQL;

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

END



--INDEXES 

