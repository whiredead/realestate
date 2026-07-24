using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;
namespace ProjectAPI.Api.Application.Notary.CreateNotaryBlocks;

public class CreateNotaryBlockHandler :
    IRequestHandler<CreateNotaryBlockCommand, CreateNotaryBlockResponse>
{
    private readonly INotaryBlockRepository _blockRepo;
    private readonly INotaryAppointmentRepository _apptRepo;

    public CreateNotaryBlockHandler(
        INotaryBlockRepository blockRepo,
        INotaryAppointmentRepository apptRepo)
    {
        _blockRepo = blockRepo;
        _apptRepo = apptRepo;
    }

    public async Task<CreateNotaryBlockResponse> Handle(CreateNotaryBlockCommand req, CancellationToken ct)
    {
        if (req.End <= req.Start)
            throw new ArgumentException("End must be after Start.");

        // normalize to UTC
        var startUtc = DateTime.SpecifyKind(req.Start, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(req.End, DateTimeKind.Utc);

        // Reject if any existing appointments overlap
        var conflicting = await _apptRepo.Find(a =>
            a.NotaireId == req.NotaryId &&
            a.AppointmentDate >= startUtc &&
            a.AppointmentDate < endUtc
        );

        if (conflicting.Any())
        {
            var times = string.Join(", ", conflicting.Select(a => a.AppointmentDate.ToString("u")));
            throw new InvalidOperationException($"Cannot block over existing appointments: {times}");
        }

        var block = new NotaryBlock
        {
            Id = Guid.NewGuid(),
            NotaryId = req.NotaryId,
            Start = startUtc,
            End = endUtc,
            Reason = req.Reason
        };

        await _blockRepo.InsertAsync(block);
        await _blockRepo.SaveAsync();

        return new CreateNotaryBlockResponse
        {
            Id = block.Id,
            StartUtc = block.Start,
            EndUtc = block.End,
            Reason = block.Reason
        };
    }
}