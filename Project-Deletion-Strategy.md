# Project Deletion Strategy for RemoveProject Endpoint

## Executive Summary

This document outlines a comprehensive soft deletion strategy for the Project entity in the GPIA Real Estate system. The strategy prioritizes data integrity, audit trail maintenance, and business continuity while ensuring safe removal of projects from active operations.

## 1. Entity Relationship Analysis

### Current Cascade Delete Behaviors
Based on the entity configurations, the following relationships exist:

**Direct Child Entities (Cascade Delete):**
- `Immeuble` → `Units`, `ImmeubleAssignments`, `Appointments`, `Features`
- `LikedProject` → Already configured with cascade delete
- `ProjectAssignment` → Configured with cascade delete
- `ProjectFeature` → Implicit cascade delete
- `EspaceTempsReel` → Implicit cascade delete
- `Appointment` → Configured with cascade delete

**Parent Entity:**
- `Quartier` → Uses `SetNull` behavior, won't be affected

**Complex Relationship Chains:**
```
Project → Immeuble → Unit → Reservation/Sale
                    → TypeBien
                    → ImmeubleFeature
                    → ImmeubleAssignment
```

## 2. Business Rules and Validation Requirements

### Preconditions for Project Deletion
1. **Project Status Validation**: Only allow deletion of projects with status "Completed", "Cancelled", or "Archived"
2. **Active Transaction Check**: Prevent deletion if any of the following exist:
   - Active appointments (status != "Completed" && status != "Cancelled")
   - Pending reservations (status = "Pending")
   - Active sales (delivery not completed)
3. **Financial Clearance**: No pending payments or unresolved financial transactions
4. **Administrative Authorization**: Require admin role or specific permissions

### Audit Trail Requirements
- Track who deleted the project
- Record deletion timestamp
- Store deletion reason
- Maintain original data for reporting

## 3. Soft Delete Implementation Strategy

### 3.1 Project Entity Modifications

Add the following properties to the `Project` entity:

```csharp
public class Project
{
    // Existing properties...
    
    /// <summary>
    /// Indicates whether the project has been soft deleted
    /// </summary>
    public bool IsDeleted { get; set; } = false;
    
    /// <summary>
    /// Timestamp when the project was soft deleted
    /// </summary>
    public DateTime? DeletedAt { get; set; }
    
    /// <summary>
    /// ID of the user who deleted the project
    /// </summary>
    public string? DeletedBy { get; set; }
    
    /// <summary>
    /// Reason for project deletion
    /// </summary>
    public string? DeletionReason { get; set; }
}
```

### 3.2 Query Modifications

All existing queries must be updated to filter out deleted projects:

```csharp
// Example modification in ProjectRepository
public async Task<List<ProjectDTO>> GetProjects(string? UserId, string? Name, string? Location, string Adress, int PageNumber, int PageSize)
{
    var likedProjectIds = new List<Guid>();

    if (!string.IsNullOrEmpty(UserId))
    {
        var likedProjects = await _context.LikedProjects.Where(lp => lp.UserId == UserId).ToListAsync();
        likedProjectIds = likedProjects.Select(lp => lp.ProjectId).ToList();
    }
    
    var projects = await _context.Projects
        .Where(p => !p.IsDeleted && // Add this filter
            (string.IsNullOrEmpty(Name) || p.Name.Contains(Name)) &&
            (string.IsNullOrEmpty(Location) || p.Location.Contains(Location)) &&
            (string.IsNullOrEmpty(Adress) || p.Address.Contains(Adress))
        )
        // Rest of the query...
}
```

## 4. Step-by-Step Deletion Approach

### 4.1 Validation Phase
1. **Existence Check**: Verify project exists and is not already deleted
2. **Permission Check**: Validate user has deletion permissions
3. **Business Rule Validation**: Check project status and active transactions
4. **Dependency Check**: Verify no critical active dependencies

