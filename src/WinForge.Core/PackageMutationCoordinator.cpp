#include "PackageMutationCoordinator.h"

#include <algorithm>
#include <cwctype>
#include <exception>
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
                auto end = start;
                while (end < value.size() &&
                    !std::iswspace(value[end]) && value[end] != L';' &&
                    value[end] != L'&' && value[end] != L',' && value[end] != L'\'')
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
                if (at != std::wstring::npos && (boundary == std::wstring::npos || at < boundary) &&
                    value.find(L':', credentialsBegin) != std::wstring::npos &&
                    value.find(L':', credentialsBegin) < at)
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
        std::wstring redacted(value);
        RedactValueAfterKeyword(redacted, L"password=");
        RedactValueAfterKeyword(redacted, L"password:");
        RedactValueAfterKeyword(redacted, L"token=");
        RedactValueAfterKeyword(redacted, L"token:");
        RedactValueAfterKeyword(redacted, L"apikey=");
        RedactValueAfterKeyword(redacted, L"api_key=");
        RedactValueAfterKeyword(redacted, L"authorization:");
        RedactUriCredentials(redacted);
        return BoundedTail(std::move(redacted));
    }

    PackageMutationSubmission PackageMutationCoordinator::Submit(PackageMutationRequest request)
    {
        std::scoped_lock lock(m_mutex);
        if (request.id.empty())
        {
            request.id = L"mutation-" + std::to_wstring(m_next_sequence);
        }

        if (!IsMutationAction(request.action))
        {
            auto record = RejectedRecord(std::move(request), L"invalid-mutation-action", m_next_sequence++);
            m_records.push_back(record);
            TrimTerminalHistoryLocked();
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
            auto record = RejectedRecord(std::move(request), built.error_code, m_next_sequence++);
            m_records.push_back(record);
            TrimTerminalHistoryLocked();
            return { false, false, std::move(record) };
        }
        if (built.command->requires_elevation)
        {
            auto record = RejectedRecord(
                std::move(request),
                L"elevated-mutations-are-not-supported", m_next_sequence++);
            m_records.push_back(record);
            TrimTerminalHistoryLocked();
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

        PackageMutationRecord record;
        record.request = std::move(request);
        record.state = PackageMutationState::AwaitingConsent;
        record.sequence = m_next_sequence++;
        record.command_preview = FormatCommandPreview(*built.command);
        record.diagnostic = L"explicit consent is required before this package command can be queued";
        m_records.push_back(record);
        TrimTerminalHistoryLocked();
        return { true, false, std::move(record) };
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
        record->diagnostic = L"cancelled before package command execution";
        TrimTerminalHistoryLocked();
        return true;
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

    std::optional<PackageMutationRecord> PackageMutationCoordinator::RunNext(
        PackageMutationExecutor const& executor)
    {
        PackageMutationRequest request;
        std::stop_token cancellationToken;
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
            m_active_id = request.id;
            m_active_stop_source.emplace();
            cancellationToken = m_active_stop_source->get_token();
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
        record->diagnostic = RedactPackageMutationText(runtime.diagnostic);
        std::wstring output = runtime.standard_output;
        if (!runtime.standard_error.empty())
        {
            if (!output.empty()) output += L'\n';
            output += runtime.standard_error;
        }
        record->output_tail = RedactPackageMutationText(output);
        if (stopRequested || runtime.cancelled)
        {
            record->state = PackageMutationState::Cancelled;
            if (record->diagnostic.empty()) record->diagnostic = L"package command cancelled";
        }
        else if (runtime.timed_out)
        {
            record->state = PackageMutationState::TimedOut;
            if (record->diagnostic.empty()) record->diagnostic = L"package command timed out";
        }
        else if (runtime.success)
        {
            record->state = PackageMutationState::Succeeded;
            if (record->diagnostic.empty()) record->diagnostic = L"package command completed";
        }
        else
        {
            record->state = PackageMutationState::Failed;
            if (record->diagnostic.empty()) record->diagnostic = L"package command failed";
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

    void PackageMutationCoordinator::TrimTerminalHistoryLocked()
    {
        while (m_records.size() > MaximumPackageMutationRecords)
        {
            auto const terminal = std::find_if(m_records.begin(), m_records.end(), [](PackageMutationRecord const& record)
            {
                return IsTerminalPackageMutationState(record.state);
            });
            if (terminal == m_records.end())
            {
                return;
            }
            m_records.erase(terminal);
        }
    }
}
