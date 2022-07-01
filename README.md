# Redirect checker

## Summary

An open source app that checks JSON format `"<source>": "<destination>"` redirects for:

  1. Invalid format: `<source>` or `<destination>` doesn't conform to `/<locale>/<assetId>`, although the destination `<assetId>` can be blank for a landing page.

  1. Conflicting destinations: multiple redirects with the same source, but different destinations.

  1. Double-hops: the destination of one redirect is the source of another redirect.

  1. Duplicate mappings: multiple redirects have the same `"<source>": "<destination>"` mapping.

  1. Invalid source locale: not found in a list of known locales.

  1. Invalid destination locale: not found in a list of known locales.

Items 1-3 are flagged as errors, 4-6 as warnings.

## Quick start

1. In the file `Program.cs`, set the paths for:

   - `oldRedirectsFilePath`: You provide this path to a `.json` or `.txt` file that should contain all existing redirects, each on a separate line. Commas and surrounding braces are ignored, so can be left in or omitted.
    
   - `newRedirectsFilePath`: You provide this path to a `.json` or `.txt` file that should contain any new redirects, each on a separate line. Commas and surrounding braces are ignored, so can be left in or omitted. If you're just checking old redirects, then this can be an empty file.

   - `analysisCsvFilePath`: On completion, the program creates a CSV report at this path. The app validates the combined set of redirects from both of the above files, and identifies the location of problematic redirects by `Old:<line number>` or `New:<line number>`.

1. Start the program. 

1. Brew a pot of tea.

1. On completion, the program creates a CSV report with analysis results.
