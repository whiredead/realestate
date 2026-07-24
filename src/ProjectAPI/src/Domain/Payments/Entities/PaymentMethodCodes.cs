namespace ProjectAPI.Domain.Payments.Entities;

/// <summary>
/// Declared payment methods (<c>method_code</c>, spec §48.6).
///
/// These are DECLARATIVE only. GPIA never initiates, authorises or collects a
/// payment (§14.1), and online payment is explicitly out of scope (§4.3).
/// Recording PAYPAL or CMI therefore means "the administration received the
/// funds through that channel" — no checkout or gateway is involved.
/// </summary>
public static class PaymentMethodCodes
{
    public const string Cash = "CASH";
    public const string BankTransfer = "TRANSFER";
    public const string Cheque = "CHEQUE";
    public const string Card = "CARD";

    /// <summary>Funds received via PayPal, reconciled manually.</summary>
    public const string PayPal = "PAYPAL";

    /// <summary>Funds received via CMI (Centre Monétique Interbancaire), reconciled manually.</summary>
    public const string Cmi = "CMI";

    public const string Other = "OTHER";

    /// <summary>All accepted codes; used to validate input.</summary>
    public static readonly string[] All =
    {
        Cash, BankTransfer, Cheque, Card, PayPal, Cmi, Other
    };

    public static bool IsValid(string? code) =>
        !string.IsNullOrWhiteSpace(code) && All.Contains(code, StringComparer.OrdinalIgnoreCase);
}
