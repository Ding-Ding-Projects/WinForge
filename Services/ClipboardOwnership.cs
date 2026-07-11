using System;

namespace WinForge.Services;

/// <summary>
/// Pure ownership check for delayed clipboard cleanup. A delayed secret-clear
/// must never erase text the user copied after WinForge placed its own value.
/// </summary>
internal static class ClipboardOwnership
{
    internal static bool CanClearText(string? ownedText, string? currentText,
        long ownedGeneration, long currentGeneration) =>
        !string.IsNullOrEmpty(ownedText) &&
        ownedGeneration == currentGeneration &&
        string.Equals(ownedText, currentText, StringComparison.Ordinal);
}
