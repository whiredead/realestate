namespace ProjectAPI.Api.Application.Feedbacks.SubmitFeedback
{
    /// <summary>
    /// Command for submitting feedback on a project or the application.
    /// </summary>
    public class SubmitFeedbackCommand : IRequest<string>
    {
        public string UserId { get; set; }
        public Guid? ProjectId { get; set; } // Nullable, can be feedback on project or app
        public int Rating { get; set; } // Rating from 1 to 5
        public string Comments { get; set; } // User's feedback comments
        public List<string> Attachements { get; set; } // Feedback attachements 
    }

}
