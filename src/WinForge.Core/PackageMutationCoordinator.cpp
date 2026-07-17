#include "PackageMutationCoordinator.h"

#include <algorithm>
#include <cwctype>
#include <exception>
#include <unordered_set>
#include <utility>

namespace winforge::core::packages
{
    namespace
    {
        [[nodiscard]] bool IsMutationAction(PackageAction action) noexcept
        {
            return action == PackageAction::Install ||
                action == PackageAction::Update ||
                action == PackageAction::Uninstall;
        }

        [[nodiscard]] std::wstring LowerAscii(std::wstring_view value)
        {
            std::wstring lowered(value);
            for (auto& character : lowered)
            {
                if (character >= L'A' && character <= L'Z')
                {
                    character = static_cast<wchar_t>(character - L'A' + L'a');
                }
            }
            return lowered;
        }

        void RedactValueAfterKeyword(std::wstring& value, std::wstring_view keyword)
        {
            std::size_t searchFrom = 0;
            for (;;)
            {
                auto const lowered = LowerAscii(value);
                auto const found = lowered.find(keyword, searchFrom);
                if (found == std::wstring::npos)
                {
                    return;
                }

                auto start = found + keyword.size();
                while (start < value.size() && std::iswspace(value[start]))
                {
                    ++start;
                }
                if (lowered.compare(start, 7, L"bearer ") == 0)
                {
                    start += 7;
                }
                auto const quote = start < value.size() &&
                    (value[start] == L'\'' || value[start] == L'"')
                    ? value[start]
                    : L'\0';
                if (quote != L'\0')
                {
                    ++start;
                }
                auto end = start;
                while (end < value.size() &&
                    (quote != L'\0'
                        ? value[end] != quote
                        : !std::iswspace(value[end]) && value[end] != L';' &&
                            value[end] != L'&' && value[end] != L',' &&
                            value[end] != L'\'' && value[end] != L'"' &&
                            value[end] != L'}' && value[end] != L']'))
                {
                    ++end;
                }
                if (end > start)
                {
                    value.replace(start, end - start, L"[redacted]");
                    searchFrom = start + std::wstring_view(L"[redacted]").size();
                }
                else
                {
                    searchFrom = found + keyword.size();
                }
            }
        }

        void RedactUriCredentials(std::wstring& value)
        {
            std::size_t searchFrom = 0;
            for (;;)
            {
                auto const scheme = value.find(L"://", searchFrom);
                if (scheme == std::wstring::npos)
                {
                    return;
                }
                auto const credentialsBegin = scheme + 3;
                auto const at = value.find(L'@', credentialsBegin);
                auto const boundary = value.find_first_of(L"/\\ \t\r\n", credentialsBegin);
                if (at != std::wstring::npos && at > credentialsBegin &&
                    (boundary == std::wstring::npos || at < boundary))
                {
                    value.replace(credentialsBegin, at - credentialsBegin, L"[redacted]");
                    searchFrom = credentialsBegin + std::wstring_view(L"[redacted]@").size();
                }
                else
                {
                    searchFrom = scheme + 3;
                }
            }
        }

        [[nodiscard]] std::wstring BoundedTail(std::wstring value)
        {
            if (value.size() <= MaximumPackageMutationOutputTail)
            {
                return value;
            }
            value.erase(0, value.size() - (MaximumPackageMutationOutputTail - 1));
            value.insert(value.begin(), L'…');
            return value;
        }

        [[nodiscard]] std::wstring RedactPackageMutationTextUnbounded(std::wstring_view value)
        {
            std::wstring redacted(value);
            for (auto const keyword : {
                L"password=", L"password:", L"\"password\":", L"'password':",
                L"token=", L"token:", L"\"token\":", L"'token':",
                L"access_token=", L"access_token:", L"\"access_token\":", L"'access_token':",
                L"access-token=", L"access-token:", L"\"access-token\":", L"'access-token':",
                L"apikey=", L"apikey:", L"\"apikey\":", L"'apikey':",
                L"api_key=", L"api_key:", L"\"api_key\":", L"'api_key':",
                L"api-key=", L"api-key:", L"\"api-key\":", L"'api-key':",
                L"authorization:"
            })
            {
                RedactValueAfterKeyword(redacted, keyword);
            }
            RedactUriCredentials(redacted);
            return redacted;
        }

