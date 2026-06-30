// Q9. DYNAMIC LINQ

public static class DynamicLinqExtensions
{
    // Lets you sort using a column name as TEXT, like from a dropdown
    // or a URL parameter e.g. query.OrderByDynamic("Salary desc")
    public static IQueryable<T> OrderByDynamic<T>(this IQueryable<T> source, string orderBy)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(orderBy)) return source; // nothing to sort by, just return as-is

        // Turn "Salary desc" into the column name ("Salary") and direction (descending)
        var (propName, descending) = ParseColumn(orderBy);

        // Build the actual "x => x.Salary" instruction that LINQ understands
        var lambda = BuildPropertyLambda<T>(propName);

        // Run the sort. false = this is the only/first sort, not a tie-breaker
        return ApplySort(source, lambda, descending, useThenBy: false);
    }

    //sorting by MULTIPLE columns at once 
    // e.g. query.OrderByMultiple("Name, Salary desc") sorts by Name first,
    // and for people with the same Name, sorts those by Salary (high to low).
    public static IQueryable<T> OrderByMultiple<T>(this IQueryable<T> source, string orderBy)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(orderBy)) return source;

        // Split "Name, Salary desc" into separate columns: ["Name", "Salary desc"]
        var columns = orderBy.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (columns.Length == 0) return source;

        IQueryable<T> result = source;

        for (int i = 0; i < columns.Length; i++)
        {
            var (propName, descending) = ParseColumn(columns[i]);
            var lambda = BuildPropertyLambda<T>(propName);

            // The FIRST column sets the main sort order.
            // Every column AFTER that is just a tie-breaker for rows
            // that are equal so far that's what ThenBy does.
            result = ApplySort(result, lambda, descending, useThenBy: i > 0);
        }

        return result;
    }

    // --- Shared helpers ---

    private static (string propertyName, bool descending) ParseColumn(string column)
    {
        var parts = column.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            throw new ArgumentException("Sort column name cannot be empty.", nameof(column));

        var propName   = parts[0];
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (propName, descending);
    }

    private static LambdaExpression BuildPropertyLambda<T>(string propertyName)
    {
        // Represents "x" — basically a placeholder for "each row" in the sort
        var param = Expression.Parameter(typeof(T), "x");

        Expression propertyAccess;
        try
        {
            // Try to build "x.Salary" (or whatever column name was passed in)
            propertyAccess = Expression.Property(param, propertyName);
        }
        catch (ArgumentException)
        {
            // This means the column name doesn't actually exist on the object
            // (e.g. someone passed "Salry" by mistake, or a hacked/bad value).
            // We catch the vague built-in error and throw a clearer one instead,
            // so whoever's debugging this immediately knows WHICH column name
            // was wrong and on WHICH object type  important since this is
            // often fed by user input (like a sort dropdown or API parameter).
            throw new ArgumentException(
                $"Property '{propertyName}' does not exist on type '{typeof(T).Name}'.",
                nameof(propertyName));
        }

        // Wrap it into the final "x => x.Salary" instruction
        return Expression.Lambda(propertyAccess, param);
    }

    private static IQueryable<T> ApplySort<T>(
        IQueryable<T> source,
        LambdaExpression keySelector,
        bool descending,
        bool useThenBy)
    {
        // useThenBy requires source to actually be IOrderedQueryable<T> 
        // true at runtime here because it's only ever set true after at
        // least one prior OrderBy/OrderByDescending call in OrderByMultiple.
        var methodName = useThenBy
            ? (descending ? "ThenByDescending" : "ThenBy")
            : (descending ? "OrderByDescending" : "OrderBy");

        var method = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), keySelector.Body.Type);

        var result = method.Invoke(null, new object[] { source, keySelector });

        return (IQueryable<T>)result!;
    }
}

//---------------------------------------------------------------------------------------------------------//

// USAGE EXAMPLES

var q = dbContext.Employees.AsQueryable();

q.OrderByDynamic("Name");           // ORDER BY Name ASC
q.OrderByDynamic("Salary desc");    // ORDER BY Salary DESC
q.OrderByDynamic("JoiningDate");    // ORDER BY JoiningDate ASC

q.OrderByMultiple("Name, Salary desc");
    // ORDER BY Name ASC, Salary DESC   (correctly chained, Name primary)

q.OrderByMultiple("Department, Salary desc, JoiningDate");
    // ORDER BY Department ASC, Salary DESC, JoiningDate ASC

Unknown property:
q.OrderByDynamic("NotAColumn");     // throws ArgumentException with a clear message