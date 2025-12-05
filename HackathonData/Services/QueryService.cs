using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackathonData.Models;
using Microsoft.EntityFrameworkCore;

namespace HackathonData.Services
{
    // Small DTOs to avoid tuple literals inside EF expression trees
    public record CategoryCount(string Category, int Count);
    public record CategoryAverage(string Category, decimal Average);

    public class QueryService
    {
        private readonly HackathonDbContext _db;

        public QueryService(HackathonDbContext db)
        {
            _db = db;
        }

        // Simple queries
        public async Task<List<Project>> Q01_AllByTeam_NeuralNova() =>
            await _db.Projects
                     .Where(p => p.TeamName == "NeuralNova")
                     .ToListAsync();

        public async Task<List<Project>> Q02_AllByEventDate_2025_10_12() =>
            await _db.Projects
                     .Where(p => p.EventDate == new DateTime(2025, 10, 12))
                     .ToListAsync();

        public async Task<List<Project>> Q03_AllByCategory_AIML() =>
            await _db.Projects
                     .Where(p => p.Category == "AI-ML")
                     .ToListAsync();

        public async Task<List<Project>> Q04_ScoreGreaterThan90() =>
            await _db.Projects
                     .Where(p => p.Score > 90m)
                     .ToListAsync();

        public async Task<List<Project>> Q05_Top5HighestScoring() =>
            await _db.Projects
                     .OrderByDescending(p => p.Score)
                     .ThenBy(p => p.EventDate)
                     .Take(5)
                     .ToListAsync();

        // Medium queries 
        public async Task<List<Project>> Q06_ProjectsIn2024() =>
            await (from p in _db.Projects
                   where p.EventDate >= new DateTime(2024, 1, 1) && p.EventDate <= new DateTime(2024, 12, 31)
                   select p).ToListAsync();

        public async Task<List<Project>> Q07_HealthTechScoreGreater88() =>
            await _db.Projects
                     .Where(p => p.Category == "HealthTech" && p.Score > 88m)
                     .ToListAsync();

        public async Task<List<Project>> Q08_SortedByDateThenScore() =>
            await _db.Projects
                     .OrderBy(p => p.EventDate)
                     .ThenByDescending(p => p.Score)
                     .ToListAsync();

        // Q09: count per category (returns DTO list, avoids tuple literal in EF)
        public async Task<List<CategoryCount>> Q09_CountPerCategory()
        {
            var data = await _db.Projects
                .GroupBy(p => p.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return data.Select(x => new CategoryCount(x.Category, x.Count)).ToList();
        }

        public async Task<List<Project>> Q10_Top3ByByteForge() =>
            await _db.Projects
                     .Where(p => p.TeamName == "ByteForge")
                     .OrderByDescending(p => p.Score)
                     .Take(3)
                     .ToListAsync();

        // Complex queries
        // Q11: average score per category (return DTOs, round in-memory)
        public async Task<List<CategoryAverage>> Q11_AvgScorePerCategory()
        {
            var data = await _db.Projects
                .GroupBy(p => p.Category)
                .Select(g => new { Category = g.Key, Average = g.Average(p => p.Score) })
                .ToListAsync();

            // Round and sort in memory
            var result = data
                .Select(x => new CategoryAverage(x.Category, Math.Round(x.Average, 2)))
                .OrderByDescending(x => x.Average)
                .ToList();

            return result;
        }

        // Q12: SmartCity or Energy and at-or-above their category average
        public async Task<List<Project>> Q12_SmartCityOrEnergy_AtOrAboveCategoryAvg()
        {
            // Compute category averages first
            var averages = await _db.Projects
                .GroupBy(p => p.Category)
                .Select(g => new { Category = g.Key, Avg = g.Average(p => p.Score) })
                .ToDictionaryAsync(x => x.Category, x => x.Avg);

            // Now filter projects - do it as an EF query where possible, then materialize and compare with dictionary
            var candidates = await _db.Projects
                .Where(p => p.Category == "SmartCity" || p.Category == "Energy")
                .ToListAsync();

            var result = candidates
                .Where(p => averages.TryGetValue(p.Category, out var avg) && p.Score >= avg)
                .ToList();

            return result;
        }

        public async Task<List<Project>> Q13_ProjectNameContainsAI_AndScoreGreater92() =>
            await _db.Projects
                     .Where(p => EF.Functions.Like(p.ProjectName, "%AI%") && p.Score > 92m)
                     .ToListAsync();

        // Q14: Top 5 by score per category (return dictionary)
        public async Task<Dictionary<string, List<Project>>> Q14_Top5ByScorePerCategory()
        {
            // Materialize projects into memory and perform grouping + take (safe and simple)
            var all = await _db.Projects.ToListAsync();

            var grouped = all
                .GroupBy(p => p.Category)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(p => p.Score).Take(5).ToList()
                );

            return grouped;
        }

        // Q15: Members at least 5 and above global average
        public async Task<List<Project>> Q15_MembersAtLeast5_AboveGlobalAvg()
        {
            var globalAvg = await _db.Projects.AverageAsync(p => p.Score);
            return await _db.Projects
                .Where(p => p.Members >= 5 && p.Score > globalAvg)
                .ToListAsync();
        }
    }
}
