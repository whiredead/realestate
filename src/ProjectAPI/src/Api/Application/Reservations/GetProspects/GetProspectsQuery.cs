using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Crm.Entities;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.GetProspects;

/// <summary>
/// The prospects already known to the platform, for the "pick an existing prospect" list of the reservation form:
/// CRM contacts that have no approved reservation yet, plus accounts that registered on the public site and are
/// still PROSPECT. A prospect becomes a buyer when a reservation of theirs is approved.
/// </summary>
public class GetProspectsQuery : IRequest<List<ProspectItem>>
{
    /// <summary>Matches name, e-mail, phone or CIN.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// The project being reserved in. When given, only prospects with a confirmed appointment (visit) for THAT
    /// project are listed: those are the people the agent has actually met or scheduled.
    /// </summary>
    public Guid? ProjectId { get; set; }
}

public class ProspectItem
{
    /// <summary>The CRM contact, when the person has one. Pass it as ProspectContactId when creating the reservation.</summary>
    public Guid? ContactId { get; set; }

    /// <summary>The account, when the person already has one (public sign-up or activated link).</summary>
    public string? UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Cin { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool HasAccount { get; set; }
}

public class GetProspectsHandler : IRequestHandler<GetProspectsQuery, List<ProspectItem>>
{
    private const int MaxResults = 50;

    // Confirmed by the agent or by the visitor, or already carried out. Requested / cancelled / no-show do not count.
    private static readonly string[] ConfirmedAppointmentStatuses = { "Confirmed", "ConfirmedByUser", "Completed" };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly ICurrentUser _currentUser;

    public GetProspectsHandler(ApplicationDbContext db, UserManager<User> userManager, ICurrentUser currentUser)
    {
        _db = db;
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<List<ProspectItem>> Handle(GetProspectsQuery request, CancellationToken ct)
    {
        var needle = request.Search?.Trim().ToLower();
        var isAdmin = _currentUser.IsGlobalAdmin || _currentUser.IsInRole(RoleCodes.ProjectAdmin);

        var contactsQuery = _db.CrmContacts.AsNoTracking()
            .Where(c => c.LifecycleStatus == ContactLifecycleStatus.Prospect && c.ArchivedAt == null);

        // A sales agent works their own prospects (and the unassigned ones); admins see everyone.
        if (!isAdmin)
        {
            var me = _currentUser.UserId;
            contactsQuery = contactsQuery.Where(c => c.OwnerSalesAgentId == null || c.OwnerSalesAgentId == me);
        }

        if (request.ProjectId is Guid projectId)
        {
            contactsQuery = contactsQuery.Where(c => _db.Set<ProjectAPI.Domain.Appointments.Entities.Appointment>().Any(a =>
                a.CrmContactId == c.Id &&
                a.ProjectId == projectId &&
                ConfirmedAppointmentStatuses.Contains(a.Status)));
        }

        if (!string.IsNullOrEmpty(needle))
        {
            contactsQuery = contactsQuery.Where(c =>
                (c.FirstName + " " + c.LastName).ToLower().Contains(needle) ||
                (c.Email != null && c.Email.ToLower().Contains(needle)) ||
                (c.Phone != null && c.Phone.Contains(needle)) ||
                (c.Cin != null && c.Cin.ToLower().Contains(needle)));
        }

        var contacts = await contactsQuery
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Take(MaxResults)
            .ToListAsync(ct);

        var result = contacts.Select(c => new ProspectItem
        {
            ContactId = c.Id,
            UserId = c.UserId,
            FirstName = c.FirstName,
            LastName = c.LastName,
            Cin = c.Cin,
            Email = c.Email,
            Phone = c.Phone,
            HasAccount = !string.IsNullOrEmpty(c.UserId)
        }).ToList();

        // Accounts that signed up on the public site have no CRM contact until a reservation or an appointment
        // creates one: list them too, unless a contact above already stands for them.
        var knownUserIds = result.Where(r => r.UserId != null).Select(r => r.UserId!).ToHashSet();
        var knownEmails = result.Where(r => r.Email != null).Select(r => r.Email!.ToLowerInvariant()).ToHashSet();
        var contactedUserIds = await _db.CrmContacts.AsNoTracking()
            .Where(c => c.UserId != null).Select(c => c.UserId!).ToListAsync(ct);
        var contacted = contactedUserIds.ToHashSet();

        // With a project selected only people with a confirmed appointment there qualify, and those all have a contact.
        var accountOnly = request.ProjectId is null
            ? await _userManager.GetUsersInRoleAsync(RoleCodes.Prospect)
            : new List<User>();
        foreach (var user in accountOnly)
        {
            if (result.Count >= MaxResults) break;
            if (knownUserIds.Contains(user.Id) || contacted.Contains(user.Id)) continue;
            if (user.Email != null && knownEmails.Contains(user.Email.ToLowerInvariant())) continue;
            if (!string.IsNullOrEmpty(needle) &&
                !($"{user.FirstName} {user.LastName}".ToLower().Contains(needle) ||
                  (user.Email?.ToLower().Contains(needle) ?? false) ||
                  (user.PhoneNumber?.Contains(needle) ?? false))) continue;

            result.Add(new ProspectItem
            {
                UserId = user.Id,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email,
                Phone = user.PhoneNumber,
                HasAccount = true
            });
        }

        return result;
    }
}
