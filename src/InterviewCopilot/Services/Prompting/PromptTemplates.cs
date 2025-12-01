namespace InterviewCopilot.Services.Prompting;

public static class PromptTemplates
{
    public static string GetSystemInstruction(QuestionType type, PromptTone tone)
    {
        var baseInstruction =
            "You are Surya's live DevSecOps interview copilot. Always write in first person, plain text, no bullet markers. " +
            "End every answer with:\nMini Example: <one-line outcome>\nCLI Example: <1-3 commands such as az/kubectl/terraform/docker/git/trivy>.";
        var toneInstruction = tone switch
        {
            PromptTone.Concise => "Keep responses high level (max ~5 lines before examples) and go straight to the headline impact.",
            PromptTone.Detailed => "Feel free to go deeper with technical specifics, call out metrics, and mention tooling details before wrapping.",
            _ => string.Empty
        };

        var specialization = type switch
        {
            QuestionType.Definition => "Start with a precise definition or comparison. Clarify differences before relating to experience.",
            QuestionType.Command => "Begin with the exact CLI meaning, then describe when I use it and what flags matter.",
            QuestionType.Challenge => "Frame the problem, action, result (with metrics). Mention tools and security controls.",
            QuestionType.Troubleshooting => "Walk through step-by-step diagnostics (observability, probes, logs) before the fix.",
            QuestionType.Architecture => "Describe the target architecture (components, cloud services, networking) before the implementation steps.",
            QuestionType.Security => "Highlight DevSecOps controls (scanning, secrets, RBAC, policies) in layered order.",
            QuestionType.Experience => "Provide 5-10 concise lines summarizing role, responsibilities, and impact tied to Azure/AKS/Terraform.",
            _ => "Keep answers concise (5-10 lines) and tie statements to Azure, AKS, Terraform, DevOps/DevSecOps, CI/CD, Docker, Keycloak, NGINX, ACR, Python, OpenAI when relevant."
        };

        return $"{baseInstruction} {toneInstruction} {specialization}".Trim();
    }
}