        [[nodiscard]] bool HasUnsupportedMutationCustomArguments(InstallOptions const& options) noexcept
        {
            return !options.custom_args_install.empty() ||
                !options.custom_args_update.empty() ||
                !options.custom_args_uninstall.empty();
        }

        [[nodiscard]] PackageMutationRecord UnstoredRejectedRecord(
            std::wstring_view id,
            std::wstring reason,
            std::uint64_t sequence)
        {
            PackageMutationRequest safeRequest;
            safeRequest.id = id;
            PackageMutationRecord record;
            record.request = std::move(safeRequest);
            record.state = PackageMutationState::Rejected;
            record.sequence = sequence;
            record.diagnostic = std::move(reason);
            return record;
        }

        [[nodiscard]] std::wstring MutationIdentity(PackageMutationRequest const& request)
        {
            return PackageSelectionKey(request.package, request.action);
        }

        [[nodiscard]] PackageMutationRecord RejectedRecord(
            PackageMutationRequest request,
            std::wstring reason,
            std::uint64_t sequence)
        {
            PackageMutationRecord record;
            record.request = std::move(request);
            record.state = PackageMutationState::Rejected;
            record.sequence = sequence;
            record.diagnostic = std::move(reason);
            return record;
        }

        [[nodiscard]] bool IsBatchRetryableState(PackageMutationState state) noexcept
        {
            return state == PackageMutationState::Failed ||
                state == PackageMutationState::Cancelled ||
                state == PackageMutationState::TimedOut;
        }

        [[nodiscard]] PackageMutationState AggregateBatchState(
            std::vector<PackageMutationRecord> const& records) noexcept
        {
            bool hasAwaiting = false;
            bool hasQueued = false;
            bool hasRunning = false;
            bool hasFailed = false;
            bool hasTimedOut = false;
            bool hasCancelled = false;
            bool hasRejected = false;
            bool hasSucceeded = false;

            for (auto const& record : records)
            {
                switch (record.state)
                {
                case PackageMutationState::AwaitingConsent: hasAwaiting = true; break;
                case PackageMutationState::Queued: hasQueued = true; break;
                case PackageMutationState::Running: hasRunning = true; break;
                case PackageMutationState::Succeeded: hasSucceeded = true; break;
                case PackageMutationState::Failed: hasFailed = true; break;
                case PackageMutationState::Cancelled: hasCancelled = true; break;
                case PackageMutationState::TimedOut: hasTimedOut = true; break;
                case PackageMutationState::Rejected: hasRejected = true; break;
                }
            }

            // A running or queued child must be visible above a stale terminal
            // sibling. Awaiting consent is next, which is also the state used
            // after a retry resets only unsuccessful children for fresh review.
            if (hasRunning) return PackageMutationState::Running;
            if (hasQueued) return PackageMutationState::Queued;
            if (hasAwaiting) return PackageMutationState::AwaitingConsent;
            if (hasFailed) return PackageMutationState::Failed;
            if (hasTimedOut) return PackageMutationState::TimedOut;
            if (hasCancelled) return PackageMutationState::Cancelled;
            if (hasRejected) return PackageMutationState::Rejected;
            if (hasSucceeded) return PackageMutationState::Succeeded;
            return PackageMutationState::Rejected;
        }