### 4.2 Soft Delete Execution
1. **Begin Transaction**: Start database transaction
2. **Update Project**: Mark project as deleted with audit information
3. **Cascade Soft Delete**: Update related entities if necessary (optional)
4. **Log Deletion**: Create audit log entry
5. **Commit Transaction**: Commit all changes

### 4.3 Post-Deletion Actions
1. **Notification**: Send notifications to relevant stakeholders
2. **Cache Invalidation**: Clear related cache entries
3. **Reporting Update**: Update analytics and reports

## 5. Transaction Management Strategy

### 5.1 Transaction Scope
```csharp
using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    // Deletion operations
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### 5.2 Isolation Level
- Use `ReadCommitted` isolation level for balance between consistency and performance
- Consider `Serializable` for critical operations requiring maximum consistency

## 6. Error Handling and Rollback Mechanisms

### 6.1 Exception Types
- `ProjectNotFoundException`: Project doesn't exist
- `ProjectAlreadyDeletedException`: Project already soft deleted
- `ActiveTransactionsException`: Project has active transactions
- `InsufficientPermissionsException`: User lacks deletion permissions
- `DatabaseOperationException`: Database operation failed

### 6.2 Rollback Strategy
- Automatic rollback on any exception during transaction
- Detailed logging of rollback reasons
- User-friendly error messages

## 7. Preconditions and Validation Checks

### 7.1 Detailed Validation Logic
```csharp
public async Task<ValidationResult> ValidateProjectDeletion(Guid projectId, string userId)
{
    var project = await _projectRepository.GetByIdAsync(projectId);
    if (project == null)
        return ValidationResult.Failure("Project not found");
    
    if (project.IsDeleted)
        return ValidationResult.Failure("Project already deleted");
    
    // Check user permissions
    if (!await HasDeletionPermissions(userId, project))
        return ValidationResult.Failure("Insufficient permissions");
    
    // Check project status
    if (!IsDeletableStatus(project.StatusGlobal))
        return ValidationResult.Failure($"Project status '{project.StatusGlobal}' does not allow deletion");
    
    // Check active appointments
    var activeAppointments = await _appointmentRepository.GetActiveAppointmentsByProjectId(projectId);
    if (activeAppointments.Any())
        return ValidationResult.Failure("Project has active appointments");
    
    // Check pending reservations
    var pendingReservations = await _reservationRepository.GetPendingReservationsByProjectId(projectId);
    if (pendingReservations.Any())
        return ValidationResult.Failure("Project has pending reservations");
    
    // Check active sales
    var activeSales = await _saleRepository.GetActiveSalesByProjectId(projectId);
    if (activeSales.Any())
        return ValidationResult.Failure("Project has active sales");
    
    return ValidationResult.Success();
}
```

## 8. API Endpoint Design

### 8.1 Endpoint Structure
```csharp
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteProject(Guid id, [FromBody] DeleteProjectRequest request)
{
    var command = new DeleteProjectCommand 
    { 
        ProjectId = id,
        UserId = User.FindFirst("userId")?.Value,
        DeletionReason = request.DeletionReason
    };
    
    var result = await _mediator.Send(command);
    
    return result.Success ? Ok(result) : BadRequest(result.Message);
}
```

### 8.2 Request/Response Models
```csharp
public class DeleteProjectRequest
{
    public string DeletionReason { get; set; }
}

public class DeleteProjectResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public Guid ProjectId { get; set; }
    public DateTime DeletedAt { get; set; }
}
```

## 9. Implementation Recommendations

### 9.1 Command Handler Structure
```csharp
public class DeleteProjectHandler : IRequestHandler<DeleteProjectCommand, DeleteProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteProjectHandler> _logger;
    
    public async Task<DeleteProjectResponse> Handle(DeleteProjectCommand command, CancellationToken cancellationToken)
    {
        // Validation
        var validationResult = await ValidateProjectDeletion(command.ProjectId, command.UserId);
        if (!validationResult.IsValid)
            return DeleteProjectResponse.Failure(validationResult.ErrorMessage);
        
        // Execute soft delete
        using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var project = await _projectRepository.GetByIdAsync(command.ProjectId);
            
            project.IsDeleted = true;
            project.DeletedAt = DateTime.UtcNow;
            project.DeletedBy = command.UserId;
            project.DeletionReason = command.DeletionReason;
            
            await _projectRepository.UpdateAsync(project);
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation("Project {ProjectId} soft deleted by user {UserId}", command.ProjectId, command.UserId);
            
            return DeleteProjectResponse.Success(command.ProjectId, project.DeletedAt.Value);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error soft deleting project {ProjectId}", command.ProjectId);
            return DeleteProjectResponse.Failure("An error occurred while deleting the project");
        }
    }
}
```

### 9.2 Repository Extensions
```csharp
public interface IProjectRepository
{
    // Existing methods...
    
