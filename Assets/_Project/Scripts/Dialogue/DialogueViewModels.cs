using System.Collections.Generic;

public class DialogueNodeViewModel
{
    public string SpeakerName;
    public string LineText;
    public bool UseEventStyle;
    public bool CanAdvance;
    public bool WillCloseOnAdvance;
    public List<DialogueChoiceViewModel> Choices;
}

public class DialogueChoiceViewModel
{
    public string ChoiceText;
    public int VisibleChoiceIndex;
}
