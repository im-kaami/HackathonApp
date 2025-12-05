using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HackathonData;
using HackathonData.Models;
using HackathonData.Services;
using HackathonApp.ConsoleHelpers;

class Program
{
    static async Task Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((ctx, services) =>
            {
                var conn = ctx.Configuration.GetConnectionString("DefaultConnection");
                services.AddDbContext<HackathonDbContext>(opt =>
                    opt.UseSqlServer(conn));

                services.AddScoped<ImportService>();
                services.AddScoped<QueryService>();
            })
            .Build();

        // Create scope
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var importService = services.GetRequiredService<ImportService>();
        var queryService = services.GetRequiredService<QueryService>();
        var config = services.GetRequiredService<IConfiguration>();

        // Subscribe to the import event
        importService.DataImported += (inserted, updated, skipped, duration) =>
        {
            Console.WriteLine("----- Import completed -----");
            Console.WriteLine($"Inserted: {inserted}, Updated: {updated}, Skipped: {skipped}");
            Console.WriteLine($"Duration: {duration.TotalSeconds:F2}s");
            Console.WriteLine("----------------------------");
        };

        var inputPath = Path.Combine(AppContext.BaseDirectory, config["Paths:InputXml"] ?? "Data/HackathonResults.xml");
        var outputFolder = Path.Combine(AppContext.BaseDirectory, config["Paths:OutputFolder"] ?? "Output");

        Directory.CreateDirectory(outputFolder);

        bool exit = false;
        while (!exit)
        {
            Console.WriteLine();
            Console.WriteLine("Hackathon Results Management System");
            Console.WriteLine("1) Import XML -> DB");
            Console.WriteLine("2) Run Simple LINQ queries");
            Console.WriteLine("3) Run Medium LINQ queries");
            Console.WriteLine("4) Run Complex LINQ queries");
            Console.WriteLine("5) Export latest queries to JSON");
            Console.WriteLine("0) Exit");
            Console.Write("Choice: ");
            var choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice)
                {
                    case "1":
                        Console.WriteLine($"Importing from {inputPath} ...");
                        await importService.ImportFromXmlAsync(inputPath);
                        break;

                    case "2":
                        await RunSimple(queryService, outputFolder);
                        break;

                    case "3":
                        await RunMedium(queryService, outputFolder);
                        break;

                    case "4":
                        await RunComplex(queryService, outputFolder);
                        break;

                    case "5":
                        Console.WriteLine("Exporting latest queries to JSON...");
                        await RunSimple(queryService, outputFolder);
                        await RunMedium(queryService, outputFolder);
                        await RunComplex(queryService, outputFolder);
                        Console.WriteLine($"JSON files are in: {outputFolder}");
                        break;

                    case "0":
                        exit = true;
                        break;

                    default:
                        Console.WriteLine("Unknown option.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        await host.StopAsync();
    }

    static JsonSerializerOptions jsonOptions = new JsonSerializerOptions { WriteIndented = true };

    static async Task SaveJsonAsync<T>(T data, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, data, jsonOptions);
    }

    // --- Helpers to print dictionary-like or DTO results robustly ---
    static bool TryExtractPairs(object? maybeEnumerable, out List<(string Key, double Value)> pairs)
    {
        pairs = new List<(string, double)>();
        if (maybeEnumerable == null) return false;

        // IDictionary<string,int/double>
        if (maybeEnumerable is IDictionary<string, int> diInt)
        {
            pairs.AddRange(diInt.Select(kv => (kv.Key, Convert.ToDouble(kv.Value))));
            return true;
        }
        if (maybeEnumerable is IDictionary<string, double> diDouble)
        {
            pairs.AddRange(diDouble.Select(kv => (kv.Key, kv.Value)));
            return true;
        }

        // IEnumerable
        if (maybeEnumerable is not IEnumerable enumerable) return false;

        var temp = enumerable.Cast<object?>().ToList();
        if (temp.Count == 0) return false;

        var first = temp[0];
        if (first == null) return false;

        var firstType = first.GetType();

        // Try common property names: Key/Value or Category/Count/Average/Value
        var keyProp = firstType.GetProperty("Key") ?? firstType.GetProperty("Category") ?? firstType.GetProperty("Name");
        var valueProp = firstType.GetProperty("Value") ?? firstType.GetProperty("Count") ?? firstType.GetProperty("Average");

        if (keyProp != null && valueProp != null)
        {
            foreach (var it in temp)
            {
                if (it == null) return false;
                var kObj = keyProp.GetValue(it);
                var vObj = valueProp.GetValue(it);
                if (kObj == null || vObj == null) return false;
                if (!double.TryParse(Convert.ToString(vObj), out double d)) return false;
                pairs.Add((Convert.ToString(kObj)!, d));
            }
            return true;
        }

        // Last resort: try to parse KeyValuePair<string, ?> via reflection
        if (firstType.IsGenericType && firstType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            var kpProp = firstType.GetProperty("Key");
            var kvProp = firstType.GetProperty("Value");
            if (kpProp != null && kvProp != null)
            {
                foreach (var it in temp)
                {
                    if (it == null) return false;
                    var kObj = kpProp.GetValue(it);
                    var vObj = kvProp.GetValue(it);
                    if (kObj == null || vObj == null) return false;
                    if (!double.TryParse(Convert.ToString(vObj), out double d)) return false;
                    pairs.Add((Convert.ToString(kObj)!, d));
                }
                return true;
            }
        }

        return false;
    }

