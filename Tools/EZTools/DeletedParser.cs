﻿using CsvHelper;
using CsvHelper.Configuration;
using ForensicTimeliner.CLI;
using ForensicTimeliner.Interfaces;
using ForensicTimeliner.Models;
using ForensicTimeliner.Utils;
using Spectre.Console;
using System.Globalization;

namespace ForensicTimeliner.Tools.EZTools;

public class DeletedParser : IArtifactParser
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
                    var parsedDt = dict.GetDateTime("DeletedOn");
                    if (parsedDt == null) continue;

                    string dtStr = parsedDt.Value.ToString("o").Replace("+00:00", "Z");

                    string fullPath = dict.GetString("FileName");
                    string filename = string.IsNullOrWhiteSpace(fullPath) ? "" : Path.GetFileName(fullPath);
                    string sourcePath = dict.GetString("SourceName");
                    if (string.IsNullOrWhiteSpace(sourcePath)) sourcePath = file;

                    rows.Add(new TimelineRow
                    {
                        DateTime = dtStr,
                        TimestampInfo = "File Deleted On",
                        ArtifactName = artifact.Artifact,
                        Tool = artifact.Tool,
                        Description = artifact.Description,
                        DataDetails = filename,
                        DataPath = fullPath,
                        FileSize = dict.GetLong("FileSize"),
                        EvidencePath = Path.GetRelativePath(baseDir, sourcePath)
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

