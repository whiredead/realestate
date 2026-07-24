namespace ProjectAPI.Api.Application.EspacesTempsReel.GetVideosByProjectId;

public class GetVideosByProjectIdQuery : IRequest<List<VideoListItem>>
{
    public Guid ProjectId { get; set; }
}

public class VideoListItem
{
    public Guid Id { get; set; }
    public string VideoLink { get; set; } = string.Empty;
    public DateTime InsertedAt { get; set; }
}