        [[nodiscard]] std::wstring BatchDiagnostic(
            PackageMutationState state,
            std::size_t count)
        {
            auto const amount = std::to_wstring(count);
            switch (state)
            {
            case PackageMutationState::AwaitingConsent:
                return L"batch review is complete; explicit consent is required before all " + amount + L" package commands can be queued";
            case PackageMutationState::Queued:
                return L"explicit batch consent recorded; package commands are waiting in the serial native queue";
            case PackageMutationState::Running:
                return L"batch execution is running serially; cancellation stops the active command and cancels remaining commands";
            case PackageMutationState::Succeeded:
                return L"every package command in this batch completed; third-party output was withheld";
            case PackageMutationState::Failed:
                return L"one or more package commands failed; only unsuccessful children can return to fresh review";
            case PackageMutationState::Cancelled:
                return L"batch cancellation completed; successful children are never replayed implicitly";
            case PackageMutationState::TimedOut:
                return L"one or more package commands timed out; only unsuccessful children can return to fresh review";
            case PackageMutationState::Rejected:
                return L"batch was rejected before any package command was queued";
            }
            return L"batch was rejected before any package command was queued";
        }
    }

    bool IsTerminalPackageMutationState(PackageMutationState state) noexcept
    {
        return state == PackageMutationState::Succeeded ||
            state == PackageMutationState::Failed ||
            state == PackageMutationState::Cancelled ||
            state == PackageMutationState::TimedOut ||
            state == PackageMutationState::Rejected;
    }

    std::wstring_view PackageMutationStateKey(PackageMutationState state) noexcept
    {
        switch (state)
        {
        case PackageMutationState::AwaitingConsent: return L"awaiting-consent";
        case PackageMutationState::Queued: return L"queued";
        case PackageMutationState::Running: return L"running";
        case PackageMutationState::Succeeded: return L"succeeded";
        case PackageMutationState::Failed: return L"failed";
        case PackageMutationState::Cancelled: return L"cancelled";
        case PackageMutationState::TimedOut: return L"timed-out";
        case PackageMutationState::Rejected: return L"rejected";
        }
        return L"rejected";
    }

    std::wstring RedactPackageMutationText(std::wstring_view value)
    {
        return BoundedTail(RedactPackageMutationTextUnbounded(value));
    }

    PackageMutationSubmission PackageMutationCoordinator::Submit(PackageMutationRequest request)
    {
        std::scoped_lock lock(m_mutex);
        if (request.id.empty())
        {
            request.id = L"mutation-" + std::to_wstring(m_next_sequence);
        }
        if (HasUnsupportedMutationCustomArguments(request.install_options))
        {
            // Custom option payloads can carry arbitrary credentials. This
            // v1 consent surface has no secure per-option secret model, so it
            // refuses them before any raw request data can enter a snapshot,
            // preview, or persisted operation event.
            auto record = UnstoredRejectedRecord(
                request.id,
                L"custom-mutation-arguments-unsupported",
                m_next_sequence++);
            return { false, false, std::move(record) };
        }
        if (FindLocked(request.id))
        {
            // Every external operation id is a stable UI/worker handle. Do
            // not allow a caller to alias an existing record by supplying the
            // same id for a different mutation. Do not reflect the rejected
            // caller-owned request back either: package metadata may itself
            // have originated outside the trusted cached result set.
            auto record = UnstoredRejectedRecord(
                request.id,
                L"duplicate-mutation-id",
                m_next_sequence);
            return { false, false, std::move(record) };
        }

        if (!IsMutationAction(request.action))
        {
            if (!MakeSpaceForNewRecordLocked())
            {
                auto record = RejectedRecord(std::move(request), L"mutation-queue-capacity-reached", m_next_sequence++);
                return { false, false, std::move(record) };
            }
            auto record = RejectedRecord(std::move(request), L"invalid-mutation-action", m_next_sequence++);
            m_records.push_back(record);
            return { false, false, std::move(record) };
        }

        auto const built = BuildPackageActionCommand(
            request.package.manager_key,
            request.package.id,
            request.package.source,
            request.action,
            request.install_options);
        if (!built)
        {
            if (!MakeSpaceForNewRecordLocked())
            {
                auto record = RejectedRecord(std::move(request), L"mutation-queue-capacity-reached", m_next_sequence++);
                return { false, false, std::move(record) };
            }
            auto record = RejectedRecord(std::move(request), built.error_code, m_next_sequence++);
            m_records.push_back(record);
            return { false, false, std::move(record) };
        }
        if (built.command->requires_elevation)
        {
            if (!MakeSpaceForNewRecordLocked())
            {
                auto record = RejectedRecord(std::move(request), L"mutation-queue-capacity-reached", m_next_sequence++);
                return { false, false, std::move(record) };
            }
            auto record = RejectedRecord(
                std::move(request),
                L"elevated-mutations-are-not-supported", m_next_sequence++);
            m_records.push_back(record);
            return { false, false, std::move(record) };
        }

        auto const identity = MutationIdentity(request);
        for (auto const& existing : m_records)
        {
            if (!IsTerminalPackageMutationState(existing.state) &&
                MutationIdentity(existing.request) == identity)
            {
                return { false, true, existing };
            }
        }
        if (!MakeSpaceForNewRecordLocked())
        {
            auto record = RejectedRecord(std::move(request), L"mutation-queue-capacity-reached", m_next_sequence++);
            return { false, false, std::move(record) };
        }

        auto const commandPreview = FormatCommandPreview(*built.command);
        auto redactedPreview = RedactPackageMutationTextUnbounded(commandPreview);
        if (redactedPreview.size() > MaximumPackageMutationOutputTail)
        {
            // A consent card must always show the full reviewed argv. A
            // truncated tail would hide the executable or early arguments.
            auto record = UnstoredRejectedRecord(
                request.id,
                L"mutation-command-preview-too-long",
                m_next_sequence++);
            return { false, false, std::move(record) };
        }

        PackageMutationRecord record;
        record.request = std::move(request);
        record.state = PackageMutationState::AwaitingConsent;
        record.sequence = m_next_sequence++;
        record.command_preview = std::move(redactedPreview);
        record.diagnostic = L"explicit consent is required before this package command can be queued";
        m_records.push_back(record);
        return { true, false, std::move(record) };
    }

