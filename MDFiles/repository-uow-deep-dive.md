# Repository vs Unit of Work — Deep Dive with Enterprise Examples

---

## The Core Difference in One Line

| Pattern | Responsibility |
|---|---|
| **Repository** | Abstracts how you **query/save a single entity type** |
| **Unit of Work** | Coordinates **multiple repositories** so all changes save together in one transaction |

Think of it this way:
- Repository = a **filing cabinet drawer** (one drawer per entity)
- Unit of Work = the **office manager** who says "don't file anything until I say go — then file everything at once"

---

## Part 1 — Repository Pattern

### The Problem Without It

```csharp
// BAD — Service talks directly to DbContext
public class OrderService
{
    private readonly AppDbContext _db;

    public async Task<List<Order>> GetPendingOrders()
    {
        // EF query is inside the service — hard to test, hard to swap
        return await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.Status == "Pending")
            .ToListAsync();
    }
}
```

**Problems:**
1. To unit test `OrderService` you need a real DB or EF in-memory DB
2. EF queries are scattered across 20 different services
3. If you ever move to Dapper, you rewrite every service

### The Fix — Repository Hides Data Access Behind an Interface

```csharp
// The contract — no EF, no SQL, just "what can I do with Orders?"
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id);
    Task<List<Order>> GetPendingOrdersAsync();
    Task<List<Order>> GetByCustomerIdAsync(int customerId);
    Task AddAsync(Order order);
    void Update(Order order);
    void Delete(Order order);
}
```

```csharp
// The implementation — EF lives HERE only
public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db) => _db = db;

    public async Task<Order?> GetByIdAsync(int id)
        => await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<List<Order>> GetPendingOrdersAsync()
        => await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.Status == OrderStatus.Pending)
            .OrderBy(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

    public async Task<List<Order>> GetByCustomerIdAsync(int customerId)
        => await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

    public async Task AddAsync(Order order)
        => await _db.Orders.AddAsync(order);

    public void Update(Order order)
        => _db.Orders.Update(order);

    public void Delete(Order order)
        => _db.Orders.Remove(order);
}
```

```csharp
// Service now depends on the interface, NOT EF Core
public class OrderService
{
    private readonly IOrderRepository _orderRepo;

    public OrderService(IOrderRepository orderRepo)
        => _orderRepo = orderRepo;

    public async Task<List<Order>> GetPendingOrdersAsync()
        => await _orderRepo.GetPendingOrdersAsync();
}
```

Now to test `OrderService` you just mock `IOrderRepository` — no database needed.

### Generic Repository — Reduce Repetition

Every entity needs GetById, GetAll, Add, Update, Delete. Don't write that 10 times.

```csharp
// Generic base interface
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
}

// Generic base implementation
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _db;
    protected readonly DbSet<T> _set;

    public Repository(AppDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id)
        => await _set.FindAsync(id);

    public async Task<IEnumerable<T>> GetAllAsync()
        => await _set.AsNoTracking().ToListAsync();

    public async Task AddAsync(T entity)
        => await _set.AddAsync(entity);

    public void Update(T entity)
        => _set.Update(entity);

    public void Delete(T entity)
        => _set.Remove(entity);
}
```

```csharp
// Specific repository inherits generic and ADDS its own queries
public interface IOrderRepository : IRepository<Order>
{
    Task<List<Order>> GetPendingOrdersAsync();
    Task<List<Order>> GetByCustomerIdAsync(int customerId);
    Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to);
}

public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext db) : base(db) { }

    public async Task<List<Order>> GetPendingOrdersAsync()
        => await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.Status == OrderStatus.Pending)
            .AsNoTracking()
            .ToListAsync();

    public async Task<List<Order>> GetByCustomerIdAsync(int customerId)
        => await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

    public async Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to)
        => await _db.Orders
            .Where(o => o.Status == OrderStatus.Completed
                     && o.CreatedAt >= from
                     && o.CreatedAt <= to)
            .SumAsync(o => o.TotalAmount);
}
```

### When Repository Is Enough (No UoW Needed)

