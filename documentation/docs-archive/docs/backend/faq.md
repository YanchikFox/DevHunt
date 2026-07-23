---
sidebar_position: 5
title: Backend FAQ
description: Frequently asked questions about DevHunt backend development
---

import Link from '@docusaurus/Link';

<div style={{textAlign: 'center', marginBottom: '2rem'}}>
  <Link className="button button--secondary button--lg" to="/docs/backend/overview">
    ← Back to Backend Overview
  </Link>
  <Link className="button button--primary button--lg margin-left--md" to="/">
    🏠 Home
  </Link>
</div>

# Backend FAQ

Frequently asked questions about the DevHunt backend architecture, development, and deployment.

## 🏗️ Architecture Questions

### Q: Why do you use Clean Architecture?

**A:** Clean Architecture provides several benefits:

- **Separation of Concerns**: Each layer has a single responsibility
- **Testability**: Business logic is isolated and easily testable
- **Maintainability**: Changes in one layer don't affect others
- **Technology Independence**: You can change frameworks without affecting business logic

The layers are:
- **Domain**: Core business entities and rules
- **Application**: Use cases and application logic
- **Infrastructure**: External concerns (database, APIs, etc.)
- **Presentation**: Controllers and API endpoints

### Q: Why .NET 10 instead of an older version?

**A:** .NET 10 provides:

- **Performance Improvements**: Better JIT compilation and runtime optimizations
- **New Language Features**: Enhanced C# capabilities
- **Security Updates**: Latest security patches and improvements
- **Long-term Support**: Microsoft's commitment to the platform
- **Modern Tooling**: Better development experience with latest tools

### Q: Why PostgreSQL and not SQL Server?

**A:** PostgreSQL was chosen for:

- **Open Source**: No licensing costs, active community
- **Cross-platform**: Runs on Windows, Linux, macOS
- **Advanced Features**: JSON support, full-text search, spatial data
- **Performance**: Excellent performance for our use cases
- **DevOps Friendly**: Easy to containerize and deploy

## 🔧 Development Questions

### Q: How do I add a new API endpoint?

**A:** Follow these steps:

1. **Create the request/response models** in the API layer
2. **Add the endpoint** in the appropriate controller
3. **Implement the business logic** in an application service
4. **Add data access** if needed in the infrastructure layer
5. **Write unit tests** for the new functionality
6. **Update API documentation**

Example:
```csharp
// Controller
[HttpPost("projects")]
public async Task<IActionResult> CreateProject(CreateProjectRequest request)
{
    var result = await _projectService.CreateProjectAsync(request);
    return CreatedAtAction(nameof(GetProject), new { id = result.Id }, result);
}
```

### Q: How do I handle database migrations?

**A:** Use Entity Framework Core migrations:

```bash
# Create a new migration
cd DevHunt.CoreApi
dotnet ef migrations add AddNewFeature

# Apply migrations
dotnet ef database update

# Generate SQL script
dotnet ef migrations script
```

Always test migrations on a copy of production data first.

### Q: How do I add authentication to a new endpoint?

**A:** Use the `[Authorize]` attribute:

```csharp
[Authorize]
[HttpGet("protected-data")]
public async Task<IActionResult> GetProtectedData()
{
    // Only authenticated users can access this
}
```

For role-based authorization:
```csharp
[Authorize(Roles = "Admin")]
[HttpPost("admin-only")]
public async Task<IActionResult> AdminOnlyAction()
{
    // Only admins can access this
}
```

### Q: How do I implement caching?

**A:** Use the built-in IDistributedCache:

```csharp
private readonly IDistributedCache _cache;

public async Task<Project> GetProjectAsync(int id)
{
    var cacheKey = $"project:{id}";
    var cached = await _cache.GetStringAsync(cacheKey);

    if (cached != null)
    {
        return JsonSerializer.Deserialize<Project>(cached);
    }

    var project = await _context.Projects.FindAsync(id);
    await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(project),
        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });

    return project;
}
```

## 🧪 Testing Questions

### Q: What types of tests do you write?

**A:** We use multiple testing levels:

- **Unit Tests**: Test individual methods and classes in isolation
- **Integration Tests**: Test interactions between components
- **API Tests**: Test HTTP endpoints end-to-end
- **Load Tests**: Test performance under load

### Q: How do I mock dependencies in unit tests?

**A:** Use Moq for mocking:

```csharp
[Test]
public async Task CreateProject_ShouldCallRepository()
{
    // Arrange
    var mockRepo = new Mock<IProjectRepository>();
    mockRepo.Setup(x => x.AddAsync(It.IsAny<Project>())).ReturnsAsync(new Project());

    var service = new ProjectService(mockRepo.Object);

    // Act
    await service.CreateProjectAsync(new CreateProjectRequest());

    // Assert
    mockRepo.Verify(x => x.AddAsync(It.IsAny<Project>()), Times.Once);
}
```