    PackageMutationBatchSubmission PackageMutationCoordinator::SubmitBatch(
        PackageMutationBatchRequest batch)
    {
        std::scoped_lock lock(m_mutex);

        PackageMutationBatchRecord rejected;
        rejected.id = batch.id;
        rejected.state = PackageMutationState::Rejected;

        if (batch.id.empty())
        {
            rejected.diagnostic = L"batch-id-is-required";
            return { false, false, std::move(rejected) };
        }
        if (batch.requests.empty())
        {
            rejected.diagnostic = L"batch-has-no-package-requests";
            return { false, false, std::move(rejected) };
        }
        if (batch.requests.size() > MaximumPackageMutationBatchRecords)
        {
            rejected.diagnostic = L"batch-review-capacity-exceeded";
            return { false, false, std::move(rejected) };
        }
        if (std::any_of(m_records.begin(), m_records.end(), [&batch](PackageMutationRecord const& record)
        {
            return record.batch_id == batch.id;
        }))
        {
            rejected.diagnostic = L"duplicate-batch-id";
            return { false, true, std::move(rejected) };
        }

        std::unordered_set<std::wstring> requestIds;
        std::unordered_set<std::wstring> identities;
        std::vector<PackageMutationRecord> reviewed;
        reviewed.reserve(batch.requests.size());

        for (std::size_t index = 0; index < batch.requests.size(); ++index)
        {
            auto request = std::move(batch.requests[index]);
            if (request.id.empty())
            {
                request.id = batch.id + L"-item-" + std::to_wstring(index + 1);
            }
            if (!requestIds.insert(request.id).second || FindLocked(request.id))
            {
                rejected.diagnostic = L"duplicate-mutation-id-in-batch";
                return { false, true, std::move(rejected) };
            }
            if (HasUnsupportedMutationCustomArguments(request.install_options))
            {
                rejected.diagnostic = L"custom-mutation-arguments-unsupported";
                return { false, false, std::move(rejected) };
            }
            if (!IsMutationAction(request.action))
            {
                rejected.diagnostic = L"invalid-mutation-action";
                return { false, false, std::move(rejected) };
            }

            auto const built = BuildPackageActionCommand(
                request.package.manager_key,
                request.package.id,
                request.package.source,
                request.action,
                request.install_options);
            if (!built)
            {
                rejected.diagnostic = built.error_code;
                return { false, false, std::move(rejected) };
            }
            if (built.command->requires_elevation)
            {
                rejected.diagnostic = L"elevated-mutations-are-not-supported";
                return { false, false, std::move(rejected) };
            }

            auto const identity = MutationIdentity(request);
            if (!identities.insert(identity).second)
            {
                rejected.diagnostic = L"duplicate-package-identity-in-batch";
                return { false, true, std::move(rejected) };
            }
            auto const activeIdentity = std::find_if(m_records.begin(), m_records.end(), [&identity](PackageMutationRecord const& existing)
            {
                return !IsTerminalPackageMutationState(existing.state) &&
                    MutationIdentity(existing.request) == identity;
            });
            if (activeIdentity != m_records.end())
            {
                rejected.diagnostic = L"duplicate-package-mutation";
                return { false, true, std::move(rejected) };
            }

            auto redactedPreview = RedactPackageMutationTextUnbounded(
                FormatCommandPreview(*built.command));
            if (redactedPreview.size() > MaximumPackageMutationOutputTail)
            {
                rejected.diagnostic = L"mutation-command-preview-too-long";
                return { false, false, std::move(rejected) };
            }

            PackageMutationRecord record;
            record.request = std::move(request);
            record.batch_id = batch.id;
            record.state = PackageMutationState::AwaitingConsent;
            record.sequence = m_next_sequence + index;
            record.command_preview = std::move(redactedPreview);
            record.diagnostic = L"batch review is complete; explicit batch consent is required before this package command can be queued";
            reviewed.push_back(std::move(record));
        }

        if (!MakeSpaceForNewRecordsLocked(reviewed.size()))
        {
            rejected.diagnostic = L"mutation-queue-capacity-reached";
            return { false, false, std::move(rejected) };
        }

        PackageMutationBatchRecord accepted;
        accepted.id = batch.id;
        accepted.state = PackageMutationState::AwaitingConsent;
        accepted.sequence = m_next_sequence;
        accepted.diagnostic = BatchDiagnostic(accepted.state, reviewed.size());
        accepted.records = reviewed;
        m_next_sequence += reviewed.size();
        for (auto& record : reviewed)
        {
            m_records.push_back(std::move(record));
        }
        return { true, false, std::move(accepted) };
    }