Use just a repository when one operation touches **one entity type**:

```csharp
// Single repo is fine here — only touching Orders
public class OrderQueryService
{
    private readonly IOrderRepository _repo;

    public OrderQueryService(IOrderRepository repo) => _repo = repo;

    public async Task<List<Order>> GetDashboardOrdersAsync()
        => await _repo.GetPendingOrdersAsync();
}
```

---

## Part 2 — Unit of Work Pattern

### The Problem Without It

```csharp
// BAD — two separate SaveChangesAsync calls
public class OrderService
{
    private readonly IOrderRepository _orderRepo;
    private readonly IInvoiceRepository _invoiceRepo;

    public async Task PlaceOrderAsync(CreateOrderDto dto)
    {
        var order = new Order { ... };
        await _orderRepo.AddAsync(order);
        await _orderRepo.SaveChangesAsync(); // ← commits to DB

        var invoice = new Invoice { OrderId = order.Id, ... };
        await _invoiceRepo.AddAsync(invoice);
        await _invoiceRepo.SaveChangesAsync(); // ← if THIS crashes, order exists but invoice doesn't!
    }
}
```

This leaves your database in a broken half-state if the second save fails.

### The Fix — Unit of Work Wraps All Repos, One Save

```csharp
// UoW interface — one SaveChangesAsync for everything
public interface IUnitOfWork : IDisposable
{
    IOrderRepository Orders { get; }
    ICustomerRepository Customers { get; }
    IInvoiceRepository Invoices { get; }
    IProductRepository Products { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

```csharp
// UoW implementation — all repos share the SAME DbContext instance
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public IOrderRepository Orders { get; }
    public ICustomerRepository Customers { get; }
    public IInvoiceRepository Invoices { get; }
    public IProductRepository Products { get; }

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
        // All pass the same _db — so EF tracks everything in one context
        Orders    = new OrderRepository(db);
        Customers = new CustomerRepository(db);
        Invoices  = new InvoiceRepository(db);
        Products  = new ProductRepository(db);
    }

    // ONE call flushes ALL pending changes atomically
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public void Dispose() => _db.Dispose();
}
```

```csharp
// Service now uses UoW — one save covers everything
public class OrderService
{
    private readonly IUnitOfWork _uow;

    public OrderService(IUnitOfWork uow) => _uow = uow;

    public async Task PlaceOrderAsync(CreateOrderDto dto)
    {
        var customer = await _uow.Customers.GetByIdAsync(dto.CustomerId);
        if (customer is null) throw new NotFoundException("Customer not found");

        var order = new Order
        {
            CustomerId = customer.Id,
            Status     = OrderStatus.Pending,
            CreatedAt  = DateTime.UtcNow
        };

        foreach (var item in dto.Items)
        {
            var product = await _uow.Products.GetByIdAsync(item.ProductId);
            product!.Stock -= item.Quantity; // reduce stock
            _uow.Products.Update(product);

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity  = item.Quantity,
                UnitPrice = product.Price
            });
        }

        await _uow.Orders.AddAsync(order);

        var invoice = new Invoice
        {
            CustomerId  = customer.Id,
            TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice),
            CreatedAt   = DateTime.UtcNow
        };
        await _uow.Invoices.AddAsync(invoice);

        // ONE call — order + invoice + stock changes all commit together
        // If anything fails, NOTHING is saved (atomic)
        await _uow.SaveChangesAsync();
    }
}
```

---

## Part 3 — Full Enterprise Example

### Project Structure

```
src/
  Domain/
    Entities/
      Order.cs
      Customer.cs
      Invoice.cs
      Product.cs
  Infrastructure/
    Data/
      AppDbContext.cs
    Repositories/
      IRepository.cs
      Repository.cs
      IOrderRepository.cs
      OrderRepository.cs
      ICustomerRepository.cs
      CustomerRepository.cs
      IInvoiceRepository.cs
      InvoiceRepository.cs
      IProductRepository.cs
      ProductRepository.cs
    UnitOfWork/
      IUnitOfWork.cs
      UnitOfWork.cs
  Application/
    Services/
      OrderService.cs
    DTOs/
      CreateOrderDto.cs
  API/
    Controllers/
      OrdersController.cs
    Program.cs
