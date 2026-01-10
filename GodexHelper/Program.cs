using EzioDll;
using System.Reflection;
using System.Text.Json;

namespace GodexHelper
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Parse command-line arguments
            var arguments = ParseArguments(args);

            GodexPrinter godexPrinter = new GodexPrinter();

            List<string> PrinterList = GodexPrinter.GetPrinter_USB();

            godexPrinter.OpenUSB(PrinterList[0]);

            // Load template from file
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "label_template.txt");
            string labelTemplate = LoadLabelTemplate(templatePath);

            // Parse issues array
            List<string> issues = new List<string>();
            if (arguments.ContainsKey("issues"))
            {
                string issuesArg = arguments["issues"];
                try
                {
                    // Try parsing as JSON array
                    issues = JsonSerializer.Deserialize<List<string>>(issuesArg) ?? new List<string>();
                }
                catch
                {
                    // Robust fallback for shell quote stripping or simple comma-separated lists
                    if (issuesArg.StartsWith("[") && issuesArg.EndsWith("]"))
                        issuesArg = issuesArg.Substring(1, issuesArg.Length - 2);

                    issues = issuesArg.Split(',')
                                      .Select(s => s.Trim().Trim('"'))
                                      .Where(s => !string.IsNullOrWhiteSpace(s))
                                      .ToList();
                }
            }

            // Build issue lines dynamically (2 issues per line)
            string issueLines = BuildIssueLines(issues);

            // Replace placeholders with actual data
            var replacements = new Dictionary<string, string>
            {
                { "{DEVICE_NAME}", GetArgument(arguments, "devicename", "") },
                { "{STORAGE}", GetArgument(arguments, "storage", "") },
                { "{MODEL_NUMBER}", GetArgument(arguments, "modelnumber", "") },
                { "{ICLOUD_STATE}", GetArgument(arguments, "icloudstate", "") },
                { "{FMI_STATE}", GetArgument(arguments, "fmistate", "") },
                { "{SIM_STATE}", GetArgument(arguments, "simstate", "") },
                { "{MDM_STATE}", GetArgument(arguments, "mdmstate", "") },
                { "{COLOR}", GetArgument(arguments, "color", "") },
                { "{IOS_VERSION}", GetArgument(arguments, "iosversion", "") },
                { "{BATTERY}", GetArgument(arguments, "battery", "") },
                { "{PORT}", GetArgument(arguments, "port", "") },
                { "{IMEI}", GetArgument(arguments, "imei", "") },
                { "{DATE}", GetArgument(arguments, "date", DateTime.Now.ToString("dd/MM/yyyy")) }
            };

            string labelData = ReplacePlaceholders(labelTemplate, replacements);

            // Replace the issue placeholder lines with dynamically generated ones
            labelData = ReplaceIssueLines(labelData, issueLines);

            // Send to printer
            godexPrinter.Command.Send(labelData);

            godexPrinter.Close();
        }

        static Dictionary<string, string> ParseArguments(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var arg in args)
            {
                if (arg.StartsWith("--"))
                {
                    var parts = arg.Substring(2).Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        result[parts[0]] = parts[1];
                    }
                }
            }
            
            return result;
        }

        static string GetArgument(Dictionary<string, string> arguments, string key, string defaultValue)
        {
            return arguments.ContainsKey(key) ? arguments[key] : defaultValue;
        }

        static string BuildIssueLines(List<string> issues)
        {
            if (issues.Count == 0)
            {
                return "";
            }

            var lines = new List<string>();
            int yPosition = 510; // Starting Y position

            // Group issues, 2 per line
            for (int i = 0; i < issues.Count; i += 2)
            {
                string lineText = issues[i];
                if (i + 1 < issues.Count)
                {
                    lineText += ", " + issues[i + 1];
                }
                
                lines.Add($"AC,33,{yPosition},1,1,0,0E,{lineText}");
                yPosition += 32; // Increment Y position for next line
            }

            return string.Join("\r\n", lines);
        }

        static string ReplaceIssueLines(string template, string issueLines)
        {
            // Find and remove the placeholder issue lines
            var lines = template.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            
            // Remove lines containing the issue placeholder ({MESSAGES} or the old long string)
            lines = lines.Where(line => !line.Contains("{MESSAGES}") && !line.Contains("{MESSAGES & PROBLEMS (MAX 2 EACH LINE)}")).ToList();

            // Find the "Notes :" line and insert issue lines after it
            int notesIndex = lines.FindIndex(line => line.Contains("Notes :"));
            
            if (notesIndex >= 0 && !string.IsNullOrEmpty(issueLines))
            {
                lines.Insert(notesIndex + 1, issueLines);
            }

            return string.Join("\r\n", lines);
        }

        static string LoadLabelTemplate(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Label template file not found: {filePath}");
            }

            // Read all lines and join with \r\n
            string[] lines = File.ReadAllLines(filePath);
            return string.Join("\r\n", lines);
        }

        static string ReplacePlaceholders(string template, Dictionary<string, string> data)
        {
            string result = template;
            foreach (var kvp in data)
            {
                result = result.Replace(kvp.Key, kvp.Value);
            }
            return result;
        }
    }
}
