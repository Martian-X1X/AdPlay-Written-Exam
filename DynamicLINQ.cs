// Q9. DYNAMIC LINQ — Extension Method for Dynamic Sorting

//Expression Tree = Lambda Expression = x => x.Salary
 
public static class DynamicLinqExtensions
{
    // This is an extension method on IQueryable<T>
    // "this IQueryable<T> source" means you call it like: query.OrderBy("Name")
    public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, string orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy)) return source; // nothing to sort
 
        // Split "Salary desc" → ["Salary", "desc"]
        var parts      = orderBy.Trim().Split(' ');
        var propName   = parts[0];                              // "Salary"
        var descending = parts.Length > 1 &&
                         parts[1].Equals("desc",
                         StringComparison.OrdinalIgnoreCase);  // true if "desc"
 
        //Build the lambda: x => x.Salary
        // "x" parameter (represents each row)
        var param = Expression.Parameter(typeof(T), "x");
 
        //"x.Salary" — access the property by name string
        var property = Expression.Property(param, propName);
        // throws InvalidOperationException if property doesn't exist — good, fail fast
 
        //wrap into lambda:  x => x.Salary
        var lambda = Expression.Lambda(property, param);
 
        //pick "OrderBy" or "OrderByDescending" method name
        var methodName = descending ? "OrderByDescending" : "OrderBy";
 
        //call Queryable.OrderBy(source, x => x.Salary) via reflection
        var result = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), property.Type) // fill in the generic types
            .Invoke(null, new object[] { source, lambda });
 
        return (IQueryable<T>)result!;
    }
 
    //support multiple columns  "Name, Salary desc"
    public static IQueryable<T> OrderByMultiple<T>(this IQueryable<T> source, string orderBy)
    {
        // split by comma → ["Name", "Salary desc"]
        var columns = orderBy.Split(',', StringSplitOptions.RemoveEmptyEntries);
 
        IQueryable<T> result = source;
        foreach (var col in columns)
            result = result.OrderBy(col.Trim()); // reuse single-column method above
 
        return result;
    }
}
 
//USAGE EXAMPLES
// var q = dbContext.Employees.AsQueryable();
// q.OrderBy("Name");           → ORDER BY Name ASC
// q.OrderBy("Salary desc");    → ORDER BY Salary DESC
// q.OrderBy("JoiningDate");    → ORDER BY JoiningDate ASC
// q.OrderByMultiple("Name, Salary desc"); → ORDER BY Name ASC, Salary DESC