```

### Domain Entities

```csharp
// Order.cs
public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<OrderItem> Items { get; set; } = new();
    public Customer Customer { get; set; } = null!;
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public enum OrderStatus { Pending, Confirmed, Completed, Cancelled }

// Customer.cs
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// Invoice.cs
public class Invoice
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int? OrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPaid { get; set; }
}

// Product.cs
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}
```

### DbContext

```csharp
// AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Product> Products => Set<Product>();
}
```

### Generic Repository

```csharp
// IRepository.cs
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}

// Repository.cs
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _db;
    protected readonly DbSet<T> _set;

    public Repository(AppDbContext db) { _db = db; _set = db.Set<T>(); }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _set.FindAsync(new object[] { id }, ct);

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => await _set.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await _set.AddAsync(entity, ct);

    public void Update(T entity) => _set.Update(entity);
    public void Delete(T entity) => _set.Remove(entity);
}
```

### Specific Repositories

```csharp
// IOrderRepository.cs
public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetWithDetailsAsync(int id, CancellationToken ct = default);
    Task<List<Order>> GetPendingOrdersAsync(CancellationToken ct = default);
    Task<List<Order>> GetByCustomerIdAsync(int customerId, CancellationToken ct = default);
    Task<decimal> GetRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

// OrderRepository.cs
public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext db) : base(db) { }

    public async Task<Order?> GetWithDetailsAsync(int id, CancellationToken ct = default)
        => await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<List<Order>> GetPendingOrdersAsync(CancellationToken ct = default)
        => await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.Status == OrderStatus.Pending)
            .OrderBy(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<List<Order>> GetByCustomerIdAsync(int customerId, CancellationToken ct = default)
        => await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<decimal> GetRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await _db.Orders
            .Where(o => o.Status == OrderStatus.Completed
                     && o.CreatedAt >= from
                     && o.CreatedAt <= to)
            .SelectMany(o => o.Items)
            .SumAsync(i => i.Quantity * i.UnitPrice, ct);
}

// ICustomerRepository.cs
public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
}

// CustomerRepository.cs
public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext db) : base(db) { }

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == email, ct);

    public async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        => await _db.Customers.AnyAsync(c => c.Id == id, ct);
}

// IInvoiceRepository.cs
public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<List<Invoice>> GetUnpaidByCustomerAsync(int customerId, CancellationToken ct = default);
}

// InvoiceRepository.cs
public class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(AppDbContext db) : base(db) { }

    public async Task<List<Invoice>> GetUnpaidByCustomerAsync(int customerId, CancellationToken ct = default)
        => await _db.Invoices
            .Where(i => i.CustomerId == customerId && !i.IsPaid)
            .AsNoTracking()
            .ToListAsync(ct);
}

// IProductRepository.cs
public interface IProductRepository : IRepository<Product>
{
    Task<bool> HasSufficientStockAsync(int productId, int quantity, CancellationToken ct = default);
}

