namespace InterviewCopilot.Models;

public class CoachingState
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<string> FollowUps { get; set; } = new();
}

