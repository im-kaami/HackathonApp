using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using HackathonData.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HackathonData.Services
{
    // Event delegate: inserted, updated, skipped, duration
    public delegate void DataImportedHandler(int inserted, int updated, int skipped, TimeSpan duration);

    public class ImportService
    {
        private readonly HackathonDbContext _db;
        private readonly ILogger<ImportService>? _logger;

        public event DataImportedHandler? DataImported;

        public ImportService(HackathonDbContext db, ILogger<ImportService>? logger = null)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Import projects from an XML file into the database.
        /// Accepts explicit Id values from the XML by enabling IDENTITY_INSERT during the save.
        /// Raises DataImported event when finished.
        /// </summary>
        /// <param name="xmlPath">Path to the XML file</param>
        public async Task ImportFromXmlAsync(string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(xmlPath)) throw new ArgumentException("xmlPath is null or empty.", nameof(xmlPath));
            if (!File.Exists(xmlPath)) throw new FileNotFoundException("XML input file not found", xmlPath);

            var stopwatch = Stopwatch.StartNew();

            XDocument doc;
            try
            {
                doc = XDocument.Load(xmlPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load XML file: {Path}", xmlPath);
                throw;
            }

            var projectNodes = doc.Root?.Elements("Project") ?? Enumerable.Empty<XElement>();

            int inserted = 0, updated = 0, skipped = 0;
            var skippedDetails = new List<string>();
            var idsInFile = new HashSet<int>();

            // Load existing projects into memory once
            // AsNoTracking to fetch a snapshot, we'll attach/update tracked entities later.
            var existingProjects = await _db.Projects
                .AsNoTracking()
                .ToListAsync();

            var existingById = existingProjects.ToDictionary(p => p.Id, p => p);
            var existingIdSet = new HashSet<int>(existingById.Keys);


            foreach (var node in projectNodes)
            {
                // Parse and validate per node
                try
                {
                    // Get raw string values (allow missing elements to be handled)
                    string idStr = (string?)node.Element("Id") ?? "";
                    string team = ((string?)node.Element("TeamName"))?.Trim() ?? "";
                    string projectName = ((string?)node.Element("ProjectName"))?.Trim() ?? "";
                    string category = ((string?)node.Element("Category"))?.Trim() ?? "";
                    string eventDateStr = (string?)node.Element("EventDate") ?? "";
                    string scoreStr = (string?)node.Element("Score") ?? "";
                    string membersStr = (string?)node.Element("Members") ?? "";
                    string captain = ((string?)node.Element("Captain"))?.Trim() ?? "";

                    var rowIdentifier = $"XML Row (Team='{team}', Project='{projectName}', Id='{idStr}')";

                    var errors = new List<string>();

                    if (!int.TryParse(idStr, out int id) || id <= 0)
                        errors.Add("Invalid or missing Id (must be positive integer).");

                    if (string.IsNullOrWhiteSpace(team) || team.Length > 100)
                        errors.Add("TeamName is missing or exceeds 100 characters.");

                    if (string.IsNullOrWhiteSpace(projectName) || projectName.Length > 120)
                        errors.Add("ProjectName is missing or exceeds 120 characters.");

                    if (string.IsNullOrWhiteSpace(category) || category.Length > 50)
                        errors.Add("Category is missing or exceeds 50 characters.");

                    if (!DateTime.TryParse(eventDateStr, out DateTime eventDate))
                        errors.Add("EventDate is missing or invalid (expected date).");
                    else if (eventDate > DateTime.Today)
                        errors.Add("EventDate cannot be in the future.");

                    if (!decimal.TryParse(scoreStr, out decimal score))
                        errors.Add("Score is missing or invalid (expected decimal).");
                    else if (score < 0m || score > 100m)
                        errors.Add("Score is out of expected range (0 - 100).");

                    if (!int.TryParse(membersStr, out int members))
                        errors.Add("Members is missing or invalid (expected integer).");
                    else if (members < 1 || members > 15)
                        errors.Add("Members is out of expected range (1 - 15).");

                    if (string.IsNullOrWhiteSpace(captain) || captain.Length > 100)
                        errors.Add("Captain is missing or exceeds 100 characters.");

                    if (errors.Count > 0)
                    {
                        skipped++;
                        skippedDetails.Add($"{rowIdentifier}: {string.Join("; ", errors)}");
                        continue;
                    }

                    if (idsInFile.Contains(id))
                    {
                        skipped++;
                        skippedDetails.Add($"{rowIdentifier}: Duplicate Id in XML file.");
                        continue;
                    }

                    idsInFile.Add(id);

                    // Upsert logic (using in-memory existingById dictionary to avoid per-row DB queries)
                    if (existingIdSet.Contains(id))
                    {
                        // Update existing (we fetched a snapshot into existingById earlier)
                        if (existingById.TryGetValue(id, out var snapshot))
                        {
                            // Create a tracked instance and set updated values, then attach as Modified
                            var tracked = new Project
                            {
                                Id = snapshot.Id,
                                TeamName = snapshot.TeamName,
                                ProjectName = snapshot.ProjectName,
                                Category = snapshot.Category,
                                EventDate = snapshot.EventDate,
                                Score = snapshot.Score,
                                Members = snapshot.Members,
                                Captain = snapshot.Captain
                            };

                            // Set new values from XML
                            tracked.TeamName = team;
                            tracked.ProjectName = projectName;
                            tracked.Category = category;
                            tracked.EventDate = eventDate.Date;
                            tracked.Score = Math.Round(score, 2);
                            tracked.Members = members;
                            tracked.Captain = captain;

                            // Attach and mark as modified so SaveChanges will issue an UPDATE
                            _db.Attach(tracked);
                            _db.Entry(tracked).State = EntityState.Modified;

                            updated++;
                            // keep existingById in sync for consistency if needed later
                            existingById[id] = tracked;
                        }
                        else
                        {
                            // Defensive fallback: if not found in snapshot (rare), insert as new
                            var entity = new Project
                            {
                                Id = id,
                                TeamName = team,
                                ProjectName = projectName,
                                Category = category,
                                EventDate = eventDate.Date,
                                Score = Math.Round(score, 2),
                                Members = members,
                                Captain = captain
                            };
                            _db.Projects.Add(entity);
                            inserted++;
                            existingIdSet.Add(id);
                            existingById[id] = entity;
                        }
                    }
                    else
                    {
                        // Insert new entity (preserve the Id coming from XML)
                        var entity = new Project
                        {
                            Id = id,
                            TeamName = team,
                            ProjectName = projectName,
                            Category = category,
                            EventDate = eventDate.Date,
                            Score = Math.Round(score, 2),
                            Members = members,
                            Captain = captain
                        };
                        _db.Projects.Add(entity);
                        inserted++;
                        existingIdSet.Add(id);
                        existingById[id] = entity;
                    }

                }
                catch (Exception ex)
                {
                    skipped++;
                    skippedDetails.Add($"Exception parsing node: {ex.Message}");
                }
            }

            // If there is nothing to save, just raise event and return
            if (inserted == 0 && updated == 0)
            {
                stopwatch.Stop();
                // changed to Debug so console won't show an extra info line
                _logger?.LogDebug("Import completed: inserted={Inserted}, updated={Updated}, skipped={Skipped} (no changes to save).",
                    inserted, updated, skipped);
                if (skippedDetails.Any())
                {
                    LogSkippedDetails(skippedDetails);
                }
                DataImported?.Invoke(inserted, updated, skipped, stopwatch.Elapsed);
                return;
            }

            // Save changes inside a transaction with IDENTITY_INSERT ON for dbo.Projects
            // Note: Ensure the table name matches your schema (dbo.Projects is the default)
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Enable explicit identity inserts
                await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Projects ON");

                await _db.SaveChangesAsync();

                // Disable explicit identity inserts
                await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Projects OFF");

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger?.LogError(ex, "Error while saving imported data to the database.");
                throw;
            }

            stopwatch.Stop();

            // Log summary and some skipped details if any
            // changed to Debug so console won't show an extra info line
            _logger?.LogDebug("Import completed: inserted={Inserted}, updated={Updated}, skipped={Skipped} in {Duration}s.",
                inserted, updated, skipped, stopwatch.Elapsed.TotalSeconds);

            if (skippedDetails.Any())
            {
                LogSkippedDetails(skippedDetails);
            }

            // Raise event for whoever subscribed
            DataImported?.Invoke(inserted, updated, skipped, stopwatch.Elapsed);
        }

        private void LogSkippedDetails(List<string> skippedDetails)
        {
            // Log up to 20 examples so the log doesn't become enormous
            const int maxShow = 20;
            int show = Math.Min(maxShow, skippedDetails.Count);

            _logger?.LogWarning("Skipped {Count} XML rows. Showing up to {Max} examples:", skippedDetails.Count, maxShow);
            for (int i = 0; i < show; i++)
            {
                _logger?.LogWarning("  {Example}", skippedDetails[i]);
            }

            if (skippedDetails.Count > maxShow)
            {
                _logger?.LogWarning("  ... and {Remaining} more skipped rows not shown.", skippedDetails.Count - maxShow);
            }
        }
    }
}