    bool PackageMutationCoordinator::Confirm(std::wstring_view id)
    {
        std::scoped_lock lock(m_mutex);
        auto* record = FindLocked(id);
        if (!record || record->state != PackageMutationState::AwaitingConsent)
        {
            return false;
        }
        record->state = PackageMutationState::Queued;
        record->diagnostic = L"explicit consent recorded; waiting for the serial native queue";
        return true;
    }

    bool PackageMutationCoordinator::ConfirmBatch(std::wstring_view id)
    {
        std::scoped_lock lock(m_mutex);
        std::vector<PackageMutationRecord*> records;
        for (auto& record : m_records)
        {
            if (record.batch_id == id)
            {
                records.push_back(&record);
            }
        }
        bool hasAwaitingConsent = false;
        if (records.empty() || !std::all_of(records.begin(), records.end(), [&hasAwaitingConsent](PackageMutationRecord const* record)
        {
            if (record->state == PackageMutationState::AwaitingConsent)
            {
                hasAwaitingConsent = true;
                return true;
            }
            // A retry intentionally leaves successful children terminal so a
            // fresh batch confirmation can queue only the unsuccessful items.
            // No successful command is ever replayed merely because another
            // child needs a retry.
            return record->state == PackageMutationState::Succeeded;
        }))
        {
            return false;
        }
        if (!hasAwaitingConsent)
        {
            return false;
        }
        for (auto* record : records)
        {
            if (record->state != PackageMutationState::AwaitingConsent)
            {
                continue;
            }
            record->state = PackageMutationState::Queued;
            record->diagnostic = L"explicit batch consent recorded; waiting for the serial native queue";
        }
        return true;
    }

