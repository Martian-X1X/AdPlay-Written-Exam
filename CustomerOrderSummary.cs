// Q11. CUSTOMER ORDER SUMMARY — single LINQ query
 
// Input model
public class Order
{
    public int      CustomerId  { get; set; }
    public string   CustomerName { get; set; }
    public decimal  Amount      { get; set; }
    public DateTime OrderDate   { get; set; }
}
 
// Output model — one row per customer
public class CustomerOrderSummary
{
    public int      CustomerId         { get; set; }
    public string   CustomerName       { get; set; }
    public int      TotalOrders        { get; set; }
    public decimal  HighestOrderAmount { get; set; }
    public decimal  AverageOrderAmount { get; set; }
    public DateTime LatestOrderDate    { get; set; }
}
 
public class OrderService
{
    public List<CustomerOrderSummary> GetSummary(List<Order> orders)
    {
        var result = orders
            .GroupBy(o => new { o.CustomerId, o.CustomerName }) // group all orders per customer
            .Select(g => new CustomerOrderSummary
            {
                CustomerId         = g.Key.CustomerId,
                CustomerName       = g.Key.CustomerName,
                TotalOrders        = g.Count(),          // how many orders this customer made
                HighestOrderAmount = g.Max(o => o.Amount),  // biggest single order
                AverageOrderAmount = g.Average(o => o.Amount), // average spend
                LatestOrderDate    = g.Max(o => o.OrderDate)   // most recent order
            })
            .ToList();
 
        return result;
    }
}
 
// ---- USAGE -------------------------------------------------
// var orders = new List<Order>
// {
//     new() { CustomerId=1, CustomerName="Alice", Amount=100, OrderDate=DateTime.Today },
//     new() { CustomerId=1, CustomerName="Alice", Amount=250, OrderDate=DateTime.Today.AddDays(-5) },
//     new() { CustomerId=2, CustomerName="Bob",   Amount=80,  OrderDate=DateTime.Today.AddDays(-1) },
// };
// var summary = new OrderService().GetSummary(orders);
// Result:
//   Alice → TotalOrders:2, Highest:250, Avg:175, Latest:Today
//   Bob   → TotalOrders:1, Highest:80,  Avg:80,  Latest:Yesterday