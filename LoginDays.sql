--Consecutive Logins for 7Days

WITH logins AS (
    SELECT DISTINCT UserId, LoginDate FROM UserLogin
),
ranked AS (
    SELECT
        UserId, LoginDate, DATE_SUB(LoginDate, INTERVAL ROW_NUMBER()
         OVER ( PARTITION BY UserId ORDER BY LoginDate) DAY) AS grp
    FROM logins
)
SELECT
    UserId, MIN(LoginDate) AS StartDate, MAX(LoginDate) AS EndDate
FROM ranked
GROUP BY UserId, grp
HAVING COUNT(*) >= 7
ORDER BY UserId, StartDate;