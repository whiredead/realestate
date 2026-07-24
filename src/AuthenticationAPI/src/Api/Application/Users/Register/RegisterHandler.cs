using AuthenticationAPI.Domain.ApplicationUser.Entities;
using AuthenticationAPI.Domain.ApplicationUser.Interfaces;
using AuthenticationAPI.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace AuthenticationAPI.Api.Application.Users.Register;

/// <summary>
/// Handles the registration command and processes user registration.
/// </summary>
public class RegisterHandler : IRequestHandler<RegisterCommand, string>
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailService _emailService;
    private readonly IPerformanceIndicatorRepository _performanceIndicatorRepository;
    private readonly IWeeklyAvailabilityRepository _weeklyRepo;

    /// <summary>
    /// Constructor for RegisterHandler.
    /// </summary>
    /// <param name="userManager">The UserManager for managing user-related operations.</param>
    /// <param name="emailService">The email service for sending confirmation emails.</param>
    /// <param name="performanceIndicatorRepository">The repository for performance indicators.</param>
    public RegisterHandler(UserManager<User> userManager, IEmailService emailService, IPerformanceIndicatorRepository performanceIndicatorRepository, IWeeklyAvailabilityRepository weeklyRepo)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _performanceIndicatorRepository = performanceIndicatorRepository ?? throw new ArgumentNullException(nameof(performanceIndicatorRepository));
        _weeklyRepo = weeklyRepo;
    }

    /// <summary>
    /// Handles the registration command and processes user registration.
    /// </summary>
    /// <param name="request">The RegisterCommand request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A string indicating the result of the registration operation.</returns>
    public async Task<string> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // Map request to your User entity
        var user = request.Adapt<User>();

        // Pick a Discriminator—if there's only one role, use it; otherwise concatenate
        user.Discriminator = request.Roles.Count == 1
            ? request.Roles[0]
            : string.Join(",", request.Roles);

        // Create the user
        var creation = await _userManager.CreateAsync(user, request.Password);
        if (!creation.Succeeded)
        {
            // Throw detailed validation exception
            var failures = creation.Errors
                .Select(e => new ValidationFailure(e.Code, e.Description));
            throw new Common.Exceptions.ValidationException(failures);
        }

        // Assign all roles in one call
        await _userManager.AddToRolesAsync(user, request.Roles);

        // If this user is an Agent, initialize their PerformanceIndicator
        if (request.Roles.Any(r => r.Equals("Agent", StringComparison.OrdinalIgnoreCase)))
        {
            var pi = new PerformanceIndicator
            {
                Id = Guid.NewGuid(),
                AgentId = user.Id,
                LeadsGenerated = 0,
                AppointmentsScheduled = 0,
                SuccessfulSales = 0,
                RecordedAt = DateTime.UtcNow
            };

            await _performanceIndicatorRepository.InsertAsync(pi);
            await _performanceIndicatorRepository.SaveAsync();
        }
        if (request.Roles.Any(r => r.Equals("Notaire", StringComparison.OrdinalIgnoreCase)))
        {
            var defaults = new[]
            {
              DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
              DayOfWeek.Thursday, DayOfWeek.Friday
            }.Select(d => new WeeklyAvailability
            {
                Id = Guid.NewGuid(),
                NotaryId = user.Id,
                DayOfWeek = d,
                StartTime = TimeSpan.FromHours(9),
                EndTime = TimeSpan.FromHours(17)
            })
            .Concat(new[]
            {
              new WeeklyAvailability {
                Id        = Guid.NewGuid(),
                NotaryId  = user.Id,
                DayOfWeek = DayOfWeek.Saturday,
                StartTime = TimeSpan.FromHours(9),
                EndTime   = TimeSpan.FromHours(12)
              }
            })
            .ToList();
            foreach(var df in defaults)
                await _weeklyRepo.InsertAsync(df);
            await _weeklyRepo.SaveAsync();
        }

        return user.Id;
    }
}