    Task<Project> GetActiveProjectByIdAsync(Guid id);
    Task<IEnumerable<Project>> GetActiveProjectsAsync();
    Task<bool> HasActiveTransactionsAsync(Guid projectId);
}

public async Task<Project> GetActiveProjectByIdAsync(Guid id)
{
    return await _context.Projects
        .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
}
```

## 10. Migration Strategy

### 10.1 Database Migration
```csharp
public partial class AddSoftDeleteToProject : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            table: "Projects",
            type: "bit",
            nullable: false,
            defaultValue: false);
            
        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            table: "Projects",
            type: "datetime2",
            nullable: true);
            
        migrationBuilder.AddColumn<string>(
            name: "DeletedBy",
            table: "Projects",
            type: "nvarchar(450)",
            nullable: true);
            
        migrationBuilder.AddColumn<string>(
            name: "DeletionReason",
            table: "Projects",
            type: "nvarchar(max)",
            nullable: true);
    }
}
```

### 10.2 Data Migration Considerations
- Set `IsDeleted = false` for all existing projects
- Handle existing deleted projects if any
- Update indexes to include `IsDeleted` column

## 11. Performance Considerations

### 11.1 Indexing Strategy
```sql
CREATE INDEX IX_Projects_IsDeleted ON Projects(IsDeleted);
CREATE INDEX IX_Projects_IsDeleted_Id ON Projects(IsDeleted, Id);
```

### 11.2 Query Optimization
- Use filtered indexes for active projects
- Consider archiving very old deleted projects
- Implement caching for frequently accessed active projects

## 12. Security Considerations

### 12.1 Authorization
- Require admin role or specific deletion permission
- Implement role-based access control
- Log all deletion attempts

### 12.2 Data Privacy
- Ensure deletion reason is appropriately sanitized
- Consider GDPR implications for personal data
- Implement data retention policies

## 13. Monitoring and Auditing

### 13.1 Audit Trail
```csharp
public class ProjectDeletionAudit
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string DeletedBy { get; set; }
    public DateTime DeletedAt { get; set; }
    public string DeletionReason { get; set; }
    public string ProjectSnapshot { get; set; } // JSON snapshot of project data
}
```

### 13.2 Monitoring Metrics
- Deletion success/failure rates
- Time taken for deletion operations
- Number of active vs deleted projects

## 14. Testing Strategy

### 14.1 Unit Tests
- Validation logic tests
- Permission checks
- Transaction rollback scenarios

### 14.2 Integration Tests
- End-to-end deletion flow
- Database transaction integrity
- Concurrent deletion scenarios

### 14.3 Performance Tests
- Large dataset deletion performance
- Impact on query performance
- Transaction timeout scenarios

## 15. Rollback Plan

### 15.1 Data Recovery
- Implement un-delete functionality for administrators
- Maintain backup procedures
- Document recovery procedures

### 15.2 System Rollback
- Version rollback strategy
- Database migration rollback
- Configuration rollback

## Conclusion

This soft deletion strategy provides a robust, auditable, and safe approach to project deletion while maintaining data integrity and business continuity. The implementation ensures that all business rules are enforced, transactions are properly managed, and a complete audit trail is maintained for compliance and reporting purposes.

The strategy balances the need for data removal from active operations with the requirement to maintain historical data for analysis and compliance purposes.