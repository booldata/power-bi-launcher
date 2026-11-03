using System.Xml.Linq;

namespace PowerBILauncher.Tests;

public class EnglishResourceSentenceCaseTests
{
    /// <summary>
    /// Product names and other words that may keep a capital letter inside
    /// an otherwise sentence-case string. Domain terms (profile, version,
    /// default version, settings, scan) are not listed - they stay lowercase
    /// unless they start a sentence.
    /// </summary>
    private static readonly string[] AllowList =
    [
        "Windows network identity",
        "Power BI Desktop",
        "Power BI Launcher",
        "Power BI",
        "Report Server",
        "Report Servers",
        "Credential Manager",
        "Microsoft Store",
        "MIT License",
        "Entra ID",
        "Entra",
        "Windows",
        "Desktop",
        "Enter",
        "Esc",
        "Reorder"
    ];

    [Fact]
    public void English_resource_strings_are_sentence_case()
    {
        IReadOnlyDictionary<string, string> table = LoadEnglishResources();
        List<string> allowList = [.. AllowList.OrderByDescending(entry => entry.Length)];
        var violations = new List<string>();

        foreach ((string key, string value) in table)
        {
            if (!IsSentenceCase(value, allowList))
            {
                violations.Add($"{key}: \"{value}\"");
            }
        }

        Assert.True(
            violations.Count == 0,
            "English chrome must be Fluent sentence case (first word and product names excepted):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Board_title_edit_header_and_settings_descriptions_match_the_copy_sweep()
    {
        IReadOnlyDictionary<string, string> table = LoadEnglishResources();

        Assert.Equal("Power BI", table["ProfilesPageTitle.Text"]);
        Assert.Equal("Edit profile", table["EditProfileSection.Text"]);
        Assert.Equal(
            "Scan this PC or browse to a Power BI executable. One default version is used when a profile has not picked one.",
            table["RegisteredVersionsHelp.Text"]);
        Assert.Equal(
            "Root folder where profile data is stored.",
            table["FolderToStoreProfilesHelp.Text"]);
        Assert.Equal("Registered versions", table["RegisteredVersions.Text"]);
        Assert.Equal("Folder to store profiles", table["FolderToStoreProfiles.Text"]);
        Assert.Equal("Power BI this profile launches.", table["VersionFieldHelp.Text"]);
        Assert.Equal("Optional.", table["IdentityHelp.Text"]);
        Assert.Equal(
            "Leave on default version unless this profile needs another. Launch will not fall back if this version is gone.",
            table["AboutVersion.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip"]);
        Assert.Equal(
            "Used when Power BI connects to Report Server or data sources that use Windows authentication. This does not sign you in to the Power BI service or replace your Entra ID/MFA sign-in.",
            table["AboutIdentity.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip"]);
        Assert.Equal(
            "Off hides this profile from the tray. It remains on the board.",
            table["AboutTray.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip"]);
        Assert.Equal(
            "Reset card color",
            table["NoAccent.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip"]);
        Assert.Equal(
            "Reset card color",
            table["NoAccent.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name"]);
        Assert.Equal("Contains all files and settings for this profile.", table["ProfileFolderHelp.Text"]);
    }

    private static bool IsSentenceCase(string value, IReadOnlyList<string> allowList)
    {
        string[] tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        bool firstSignificant = true;
        int index = 0;
        while (index < tokens.Length)
        {
            if (MatchAllowListPhrase(tokens, index, allowList, out int consumed))
            {
                string last = tokens[index + consumed - 1];
                index += consumed;
                firstSignificant = EndsSentence(last);
                continue;
            }

            string token = tokens[index];
            string letters = LettersOf(token);
            if (letters.Length == 0)
            {
                if (EndsSentence(token))
                {
                    firstSignificant = true;
                }

                index++;
                continue;
            }

            if (IsAcronym(letters) || IsKeyboardShortcut(token))
            {
                firstSignificant = EndsSentence(token);
                index++;
                continue;
            }

            if (firstSignificant)
            {
                firstSignificant = EndsSentence(token);
                index++;
                continue;
            }

            if (!IsLowercase(letters) && !IsFormatPlaceholder(token))
            {
                return false;
            }

            firstSignificant = EndsSentence(token);
            index++;
        }

        return true;
    }

    private static bool MatchAllowListPhrase(
        string[] tokens,
        int index,
        IReadOnlyList<string> allowList,
        out int consumed)
    {
        foreach (string phrase in allowList)
        {
            string[] phraseWords = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (index + phraseWords.Length > tokens.Length)
            {
                continue;
            }

            bool match = true;
            for (int offset = 0; offset < phraseWords.Length; offset++)
            {
                string token = tokens[index + offset];
                string letters = LettersOf(token);
                if (!token.Equals(phraseWords[offset], StringComparison.Ordinal)
                    && !letters.Equals(phraseWords[offset], StringComparison.Ordinal))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                consumed = phraseWords.Length;
                return true;
            }
        }

        consumed = 0;
        return false;
    }

    private static string LettersOf(string token)
    {
        if (IsFormatPlaceholder(token))
        {
            return "";
        }

        var chars = new char[token.Length];
        int count = 0;
        foreach (char c in token)
        {
            if (char.IsLetter(c))
            {
                chars[count++] = c;
            }
        }

        return new string(chars, 0, count);
    }

    private static bool IsLowercase(string letters)
    {
        foreach (char c in letters)
        {
            if (char.IsLetter(c) && !char.IsLower(c))
            {
                return false;
            }
        }

        return letters.Length > 0;
    }

    private static bool IsAcronym(string letters) =>
        letters.Length is >= 2 and <= 6 && letters.All(char.IsUpper);

    private static bool IsKeyboardShortcut(string token) =>
        token.Contains('+', StringComparison.Ordinal);

    private static bool EndsSentence(string token)
    {
        if (token.EndsWith("...", StringComparison.Ordinal) || token.EndsWith('…'))
        {
            return false;
        }

        char last = token[^1];
        return last is '.' or '?' or '!';
    }

    private static bool IsFormatPlaceholder(string token) =>
        token.StartsWith('{') && token.EndsWith('}');

    private static IReadOnlyDictionary<string, string> LoadEnglishResources()
    {
        string path = Path.Combine(FindRepoRoot(), "Power BI Launcher", "Strings", "en-US", "Resources.resw");
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement data in XDocument.Load(path).Root!.Elements("data"))
        {
            string? name = (string?)data.Attribute("name");
            string? value = (string?)data.Element("value");
            if (name is not null && value is not null)
            {
                table[name] = value;
            }
        }

        return table;
    }

    private static string FindRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Power BI Launcher.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the repository root from the test output directory.");
    }
}
