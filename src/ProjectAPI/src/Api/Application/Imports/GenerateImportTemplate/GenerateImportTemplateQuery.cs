using ClosedXML.Excel;

namespace ProjectAPI.Api.Application.Imports.GenerateImportTemplate;

/// <summary>
/// §5.11/§23 — the Excel template admins fill in and re-upload. Two tabs only
/// (Buildings, Units) since this backend has no Floor entity — Unit.Floor is
/// free text, filled directly on the Units tab rather than through a
/// separate Floors tab.
/// </summary>
public class GenerateImportTemplateQuery : IRequest<byte[]>
{
}

public class GenerateImportTemplateHandler : IRequestHandler<GenerateImportTemplateQuery, byte[]>
{
    public Task<byte[]> Handle(GenerateImportTemplateQuery request, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();

        var buildings = workbook.Worksheets.Add("Buildings");
        var buildingHeaders = new[]
        {
            "Name", "Location", "Type", "ResidencyType", "MinPrice", "MaxPrice",
            "Latitude", "Longitude", "Description"
        };
        for (var i = 0; i < buildingHeaders.Length; i++)
        {
            buildings.Cell(1, i + 1).Value = buildingHeaders[i];
        }
        buildings.Row(1).Style.Font.Bold = true;

        var units = workbook.Worksheets.Add("Units");
        var unitHeaders = new[]
        {
            "BuildingName", "Floor", "UnitNumber", "NumberOfBedrooms", "NumberOfBathrooms",
            "ApartmentSurface", "BalconySurface", "TerraceSurface", "GardenSurface",
            "TotalSurface", "View", "Orientation", "LatestPrice"
        };
        for (var i = 0; i < unitHeaders.Length; i++)
        {
            units.Cell(1, i + 1).Value = unitHeaders[i];
        }
        units.Row(1).Style.Font.Bold = true;

        // BuildingName on the Units tab matches a Name on the Buildings tab
        // WITHIN THE SAME FILE — new buildings are created from this same
        // upload, so there is no pre-existing id to reference.
        buildings.Columns().AdjustToContents();
        units.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }
}
