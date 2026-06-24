using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>一個環境變數 · One environment variable.</summary>
public sealed class EnvVar
{
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
    public bool Machine { get; init; }
}

/// <summary>
/// 應用程式內環境變數編輯（PowerToys 式）· In-app Environment Variables editor. User scope is writable
/// without admin; Machine (system) scope needs admin (SetEnvironmentVariable throws otherwise). No redirect.
/// </summary>
public static class EnvVarService
{
    public static List<EnvVar> List(bool machine)
    {
        var target = machine ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
        var list = new List<EnvVar>();
        try
        {
            foreach (DictionaryEntry e in Environment.GetEnvironmentVariables(target))
                list.Add(new EnvVar { Name = e.Key?.ToString() ?? "", Value = e.Value?.ToString() ?? "", Machine = machine });
        }
        catch { }
        return list.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Create or update a variable. Throws (e.g. SecurityException) for Machine scope without admin.</summary>
    public static void Set(string name, string value, bool machine)
        => Environment.SetEnvironmentVariable(name, value,
            machine ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User);

    public static void Delete(string name, bool machine)
        => Environment.SetEnvironmentVariable(name, null,
            machine ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User);
}
