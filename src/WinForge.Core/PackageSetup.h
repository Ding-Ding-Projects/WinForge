#pragma once

#include "PackageManager.h"

#include <cstdint>
#include <optional>
#include <span>
#include <string_view>
#include <vector>

namespace winforge::core::packages
{
    // Setup is intentionally declarative. Every reviewable row maps to one
    // immutable Winget ID; it cannot carry an arbitrary URL, shell fragment,
    // source override, hook, or custom argument.
    enum class PackageSetupKind : std::uint8_t
    {
        ManagerBootstrap,
        CuratedDependency,
    };

    enum class PackageSetupReadiness : std::uint8_t
    {
        ReadyToReview,
        AlreadyAvailable,
        RequiresWinget,
        ManualOnly,
        UnknownEntry,
    };

    struct PackageSetupDescriptor
    {
        std::wstring_view key;
        std::wstring_view name_en;
        std::wstring_view name_zh;
        std::wstring_view winget_id;
        // Manager bootstrap rows use this to avoid offering a plan when the
        // target engine has already passed its own native availability probe.
        // Curated dependencies deliberately leave it empty because this slice
        // does not claim to know their installed state.
        std::wstring_view target_manager_key;
        PackageSetupKind kind;
    };

    [[nodiscard]] std::span<PackageSetupDescriptor const> PackageSetupEntries() noexcept;
    [[nodiscard]] PackageSetupDescriptor const* FindPackageSetupEntry(std::wstring_view key) noexcept;
    [[nodiscard]] PackageSetupReadiness EvaluatePackageSetupEntry(
        PackageSetupDescriptor const& descriptor,
        bool winget_available,
        bool target_manager_available) noexcept;

    // Returns a safe, source-default Winget package row only for a fixed
    // allowlisted descriptor. Callers still must send it through the existing
    // review/consent coordinator before any normal-integrity execution.
    [[nodiscard]] std::optional<PackageItem> BuildPackageSetupPackage(
        std::wstring_view descriptor_key) noexcept;

    // Keeps first-occurrence order, ignores manual-only/unknown keys, and
    // de-duplicates immutable package identities. It never starts a process.
    [[nodiscard]] std::vector<PackageItem> BuildPackageSetupPackages(
        std::span<std::wstring_view const> descriptor_keys);
}
