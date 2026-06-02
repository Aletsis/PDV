using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PDV.Infrastructure.Identity;
using PDV.Application.Features.CashRegisters.Queries.GetCashRegisterByIp;
using PDV.Application.Features.CashRegisters.Queries.ListCashRegisters;
using PDV.Application.Features.Shifts.Queries.GetActiveShift;

namespace PDV.WebUI.Controllers;

[Route("[controller]")]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _mediator = mediator;
        _configuration = configuration;
    }

    [HttpPost("Login")]
    public async Task<IActionResult> Login(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string returnUrl = "/")
    {
        // 1. Validar que la cuenta esté activa
        var user = await _userManager.FindByEmailAsync(email)
                   ?? await _userManager.FindByNameAsync(email);

        if (user != null && !user.IsActive)
            return Redirect($"/login?error=InactiveAccount&returnUrl={returnUrl}");

        var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);

        if (!result.Succeeded)
            return Redirect($"/login?error=InvalidCredentials&returnUrl={returnUrl}");

        // Redirigir estrictamente por rol del usuario
        var roles = user is not null
            ? await _userManager.GetRolesAsync(user)
            : Array.Empty<string>() as IList<string>;

        if (roles.Contains("Cashier"))
        {
            return Redirect("/shift/open");
        }
        else if (roles.Contains("Admin") || roles.Contains("Manager"))
        {
            return Redirect("/dashboard");
        }

        return LocalRedirect(returnUrl == "/" ? "/" : returnUrl);
    }

    [HttpGet("Logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/login");
    }
}
