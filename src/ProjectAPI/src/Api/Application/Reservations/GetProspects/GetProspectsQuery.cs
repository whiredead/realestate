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

        if (request.ProjectId is Guid forProject)
        {
            return await ForProjectAsync(forProject, needle, isAdmin, ct);
        }

        var contactsQuery = _db.CrmContacts.AsNoTracking()
            .Where(c => c.LifecycleStatus == ContactLifecycleStatus.Prospect && c.ArchivedAt == null);

        // A sales agent works their own prospects (and the unassigned ones); admins see everyone.
        if (!isAdmin)
        {
            var me = _currentUser.UserId;
            contactsQuery = contactsQuery.Where(c => c.OwnerSalesAgentId == null || c.OwnerSalesAgentId == me);
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

        var now = DateTimeOffset.UtcNow;
        foreach (var user in await _userManager.GetUsersInRoleAsync(RoleCodes.Prospect))
        {
            // A deactivated account (locked out) is not a live prospect.
            if (user.LockoutEnd is DateTimeOffset lockedUntil && lockedUntil > now) continue;
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

    /// <summary>
    /// The people the agent has a confirmed appointment with on this project. Built from the appointments, not
    /// only from CRM contacts: appointments made before contacts existed have none, and those people still count.
    /// A person who already has a contact is listed through it (so choosing them reuses that contact); a person
    /// without one is listed from the appointment's own details and gets their contact when the reservation is created.
    /// </summary>
    private async Task<List<ProspectItem>> ForProjectAsync(Guid projectId, string? needle, bool isAdmin, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        var appointments = await _db.Set<ProjectAPI.Domain.Appointments.Entities.Appointment>().AsNoTracking()
            .Where(a => a.ProjectId == projectId && ConfirmedAppointmentStatuses.Contains(a.Status))
            .Where(a => isAdmin || a.SalesAgentId == me)
            .Select(a => new { a.CrmContactId, a.UserId, a.Name, a.LastName, a.Email, a.PhoneNumber, a.AppointmentDate })
            .ToListAsync(ct);

        var contactIds = appointments.Where(a => a.CrmContactId != null).Select(a => a.CrmContactId!.Value).Distinct().ToList();
        var emails = appointments.Where(a => !string.IsNullOrWhiteSpace(a.Email))
            .Select(a => CrmContact.NormalizeEmail(a.Email)!).Distinct().ToList();

        var contacts = await _db.CrmContacts.AsNoTracking()
            .Where(c => c.ArchivedAt == null && (contactIds.Contains(c.Id) || (c.EmailNormalized != null && emails.Contains(c.EmailNormalized))))
            .ToListAsync(ct);

        var result = new Dictionary<string, ProspectItem>();

        foreach (var appointment in appointments.OrderByDescending(a => a.AppointmentDate))
        {
            var emailKey = CrmContact.NormalizeEmail(appointment.Email);
            var contact = contacts.FirstOrDefault(c => c.Id == appointment.CrmContactId)
                          ?? (emailKey is null ? null : contacts.FirstOrDefault(c => c.EmailNormalized == emailKey));

            // Someone who already bought is a buyer, not a prospect.
            if (contact is not null && contact.LifecycleStatus != ContactLifecycleStatus.Prospect) continue;

            var item = contact is not null
                ? new ProspectItem
                {
                    ContactId = contact.Id, UserId = contact.UserId, FirstName = contact.FirstName, LastName = contact.LastName,
                    Cin = contact.Cin, Email = contact.Email, Phone = contact.Phone, HasAccount = !string.IsNullOrEmpty(contact.UserId)
                }
                : new ProspectItem
                {
                    UserId = appointment.UserId, FirstName = appointment.Name, LastName = appointment.LastName,
                    Email = appointment.Email, Phone = appointment.PhoneNumber, HasAccount = !string.IsNullOrEmpty(appointment.UserId)
                };

            var key = contact is not null ? $"c:{contact.Id}" : emailKey is not null ? $"e:{emailKey}" : $"n:{item.LastName}|{item.FirstName}".ToLowerInvariant();
            if (result.ContainsKey(key)) continue;

            if (!string.IsNullOrEmpty(needle) &&
                !($"{item.FirstName} {item.LastName}".ToLower().Contains(needle) ||
                  (item.Email?.ToLower().Contains(needle) ?? false) ||
                  (item.Phone?.Contains(needle) ?? false) ||
                  (item.Cin?.ToLower().Contains(needle) ?? false))) continue;

            result[key] = item;
        }

        return result.Values.OrderBy(r => r.LastName).ThenBy(r => r.FirstName).Take(MaxResults).ToList();
    }
}