    bool PackageMutationCoordinator::Cancel(std::wstring_view id)
    {
        std::scoped_lock lock(m_mutex);
        auto* record = FindLocked(id);
        if (!record || IsTerminalPackageMutationState(record->state))
        {
            return false;
        }
        if (record->state == PackageMutationState::Running)
        {
            record->cancellation_requested = true;
            record->diagnostic = L"cancellation requested; waiting for the package process to stop";
            if (m_active_stop_source && m_active_id == record->request.id)
            {
                m_active_stop_source->request_stop();
            }
            return true;
        }
        record->state = PackageMutationState::Cancelled;
        record->cancellation_requested = true;
        record->diagnostic = L"cancelled before package command execution";
        TrimTerminalHistoryLocked();
        return true;
    }

    bool PackageMutationCoordinator::CancelBatch(std::wstring_view id)
    {
        std::scoped_lock lock(m_mutex);
        bool found = false;
        bool cancelled = false;
        for (auto& record : m_records)
        {
            if (record.batch_id != id)
            {
                continue;
            }
            found = true;
            if (IsTerminalPackageMutationState(record.state))
            {
                continue;
            }

            cancelled = true;
            record.cancellation_requested = true;
            if (record.state == PackageMutationState::Running)
            {
                record.diagnostic = L"batch cancellation requested; waiting for the package process to stop";
                if (m_active_stop_source && m_active_id == record.request.id)
                {
                    m_active_stop_source->request_stop();
                }
                continue;
            }
            record.state = PackageMutationState::Cancelled;
            record.diagnostic = L"cancelled before package command execution because its reviewed batch was cancelled";
        }
        if (found && cancelled)
        {
            TrimTerminalHistoryLocked();
        }
        return found && cancelled;
    }

    bool PackageMutationCoordinator::CancelAll()
    {
        std::scoped_lock lock(m_mutex);
        bool cancelled = false;
        for (auto& record : m_records)
        {
            if (IsTerminalPackageMutationState(record.state))
            {
                continue;
            }

            cancelled = true;
            record.cancellation_requested = true;
            if (record.state == PackageMutationState::Running)
            {
                record.diagnostic = L"cancellation requested because the Package Manager lifecycle ended";
                if (m_active_stop_source && m_active_id == record.request.id)
                {
                    m_active_stop_source->request_stop();
                }
                continue;
            }

            record.state = PackageMutationState::Cancelled;
            record.diagnostic = L"cancelled because the Package Manager lifecycle ended";
        }
        TrimTerminalHistoryLocked();
        return cancelled;
    }

    bool PackageMutationCoordinator::Retry(std::wstring_view id)
    {
        std::scoped_lock lock(m_mutex);
        auto* record = FindLocked(id);
        if (!record || record->state == PackageMutationState::Rejected ||
            !IsTerminalPackageMutationState(record->state))
        {
            return false;
        }
        record->state = PackageMutationState::AwaitingConsent;
        ++record->retry_count;
        record->command_started = false;
        record->cancellation_requested = false;
        record->exit_code.reset();
        record->output_tail.clear();
        record->diagnostic = L"retry requires fresh explicit consent";
        return true;
    }

    bool PackageMutationCoordinator::RetryBatch(std::wstring_view id)
    {
        std::scoped_lock lock(m_mutex);
        bool found = false;
        bool retryable = false;
        for (auto& record : m_records)
        {
            if (record.batch_id != id)
            {
                continue;
            }
            found = true;
            if (!IsBatchRetryableState(record.state))
            {
                continue;
            }
            retryable = true;
            record.state = PackageMutationState::AwaitingConsent;
            ++record.retry_count;
            record.command_started = false;
            record.cancellation_requested = false;
            record.exit_code.reset();
            record.output_tail.clear();
            record.diagnostic = L"batch retry requires fresh explicit consent";
        }
        return found && retryable;
    }