// ProductRepository.cs
public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext db) : base(db) { }

    public async Task<bool> HasSufficientStockAsync(int productId, int quantity, CancellationToken ct = default)
        => await _db.Products.AnyAsync(p => p.Id == productId && p.Stock >= quantity, ct);
}
```

### Unit of Work

```csharp
// IUnitOfWork.cs
public interface IUnitOfWork : IDisposable
{
    IOrderRepository    Orders    { get; }
    ICustomerRepository Customers { get; }
    IInvoiceRepository  Invoices  { get; }
    IProductRepository  Products  { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// UnitOfWork.cs
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public IOrderRepository    Orders    { get; }
    public ICustomerRepository Customers { get; }
    public IInvoiceRepository  Invoices  { get; }
    public IProductRepository  Products  { get; }

    public UnitOfWork(AppDbContext db)
    {
        _db       = db;
        Orders    = new OrderRepository(db);
        Customers = new CustomerRepository(db);
        Invoices  = new InvoiceRepository(db);
        Products  = new ProductRepository(db);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public void Dispose() => _db.Dispose();
}
```

### DTOs

```csharp
// CreateOrderDto.cs
public record CreateOrderDto(
    int CustomerId,
    List<OrderItemDto> Items
);

public record OrderItemDto(
    int ProductId,
    int Quantity
);
```

### Application Service

```csharp
// OrderService.cs
public class OrderService
{
    private readonly IUnitOfWork _uow;

    public OrderService(IUnitOfWork uow) => _uow = uow;

    // READ — UoW not strictly needed here, but it's still accessible
    public async Task<Order?> GetOrderAsync(int id, CancellationToken ct = default)
        => await _uow.Orders.GetWithDetailsAsync(id, ct);

    public async Task<List<Order>> GetPendingOrdersAsync(CancellationToken ct = default)
        => await _uow.Orders.GetPendingOrdersAsync(ct);

    // WRITE — touches Orders + Products + Invoices atomically
    public async Task<int> PlaceOrderAsync(CreateOrderDto dto, CancellationToken ct = default)
    {
        // 1. Validate customer exists
        if (!await _uow.Customers.ExistsAsync(dto.CustomerId, ct))
            throw new InvalidOperationException($"Customer {dto.CustomerId} not found");

        // 2. Validate stock for all items upfront
        foreach (var item in dto.Items)
        {
            if (!await _uow.Products.HasSufficientStockAsync(item.ProductId, item.Quantity, ct))
                throw new InvalidOperationException($"Insufficient stock for product {item.ProductId}");
        }

        // 3. Build the order
        var order = new Order { CustomerId = dto.CustomerId };

        decimal totalAmount = 0;
        foreach (var item in dto.Items)
        {
            var product = await _uow.Products.GetByIdAsync(item.ProductId, ct);
            product!.Stock -= item.Quantity;
            _uow.Products.Update(product);

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity  = item.Quantity,
                UnitPrice = product.Price
            });
            totalAmount += item.Quantity * product.Price;
        }

        await _uow.Orders.AddAsync(order, ct);

        // 4. Create matching invoice
        var invoice = new Invoice
        {
            CustomerId  = dto.CustomerId,
            TotalAmount = totalAmount
        };
        await _uow.Invoices.AddAsync(invoice, ct);

        // 5. ONE database round-trip — all or nothing
        await _uow.SaveChangesAsync(ct);

        // Now order.Id is populated by EF
        invoice.OrderId = order.Id;
        await _uow.SaveChangesAsync(ct);

        return order.Id;
    }

    public async Task CancelOrderAsync(int orderId, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetWithDetailsAsync(orderId, ct);
        if (order is null) throw new InvalidOperationException("Order not found");
        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be cancelled");

        // Restore stock for each item
        foreach (var item in order.Items)
        {
            var product = await _uow.Products.GetByIdAsync(item.ProductId, ct);
            product!.Stock += item.Quantity;
            _uow.Products.Update(product);
        }

        order.Status = OrderStatus.Cancelled;
        _uow.Orders.Update(order);

        // One save — order status + all stock restorations together
        await _uow.SaveChangesAsync(ct);
    }
}
```

### Controller

```csharp
// OrdersController.cs
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService)
        => _orderService = orderService;

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var order = await _orderService.GetOrderAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken ct)
        => Ok(await _orderService.GetPendingOrdersAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderDto dto, CancellationToken ct)
    {
        var id = await _orderService.PlaceOrderAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpDelete("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _orderService.CancelOrderAsync(id, ct);
        return NoContent();
    }
}
```

### Registration in Program.cs

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Register UoW — single registration covers all repositories inside
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register service
builder.Services.AddScoped<OrderService>();
```

---

## Part 4 — When to Use What

### Use Repository Only (no UoW)

When a service **never writes to more than one table at once**:

```csharp
// Pure read service — just inject the specific repo
public class ReportService
{
    private readonly IOrderRepository _orders;

    public ReportService(IOrderRepository orders) => _orders = orders;

    public async Task<decimal> GetMonthlyRevenueAsync()
        => await _orders.GetRevenueAsync(
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
}
```

