namespace Bribery.Domain.Models;

public enum BribeSubmissionType
{
    Text,
    Image
}

public readonly record struct BribeSubmission(BribeSubmissionType Type, string Content)
{
    public static BribeSubmission FromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new GameRuleException("Bribe text cannot be empty.");
        }

        return new BribeSubmission(BribeSubmissionType.Text, text.Trim());
    }

    public static BribeSubmission FromImage(string imageReference)
    {
        if (string.IsNullOrWhiteSpace(imageReference))
        {
            throw new GameRuleException("Image bribes must include a reference.");
        }

        return new BribeSubmission(BribeSubmissionType.Image, imageReference);
    }
}
