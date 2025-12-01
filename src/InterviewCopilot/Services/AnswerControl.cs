namespace InterviewCopilot.Services;

public enum AnswerPersona
{
    Junior,
    Senior,
    Director,
    Rapid
}

public static class AnswerControl
{
    public static AnswerPersona Persona { get; private set; } = AnswerPersona.Senior;

    public static void SetPersona(AnswerPersona persona) => Persona = persona;

    public static int GetTargetDurationSeconds() => Persona switch
    {
        AnswerPersona.Junior => 10,
        AnswerPersona.Senior => 15,
        AnswerPersona.Director => 20,
        AnswerPersona.Rapid => 5,
        _ => 15
    };
}