### Use UoW

When a **single business operation touches multiple tables** and they must all succeed or all fail:

```csharp
// Place order = write Order + write Invoice + update Product.Stock
// All three must succeed together — use UoW
public async Task PlaceOrderAsync(CreateOrderDto dto, CancellationToken ct)
{
    await _uow.Orders.AddAsync(order, ct);
    await _uow.Invoices.AddAsync(invoice, ct);
    _uow.Products.Update(product);
    await _uow.SaveChangesAsync(ct); // all or nothing
}
```

### Decision Table

| Scenario | Use |
|---|---|
| Read-only queries | Repository only |
| Write to one table | Repository only |
| Write to 2+ tables atomically | Unit of Work |
| Multiple services need the same queries | Repository (centralises queries) |
| Unit test service without hitting DB | Repository (mock the interface) |

---

## Part 5 — Unit Testing With Mocks (the Payoff)

This is why you did all of the above. You can test `OrderService` with zero database.

```csharp
// OrderServiceTests.cs
public class OrderServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<ICustomerRepository> _customerRepoMock = new();
    private readonly Mock<IProductRepository> _productRepoMock = new();
    private readonly Mock<IInvoiceRepository> _invoiceRepoMock = new();

    private OrderService CreateService()
    {
        _uowMock.Setup(u => u.Orders).Returns(_orderRepoMock.Object);
        _uowMock.Setup(u => u.Customers).Returns(_customerRepoMock.Object);
        _uowMock.Setup(u => u.Products).Returns(_productRepoMock.Object);
        _uowMock.Setup(u => u.Invoices).Returns(_invoiceRepoMock.Object);
        return new OrderService(_uowMock.Object);
    }

    [Fact]
    public async Task PlaceOrder_CustomerNotFound_Throws()
    {
        _customerRepoMock
            .Setup(r => r.ExistsAsync(99, default))
            .ReturnsAsync(false);

        var service = CreateService();
        var dto = new CreateOrderDto(99, new List<OrderItemDto>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlaceOrderAsync(dto));
    }

    [Fact]
    public async Task PlaceOrder_InsufficientStock_Throws()
    {
        _customerRepoMock
            .Setup(r => r.ExistsAsync(1, default))
            .ReturnsAsync(true);
        _productRepoMock
            .Setup(r => r.HasSufficientStockAsync(5, 100, default))
            .ReturnsAsync(false);

        var service = CreateService();
        var dto = new CreateOrderDto(1, new List<OrderItemDto> { new(5, 100) });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlaceOrderAsync(dto));
    }

    [Fact]
    public async Task PlaceOrder_ValidOrder_CallsSaveOnce()
    {
        _customerRepoMock.Setup(r => r.ExistsAsync(1, default)).ReturnsAsync(true);
        _productRepoMock.Setup(r => r.HasSufficientStockAsync(1, 2, default)).ReturnsAsync(true);
        _productRepoMock.Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(new Product { Id = 1, Price = 10m, Stock = 5 });
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), default)).Returns(Task.CompletedTask);
        _invoiceRepoMock.Setup(r => r.AddAsync(It.IsAny<Invoice>(), default)).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var service = CreateService();
        var dto = new CreateOrderDto(1, new List<OrderItemDto> { new(1, 2) });

        await service.PlaceOrderAsync(dto);

        // Verify SaveChangesAsync was called — not caring about DB details
        _uowMock.Verify(u => u.SaveChangesAsync(default), Times.AtLeastOnce);
    }
}
```

---

## Summary

```
Repository
  └── Hides WHERE data comes from (EF, Dapper, Mongo — service doesn't care)
  └── Centralises queries for one entity type
  └── Makes services testable via mock

Unit of Work
  └── Wraps multiple repositories that share ONE DbContext
  └── ONE SaveChangesAsync = one database transaction
  └── Prevents partial saves when a business operation touches multiple tables

Use both together for enterprise apps that:
  - Need unit tests
  - Have multi-table write operations
  - Want one consistent data access layer
```
