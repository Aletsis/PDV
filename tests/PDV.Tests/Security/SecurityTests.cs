using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using PDV.Application.Common.Behaviors;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Commands.CancelSale;
using PDV.Domain.Entities;
using PDV.Infrastructure;
using PDV.Infrastructure.Identity;
using PDV.Infrastructure.Persistence;
using Xunit;

namespace PDV.Tests.Security;

public class SecurityTests
{
    [Fact]
    public void IdentityOptions_ShouldHaveStrongPasswordPolicies()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddDbContext<AppDbContext>(opt => 
            opt.UseInMemoryDatabase($"PDV_Identity_Test_{Guid.NewGuid()}"));
        
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["RunMode"]).Returns("Local");
        services.AddSingleton<IConfiguration>(mockConfig.Object);
        
        services.AddLogging();
        services.AddCommonInfrastructureServices();

        var serviceProvider = services.BuildServiceProvider();
        var identityOptions = serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Assert
        Assert.True(identityOptions.Password.RequireDigit);
        Assert.True(identityOptions.Password.RequireLowercase);
        Assert.True(identityOptions.Password.RequireNonAlphanumeric);
        Assert.True(identityOptions.Password.RequireUppercase);
        Assert.Equal(8, identityOptions.Password.RequiredLength);
        Assert.False(identityOptions.User.RequireUniqueEmail);
    }

    [Fact]
    public async Task AuthorizationBehavior_UnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(m => m.IsAuthenticated).Returns(false);
        
        var mockPermissionService = new Mock<IPermissionService>();
        
        var behavior = new AuthorizationBehavior<CancelSaleCommand, bool>(mockCurrentUser.Object, mockPermissionService.Object);
        var command = new CancelSaleCommand(Guid.NewGuid(), "Reason", "user");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            behavior.Handle(command, new RequestHandlerDelegate<bool>((_) => Task.FromResult(true)), CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizationBehavior_UserHasPermission_ShouldSucceed()
    {
        // Arrange
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(m => m.IsAuthenticated).Returns(true);
        mockCurrentUser.Setup(m => m.Roles).Returns(new List<string> { "Cashier" });
        
        var mockPermissionService = new Mock<IPermissionService>();
        mockPermissionService.Setup(m => m.HasPermissionAsync(It.IsAny<List<string>>(), "sales.cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        var behavior = new AuthorizationBehavior<CancelSaleCommand, bool>(mockCurrentUser.Object, mockPermissionService.Object);
        var command = new CancelSaleCommand(Guid.NewGuid(), "Reason", "user");

        // Act
        var result = await behavior.Handle(command, new RequestHandlerDelegate<bool>((_) => Task.FromResult(true)), CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AuthorizationBehavior_UserLacksPermission_NoSupervisor_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(m => m.IsAuthenticated).Returns(true);
        mockCurrentUser.Setup(m => m.Roles).Returns(new List<string> { "Cashier" });
        
        var mockPermissionService = new Mock<IPermissionService>();
        mockPermissionService.Setup(m => m.HasPermissionAsync(It.IsAny<List<string>>(), "sales.cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var behavior = new AuthorizationBehavior<CancelSaleCommand, bool>(mockCurrentUser.Object, mockPermissionService.Object);
        var command = new CancelSaleCommand(Guid.NewGuid(), "Reason", "user");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            behavior.Handle(command, new RequestHandlerDelegate<bool>((_) => Task.FromResult(true)), CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizationBehavior_UserLacksPermission_ValidSupervisor_ShouldSucceedAndSetAuthorizedByUserId()
    {
        // Arrange
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(m => m.IsAuthenticated).Returns(true);
        mockCurrentUser.Setup(m => m.Roles).Returns(new List<string> { "Cashier" });
        
        var mockPermissionService = new Mock<IPermissionService>();
        mockPermissionService.Setup(m => m.HasPermissionAsync(It.IsAny<List<string>>(), "sales.cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockPermissionService.Setup(m => m.ValidateSupervisorPermissionAsync("supervisor", "pass123", "sales.cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "supervisor-id", null));
        
        var behavior = new AuthorizationBehavior<CancelSaleCommand, bool>(mockCurrentUser.Object, mockPermissionService.Object);
        var command = new CancelSaleCommand(Guid.NewGuid(), "Reason", "user", "supervisor", "pass123");

        // Act
        var result = await behavior.Handle(command, new RequestHandlerDelegate<bool>((_) => Task.FromResult(true)), CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal("supervisor-id", command.AuthorizedByUserId);
    }

    [Fact]
    public async Task AuthorizationBehavior_UserLacksPermission_InvalidSupervisor_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(m => m.IsAuthenticated).Returns(true);
        mockCurrentUser.Setup(m => m.Roles).Returns(new List<string> { "Cashier" });
        
        var mockPermissionService = new Mock<IPermissionService>();
        mockPermissionService.Setup(m => m.HasPermissionAsync(It.IsAny<List<string>>(), "sales.cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockPermissionService.Setup(m => m.ValidateSupervisorPermissionAsync("supervisor", "wrongpass", "sales.cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, "Contraseña incorrecta"));
        
        var behavior = new AuthorizationBehavior<CancelSaleCommand, bool>(mockCurrentUser.Object, mockPermissionService.Object);
        var command = new CancelSaleCommand(Guid.NewGuid(), "Reason", "user", "supervisor", "wrongpass");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            behavior.Handle(command, new RequestHandlerDelegate<bool>((_) => Task.FromResult(true)), CancellationToken.None));
        Assert.Contains("Autorización de supervisor fallida", ex.Message);
    }

    [Fact]
    public async Task AuditLogs_ShouldBeGeneratedOnChanges()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Audit_Test_{Guid.NewGuid()}")
            .Options;
        
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(m => m.UserId).Returns("test-cashier");
        mockCurrentUser.Setup(m => m.IsAuthenticated).Returns(true);
        
        var mockDateTime = new Mock<IDateTimeService>();
        mockDateTime.Setup(m => m.UtcNow).Returns(DateTime.UtcNow);
        
        var mockAuditService = new Mock<IAuditService>();
        mockAuditService.Setup(m => m.CurrentAction).Returns("TestCommand");

        await using var context = new AppDbContext(
            options: options,
            currentUserService: mockCurrentUser.Object,
            syncNotifier: null,
            dateTimeService: mockDateTime.Object,
            auditService: mockAuditService.Object
        );

        // Act
        var client = new Client("C001", "Client A", "XAXX010101000", "5551234567", "client@test.com");
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        // Assert
        var logs = await context.AuditLogs.ToListAsync();
        Assert.NotEmpty(logs);
        var log = logs.First();
        Assert.Equal("test-cashier", log.UserId);
        Assert.Equal("TestCommand", log.ActionName);
        Assert.Contains("Client A", log.NewValues);
    }

    [Fact]
    public async Task AuditLog_ShouldBeImmutable_ThrowsExceptionOnUpdateOrDelete()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Audit_Immutability_Test_{Guid.NewGuid()}")
            .Options;
            
        await using var context = new AppDbContext(options);

        var log = new AuditLog("user1", "TestAction", DateTime.UtcNow, "{}", "{}", "127.0.0.1");
        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        // Act & Assert Update
        context.Entry(log).State = EntityState.Modified;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

        // Act & Assert Delete
        context.Entry(log).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task TelephonistRole_And_OptionalEmail_ShouldBeSupported()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseInMemoryDatabase($"PDV_Telephonist_Test_{Guid.NewGuid()}"));
        
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["RunMode"]).Returns("Local");
        services.AddSingleton<IConfiguration>(mockConfig.Object);
        services.AddLogging();
        services.AddCommonInfrastructureServices();

        var serviceProvider = services.BuildServiceProvider();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        // Act - Seed
        await AppDbContextSeed.SeedDefaultUserAsync(userManager, roleManager, context);

        // Assert - Roles Exist
        Assert.True(await roleManager.RoleExistsAsync("Telephonist"));
        Assert.True(await roleManager.RoleExistsAsync("Cashier"));

        // Assert - Permissions for Telephonist
        var telephonistRole = await roleManager.FindByNameAsync("Telephonist");
        Assert.NotNull(telephonistRole);
        var rolePermissions = await context.RolePermissions
            .Where(rp => rp.RoleId == telephonistRole.Id)
            .Join(context.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
            .ToListAsync();

        Assert.Contains("products.view_catalog", rolePermissions);
        Assert.Contains("clients.create_edit", rolePermissions);
        Assert.Contains("orders.capture", rolePermissions);

        // Act - Create user with null email
        var cashierUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "cajero_sin_correo",
            Email = null,
            FullName = "Cajero Operativo",
            IsActive = true
        };

        var result = await userManager.CreateAsync(cashierUser, "Password123!");
        Assert.True(result.Succeeded);
        Assert.Null(cashierUser.Email);
    }
}
