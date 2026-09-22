namespace ProjectAPI.Api.Application.Imports.ValidateImportBatch;

/// <summary>
/// §5.11/§23 — dry-run validation against a caller-chosen project. Parses a
/// workbook where each sheet is one building, reports every row's errors
/// WITHOUT writing anything to the stock tables — only the
/// ImportBatch/ImportRow bookkeeping rows are persisted, so the batch can be
/// re-inspected before CommitImportBatchCommand actually applies it.
/// </summary>
public class ValidateImportBatchCommand : IRequest<ValidateImportBatchResponse>
{
    public Guid ProjectId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
    public string? CreatedBy { get; set; }
}

public class ValidateImportBatchResponse
{
    public Guid BatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ErrorRowCount { get; set; }
    public List<RowErrorDto> Errors { get; set; } = new();

    public class RowErrorDto
    {
        public string Sheet { get; set; } = string.Empty;
        public int RowNumber { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