### Q: How do I test database operations?

**A:** Use an in-memory database for unit tests:

```csharp
private DevHuntDbContext CreateTestContext()
{
    var options = new DbContextOptionsBuilder<DevHuntDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    return new DevHuntDbContext(options);
}
```

For integration tests, use a test database.

## 📡 Real-time Features Questions

### Q: How do I add a new SignalR hub?

**A:** Create a hub class and register it:

```csharp
// Hub class
public class NotificationHub : Hub
{
    public async Task SendNotification(string userId, string message)
    {
        await Clients.User(userId).SendAsync("ReceiveNotification", message);
    }
}

// Registration in Program.cs
builder.Services.AddSignalR();
app.MapHub<NotificationHub>("/notificationHub");
```

### Q: How do I handle SignalR authentication?

**A:** Use JWT tokens in query string or headers:

```csharp
// Client-side
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub", {
        accessTokenFactory: () => localStorage.getItem('token')
    })
    .build();

// Server-side validation
public override async Task OnConnectedAsync()
{
    var token = Context.GetHttpContext().Request.Query["access_token"];
    // Validate token...
    await base.OnConnectedAsync();
}
```

## 🚀 Deployment Questions

### Q: How do you deploy the backend?

**A:** We use containerized deployment:

1. **Build Docker images** for each service
2. **Push to container registry**
3. **Deploy using Kubernetes** or Docker Compose
4. **Use reverse proxy** (nginx) for routing
5. **Configure load balancer** for scaling

### Q: How do you handle environment-specific configuration?

**A:** Use multiple appsettings files:

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Staging.json` - Staging overrides
- `appsettings.Production.json` - Production overrides

Environment variables override all settings.

### Q: How do you handle secrets in production?

**A:** Use Azure Key Vault or similar secret management:

- **Never commit secrets** to version control
- **Use environment variables** for development
- **Use managed identity** in production
- **Rotate secrets regularly**
- **Audit secret access**

## 🔒 Security Questions

### Q: How do you handle password hashing?

**A:** Use ASP.NET Core Identity's built-in password hasher:

```csharp
private readonly IPasswordHasher<User> _passwordHasher;

public async Task<User> CreateUserAsync(CreateUserRequest request)
{
    var user = new User { Email = request.Email };
    user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
    return await _userRepository.AddAsync(user);
}
```

### Q: How do you implement rate limiting?

**A:** Use ASP.NET Core rate limiting middleware:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
    });
});

app.UseRateLimiter();
```

### Q: How do you handle CORS?

**A:** Configure CORS policy:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://yourdomain.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

app.UseCors("AllowFrontend");
```

## 📊 Performance Questions

### Q: How do you optimize database queries?

**A:** Follow these best practices:

1. **Use indexes** on frequently queried columns
2. **Avoid N+1 queries** with `.Include()` or `.Select()`
3. **Use pagination** for large result sets
4. **Monitor slow queries** with logging
5. **Consider read replicas** for heavy read workloads

### Q: How do you implement caching?

**A:** Multiple caching layers:

- **Browser caching** for static assets
- **CDN** for global distribution
- **Application caching** with Redis
- **Database query caching**
- **API response caching**

### Q: How do you handle high traffic?

**A:** Use multiple strategies:

- **Horizontal scaling** with load balancers
- **Database read replicas**
- **Redis clustering**
- **API rate limiting**
- **Background job processing**

## 🐛 Debugging Questions

### Q: How do I debug a production issue?

**A:** Follow this process:

1. **Check application logs** and metrics
2. **Reproduce locally** if possible
3. **Use remote debugging** if necessary
4. **Check database state** and recent changes
5. **Review recent deployments**
6. **Check external service status**

### Q: How do I enable detailed logging?

**A:** Configure logging levels:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "DevHunt": "Debug"
    }
  }
}
```

Use structured logging with Serilog for better searchability.

## 📚 Learning Resources

### Recommended Books
- **"Clean Architecture" by Robert C. Martin**
- **"Dependency Injection in .NET" by Mark Seemann**
- **"Entity Framework Core in Action" by Jon P. Smith**

### Online Resources
- **[Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/)** - Official .NET documentation
- **[Entity Framework Documentation](https://learn.microsoft.com/en-us/ef/)** - EF Core guides
- **[SignalR Documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/)** - Real-time communication

### Community
- **Stack Overflow** - For specific technical questions
- **Reddit r/dotnet** - Community discussions
- **GitHub Issues** - Bug reports and feature requests

## ❓ Still Have Questions?

If you can't find the answer here:

1. **Check the [Troubleshooting Guide](/docs/backend/troubleshooting)**
2. **Search existing GitHub issues**
3. **Ask in our community Discord/Slack**
4. **Create a new GitHub issue**

Remember to include:
- Your environment (OS, .NET version, etc.)
- Steps to reproduce the issue
- Expected vs actual behavior
- Any error messages or logs