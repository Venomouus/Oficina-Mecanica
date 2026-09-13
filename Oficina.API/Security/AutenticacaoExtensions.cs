using System.Data.Common;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Oficina.Application.Interfaces;

namespace Oficina.API.Security;

public static class AutenticacaoExtensions
{
    public const string Admin = JwtBearerDefaults.AuthenticationScheme;
    public const string Cliente = "ClienteJwt";
    public const string Administrador = "Administrador";
    public const string ClienteAutorizado = "ClienteAutorizado";
    public const string ConsultarOrdem = "ConsultarOrdem";
    private const string BancoIndisponivel = "ClienteJwt.BancoIndisponivel";

    public static void AddAutenticacaoOficina(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<ClienteJwtSettings>().Bind(configuration.GetSection("ClienteJwt"))
            .Validate(settings => settings.Valido(environment), "ClienteJwt: configure issuer HTTPS e audience; HTTP somente loopback em Development/Testing.")
            .ValidateOnStart();
        var jwt = configuration.GetSection("Jwt");
        services.AddAuthentication("OficinaJwt")
            .AddPolicyScheme("OficinaJwt", "JWT administrativo ou de cliente", options =>
            {
                // O issuer nao validado apenas seleciona o validador. O acesso depende
                // da verificacao criptografica e das politicas, nunca dessa leitura.
                options.ForwardDefaultSelector = context =>
                {
                    var settings = context.RequestServices.GetRequiredService<IOptions<ClienteJwtSettings>>().Value;
                    var header = context.Request.Headers.Authorization.ToString();
                    if (settings.Enabled && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = header[7..].Trim();
                        var handler = new JsonWebTokenHandler { MaximumTokenSizeInBytes = 16384 };
                        if (handler.CanReadToken(token))
                        {
                            try { if (handler.ReadJsonWebToken(token).Issuer == settings.Issuer) return Cliente; }
                            catch (ArgumentException) { }
                        }
                    }
                    return Admin;
                };
            })
            .AddJwtBearer(Admin, options =>
            {
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    AuthenticationType = Admin,
                    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                    ValidateIssuerSigningKey = true, RequireSignedTokens = true, RequireExpirationTime = true,
                    ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"],
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]
                        ?? throw new InvalidOperationException("Jwt:Key nao configurada.")))
                };
            })
            .AddJwtBearer(Cliente, _ => { });

        services.AddOptions<JwtBearerOptions>(Cliente).Configure<IOptions<ClienteJwtSettings>>((options, configured) =>
        {
            var settings = configured.Value;
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.BackchannelTimeout = TimeSpan.FromSeconds(5);
            options.RequireHttpsMetadata = !(environment.IsDevelopment() || environment.IsEnvironment("Testing"));
            if (settings.Enabled) options.MetadataAddress = settings.Issuer + "/.well-known/openid-configuration";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                AuthenticationType = Cliente, NameClaimType = "sub", RoleClaimType = "role",
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                ValidateIssuerSigningKey = true, RequireSignedTokens = true, RequireExpirationTime = true,
                ValidIssuer = settings.Issuer, ValidAudience = settings.Audience,
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }, ValidTypes = new[] { "at+jwt" },
                ClockSkew = TimeSpan.FromSeconds(30),
                IssuerValidator = (issuer, _, _) => settings.Enabled && issuer == settings.Issuer
                    ? issuer : throw new SecurityTokenInvalidIssuerException("Emissor invalido.")
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal!;
                    var subjects = principal.FindAll("sub").ToArray();
                    var roles = principal.FindAll("role").ToArray();
                    var scopes = principal.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    if (!settings.Enabled || subjects.Length != 1 || !Guid.TryParse(subjects[0].Value, out var id)
                        || id == Guid.Empty || roles.Length != 1 || roles[0].Value != "Cliente"
                        || !scopes.Contains("oficina:cliente", StringComparer.Ordinal))
                    {
                        context.Fail("Token de cliente invalido.");
                        return;
                    }
                    try
                    {
                        var repository = context.HttpContext.RequestServices.GetRequiredService<IClienteRepository>();
                        var cliente = await repository.ObterPorIdAsync(id);
                        if (cliente is null || !cliente.Ativo) context.Fail("Cliente nao autorizado.");
                    }
                    // O provider EF/Npgsql pode encapsular falhas transitorias.
                    catch (Exception ex) when (ex is DbException or TimeoutException
                        || ex is InvalidOperationException { InnerException: DbException or TimeoutException })
                    {
                        context.HttpContext.Items[BancoIndisponivel] = true;
                        context.Fail("Consulta de cliente indisponivel.");
                    }
                },
                OnChallenge = async context =>
                {
                    if (context.HttpContext.Items.ContainsKey(BancoIndisponivel))
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        await context.Response.WriteAsJsonAsync(new { message = "Validacao de acesso temporariamente indisponivel." });
                    }
                }
            };
        });
        services.AddAuthorization(options =>
        {
            var adminPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
                .RequireAssertion(context => context.User.EhAdministrador()).Build();
            options.DefaultPolicy = adminPolicy;
            options.AddPolicy(Administrador, adminPolicy);
            options.AddPolicy(ClienteAutorizado, policy => policy.RequireAuthenticatedUser()
                .RequireAssertion(context => context.User.EhCliente()));
            options.AddPolicy(ConsultarOrdem, policy => policy.RequireAuthenticatedUser()
                .RequireAssertion(context => context.User.EhAdministrador() || context.User.EhCliente()));
        });
    }

    public static bool EhAdministrador(this ClaimsPrincipal principal) => principal.Identities.Any(identity =>
        identity.IsAuthenticated && identity.AuthenticationType == Admin && identity.HasClaim(ClaimTypes.Role, "Admin"));

    public static bool EhCliente(this ClaimsPrincipal principal) => principal.Identities.Any(identity =>
        identity.IsAuthenticated && identity.AuthenticationType == Cliente && identity.HasClaim("role", "Cliente"));

    public static Guid ClienteId(this ClaimsPrincipal principal) => Guid.Parse(principal.Identities
        .Single(identity => identity.IsAuthenticated && identity.AuthenticationType == Cliente).FindFirst("sub")!.Value);
}
