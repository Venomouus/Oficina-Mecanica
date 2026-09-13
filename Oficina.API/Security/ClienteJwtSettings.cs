namespace Oficina.API.Security;

public sealed class ClienteJwtSettings
{
    public bool Enabled { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    public bool Valido(IHostEnvironment environment) => !Enabled ||
        (!string.IsNullOrWhiteSpace(Audience)
         && Uri.TryCreate(Issuer, UriKind.Absolute, out var uri)
         && (uri.Scheme == "https" || ((environment.IsDevelopment() || environment.IsEnvironment("Testing"))
             && uri.Scheme == "http" && uri.IsLoopback))
         && !Issuer.EndsWith('/') && string.IsNullOrEmpty(uri.Query)
         && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo));
}
