using System;
using System.Collections.Generic;
using System.Linq;

namespace alishadev
{
    internal enum RedirectType { Old, New, Ignore }
    internal enum IssueLevel { Error, Warning }

    internal class RedirectEntry
    {
        internal Guid Guid = Guid.NewGuid();
        internal RedirectType Type;
        internal int LineNum;
        internal string Mapping;
        internal string Source;
        internal string SourceLocale;
        internal string SourceAsset;
        internal string Destination;
        internal string DestinationLocale;
        internal string DestinationAsset;
        internal RedirectIssue Issue = new();

        internal void GetRedirectIssues(List<RedirectEntry> entryList)
        {
            if (Type == RedirectType.Ignore) return;
            if (Issue.InvalidFormat.IsFound) return;

            // This mapping is the same as another redirect's mapping.
            Issue.DuplicateMapping.IsFound = entryList.Any(x => x.Guid != Guid && string.Equals(x.Mapping, Mapping, StringComparison.OrdinalIgnoreCase));
            if (Issue.DuplicateMapping.IsFound)
            {
                RedirectEntry[] relatedEntries = entryList.Where(x => x.Guid != Guid && string.Equals(x.Mapping, Mapping, StringComparison.OrdinalIgnoreCase)).ToArray();
                Issue.DuplicateMapping.RelatedEntries = string.Join(" | ", relatedEntries.Select(x => $"{x.Type}:{x.LineNum}"));
                Issue.DuplicateMapping.Message = $"Duplicate mapping in {Type.ToString().ToLower()} redirect on line {LineNum}: {Mapping}";
                if (Type == RedirectType.New)
                {
                    Issue.DuplicateMapping.Resolution = "Delete";
                }
                var relatedGuid = Guid.NewGuid();
                Issue.DuplicateMapping.RelatedGuid = relatedGuid;
                relatedEntries.ToList().ForEach(x => x.Issue.DuplicateMapping.RelatedGuid = relatedGuid);
            }

            // This source is the same as another redirect's source, but different destinations (conflicting).
            Issue.ConflictingDestination.IsFound = entryList.Any(x => string.Equals(x.Source, Source, StringComparison.OrdinalIgnoreCase) && !string.Equals(x.Destination, Destination, StringComparison.OrdinalIgnoreCase));
            if (Issue.ConflictingDestination.IsFound)
            {
                RedirectEntry[] relatedEntries = entryList.Where(x => string.Equals(x.Source, Source, StringComparison.OrdinalIgnoreCase) && !string.Equals(x.Destination, Destination, StringComparison.OrdinalIgnoreCase)).ToArray();
                Issue.ConflictingDestination.RelatedEntries = string.Join(" | ", relatedEntries.Select(x => $"{x.Type}:{x.LineNum}"));
                Issue.ConflictingDestination.Message = $"Conflicting mapping in {Type.ToString().ToLower()} redirect on line {LineNum}: {Mapping}";
                if ((DestinationAsset.IsEmpty() || !DestinationAsset.StartsWithXx("tm")) && relatedEntries.Any(x => x.DestinationAsset.IsNotEmpty() || x.DestinationAsset.StartsWithXx("tm")) && !Issue.DuplicateMapping.IsFound)
                {
                    Issue.ConflictingDestination.Resolution = "Delete";
                }
                var relatedGuid = Guid.NewGuid();
                Issue.ConflictingDestination.RelatedGuid = relatedGuid;
                relatedEntries.ToList().ForEach(x => x.Issue.ConflictingDestination.RelatedGuid = relatedGuid);
            }

            // This source is the same as another redirect's destination or vice-versa (double-hop).
            Issue.DoubleHop.IsFound = entryList.Any(x => string.Equals(x.Destination, Source, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Source, Destination, StringComparison.OrdinalIgnoreCase));
            if (Issue.DoubleHop.IsFound)
            {
                RedirectEntry[] relatedEntries = entryList.Where(x => string.Equals(x.Destination, Source, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Source, Destination, StringComparison.OrdinalIgnoreCase)).ToArray();
                Issue.DoubleHop.RelatedEntries = string.Join(" | ", relatedEntries.Select(x => $"{x.Type}:{x.LineNum}"));
                Issue.DoubleHop.Message = $"Double-hop mapping in {Type.ToString().ToLower()} redirect on line {LineNum}: {Mapping}";
                var newerRelatedEntries = relatedEntries.Where(x => string.Equals(x.Source, Destination, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (newerRelatedEntries.Length == 1 && !Issue.DuplicateMapping.IsFound && !Issue.ConflictingDestination.IsFound)
                {
                    Issue.DoubleHop.Resolution = $"\"{Source}\":\"/{DestinationLocale}/{newerRelatedEntries[0].DestinationAsset}";
                }
                var relatedGuid = Guid.NewGuid();
                Issue.DoubleHop.RelatedGuid = relatedGuid;
                relatedEntries.ToList().ForEach(x => x.Issue.DoubleHop.RelatedGuid = relatedGuid);
            }
        }
    }

    internal class RedirectIssue
    {
        // Errors.
        internal Problem InvalidFormat = new(IssueLevel.Error);
        internal Problem ConflictingDestination = new(IssueLevel.Error);
        internal Problem DoubleHop = new(IssueLevel.Error);

        // Warnings.
        internal Problem InconsistentCasing = new(IssueLevel.Warning);
        internal Problem InvalidSrcLocale = new(IssueLevel.Warning);
        internal Problem InvalidDstLocale = new(IssueLevel.Warning);
        internal Problem DuplicateMapping = new(IssueLevel.Warning);
    }

    internal class Problem
    {
        internal IssueLevel Level;
        internal bool IsFound;
        internal string Resolution;
        internal string RelatedEntries;
        internal Guid RelatedGuid;
        internal string Message;

        public Problem(IssueLevel level)
        {
            Level = level;
        }
    }
}