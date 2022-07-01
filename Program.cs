using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace alishadev
{
    // Resolve order: duplicates, conflicting, double-hops.
    class Program
    {
        internal static string[] Locales;

        static void Main(string[] args)
        {
            // Set paths.
            string oldRedirectsFilePath = args[0];  // Old redirects file path (e.g. "C:\old-redirects.txt").
            string newRedirectsFilePath = args[1];  // New redirects file path (e.g. "C:\new-redirects.txt").
            string analysisCsvDirPath = args[2]; // Output analysis folder path (e.g. "C:\analysis");

            // Read input files.
            List<RedirectEntry> newRedirectEntries = new();
            List<RedirectEntry> oldRedirectEntries = new();
            string oldRedirectsContent = File.ReadAllText(oldRedirectsFilePath);
            string newRedirectsContent = File.ReadAllText(newRedirectsFilePath);
            string localesContent = RedirectChecker.Properties.Resources.locales;

            // Build lists.
            string[] oldRedirects = oldRedirectsContent.Split(new[] { '\r', '\n' }).Where(x => x.IsNotEmpty()).Select(x => x.Trim(new[] { ',', ' ' })).ToArray();
            string[] newRedirects = newRedirectsContent.Split(new[] { '\r', '\n' }).Where(x => x.IsNotEmpty()).Select(x => x.Trim(new[] { ',', ' ' })).ToArray();
            Locales = localesContent.Split(new[] { '\r', '\n' }).Where(x => x.IsNotEmpty()).Select(x => x.Trim(new[] { ',', ' ' })).ToArray();

            // Deconstruct old & new redirect entries.
            for (int i = 0; i < newRedirects.Length; i++) newRedirectEntries.Add(DeconstructRedirectEntry(newRedirects[i], lineNum: i + 1, type: RedirectType.New));
            for (int i = 0; i < oldRedirects.Length; i++) oldRedirectEntries.Add(DeconstructRedirectEntry(oldRedirects[i], lineNum: i + 1, type: RedirectType.Old));

            // Check for redirect errors in combined old & new redirects.
            int indicator = 0;
            int prevPercent = -1;
            List<RedirectEntry> allRedirectEntries = oldRedirectEntries.Concat(newRedirectEntries).ToList();

            allRedirectEntries.ForEach(x =>
            {
                // Time-consuming operation.
                x.GetRedirectIssues(allRedirectEntries);

                // Update progress stats.
                int percent = 100 * ++indicator / allRedirectEntries.Count;
                if (percent != prevPercent) Console.Write($"\rCompleted {percent}%");
                prevPercent = percent;
            });
            Console.WriteLine($"\rCompleted 100%");

            StringBuilder sb = new("Level,Location,Issue,Mapping,Related locations,Proposed resolution\r\n");

            // Print invalid formats (error).
            List<RedirectEntry> entries = allRedirectEntries.Where(x => x.Issue.InvalidFormat.IsFound).ToList();
            entries.ForEach(x => sb.AppendLine($"{x.Issue.InvalidFormat.Level},{x.Type}:{x.LineNum},Invalid format,{x.Mapping},"));

            // Conflicting destinations (error).
            entries = allRedirectEntries.Where(x => x.Issue.ConflictingDestination.IsFound).ToList();
            List<RedirectEntry> orderedEntries = entries.OrderBy(x => x.Issue.ConflictingDestination.RelatedGuid).ThenBy(x => x.Type).ToList();
            Guid previousGuid = new();
            foreach (var x in orderedEntries)
            {
                // Separate related issues with blank line.
                Guid guid = x.Issue.ConflictingDestination.RelatedGuid;
                if (guid != previousGuid) sb.AppendLine();
                previousGuid = guid;

                sb.AppendLine($"{x.Issue.ConflictingDestination.Level},{x.Type}:{x.LineNum},Conflicting destination,{x.Mapping},{x.Issue.ConflictingDestination.RelatedEntries},{x.Issue.ConflictingDestination.Resolution}");
            }

            // Double-hops (error).
            entries = allRedirectEntries.Where(x => x.Issue.DoubleHop.IsFound).ToList();
            orderedEntries = entries.OrderBy(x => x.Issue.DoubleHop.RelatedGuid).ThenBy(x => x.Type).ToList();
            foreach (var x in orderedEntries)
            {
                // Separate related issues with blank line.
                Guid guid = x.Issue.DoubleHop.RelatedGuid;
                if (guid != previousGuid) sb.AppendLine();
                previousGuid = guid;

                sb.AppendLine($"{x.Issue.DoubleHop.Level},{x.Type}:{x.LineNum},Double-hop,{x.Mapping},{x.Issue.DoubleHop.RelatedEntries},{x.Issue.DoubleHop.Resolution}");
            }

            // Print duplicate mappings (warning).
            entries = allRedirectEntries.Where(x => x.Issue.DuplicateMapping.IsFound).ToList();
            orderedEntries = entries.OrderBy(x => x.Issue.DuplicateMapping.RelatedGuid).ThenBy(x => x.Type).ToList();
            foreach (var x in orderedEntries)
            {
                // Separate related issues with blank line.
                Guid guid = x.Issue.DuplicateMapping.RelatedGuid;
                if (guid != previousGuid) sb.AppendLine();
                previousGuid = guid;

                sb.AppendLine($"{x.Issue.DuplicateMapping.Level},{x.Type}:{x.LineNum},Duplicate mapping,{x.Mapping},{x.Issue.DuplicateMapping.RelatedEntries},{x.Issue.DuplicateMapping.Resolution}");
            }

            // Print invalid source locale (warning).
            entries = allRedirectEntries.Where(x => x.Issue.InvalidSrcLocale.IsFound).ToList();
            entries.ForEach(x => sb.AppendLine($"{x.Issue.InvalidSrcLocale.Level},{x.Type}:{x.LineNum},Invalid source locale,{x.Mapping},"));

            // Print invalid destination locale (warning).
            entries = allRedirectEntries.Where(x => x.Issue.InvalidDstLocale.IsFound).ToList();
            entries.ForEach(x => sb.AppendLine($"{x.Issue.InvalidDstLocale.Level},{x.Type}:{x.LineNum},Invalid destination locale,{x.Mapping},"));

            // Print inconsistent casing redirects (ignore).
            //entries = allRedirectEntries.Where(x => x.Issue.InconsistentCasing.IsFound).ToList();
            //entries.ForEach(x => sb.AppendLine($"{x.Issue.InconsistentCasing.Level},{x.Type}:{x.LineNum},Inconsistent casing,{x.Mapping},"));

            string analysisContent = sb.ToString();
            File.WriteAllText(Path.Combine(analysisCsvDirPath, "analysis.csv"), sb.ToString());

            Console.WriteLine($"\r\nIssues published to: \"{analysisCsvDirPath}\"");
            Console.WriteLine("\r\nPress any key to exit.");
            Console.Read();
        }

        private static RedirectEntry DeconstructRedirectEntry(string mapping, int lineNum, RedirectType type)
        {
            RedirectEntry redirectEntry = new();
            if (mapping.EqualsAny("{", "}"))
            {
                redirectEntry.Type = RedirectType.Ignore;
                return redirectEntry;
            }

            Match match = Regex.Match(mapping, @"""(?<src>/(?<srcLocale>.*?)/(?<srcAsset>.*?))"": ""(?<dst>/(?<dstLocale>.*?)/(?<dstAsset>.*?))""");
            redirectEntry.Type = type;
            redirectEntry.LineNum = lineNum;
            redirectEntry.Mapping = mapping;
            redirectEntry.Source = match.Groups["src"].Value;
            redirectEntry.SourceLocale = match.Groups["srcLocale"].Value;
            redirectEntry.SourceAsset = match.Groups["srcAsset"].Value;
            redirectEntry.Destination = match.Groups["dst"].Value;
            redirectEntry.DestinationLocale = match.Groups["dstLocale"].Value;
            redirectEntry.DestinationAsset = match.Groups["dstAsset"].Value;

            // Validation.
            if (!match.Success || redirectEntry.SourceLocale.IsEmpty() || redirectEntry.SourceAsset.IsEmpty() || redirectEntry.DestinationLocale.IsEmpty())
            {
                redirectEntry.Issue.InvalidFormat.IsFound = true;
            }
            if (!redirectEntry.Issue.InvalidFormat.IsFound)
            {
                //redirectEntry.Issue.InconsistentCasing.IsFound = !string.Equals(mapping, mapping.ToLower());  // ignore inconsistent casing.
                redirectEntry.Issue.InvalidSrcLocale.IsFound = !Locales.ContainsXx(redirectEntry.SourceLocale);
                redirectEntry.Issue.InvalidDstLocale.IsFound = !Locales.ContainsXx(redirectEntry.DestinationLocale);
            }

            return redirectEntry;
        }
    }
}
