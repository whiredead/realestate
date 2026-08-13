namespace AuthenticationAPI.Infrastructure.Settings;

/// <summary>
/// Outbound mail credentials, bound from configuration.
///
/// These used to be hardcoded inside <c>EmailService</c>, which put a live
/// mailbox password in source control: anyone with repository access could
/// send mail as the company, and rotating the password meant editing and
/// redeploying code. Supply them per environment instead —
/// <c>Smtp:Password</c> in user-secrets locally, or the
/// <c>Smtp__Password</c> environment variable in a deployment. Never commit a
/// real value to appsettings.json.
///
/// This API sends OTP and password-reset mail, so a missing value here breaks
/// account recovery — the binding in DependencyInjection fails fast at startup
/// rather than at the first reset attempt.
/// </summary>
public class SmtpSettings
{
    /// <summary>SMTP host, e.g. "smtp.gmail.com".</summary>
    public string Host { get; set; } = "smtp.gmail.com";

    /// <summary>SMTP port. 587 for STARTTLS, 465 for implicit TLS.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Account used to authenticate, and the default From address.</summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// Account password or app-specific password. Must come from
    /// environment/user-secrets — see the class remarks.
    /// </summary>
    public string Password { get; set; } = default!;

    /// <summary>
    /// From address on outgoing mail. Falls back to <see cref="UserName"/>
    /// when unset, which is the common case for a single-mailbox setup.
    /// </summary>
    public string? FromAddress { get; set; }

    /// <summary>Whether to negotiate TLS. Effectively always true in practice.</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>The address mail is sent from.</summary>
    public string ResolvedFrom => string.IsNullOrWhiteSpace(FromAddress) ? UserName : FromAddress;
}
