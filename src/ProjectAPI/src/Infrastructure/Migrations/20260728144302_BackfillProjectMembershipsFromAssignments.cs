using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillProjectMembershipsFromAssignments : Migration
    {
        /// <summary>
        /// Phase 1 — one-time backfill of the legacy agent/notary-only
        /// ProjectAssignment rows into the generic ProjectMembership model.
        /// AssignedByUserId is NULL here and ONLY here: no acting admin
        /// exists for this history, and every membership the application
        /// creates from this point on must supply one. ValidFrom/AssignedAt
        /// both take ProjectAssignment.CreatedAt — the only timestamp the old
        /// model ever recorded — and ValidUntil is left open-ended (NULL):
        /// the old model had no end-date concept, so IsActive alone (copied
        /// verbatim) is what marks a backfilled row historical vs. current.
        /// A ProjectAssignment row with BOTH AgentId and NotaryId set (not
        /// produced by any handler today, but not database-forbidden either)
        /// correctly yields two membership rows, one per role — the two
        /// INSERTs below are independent, not a single mutually-exclusive
        /// mapping.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO [ProjectMemberships]
                    ([Id], [ProjectId], [UserId], [RoleCode], [ValidFrom], [ValidUntil], [IsActive], [AssignedByUserId], [AssignedAt])
                SELECT NEWID(), pa.[ProjectId], pa.[AgentId], N'SALES_AGENT', pa.[CreatedAt], NULL, pa.[IsActive], NULL, pa.[CreatedAt]
                FROM [ProjectAssignments] pa
                WHERE pa.[AgentId] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM [ProjectMemberships] pm
                      WHERE pm.[ProjectId] = pa.[ProjectId] AND pm.[UserId] = pa.[AgentId] AND pm.[RoleCode] = N'SALES_AGENT'
                  );

                INSERT INTO [ProjectMemberships]
                    ([Id], [ProjectId], [UserId], [RoleCode], [ValidFrom], [ValidUntil], [IsActive], [AssignedByUserId], [AssignedAt])
                SELECT NEWID(), pa.[ProjectId], pa.[NotaryId], N'NOTARY', pa.[CreatedAt], NULL, pa.[IsActive], NULL, pa.[CreatedAt]
                FROM [ProjectAssignments] pa
                WHERE pa.[NotaryId] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM [ProjectMemberships] pm
                      WHERE pm.[ProjectId] = pa.[ProjectId] AND pm.[UserId] = pa.[NotaryId] AND pm.[RoleCode] = N'NOTARY'
                  );
            ");
        }

        /// <summary>
        /// Only removes rows this migration itself could have created
        /// (AssignedByUserId IS NULL is otherwise never written by the
        /// application) — safe to reverse without touching any membership a
        /// real admin has since created through the app.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM [ProjectMemberships]
                WHERE [AssignedByUserId] IS NULL
                  AND [RoleCode] IN (N'SALES_AGENT', N'NOTARY');
            ");
        }
    }
}