    std::optional<PackageMutationRecord> PackageMutationCoordinator::RunNext(
        PackageMutationExecutor const& executor,
        PackageMutationStartedCallback const& started)
    {
        PackageMutationRequest request;
        std::stop_token cancellationToken;
        PackageMutationRecord startedRecord;
        bool startedRecordAvailable = false;
        {
            std::scoped_lock lock(m_mutex);
            if (m_active_stop_source)
            {
                return std::nullopt;
            }
            auto found = std::find_if(m_records.begin(), m_records.end(), [](PackageMutationRecord const& record)
            {
                return record.state == PackageMutationState::Queued;
            });
            if (found == m_records.end())
            {
                return std::nullopt;
            }
            found->state = PackageMutationState::Running;
            found->cancellation_requested = false;
            found->diagnostic = L"package command running in the serial native queue";
            request = found->request;
            startedRecord = *found;
            startedRecordAvailable = true;
            m_active_id = request.id;
            m_active_stop_source.emplace();
            cancellationToken = m_active_stop_source->get_token();
        }
        if (startedRecordAvailable && started)
        {
            try
            {
                started(startedRecord);
            }
            catch (...)
            {
                // UI/status observers are non-authoritative: a failed
                // notification must never weaken the contained mutation run.
            }
        }

        PackageRuntimeResult runtime;
        try
        {
            if (!executor)
            {
                runtime.diagnostic = L"native mutation executor is unavailable";
            }
            else
            {
                runtime = executor(request, cancellationToken);
            }
        }
        catch (std::exception const& error)
        {
            runtime.diagnostic = error.what() ? std::wstring(error.what(), error.what() + std::char_traits<char>::length(error.what())) : L"native mutation executor threw";
        }
        catch (...)
        {
            runtime.diagnostic = L"native mutation executor threw an unknown exception";
        }

        std::scoped_lock lock(m_mutex);
        auto* record = FindLocked(request.id);
        if (!record)
        {
            m_active_stop_source.reset();
            m_active_id.clear();
            return std::nullopt;
        }

        auto const stopRequested = cancellationToken.stop_requested();
        record->command_started = runtime.command_started;
        record->exit_code = runtime.exit_code;
        record->cancellation_requested = record->cancellation_requested || stopRequested;
        // Package tools may write arbitrary, secret-bearing text. Do not rely on
        // best-effort redaction for third-party stdout, stderr, or diagnostics:
        // retain only a fixed, non-sensitive lifecycle summary and the numeric
        // exit code above. Reviewed argv previews are separately redacted.
        record->output_tail.clear();
        if (stopRequested || runtime.cancelled)
        {
            record->state = PackageMutationState::Cancelled;
            record->diagnostic = L"package command cancelled; external tool output withheld";
        }
        else if (runtime.timed_out)
        {
            record->state = PackageMutationState::TimedOut;
            record->diagnostic = L"package command timed out; external tool output withheld";
        }
        else if (runtime.success)
        {
            record->state = PackageMutationState::Succeeded;
            record->diagnostic = L"package command completed; external tool output withheld";
        }
        else
        {
            record->state = PackageMutationState::Failed;
            record->diagnostic = runtime.command_started
                ? L"package command failed; external tool output withheld"
                : L"package command did not start; external tool output withheld";
        }
        m_active_stop_source.reset();
        m_active_id.clear();
        auto completed = *record;
        TrimTerminalHistoryLocked();
        return completed;
    }

    std::vector<PackageMutationRecord> PackageMutationCoordinator::Snapshot() const
    {
        std::scoped_lock lock(m_mutex);
        return m_records;
    }

