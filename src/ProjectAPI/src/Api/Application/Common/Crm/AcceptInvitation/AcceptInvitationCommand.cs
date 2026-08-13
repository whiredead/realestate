namespace ProjectAPI.Api.Application.Common.Crm.AcceptInvitation;

/// <summary>
/// §1.1/§6.2 Phase 2 — the invitee sets their own password against a valid,
/// unexpired, unused AccountInvitation token. This is the piece
/// ApproveReservationHandler's invitation issue was always waiting on: the
/// token has existed since approval, but nothing accepted it (N20).
/// </summary>
public class AcceptInvitationCommand : IRequest<AcceptInvitationResponse>
{
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AcceptInvitationResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
