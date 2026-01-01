using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;

/*
    TESTPATH: @"C:\Windows\System32\drivers\etc\testHost.txt"
    REAL PATH: @"C:\Windows\System32\drivers\etc\hosts"
*/

class Blocker
{
    static string HostsPath() => Path.Combine(Environment.SystemDirectory, @"drivers\etc\testHost.txt");
    static string AppDataDir() => AppContext.BaseDirectory;
    static string DefaultTemplatePath() => Path.Combine(AppDataDir(), "hosts_default.txt");
    static string OriginalBackupPath() => Path.Combine(AppDataDir(), "hosts_original.bak");
    //static string TimedstampedBackupPath() => Path.Combine(AppDataDir(), $"hosts_{DateTime.Now:yyyyMMdd_HHmmss}");

    static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    static void RelaunchAsAdmin(string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Process.GetCurrentProcess().MainModule!.FileName!,
            Arguments = string.Join(" ", args),  // preserve args if you want
            UseShellExecute = true,              // required for Verb="runas"
            Verb = "runas"                       // triggers UAC prompt
        };
        Process.Start(psi);
    }

    public void FlushDNS()
    {
        var process = new System.Diagnostics.Process();
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            FileName = "cmd.exe",
            Arguments = "/C ipconfig /flushdns"
        };
        process.StartInfo = startInfo;
        process.Start();
    }

    static BlockListFile LoadBlockList(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<BlockListFile>(
        json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }   // <-- key fix
    ) ?? new BlockListFile();
    }

    static string NormalizeDomaim(string input)
    {
        input = Regex.Replace(input.Trim(), @"^\s*https?://", "", RegexOptions.IgnoreCase);
        int slash = input.IndexOf('/');
        if (slash >= 0) input = input[..slash];
        return input.Trim().TrimEnd('.');
    }

    static IEnumerable<string> ExpandPreset(Preset p)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in p.Domains)
        {
            string domain = NormalizeDomaim(d);
            if (seen.Add(domain))
            {
                yield return $"0.0.0.0 {domain}";
                if (p.Ipv6) yield return $":: {domain}";
            }

            foreach (var v in p.Auto_Varients)
            {
                string sub = $"{v.Trim('.')}.{domain}";
                if (seen.Add(sub))
                {
                    yield return $"0.0.0.0 {sub}";
                    if (p.Ipv6) yield return $":: {sub}";
                }
            }
        }
    }

    public void EnsureOriginalBackup()
    {
        string hosts = HostsPath();
        string orig = OriginalBackupPath();
        if (!File.Exists(hosts)) throw new FileNotFoundException("Hosts File Not Found: ", hosts);

        if (!File.Exists(orig))
        {
            File.Copy(hosts, orig, overwrite: false);
            Console.WriteLine($"Saved original hosts: {orig}");
        }
    }

    public void RestoreDefaultHosts()
    {
        string tpl = DefaultTemplatePath();
        string hosts = HostsPath();

        if (!File.Exists(tpl))
        {
            Console.WriteLine("Default template (hosts_default.txt) not found.");
            return;
        }

        File.Copy(hosts, hosts + ".preDefaultRestore_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak", overwrite: true);

        File.Copy(tpl, hosts, overwrite: true);
        Console.WriteLine("Restored hosts to default template.");
    }

    public void RestoreOriginalHosts()
    {
        string orig = OriginalBackupPath();
        string hosts = HostsPath();

        if (!File.Exists(orig))
        {
            Console.WriteLine("No original backup found.");
            return;
        }

        File.Copy(hosts, hosts + ".preOriginalRestore_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak", overwrite: true);

        File.Copy(orig, hosts, overwrite: true);
        Console.WriteLine("Restored hosts to Original backup");
    }


    public int AddHostEntry()
    {
        try
        {
            Console.WriteLine("Site to Block: ");
            string? input = Console.ReadLine();
            string website = input ?? string.Empty;

            string path = @"C:\Windows\System32\drivers\etc\testHost.txt";

            string[] lines =
            {
                $"# Blocked site: {website}",
                ParseHostEntry(website),
                ""
            };

            File.AppendAllLines(path, lines);
            Console.WriteLine("Successfully added to Host Files");

            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("Access denied. Make sure the app is running as Administrator.");
            return 1;
        }
    }

    public void RemoveHostEntry(string hostPath, string website)
    {
        var lines = File.ReadAllLines(hostPath).ToList();

        int start = lines.FindIndex(l =>
        l.Trim().Equals($"# {website}", StringComparison.OrdinalIgnoreCase) ||
        l.Trim().Equals($"# Blocked site: {website}", StringComparison.OrdinalIgnoreCase));

        if (start < 0) return;

        int end = start + 1;
        while (end < lines.Count && !string.IsNullOrWhiteSpace(lines[end])) end++;

        int count = end - start;
        if (end < lines.Count && string.IsNullOrWhiteSpace(lines[end])) count++;
        lines.RemoveRange(start, Math.Min(count, lines.Count - start));

        for (int i = lines.Count - 2; i >= 0; i--)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) && string.IsNullOrWhiteSpace(lines[i + 1]))
                lines.RemoveAt(i + 1);
        }

        File.WriteAllLines(hostPath, lines);
    }

    public void ListHostFile(string hostPath)
    {
        if (!File.Exists(hostPath))
        {
            Console.WriteLine("Hosts File Not Found");
        }

        Console.WriteLine("=====HOSTS BEGIN=====");
        foreach (var line in File.ReadAllLines(hostPath))
            Console.WriteLine(line);
        Console.WriteLine("=====HOSTS END=====");
    }

    static void ApplyJsonBlock(string jsonPath)
    {
        string hosts = HostsPath();
        var blocklist = LoadBlockList(jsonPath);

        var lines = new List<string>();
        lines.Add($"# BEGIN CATEGORY: {Path.GetFileNameWithoutExtension(jsonPath)}");

        foreach (var p in blocklist.Presets)
            lines.AddRange(ExpandPreset(p));

        lines.Add($"# END CATEGORY: {Path.GetFileNameWithoutExtension(jsonPath)}");
        lines.Add("");

        File.AppendAllLines(hosts, lines);
        Console.WriteLine($"Applied blocklist from {jsonPath}");
    }

    static string ParseHostEntry(string website) => $"0.0.0.0 {website}";
    static int Main(string[] args)
    {
        var blocker = new Blocker();
        string hostPath = @"C:\Windows\System32\drivers\etc\testHost.txt";

        if (!IsRunningAsAdmin())
        {
            try
            {
                RelaunchAsAdmin(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Elevation failed: " + ex.Message);
            }
            return 0;
        }

        Console.WriteLine("Running as Administrator.");

        blocker.EnsureOriginalBackup();

        while (true)
        {
            Console.WriteLine("###################################");
            Console.WriteLine("1. List Host File:");
            Console.WriteLine("2. Add Host Entry:");
            Console.WriteLine("3. Remove Host Entry:");
            Console.WriteLine("4. Exit");
            Console.WriteLine("5. Restore DEFAULT hosts");
            Console.WriteLine("6. Restore ORIGINAL hosts");
            Console.WriteLine("7. List BLOCK LISTS");
            Console.WriteLine("###################################");
            Console.Write("Select an option: ");

            string? choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    blocker.ListHostFile(hostPath);
                    break;

                case "2":
                    blocker.EnsureOriginalBackup();
                    blocker.AddHostEntry();
                    //blocker.FlushDNS();
                    break;

                case "3":
                    blocker.EnsureOriginalBackup();
                    Console.WriteLine("Site to Remove:");
                    string input = Console.ReadLine()?.Trim() ?? "";
                    blocker.RemoveHostEntry(hostPath, input);
                    //blocker.FlushDNS();
                    break;

                case "4":
                    return 0;

                case "5":
                    blocker.EnsureOriginalBackup();
                    blocker.RestoreDefaultHosts();
                    //blocker.FlushDNS();
                    break;

                case "6":
                    blocker.RestoreOriginalHosts();
                    //blocker.FlushDNS();
                    break;

                case "7":
                    Console.WriteLine("=====BLOCK LIST=====");
                    Console.WriteLine("1. Adult");
                    Console.WriteLine("2. AI");
                    Console.WriteLine("3. Gambling");
                    Console.WriteLine("4. Gaming");
                    Console.WriteLine("5. Messanger");
                    Console.WriteLine("6. News");
                    Console.WriteLine("7. Shopping");
                    Console.WriteLine("8. Social");
                    Console.WriteLine("9. Streaming");

                    string? blockListChoice = Console.ReadLine();

                    switch (blockListChoice)
                    {
                        case "1":
                            ApplyJsonBlock(@"bin\Debug\net9.0\blocklist\adult.json");
                            break;

                        case "2":
                            ApplyJsonBlock(@"bin\Debug\net9.0\blocklist\ai.json");
                            break;

                        case "3":
                            ApplyJsonBlock(@"bin\Debug\net9.0\blocklist\gambling.json");
                            break;

                        case "4":
                            ApplyJsonBlock(@"bin\Debug\net9.0\blocklist\gaming.json");
                            break;

                        case "5":
                            ApplyJsonBlock(@"bin\Debug\net9.0\blocklist\messanger.json");
                            break;

                        case "6":
                            ApplyJsonBlock(@"bin\Debug\net9.0\blocklist\news.json");
                            break;

                        case "7":
                            ApplyJsonBlock(@"bin\Debug\net9.0\blocklist\shopping.json");
                            break;

                        case "8":
                            ApplyJsonBlock(@"bin\Debug\net9.0\blocklist\social.json");
                            break;

                        case "9":
                            ApplyJsonBlock(@"bin\Debug\net9.0\blocklist\streaming.json");
                            break;

                        default:
                            Console.WriteLine("WRONG COMMAND");
                            break;
                    }
                    break;

                default:
                    Console.WriteLine("WRONG COMMAND");
                    break;
            }

        }
    }
}
