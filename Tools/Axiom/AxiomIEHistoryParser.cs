﻿using CsvHelper;
using CsvHelper.Configuration;
using ForensicTimeliner.CLI;
using ForensicTimeliner.Interfaces;
using ForensicTimeliner.Models;
using ForensicTimeliner.Utils;
using Spectre.Console;
using System.Globalization;

namespace ForensicTimeliner.Tools.Axiom;

public class AxiomIEHistoryParser : IArtifactParser
{
    public List<TimelineRow> Parse(string inputDir, string baseDir, ArtifactDefinition artifact, ParsedArgs args)
    {
        var rows = new List<TimelineRow>();

        Logger.PrintAndLog($"[>] - [{artifact.Artifact}] Scanning for relevant CSVs under: [{inputDir}]", "SCAN");

        var files = Discovery.FindArtifactFiles(inputDir, baseDir, artifact.Artifact);
        if (!files.Any())
        {
            Logger.PrintAndLog($"[!] - [{artifact.Artifact}] No matching files found in: {inputDir}", "WARN");
            return rows;
        }

        foreach (var file in files)
        {
            int timelineCount = 0;
            Logger.PrintAndLog($"[+] - [{artifact.Artifact}] Processing: {Path.GetRelativePath(baseDir, file)}", "PROCESS");

            try
            {
                using var reader = new StreamReader(file);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null
                });

                var records = csv.GetRecords<dynamic>();
                foreach (var record in records)
                {
                    var dict = (IDictionary<string, object>)record;
                    var parsedDt = dict.GetDateTime("Accessed Date/Time - UTC+00:00 (M/d/yyyy)");
                    if (parsedDt == null) continue;

                    string url = dict.GetString("URL")?.Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(url)) continue;

                    string activity = "";

                    if (url.StartsWith("file:///"))
                    {
                        activity = " + File Open Access";
                    }
                    else if (new[] { "search", "query", "q=", "p=", "find", "lookup", "google.com/search", "bing.com/search", "duckduckgo.com/?q=", "yahoo.com/search" }
                             .Any(term => url.Contains(term)))
                    {
                        activity = " + Search";
                    }
                    else if (new[] { "download", ".exe", ".zip", ".rar", ".7z", ".msi", ".iso", ".pdf", ".dll", "/downloads/" }
                             .Any(term => url.Contains(term)))
                    {
                        activity = " + Download";
                    }

                    string title = dict.GetString("Page Title");
                    string evidencePath = dict.GetString("Browser Source");
                    if (string.IsNullOrWhiteSpace(evidencePath))
                    {
                        evidencePath = Path.GetRelativePath(baseDir, file);
                    }

                    string dtStr = parsedDt.Value.ToString("o").Replace("+00:00", "Z");


                    rows.Add(new TimelineRow
                    {
                        DateTime = dtStr,
                        TimestampInfo = "Last Visited",
                        ArtifactName = "Web History",
                        Tool = artifact.Tool,
                        Description = "IE History" + activity,
                        DataPath = url,
                        DataDetails = title,
                        EvidencePath = evidencePath
                    });

                    timelineCount++;
                }

                Logger.PrintAndLog($"[✓] - [{artifact.Artifact}] Parsed {timelineCount} timeline rows from: {Path.GetFileName(file)}", "SUCCESS");
                LoggerSummary.TrackSummary(artifact.Tool, artifact.Artifact, timelineCount);
            }
            catch (Exception ex)
            {
                Logger.PrintAndLog($"[{artifact.Artifact}] Failed to parse {file}: {ex.Message}", "ERROR");
            }
        }

        return rows;
    }
}