    static void PrintKeyValuePairs(IEnumerable<(string Key, double Value)> pairs, string keyHeader = "Category", string valueHeader = "Value")
    {
        var rows = pairs.Select(p => new { Key = p.Key, Value = Math.Round(p.Value, 2) }).ToList();
        ConsoleTablePrinter.PrintTable(rows,
            new[] { keyHeader, valueHeader },
            r => new object?[] { r.Key, r.Value });
    }

    // Simple queries runner
    static async Task RunSimple(QueryService q, string output)
    {
        var a1 = await q.Q01_AllByTeam_NeuralNova();
        await SaveJsonAsync(a1, Path.Combine(output, "q01_neuralnova.json"));
        Console.WriteLine($"q01: {a1.Count} results -> q01_neuralnova.json");
        if (a1.Any()) ConsoleTablePrinter.PrintTable(a1,
            new[] { "Id", "Team", "Project", "Category", "EventDate", "Score", "Captain" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.Category, p.EventDate.ToString("yyyy-MM-dd"), p.Score, p.Captain });

        var a2 = await q.Q02_AllByEventDate_2025_10_12();
        await SaveJsonAsync(a2, Path.Combine(output, "q02_2025-10-12.json"));
        Console.WriteLine($"q02: {a2.Count} results -> q02_2025-10-12.json");
        if (a2.Any()) ConsoleTablePrinter.PrintTable(a2,
            new[] { "Id", "Team", "Project", "Category", "EventDate", "Score", "Captain" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.Category, p.EventDate.ToString("yyyy-MM-dd"), p.Score, p.Captain });

        var a3 = await q.Q03_AllByCategory_AIML();
        await SaveJsonAsync(a3, Path.Combine(output, "q03_aiml.json"));
        Console.WriteLine($"q03: {a3.Count} results -> q03_aiml.json");
        if (a3.Any()) ConsoleTablePrinter.PrintTable(a3,
            new[] { "Id", "Team", "Project", "EventDate", "Score" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.EventDate.ToString("yyyy-MM-dd"), p.Score });

        var a4 = await q.Q04_ScoreGreaterThan90();
        await SaveJsonAsync(a4, Path.Combine(output, "q04_score_gt_90.json"));
        Console.WriteLine($"q04: {a4.Count} results -> q04_score_gt_90.json");
        if (a4.Any()) ConsoleTablePrinter.PrintTable(a4,
            new[] { "Id", "Team", "Project", "Score" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.Score });

        var a5 = await q.Q05_Top5HighestScoring();
        await SaveJsonAsync(a5, Path.Combine(output, "q05_top5.json"));
        Console.WriteLine($"q05: {a5.Count} results -> q05_top5.json");
        if (a5.Any()) ConsoleTablePrinter.PrintTable(a5,
            new[] { "Id", "Team", "Project", "Score" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.Score });
    }

    static async Task RunMedium(QueryService q, string output)
    {
        var b6 = await q.Q06_ProjectsIn2024();
        await SaveJsonAsync(b6, Path.Combine(output, "q06_2024.json"));
        Console.WriteLine($"q06: {b6.Count} results -> q06_2024.json");
        if (b6.Any()) ConsoleTablePrinter.PrintTable(b6,
            new[] { "Id", "Team", "Project", "EventDate", "Score" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.EventDate.ToString("yyyy-MM-dd"), p.Score });

        var b7 = await q.Q07_HealthTechScoreGreater88();
        await SaveJsonAsync(b7, Path.Combine(output, "q07_healthtech_gt88.json"));
        Console.WriteLine($"q07: {b7.Count} results -> q07_healthtech_gt88.json");
        if (b7.Any()) ConsoleTablePrinter.PrintTable(b7,
            new[] { "Id", "Team", "Project", "Score" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.Score });

        var b8 = await q.Q08_SortedByDateThenScore();
        await SaveJsonAsync(b8, Path.Combine(output, "q08_sorted.json"));
        Console.WriteLine($"q08: {b8.Count} results -> q08_sorted.json");
        if (b8.Any()) ConsoleTablePrinter.PrintTable(b8,
            new[] { "Id", "Team", "Project", "EventDate", "Score" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.EventDate.ToString("yyyy-MM-dd"), p.Score });

        var b9 = await q.Q09_CountPerCategory();
        await SaveJsonAsync(b9, Path.Combine(output, "q09_counts.json"));
        // print a safe summary count
        var b9Count = TryCountEnumerable(b9);
        Console.WriteLine($"q09: {b9Count} categories -> q09_counts.json");

        // Print counts robustly
        if (TryExtractPairs(b9, out var countPairs))
        {
            PrintKeyValuePairs(countPairs, "Category", "Count");
        }
        else
        {
            // fallback: try to print ToString() of each element
            if (b9 is IEnumerable enumB9)
            {
                var list = enumB9.Cast<object?>().Select(o => new { Item = o?.ToString() ?? "" }).ToList();
                if (list.Any())
                {
                    ConsoleTablePrinter.PrintTable(list, new[] { "Item" }, it => new object?[] { it.Item });
                }
            }
        }

        var b10 = await q.Q10_Top3ByByteForge();
        await SaveJsonAsync(b10, Path.Combine(output, "q10_byteforge_top3.json"));
        Console.WriteLine($"q10: {b10.Count} results -> q10_byteforge_top3.json");
        if (b10.Any()) ConsoleTablePrinter.PrintTable(b10,
            new[] { "Id", "Team", "Project", "Score" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.Score });
    }

