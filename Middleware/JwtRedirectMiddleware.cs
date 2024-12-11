using System.Security.Claims;

public class JwtRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TokenService _tokenService;

    public JwtRedirectMiddleware(RequestDelegate next, TokenService tokenService)
    {
        _next = next;
        _tokenService = tokenService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.ToString().ToLower();

        if (!path.Equals("/") && !path.StartsWith("/index") && !path.StartsWith("/login") && !path.StartsWith("/register"))
        {
            if (context.Request.Cookies.TryGetValue("access_token", out string token))
            {
                var principal = _tokenService.ValidateToken(token, out bool shouldRefreshToken);
                if (principal == null)
                {
                    context.Response.Cookies.Delete("access_token");
                    context.Response.Redirect("/Index");
                    return;
                }

                if (shouldRefreshToken)
                {
                    var userId = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier).Value);
                    var newToken = _tokenService.GenerateToken(userId);
                    context.Response.Cookies.Append("access_token", newToken);
                }
            }
            else
            {
                context.Response.Redirect("/Index");
                return;
            }
        }

        if ((path == "/login" || path == "/register" || path == "/index" || path == "" || path == "/") && context.Request.Cookies.ContainsKey("access_token"))
        {
            context.Response.Redirect("/Profile");
            return;
        }

        await _next(context);
    }
}