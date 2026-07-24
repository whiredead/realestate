using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.EspacesTempsReel.GetVideosByProjectId;

public class GetVideosByProjectIdHandler : IRequestHandler<GetVideosByProjectIdQuery, List<VideoListItem>>
{
    private readonly IEspaceTempsReelRepository _repository;

    public GetVideosByProjectIdHandler(IEspaceTempsReelRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<VideoListItem>> Handle(GetVideosByProjectIdQuery request, CancellationToken cancellationToken)
    {
        var videos = await _repository.Find(v => v.ProjectId == request.ProjectId);
        
        return videos.Select(v => new VideoListItem
        {
            Id = v.Id,
            VideoLink = v.VideoLink,
            InsertedAt = v.InsertedAt
        }).ToList();
    }
}
