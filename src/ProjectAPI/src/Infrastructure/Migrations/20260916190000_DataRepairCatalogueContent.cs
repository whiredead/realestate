using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <summary>
    /// Data repair, not a code fix — same category as N28
    /// (FixFeedbackCommentsMojibake) and the same underlying mechanism: no
    /// application code in this repo writes or seeds these columns, so there
    /// is nothing to fix except the rows themselves.
    ///
    /// Found auditing the public catalogue (frontend redesign work,
    /// 2026-09-16): two unrelated problems, both isolated to specific rows.
    ///
    /// 1) Mojibake — "Les Jardins de l'Atlas" (Projects.Description),
    ///    "Hay Riad" (Quartiers.Description) and 3 of its ProjectFeature
    ///    names were imported with their UTF-8 bytes each reinterpreted as
    ///    Latin-1/Windows-1252 before storage ("é" -&gt; "Ã©"), confirmed
    ///    byte-for-byte via the public API response. Every other project
    ///    checked (Beaulieu Zenata Ecocité) is stored correctly, so this is
    ///    isolated to whatever import produced the Atlas/Hay Riad rows.
    ///
    /// 2) Placeholder photography passed off as real — TypeBien.Image on 8
    ///    plans across all 3 live projects holds a small set of royalty-free
    ///    Wikimedia Commons photos (a Casablanca street with an unrelated
    ///    billboard, a generic marina shot, even the Hassan II Mosque for a
    ///    "Marina Bay Residences" penthouse), reused across unrelated plans
    ///    and projects — never a photo of the property it's attached to.
    ///    Projects.Images / Quartiers.Images for "Les Jardins de l'Atlas" and
    ///    "Marina Bay Residences" are 100% the same class of stock Unsplash/
    ///    Wikimedia URLs, with no real photography anywhere in either
    ///    project's row. "Beaulieu Zenata Ecocité" is the one project with
    ///    genuine uploaded photography (its own blob storage account) — only
    ///    its plans' placeholder TypeBien.Image values are cleared here, so
    ///    they fall through to that real project gallery instead (the public
    ///    catalogue projection already prefers a plan's own image, then the
    ///    project's) rather than pretending three Wikimedia photos are theirs.
    ///
    ///    Clearing these to NULL/empty is not a workaround: the public site
    ///    already renders an honest "no photo yet" state everywhere a plan or
    ///    project has no images (PlanCard's "Photo à venir",
    ///    PublicPlanDetailPage's "Les plans et photos de ce bien seront
    ///    publiés prochainement."). Uploading real photography for "Les
    ///    Jardins de l'Atlas" and "Marina Bay Residences" through the
    ///    existing admin media panel is a content task, not an engineering
    ///    one — this migration only stops false photography from displaying
    ///    while that happens.
    /// </summary>
    public partial class DataRepairCatalogueContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- 0) Real map coordinates --------------------------------------
            // Project.Location holds an external Google Maps share link (a place
            // id, not decodable to coordinates without a paid API call) despite
            // its own doc comment claiming "coordinates x,y" — there has never
            // been a real Latitude/Longitude column. Added here for the public
            // map (redesign brief §4), and seeded for the 3 live projects with
            // real, verified neighbourhood-level coordinates (OpenStreetMap
            // Nominatim, no fabricated data) — precise enough for a quartier pin,
            // not claiming a building-exact address this data doesn't have.
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Projects",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Projects",
                type: "float",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE [Projects] SET [Latitude] = 33.9567844, [Longitude] = -6.8777482 WHERE [Name] = N'Les Jardins de l''Atlas';
                UPDATE [Projects] SET [Latitude] = 33.6068698, [Longitude] = -7.6206498 WHERE [Name] = N'Marina Bay Residences';
                UPDATE [Projects] SET [Latitude] = 33.6594285, [Longitude] = -7.4188426 WHERE [Name] = N'Beaulieu Zenata Ecocité';
            ");

            // ---- 1) Mojibake -------------------------------------------------
            migrationBuilder.Sql(@"
                UPDATE [Projects]
                SET [Description] = N'Un programme résidentiel intimiste au cœur de Hay Riad : villas et duplex haut de gamme entourés d''espaces verts, à deux pas des écoles internationales et du centre d''affaires.'
                WHERE [Name] = N'Les Jardins de l''Atlas';

                UPDATE [Quartiers]
                SET [Description] = N'Quartier résidentiel et d''affaires de Rabat, connu pour ses avenues arborées, ses ambassades et sa proximité avec les grandes écoles et le technopole.'
                WHERE [Name] = N'Hay Riad';

                UPDATE [ProjectFeature] SET [Name] = N'Sécurité 24h/24'     WHERE [Name] = N'SÃ©curitÃ© 24h/24';
                UPDATE [ProjectFeature] SET [Name] = N'Parking résidents'   WHERE [Name] = N'Parking rÃ©sidents';
                UPDATE [ProjectFeature] SET [Name] = N'Piscine partagée'    WHERE [Name] = N'Piscine partagÃ©e';
            ");

            // ---- 2) Placeholder photography ----------------------------------
            // Every URL below is a stock/Wikimedia photo confirmed NOT to depict
            // the property it was attached to. Matched by exact value so this
            // only ever touches rows still holding that specific placeholder.
            migrationBuilder.Sql(@"
                UPDATE [TypeBiens] SET [Image] = NULL WHERE [Image] IN (
                    N'https://upload.wikimedia.org/wikipedia/commons/thumb/6/62/Twin_Center%2C_Boulevard_Mohamed_Zerktouni%2C_Casablanca.JPG/1280px-Twin_Center%2C_Boulevard_Mohamed_Zerktouni%2C_Casablanca.JPG',
                    N'https://images.unsplash.com/photo-1600607687939-ce8a6c25118c?w=1200&q=80&auto=format&fit=crop',
                    N'https://images.unsplash.com/photo-1613977257363-707ba9348227?w=1200&q=80&auto=format&fit=crop',
                    N'https://upload.wikimedia.org/wikipedia/commons/thumb/0/0a/Marina_Casablanca_._image_%283%29.jpg/1280px-Marina_Casablanca_._image_%283%29.jpg',
                    N'https://upload.wikimedia.org/wikipedia/commons/thumb/3/32/Hassan_II_Mosque_Plaza.jpg/1280px-Hassan_II_Mosque_Plaza.jpg',
                    N'https://upload.wikimedia.org/wikipedia/commons/thumb/4/48/Marina_Casablanca_._image_%284%29.jpg/1280px-Marina_Casablanca_._image_%284%29.jpg',
                    N'https://upload.wikimedia.org/wikipedia/commons/thumb/1/15/Marina_Casablanca_._image_%282%29.jpg/1280px-Marina_Casablanca_._image_%282%29.jpg'
                );

                UPDATE [Projects] SET [Images] = N'' WHERE [Name] IN (N'Les Jardins de l''Atlas', N'Marina Bay Residences');
                UPDATE [Quartiers] SET [Images] = N'' WHERE [Name] IN (N'Hay Riad', N'Casablanca Marina');
            ");
        }

        /// <summary>
        /// Only the schema change (Latitude/Longitude) is reversed. The data
        /// repairs are not: the corrected text can't be transformed back into
        /// its original mojibake bytes losslessly for every codepoint, the
        /// cleared image URLs were never real photography of the property
        /// they were attached to, and there is no reason to want either back.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Latitude", table: "Projects");
            migrationBuilder.DropColumn(name: "Longitude", table: "Projects");
        }
    }
}
