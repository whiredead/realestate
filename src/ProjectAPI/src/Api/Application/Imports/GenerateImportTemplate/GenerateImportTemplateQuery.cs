using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Imports.GenerateImportTemplate;

/// <summary>
/// §5.11/§23 — the Excel template an admin fills in and re-uploads. Given
/// <see cref="BuildingNames"/>, the workbook ships with one already-named
/// sheet per building — nothing left to rename. Given none, it falls back to
/// a single "Exemple" sheet the admin duplicates by hand (Excel: right-click
/// the tab → "Déplacer ou copier" → "Créer une copie") and renames per
/// building. Either way: row 2 holds the building's own fields, row 4 is the
/// unit table's header, row 5+ are unit rows — the tab name IS the
/// building's name on import (see ImportWorkbookReader).
///
/// No Images column: photos are attached separately, per building, from its
/// detail page (bulk folder upload straight to blob storage) rather than by
/// typing URLs into a spreadsheet cell. TypeBien offers an in-cell dropdown
/// of the TARGET PROJECT's own linked types (same set CreateUnit/UpdateUnit
/// offer), so a name can never be mistyped or reference a type this project
/// doesn't offer; left blank, the unit is matched on bedroom count instead,
/// same as every other unit-creation path.
///
/// Every column mirrors a field the normal single-building/single-unit admin
/// forms already expose — Module3DLink (Building, create-only there too) and
/// SaleableValue/SaleableValue1/PriceSaleableValue/PriceSaleableValue1 (Unit,
/// update-only there — this import offers them on both create and update
/// since a spreadsheet row can't distinguish the two the way separate forms
/// do). Fields no admin form exposes (AgentId, ImagePrincipale, computed
/// stock counters) are deliberately absent.
/// </summary>
public class GenerateImportTemplateQuery : IRequest<byte[]>
{
    public Guid ProjectId { get; set; }

    /// <summary>Building names to pre-create one sheet each for. Empty ⇒ a single generic "Exemple" sheet.</summary>
    public List<string> BuildingNames { get; set; } = new();
}

public class GenerateImportTemplateHandler : IRequestHandler<GenerateImportTemplateQuery, byte[]>
{
    /// <summary>Excel forbids these in a sheet name.</summary>
    private static readonly char[] ForbiddenSheetNameChars = { ':', '\\', '/', '?', '*', '[', ']' };
    private const int MaxSheetNameLength = 31;

    private readonly ApplicationDbContext _db;

    public GenerateImportTemplateHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> Handle(GenerateImportTemplateQuery request, CancellationToken ct)
    {
        var project = await _db.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");

        var typeBienNames = await _db.Set<Project>()
            .Where(p => p.Id == request.ProjectId)
            .SelectMany(p => p.TypeBiens.Where(link => link.TypeBien != null).Select(link => link.TypeBien!.Name))
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);

        using var workbook = new XLWorkbook();

        IXLRange? typeBienRange = null;
        if (typeBienNames.Count > 0)
        {
            // TypeBien dropdown: written once to a hidden lookup sheet and
            // referenced by range from every building sheet, rather than as a
            // literal list, since Excel's in-cell list literal is capped at
            // 255 characters.
            var lookup = workbook.Worksheets.Add(ImportWorkbookReader.LookupSheetName);
            for (var i = 0; i < typeBienNames.Count; i++)
            {
                lookup.Cell(i + 1, 1).Value = typeBienNames[i];
            }
            lookup.Visibility = XLWorksheetVisibility.VeryHidden;
            typeBienRange = lookup.Range(1, 1, typeBienNames.Count, 1);
        }

