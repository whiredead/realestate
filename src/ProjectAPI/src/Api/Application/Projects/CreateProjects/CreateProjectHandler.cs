using ProjectAPI.Api.Application.Common.Media;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Projects.CreateProjects;

public class CreateProjectHandler : IRequestHandler<CreateProjectCommand, CreateProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IQuartierRepository _quartierRepository;
    private readonly MediaUrlPolicy _media;
    private readonly ICurrentUser _currentUser;
    private readonly ApplicationDbContext _db;

    public CreateProjectHandler(
        IProjectRepository projectRepository,
        IQuartierRepository quartierRepository,
        MediaUrlPolicy media,
        ICurrentUser currentUser,
        ApplicationDbContext db)
    {
        _projectRepository = projectRepository;
        _quartierRepository = quartierRepository;
        _media = media;
        _currentUser = currentUser;
        _db = db;
    }

    /// <summary>Default reservation checklist seeded on every new project (codes are stored upper-case).</summary>
    public static readonly (string Code, string LabelFr)[] DefaultDocumentRequirements =
    {
        ("CIN", "CIN ou passeport"),
        ("RESERVATION_CONTRACT", "Contrat ou bulletin de réservation signé"),
        ("RESERVATION_PAYMENT_PROOF", "Justificatif de paiement de la réservation"),
    };

    public async Task<CreateProjectResponse> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        // §7.2 — these go straight into the public catalogue, so they are checked
        // before anything is written rather than sanitised at render time.
        _media.EnsureImageUrls(request.Images, "Images");
        _media.Ensure3DLink(request.Module3DLink, "Module3DLink");
        _media.EnsureImageUrl(request.QuartierImages, "QuartierImages");

        await ProjectTypeBienLinks.EnsureQuartierExistsAsync(_db, request.QuartierId, cancellationToken);

        // Step 1: Resolve or create Quartier
        Guid? quartierId = null;
        if (request.QuartierId.HasValue)
        {
            // Use existing quartier
            quartierId = request.QuartierId;
        }
        else if (!string.IsNullOrEmpty(request.QuartierName))
        {
            // Create new quartier
            var newQuartier = new Quartier
            {
                Id = Guid.NewGuid(),
                Name = request.QuartierName!,
                Description = request.QuartierDescription ?? string.Empty,
                Images = request.QuartierImages ?? string.Empty
            };

            await _quartierRepository.InsertAsync(newQuartier);
            await _quartierRepository.SaveAsync();
            quartierId = newQuartier.Id;
        }

        // Step 2: Create a new Project entity
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Location = request.Location,
            Address = request.Address,
            Description = request.Description ?? string.Empty,
            // Module3DLink is a NOT NULL column; a client that omits it (the admin
            // "Nouveau projet" form does) must not crash the insert. Coalesce.
            Module3DLink = request.Module3DLink ?? string.Empty,
            Images = request.Images,
            QuartierId = quartierId,
            Type = request.Type ?? "Livraison immédiate",
            // §3 / FR-CMS-001 — a project is created in DRAFT. Normalised so the
            // column only ever holds canonical codes, never the legacy (and
            // misspelled) "CommingSoon" spellings.
            StatusGlobal = ProjectStatusCodes.Normalize(request.StatusGlobal ?? ProjectStatusCodes.Draft),
            // §8 — omitted takes the entity default (12 months).
            WarrantyMonths = request.WarrantyMonths ?? 12
        };

        // Step 3: Save to repository
        await _projectRepository.InsertAsync(project);
        await _projectRepository.SaveAsync();

        // §12.1 — every project starts with the default reservation checklist
        // (identity, signed reservation contract, proof of the deposit). The
        // project admin can then change it per project; without a default, a
        // new project required nothing and any file could be submitted bare.
        var now0 = DateTime.UtcNow;
        var seq = 1;
        foreach (var (code, label) in DefaultDocumentRequirements)
        {
            _db.Add(new ProjectDocumentRequirement
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                DocumentType = code,
                LabelFr = label,
                IsRequired = true,
                SequenceNo = seq++,
                CreatedAt = now0,
                CreatedBy = _currentUser.UserId
            });
        }
        await _db.SaveChangesAsync(cancellationToken);

        if (request.TypeBienIds is { Count: > 0 })
        {
            await ProjectTypeBienLinks.SyncAsync(_db, project.Id, request.TypeBienIds, cancellationToken);
        }

        // §6.4 — a PROJECT_ADMIN's perimeter is their memberships. Without one on
        // the project they just created, every later action on it (edit,
        // buildings, approvals) was denied as out of scope. GLOBAL_ADMIN is
        // unrestricted and never gets a membership row.
        if (!_currentUser.IsGlobalAdmin && _currentUser.IsInRole(RoleCodes.ProjectAdmin) && !string.IsNullOrEmpty(_currentUser.UserId))
        {
            var now = DateTime.UtcNow;
            _db.Add(new ProjectMembership
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = _currentUser.UserId,
                RoleCode = RoleCodes.ProjectAdmin,
                ValidFrom = now,
                IsActive = true,
                AssignedByUserId = _currentUser.UserId,
                AssignedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Step 4: Build response
        return new CreateProjectResponse
        {
            Id = project.Id,
            Message = "Project created successfully."
        };
    }
}