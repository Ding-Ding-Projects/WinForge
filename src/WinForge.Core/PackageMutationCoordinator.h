#pragma once

#include "PackageRuntime.h"

#include <cstddef>
#include <cstdint>
#include <functional>
#include <mutex>
#include <optional>
#include <stop_token>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::packages
{
    // These limits apply to the in-memory, local-only mutation coordinator. They
    // bound reviewed command previews and ensure that a long-running session
    // cannot turn its activity pane into an unbounded log. Raw third-party
    // stdout, stderr, and diagnostics are deliberately never retained here.
    inline constexpr std::size_t MaximumPackageMutationRecords = 50;
    // A batch is deliberately smaller than the total in-memory queue so every
    // reviewed argv can remain visible without silently truncating a plan.
    // The same bound is used by the Package Manager's selected-row and
    // Update-all review surfaces.
    inline constexpr std::size_t MaximumPackageMutationBatchRecords = 25;
    inline constexpr std::size_t MaximumPackageMutationOutputTail = 4096;

    enum class PackageMutationState : std::uint8_t
    {
        AwaitingConsent,
        Queued,
        Running,
        Succeeded,
        Failed,
        Cancelled,
        TimedOut,
        Rejected,
    };

    struct PackageMutationRequest
    {
        std::wstring id;
        PackageItem package;
        PackageAction action{ PackageAction::Install };
        InstallOptions install_options;
    };

    struct PackageMutationRecord
    {
        PackageMutationRequest request;
        // Empty for a standalone mutation. Batch children retain the opaque
        // batch id solely so the coordinator can atomically confirm, cancel,
        // retry, and snapshot their shared reviewed plan.
        std::wstring batch_id;
        PackageMutationState state{ PackageMutationState::Rejected };
        std::uint64_t sequence{ 0 };
        std::uint32_t retry_count{ 0 };
        bool command_started{ false };
        bool cancellation_requested{ false };
        std::optional<std::uint32_t> exit_code;
        std::wstring command_preview;
        std::wstring diagnostic;
        std::wstring output_tail;
    };

    struct PackageMutationSubmission
    {
        bool accepted{ false };
        bool duplicate{ false };
        PackageMutationRecord record;
    };

    struct PackageMutationBatchRequest
    {
        std::wstring id;
        std::vector<PackageMutationRequest> requests;
    };

    // Snapshots are reconstructed from the authoritative child records rather
    // than retaining a second mutable queue. This keeps one source of truth
    // for process state while giving the UI an immutable, fully reviewed batch
    // surface that cannot hide an argv preview behind a summary count.
    struct PackageMutationBatchRecord
    {
        std::wstring id;
        PackageMutationState state{ PackageMutationState::Rejected };
        std::uint64_t sequence{ 0 };
        std::uint32_t retry_count{ 0 };
        bool cancellation_requested{ false };
        std::wstring diagnostic;
        std::vector<PackageMutationRecord> records;
    };

    struct PackageMutationBatchSubmission
    {
        bool accepted{ false };
        bool duplicate{ false };
        PackageMutationBatchRecord batch;
    };

    // The coordinator never starts a process itself. The caller injects the
    // runtime executor at RunNext, which keeps queue/cancellation/consent logic
    // deterministic in unit tests and keeps process policy in PackageRuntime.
    using PackageMutationExecutor = std::function<PackageRuntimeResult(
        PackageMutationRequest const& request,
        std::stop_token cancellation_token)>;
    using PackageMutationStartedCallback = std::function<void(
        PackageMutationRecord const& record)>;

    [[nodiscard]] bool IsTerminalPackageMutationState(PackageMutationState state) noexcept;
    [[nodiscard]] std::wstring_view PackageMutationStateKey(PackageMutationState state) noexcept;
    [[nodiscard]] std::wstring RedactPackageMutationText(std::wstring_view value);

    class PackageMutationCoordinator
    {
    public:
        [[nodiscard]] PackageMutationSubmission Submit(PackageMutationRequest request);

        // Batch submission is all-or-nothing: every child reference, action,
        // redacted argv preview, duplicate identity, and capacity constraint is
        // validated before any child enters the coordinator. A failed review
        // therefore cannot leave a partial set of package commands awaiting
        // consent.
        [[nodiscard]] PackageMutationBatchSubmission SubmitBatch(PackageMutationBatchRequest request);

        // A separate user action must call Confirm before a request becomes
        // runnable. Retrying returns to AwaitingConsent for the same reason.
        [[nodiscard]] bool Confirm(std::wstring_view id);
        [[nodiscard]] bool ConfirmBatch(std::wstring_view id);
        [[nodiscard]] bool Cancel(std::wstring_view id);
        [[nodiscard]] bool CancelBatch(std::wstring_view id);
        // Cancels every pending request and requests a contained stop for any
        // active process. Hosts call this when the Package Manager is closed or
        // navigated away from so a confirmed mutation never continues invisibly.
        [[nodiscard]] bool CancelAll();
        [[nodiscard]] bool Retry(std::wstring_view id);
        // Retries only unsuccessful batch children and always returns them to
        // review. Successful children are never replayed implicitly.
        [[nodiscard]] bool RetryBatch(std::wstring_view id);

        // Runs at most one queued request. It is safe to call from a worker
        // thread; the executor and optional started callback run without the
        // coordinator mutex held.
        [[nodiscard]] std::optional<PackageMutationRecord> RunNext(
            PackageMutationExecutor const& executor,
            PackageMutationStartedCallback const& started = {});

        [[nodiscard]] std::vector<PackageMutationRecord> Snapshot() const;
        [[nodiscard]] std::vector<PackageMutationBatchRecord> SnapshotBatches() const;
        [[nodiscard]] bool HasRunnableWork() const;

    private:
        [[nodiscard]] PackageMutationRecord* FindLocked(std::wstring_view id) noexcept;
        [[nodiscard]] PackageMutationRecord const* FindLocked(std::wstring_view id) const noexcept;
        [[nodiscard]] bool MakeSpaceForNewRecordLocked();
        [[nodiscard]] bool MakeSpaceForNewRecordsLocked(std::size_t required);
        void TrimTerminalHistoryLocked();

        mutable std::mutex m_mutex;
        std::vector<PackageMutationRecord> m_records;
        std::optional<std::stop_source> m_active_stop_source;
        std::wstring m_active_id;
        std::uint64_t m_next_sequence{ 1 };
    };
}
