using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PDV.Infrastructure.Identity;
using PDV.Application.Features.CashRegisters.Queries.GetCashRegisterByIp;
using PDV.Application.Features.CashRegisters.Queries.ListCashRegisters;
using PDV.Application.Features.Shifts.Queries.GetActiveShift;
using PDV.Application.Features.Shifts.Queries.GetActiveShiftByUserId;
using PDV.Domain.Enums;

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

        var result = await _signInManager.PasswordSignInAsync(user?.UserName ?? email, password, isPersistent: false, lockoutOnFailure: false);

        if (!result.Succeeded)
            return Redirect($"/login?error=InvalidCredentials&returnUrl={returnUrl}");

        // Si el usuario ya tiene un turno abierto activo, consultar el modo de su caja y redirigir a la terminal que le corresponde
        if (user != null)
        {
            var activeShift = await _mediator.Send(new GetActiveShiftByUserIdQuery(user.Id));
            if (activeShift != null)
            {
                if (activeShift.CashRegisterMode == CashRegisterMode.Orders)
                {
                    return Redirect("/orders/capture");
                }
                return Redirect("/pos");
            }
        }

        // Redirigir por rol del usuario si no hay turno activo previo
        var roles = user is not null
            ? await _userManager.GetRolesAsync(user)
            : Array.Empty<string>() as IList<string>;

        if (roles.Contains("Cashier"))
        {
            return Redirect("/shift/open");
        }
        else if (roles.Contains("Telephonist"))
        {
            return Redirect("/orders/capture");
        }
        else if (roles.Contains("Picker"))
        {
            return Redirect("/orders/fulfillment");
        }
        else if (roles.Contains("Verifier"))
        {
            return Redirect("/orders/verify");
        }
        else if (roles.Contains("DeliveryMan"))
        {
            return Redirect("/orders/my-route");
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