    std::vector<PackageMutationBatchRecord> PackageMutationCoordinator::SnapshotBatches() const
    {
        std::scoped_lock lock(m_mutex);
        std::vector<PackageMutationBatchRecord> batches;
        for (auto const& record : m_records)
        {
            if (record.batch_id.empty())
            {
                continue;
            }
            auto found = std::find_if(batches.begin(), batches.end(), [&record](PackageMutationBatchRecord const& batch)
            {
                return batch.id == record.batch_id;
            });
            if (found == batches.end())
            {
                PackageMutationBatchRecord batch;
                batch.id = record.batch_id;
                batch.sequence = record.sequence;
                batch.records.push_back(record);
                batches.push_back(std::move(batch));
            }
            else
            {
                found->sequence = std::min(found->sequence, record.sequence);
                found->records.push_back(record);
            }
        }
        for (auto& batch : batches)
        {
            batch.state = AggregateBatchState(batch.records);
            batch.retry_count = 0;
            batch.cancellation_requested = false;
            for (auto const& record : batch.records)
            {
                batch.retry_count = std::max(batch.retry_count, record.retry_count);
                batch.cancellation_requested = batch.cancellation_requested || record.cancellation_requested;
            }
            batch.diagnostic = BatchDiagnostic(batch.state, batch.records.size());
        }
        std::sort(batches.begin(), batches.end(), [](PackageMutationBatchRecord const& left, PackageMutationBatchRecord const& right)
        {
            return left.sequence < right.sequence;
        });
        return batches;
    }

    bool PackageMutationCoordinator::HasRunnableWork() const
    {
        std::scoped_lock lock(m_mutex);
        return !m_active_stop_source && std::any_of(
            m_records.begin(), m_records.end(), [](PackageMutationRecord const& record)
            {
                return record.state == PackageMutationState::Queued;
            });
    }

    PackageMutationRecord* PackageMutationCoordinator::FindLocked(std::wstring_view id) noexcept
    {
        auto const found = std::find_if(m_records.begin(), m_records.end(), [id](PackageMutationRecord const& record)
        {
            return record.request.id == id;
        });
        return found == m_records.end() ? nullptr : &*found;
    }

    PackageMutationRecord const* PackageMutationCoordinator::FindLocked(std::wstring_view id) const noexcept
    {
        auto const found = std::find_if(m_records.begin(), m_records.end(), [id](PackageMutationRecord const& record)
        {
            return record.request.id == id;
        });
        return found == m_records.end() ? nullptr : &*found;
    }

    bool PackageMutationCoordinator::MakeSpaceForNewRecordLocked()
    {
        return MakeSpaceForNewRecordsLocked(1);
    }

    bool PackageMutationCoordinator::MakeSpaceForNewRecordsLocked(std::size_t required)
    {
        if (required > MaximumPackageMutationRecords)
        {
            return false;
        }
        while (m_records.size() + required > MaximumPackageMutationRecords)
        {
            auto const terminal = std::find_if(m_records.begin(), m_records.end(), [this](PackageMutationRecord const& record)
            {
                if (!IsTerminalPackageMutationState(record.state))
                {
                    return false;
                }
                if (record.batch_id.empty())
                {
                    return true;
                }
                // A completed child of a still-active batch is part of the
                // shared consent/audit surface. Never evict it on its own:
                // doing so could hide a successful command before an
                // unsuccessful sibling is retried. A batch reclaims capacity
                // only once every child is terminal.
                return std::all_of(m_records.begin(), m_records.end(), [&record](PackageMutationRecord const& candidate)
                {
                    return candidate.batch_id != record.batch_id ||
                        IsTerminalPackageMutationState(candidate.state);
                });
            });
            if (terminal == m_records.end())
            {
                return false;
            }
            if (!terminal->batch_id.empty())
            {
                auto const batchId = terminal->batch_id;
                std::erase_if(m_records, [&batchId](PackageMutationRecord const& record)
                {
                    return record.batch_id == batchId;
                });
                continue;
            }
            m_records.erase(terminal);
        }
        return true;
    }

    void PackageMutationCoordinator::TrimTerminalHistoryLocked()
    {
        static_cast<void>(MakeSpaceForNewRecordsLocked(0));
    }
}
