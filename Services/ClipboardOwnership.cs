using System;

namespace WinForge.Services;

/// <summary>
/// Pure ownership check for delayed clipboard cleanup. A delayed secret-clear
/// must never erase text the user copied after WinForge placed its own value.
/// </summary>
internal static class ClipboardOwnership
{
    internal static bool CanClearText(string? ownedText, string? currentText) =>
        !string.IsNullOrEmpty(ownedText) &&
        string.Equals(ownedText, currentText, StringComparison.Ordinal);
}
