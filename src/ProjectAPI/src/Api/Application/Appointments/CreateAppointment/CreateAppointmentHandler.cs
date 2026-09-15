using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Assignment;
using ProjectAPI.Api.Application.Common.Crm;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Appointments.CreateAppointment;

/// <summary>
/// Requests a commercial appointment (spec §10.1, §47.3).
///
/// Works without an account (§1.1): the visitor's details resolve to a single
/// CrmContact via <see cref="IContactResolver"/>, never a second one on repeat
/// visits. The initial status is REQUESTED, the same shared machine used by
/// final-visit, notary and handover appointments — this used to be the
/// invented "En cours de traitement" string, which no other appointment type
/// recognised.
/// </summary>
public class CreateAppointmentHandler : IRequestHandler<CreateAppointmentCommand, CreateAppointmentResponse>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly UserManager<User> _userManager;
    private readonly IPerformanceIndicatorRepository _performanceIndicatorRepository;
    private readonly IContactResolver _contacts;
    private readonly ISalesAgentAssignmentService _assignmentService;
    private readonly IAppointmentAssignmentHistoryRepository _historyRepository;
    private readonly ApplicationDbContext _db;

    public CreateAppointmentHandler(
        IAppointmentRepository appointmentRepository,
        UserManager<User> userManager,
        IPerformanceIndicatorRepository performanceIndicatorRepository,
        IContactResolver contacts,
        ISalesAgentAssignmentService assignmentService,
        IAppointmentAssignmentHistoryRepository historyRepository,
        ApplicationDbContext db)
    {
        _appointmentRepository = appointmentRepository;
        _userManager = userManager;
        _performanceIndicatorRepository = performanceIndicatorRepository;
        _contacts = contacts;
        _assignmentService = assignmentService;
        _historyRepository = historyRepository;
        _db = db;
    }

    public async Task<CreateAppointmentResponse> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        // Fill user information if UserId is provided and user is authenticated
        if (!string.IsNullOrEmpty(request.UserId))
        {
            var user = await _userManager.FindByIdAsync(request.UserId) ?? throw new NotFoundException("User not found.");
            request.Name = user.FirstName;
            request.LastName = user.LastName;
            request.Email = user.Email;
            request.PhoneNumber = user.PhoneNumber;
        }

        // §1.1 — resolve or create the one CrmContact this person is, so a
        // repeat visitor is recognised rather than duplicated on each request.
        // Done BEFORE agent selection: rule 1 (existing responsible agent)
        // reads contact.OwnerSalesAgentId.
        var contact = await _contacts.ResolveAsync(
            request.Name, request.LastName, request.Email, request.PhoneNumber,
            userId: request.UserId, ct: cancellationToken);

        string? agentId;
        string? assignmentSource;

        if (request.AgentId.HasValue)
        {
            // Manual path: an authenticated agent booking themselves, or an
            // admin explicitly naming an agent (see AppointmentsController).
            // §10.1 — no two active appointments for the same agent at the
            // same instant; the unique index is the backstop, this app-level
            // check additionally rejects near-miss overlaps within one minute.
            agentId = request.AgentId.Value.ToString();
            await EnsureSlotAvailableAsync(agentId, request.AppointmentDate, cancellationToken);
            assignmentSource = "MANUAL_REASSIGNMENT";
        }
        else
        {
            // Self-service path: system picks an eligible agent — existing
            // owner, else the project's configured rule.
            var slotEnd = request.AppointmentDate.AddMinutes(30);
            var result = await _assignmentService.AssignAsync(request.ProjectId, contact, request.AppointmentDate, slotEnd, cancellationToken);
            agentId = result.AgentId;
            assignmentSource = result.AssignmentSource;
        }

        // First-time ownership: whoever gets picked for a prospect with no
        // existing owner becomes their owner going forward. Reassignments
        // later (agent unavailable, etc.) change Appointment.SalesAgentId but
        // deliberately never overwrite an established ownership.
        if (string.IsNullOrWhiteSpace(contact.OwnerSalesAgentId))
        {
            contact.OwnerSalesAgentId = agentId;
        }

        var now = DateTime.UtcNow;
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            SalesAgentId = agentId,
            AssignmentSource = assignmentSource,
            AssignedAt = now,
            AssignedByUserId = request.UserId,
            AppointmentDate = request.AppointmentDate,
            TypeBienIds = request.TypeBienIds,
            PropertyType = request.PropertyType,
            UserId = string.IsNullOrEmpty(request.UserId) ? null : request.UserId,
            CrmContactId = contact.Id,
            Name = request.Name,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            LastName = request.LastName,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = AppointmentAttemptStatus.Requested.ToString(),
        };

        await _appointmentRepository.InsertAsync(appointment);

        await _historyRepository.InsertAsync(new AppointmentAssignmentHistory
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            SalesAgentId = agentId,
            PreviousSalesAgentId = null,
            AssignmentSource = assignmentSource!,
            ActorUserId = request.UserId,
            AssignedAt = now
        });

// Increment AppointmentsScheduled for the agent
        if (!string.IsNullOrEmpty(agentId))
        {
            var performanceIndicatorList = await _performanceIndicatorRepository.Find(pi => pi.AgentId == agentId);

            PerformanceIndicator performanceIndicator;

            if (performanceIndicatorList == null || !performanceIndicatorList.Any())
            {
                // Create new performance indicator for the agent. This call IS
                // the agent's first scheduled appointment, so the counter starts
                // at 1 — it used to start at 0, silently under-counting every
                // agent's very first appointment forever (nothing ever revisits
                // this row to correct it).
                performanceIndicator = new PerformanceIndicator
                {
                    Id = Guid.NewGuid(),
                    AgentId = agentId,
                    LeadsGenerated = 0,
                    AppointmentsScheduled = 1,
                    SuccessfulSales = 0,
                    RecordedAt = DateTime.UtcNow
                };

                await _performanceIndicatorRepository.InsertAsync(performanceIndicator);
            }
            else
            {
                performanceIndicator = performanceIndicatorList.First();
                performanceIndicator.IncrementAppointmentsScheduled();
                await _performanceIndicatorRepository.Update(performanceIndicator);
            }
        }

        await _appointmentRepository.SaveAsync();

        return new CreateAppointmentResponse
        {
            AppointmentId = appointment.Id,
            Message = "Appointment created successfully."
        };
    }

    /// <summary>Rejects a slot that overlaps another active appointment for the same agent (§10.1).</summary>
    private async Task EnsureSlotAvailableAsync(string agentId, DateTime appointmentDate, CancellationToken ct)
    {
        var blockingStatuses = AppointmentStateMachine.BlockingStatuses.Select(s => s.ToString()).ToArray();

        var conflictWindowStart = appointmentDate.AddMinutes(-1);
        var conflictWindowEnd = appointmentDate.AddMinutes(1);

        var hasConflict = await _db.Set<Appointment>().AnyAsync(a =>
            a.SalesAgentId == agentId &&
            blockingStatuses.Contains(a.Status) &&
            a.AppointmentDate > conflictWindowStart &&
            a.AppointmentDate < conflictWindowEnd, ct);

        if (hasConflict)
        {
            throw BusinessRuleException.AppointmentSlotConflict(appointmentDate);
        }
    }
}