        var sheetNames = SanitizeSheetNames(request.BuildingNames);
        if (sheetNames.Count == 0)
        {
            AddBuildingSheet(workbook, "Exemple", project, typeBienNames, typeBienRange, includeExampleValues: true);
            var exampleSheet = workbook.Worksheet("Exemple");
            exampleSheet.Cell(1, 7).Value = "← Le nom de CET ONGLET est le nom du bâtiment. Renommez-le, ne le laissez pas \"Exemple\".";
            exampleSheet.Cell(1, 7).Style.Font.Italic = true;
            exampleSheet.Cell(1, 7).Style.Font.FontColor = XLColor.FromArgb(0x99, 0x33, 0x00);
        }
        else
        {
            foreach (var name in sheetNames)
            {
                AddBuildingSheet(workbook, name, project, typeBienNames, typeBienRange, includeExampleValues: false);
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Trims, drops blanks, and makes each name a legal, unique Excel sheet
    /// name: strips characters Excel forbids in a tab name, truncates to 31
    /// characters, and de-duplicates (case-insensitively — Excel tab names
    /// collide that way too) by appending " (2)", " (3)"... A name that
    /// collides with the reserved Lookup sheet is suffixed the same way.
    /// </summary>
    private static List<string> SanitizeSheetNames(IEnumerable<string> rawNames)
    {
        var result = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ImportWorkbookReader.LookupSheetName };

        foreach (var raw in rawNames)
        {
            var name = (raw ?? string.Empty).Trim();
            if (name.Length == 0) continue;

            foreach (var c in ForbiddenSheetNameChars)
            {
                name = name.Replace(c, '-');
            }
            if (name.Length > MaxSheetNameLength)
            {
                name = name[..MaxSheetNameLength];
            }

            var candidate = name;
            var suffix = 2;
            while (!used.Add(candidate))
            {
                var tag = $" ({suffix++})";
                candidate = name.Length + tag.Length > MaxSheetNameLength
                    ? name[..(MaxSheetNameLength - tag.Length)] + tag
                    : name + tag;
            }

            result.Add(candidate);
        }

        return result;
    }

    private static void AddBuildingSheet(
        XLWorkbook workbook,
        string sheetName,
        Project project,
        List<string> typeBienNames,
        IXLRange? typeBienRange,
        bool includeExampleValues)
    {
        var sheet = workbook.Worksheets.Add(sheetName);

        // Row 1: labels for the building-info row below (row 2). Row 3 is
        // left blank as a visual separator before the unit table.
        // Mirrors the two fields in the new-building form. The tab itself is
        // the building name and the selected project is already known.
        var buildingHeaders = new[] { "Localisation", "Description" };
        for (var i = 0; i < buildingHeaders.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = buildingHeaders[i];
        }
        sheet.Row(1).Style.Font.Bold = true;

        var unitHeaders = new[]
        {
            "Étage", "Numéro d’unité", "Type de bien", "Chambres", "Salles de bain",
            "Surface appartement (m²)", "Surface balcon (m²)", "Surface terrasse (m²)", "Surface jardin privé (m²)",
            "Vue", "Orientation", "Prix final (MAD)", "Prix / SV (MAD)", "Prix / SV1 (MAD)",
            "Surface totale (calculée)", "SV (calculée)", "SV1 (calculée)"
        };
        var headerRow = ImportWorkbookReader.UnitHeaderRow;
        for (var i = 0; i < unitHeaders.Length; i++)
        {
            sheet.Cell(headerRow, i + 1).Value = unitHeaders[i];
        }
        sheet.Row(headerRow).Style.Font.Bold = true;

        var firstDataRow = ImportWorkbookReader.UnitFirstDataRow;
        if (includeExampleValues)
        {
            sheet.Cell(ImportWorkbookReader.BuildingInfoRow, 1).Value = "Ouest";
            sheet.Cell(ImportWorkbookReader.BuildingInfoRow, 2).Value = $"Exemple — remplacez par la description de {project.Name}.";

            sheet.Cell(firstDataRow, 1).Value = "RDC";
            sheet.Cell(firstDataRow, 2).Value = "A-001";
            sheet.Cell(firstDataRow, 3).Value = typeBienNames.Count > 0 ? typeBienNames[0] : string.Empty;
            sheet.Cell(firstDataRow, 4).Value = 2;
            sheet.Cell(firstDataRow, 5).Value = 1;
            sheet.Cell(firstDataRow, 6).Value = 65.5;
            sheet.Cell(firstDataRow, 7).Value = 8;
            sheet.Cell(firstDataRow, 8).Value = 0;
            sheet.Cell(firstDataRow, 9).Value = 0;
            sheet.Cell(firstDataRow, 10).Value = "Mer";
            sheet.Cell(firstDataRow, 11).Value = "Sud";
            sheet.Cell(firstDataRow, 12).Value = 1200000;
        }

        // The workbook shows the same formulas as the commercial source.
        // Only the input columns are imported; the server recalculates these
        // values again before persisting the Unit.
        for (var row = firstDataRow; row < firstDataRow + 1000; row++)
        {
            sheet.Cell(row, 15).FormulaA1 = $"=F{row}+G{row}+H{row}+I{row}";
            sheet.Cell(row, 16).FormulaA1 = $"=F{row}+G{row}/2+H{row}/2+I{row}*30%";
            sheet.Cell(row, 17).FormulaA1 = $"=F{row}+G{row}+H{row}/2+I{row}*30%";
        }

        if (typeBienRange is not null)
        {
            const int unitDataRows = 1000;
            var typeBienColumn = sheet.Range(firstDataRow, 3, firstDataRow + unitDataRows, 3);
            var validation = typeBienColumn.SetDataValidation();
            validation.List(typeBienRange, true);
            validation.InputMessage = $"Sélectionnez un type de bien déjà rattaché à {project.Name}, ou laissez vide.";
            validation.ErrorMessage = $"Ce type de bien n'est pas rattaché à {project.Name}.";
        }

        sheet.Columns().AdjustToContents();
    }
}