    static async Task RunComplex(QueryService q, string output)
    {
        var c11 = await q.Q11_AvgScorePerCategory();
        await SaveJsonAsync(c11, Path.Combine(output, "q11_avg_per_category.json"));

        var c11Count = TryCountEnumerable(c11);
        Console.WriteLine($"q11: {c11Count} categories -> q11_avg_per_category.json");

        if (TryExtractPairs(c11, out var avgPairs))
        {
            PrintKeyValuePairs(avgPairs, "Category", "Average");
        }
        else
        {
            if (c11 is IEnumerable enumC11)
            {
                var list = enumC11.Cast<object?>().Select(o => new { Item = o?.ToString() ?? "" }).ToList();
                if (list.Any())
                {
                    ConsoleTablePrinter.PrintTable(list, new[] { "Item" }, it => new object?[] { it.Item });
                }
            }
        }

        var c12 = await q.Q12_SmartCityOrEnergy_AtOrAboveCategoryAvg();
        await SaveJsonAsync(c12, Path.Combine(output, "q12_smartcity_energy_above_avg.json"));
        Console.WriteLine($"q12: {c12.Count} results -> q12_smartcity_energy_above_avg.json");
        if (c12.Any()) ConsoleTablePrinter.PrintTable(c12,
            new[] { "Id", "Team", "Project", "Category", "Score" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.Category, p.Score });

        var c13 = await q.Q13_ProjectNameContainsAI_AndScoreGreater92();
        await SaveJsonAsync(c13, Path.Combine(output, "q13_name_contains_ai_score_gt92.json"));
        Console.WriteLine($"q13: {c13.Count} results -> q13_name_contains_ai_score_gt92.json");
        if (c13.Any()) ConsoleTablePrinter.PrintTable(c13,
            new[] { "Id", "Team", "Project", "Score" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.Score });

        var c14 = await q.Q14_Top5ByScorePerCategory();

        var c14Array = c14.Select(kv => new
        {
            Category = kv.Key,
            Projects = kv.Value,
            ProjectsCount = kv.Value.Count
        })
        .ToList();

        await SaveJsonAsync(c14Array, Path.Combine(output, "q14_top5_per_category.json"));
        Console.WriteLine($"q14: {c14.Count} categories -> q14_top5_per_category.json");

        // Print top-5 per category (iterate small sets)
        foreach (var kv in c14)
        {
            Console.WriteLine($"Category: {kv.Key} (top {kv.Value.Count})");
            if (kv.Value.Any())
            {
                ConsoleTablePrinter.PrintTable(kv.Value,
                    new[] { "Id", "Team", "Project", "Score" },
                    p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.Score },
                    maxRowsToShow: 5,
                    borderColor: ConsoleColor.Cyan,
                    headerColor: ConsoleColor.Yellow,
                    rowColor: null);
            }
        }

        var c15 = await q.Q15_MembersAtLeast5_AboveGlobalAvg();
        await SaveJsonAsync(c15, Path.Combine(output, "q15_members5_above_global_avg.json"));
        Console.WriteLine($"q15: {c15.Count} results -> q15_members5_above_global_avg.json");
        if (c15.Any()) ConsoleTablePrinter.PrintTable(c15,
            new[] { "Id", "Team", "Project", "Members", "Score" },
            p => new object?[] { p.Id, p.TeamName, p.ProjectName, p.Members, p.Score });
    }

    // Helper to count items from unknown enumerable shape
    static int TryCountEnumerable(object? maybeEnum)
    {
        if (maybeEnum == null) return 0;
        if (maybeEnum is ICollection coll) return coll.Count;
        if (maybeEnum is IEnumerable enumerable)
        {
            int cnt = 0;
            foreach (var _ in enumerable) cnt++;
            return cnt;
        }
        return 0;
    }
}
