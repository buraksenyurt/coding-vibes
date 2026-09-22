using System.Text;

namespace DockerCity.Domain.Runtime;

// Compose derives a project name from the folder the file sits in, unless it
// is told otherwise. It lowercases the name and throws away anything that is
// not a letter, a digit, an underscore or a dash. Matching containers to
// services needs the same name, so the rule is repeated here.
public static class ComposeProjectName
{
    public static string FromPath(string composeFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(composeFilePath);

        var folder = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(composeFilePath)));

        return Normalize(folder ?? string.Empty);
    }

    public static string Normalize(string name)
    {
        var text = new StringBuilder(name.Length);

        foreach (var character in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '_' or '-')
            {
                text.Append(character);
            }
        }

        // A project name has to start with a letter or a digit.
        while (text.Length > 0 && !char.IsAsciiLetterOrDigit(text[0]))
        {
            text.Remove(0, 1);
        }

        return text.ToString();
    }
}
