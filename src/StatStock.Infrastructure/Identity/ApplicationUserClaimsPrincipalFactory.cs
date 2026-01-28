using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace StatStock.Infrastructure.Identity;

public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationIdentityUser, IdentityRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationIdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationIdentityUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        
        // Add role claim from the user's Role property
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        
        // Add additional custom claims if needed
        identity.AddClaim(new Claim("Area", user.Area ?? string.Empty));
        identity.AddClaim(new Claim("FullName", $"{user.FirstName} {user.LastName}"));
        
        return identity;
    }
}
