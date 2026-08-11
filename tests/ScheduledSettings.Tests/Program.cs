using System;
using System.Collections.Generic;
using System.IO;
using WinForge.Services;

string root = Path.Combine(Path.GetTempPath(), "WinForge-ScheduledSettings-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Environment.SetEnvironmentVariable("WINFORGE_AUTOMATION_DATA_ROOT", root);

var failures = new List<string>();
var passed = 0;
Run("rejects malformed rule shapes", RejectsMalformedRuleShapes);
Run("resolves priority and cross-midnight windows", ResolvesPriorityAndMidnight);
Run("persists local rule data without applying it as a base setting", PersistsLocalRule);

try { Directory.Delete(root, recursive: true); } catch { }

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} scheduled-settings contract tests");
    return 0;
}

foreach (string failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} scheduled-settings contract tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {name}: {exception.Message}");
    }
}

void RejectsMalformedRuleShapes()
{
    ScheduledSettingRule partialTime = NewRule();
    partialTime.EndTime = null;
    Assert(ScheduledSettingsService.Validate(partialTime).Count > 0, "partial time input was accepted");

    ScheduledSettingRule unsafeApi = NewRule();
    unsafeApi.Source = ScheduledSettingSource.HttpsApi;
    unsafeApi.Endpoint = "http://example.invalid/settings";
    Assert(ScheduledSettingsService.Validate(unsafeApi).Count > 0, "non-loopback HTTP endpoint was accepted");

    ScheduledSettingRule unknownField = NewRule();
    unknownField.Values["notAllowlisted"] = "value";
    Assert(ScheduledSettingsService.Validate(unknownField).Count > 0, "unknown setting field was accepted");
}

void ResolvesPriorityAndMidnight()
{
    DateTimeOffset instant = new(2026, 8, 11, 1, 30, 0, TimeSpan.Zero);
    var earlier = NewRule("earlier");
    earlier.Priority = 1;
    earlier.StartTime = new TimeOnly(22, 0);
    earlier.EndTime = new TimeOnly(2, 0);
    earlier.StartDate = new DateOnly(2026, 8, 10);
    earlier.EndDate = new DateOnly(2026, 8, 10);
    earlier.EveryDay = true;
    var later = NewRule("later");
    later.Priority = 2;
    later.StartTime = new TimeOnly(0, 0);
    later.EndTime = new TimeOnly(3, 0);
    later.EveryDay = true;
    ScheduledSettingsService.Upsert(earlier);
    ScheduledSettingsService.Upsert(later);
    ScheduledSettingsResolution result = ScheduledSettingsService.Resolve(instant);
    Assert(result.Label == "later", "higher priority did not win");
    Assert(result.Values.TryGetValue("theme", out string? value) && value == "later", "resolved value was incorrect");
}

void PersistsLocalRule()
{
    var rule = NewRule("persisted");
    ScheduledSettingsService.Upsert(rule);
    Assert(ScheduledSettingsService.Rules.Any(item => item.Label == "persisted"), "local rule did not persist");
    Assert(SettingsStore.Get("theme", "base") == "base", "scheduled value overwrote the base setting");
}

ScheduledSettingRule NewRule(string label = "rule") => new()
{
    Id = Guid.NewGuid().ToString("D"),
    Label = label,
    Enabled = true,
    EveryDay = true,
    TimeZoneId = TimeZoneInfo.Utc.Id,
    Source = ScheduledSettingSource.Local,
    Values = new Dictionary<string, string> { ["theme"] = label },
    StartTime = new TimeOnly(0, 0),
    EndTime = new TimeOnly(23, 59),
};

void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
