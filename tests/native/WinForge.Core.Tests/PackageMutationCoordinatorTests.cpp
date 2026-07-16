#include "PackageMutationCoordinator.h"
#include "PackageMutationCoordinatorTests.h"

#include <iostream>
#include <string_view>

namespace
{
    NativeTestCounts counts;

    void Expect(bool condition, std::string_view name)
    {
        if (condition)
        {
            ++counts.passed;
            std::cout << "PASS package mutation coordinator: " << name << '\n';
        }
        else
        {
            ++counts.failed;
            std::cerr << "FAIL package mutation coordinator: " << name << '\n';
        }
    }

    winforge::core::packages::PackageMutationRequest Request(
        std::wstring id,
        std::wstring packageId = L"Contoso.SafeTool",
        winforge::core::packages::PackageAction action = winforge::core::packages::PackageAction::Install)
    {
        winforge::core::packages::PackageMutationRequest request;
        request.id = std::move(id);
        request.package.name = L"Safe Tool";
        request.package.id = std::move(packageId);
        request.package.version = L"1.0.0";
        request.package.source = L"winget";
        request.package.manager_key = L"winget";
        request.action = action;
        return request;
    }
}

NativeTestCounts RunPackageMutationCoordinatorTests()
{
    using namespace winforge::core::packages;
    counts = {};

    PackageMutationCoordinator coordinator;
    int executorCalls = 0;
    auto successExecutor = [&executorCalls](PackageMutationRequest const&, std::stop_token)
    {
        ++executorCalls;
        PackageRuntimeResult result;
        result.success = true;
        result.command_started = true;
        result.exit_code = 0;
        result.standard_output = L"operation completed";
        return result;
    };

    auto first = coordinator.Submit(Request(L"first"));
    Expect(first.accepted && !first.duplicate &&
        first.record.state == PackageMutationState::AwaitingConsent,
        "a validated request waits for explicit consent");
    Expect(!coordinator.RunNext(successExecutor) && executorCalls == 0,
        "awaiting-consent work cannot start a package process");
    Expect(coordinator.Confirm(L"first"), "explicit consent queues the request");
    auto completed = coordinator.RunNext(successExecutor);
    Expect(completed && completed->state == PackageMutationState::Succeeded && executorCalls == 1,
        "confirmed request runs through the injected executor once");

    auto duplicatePending = coordinator.Submit(Request(L"duplicate-a", L"Contoso.Duplicate"));
    auto duplicate = coordinator.Submit(Request(L"duplicate-b", L"Contoso.Duplicate"));
    Expect(duplicatePending.accepted && duplicate.duplicate && !duplicate.accepted,
        "active duplicate package mutations are suppressed by stable identity");
    Expect(coordinator.Cancel(L"duplicate-a"), "queued-consent request can be cancelled before execution");
    Expect(!coordinator.RunNext(successExecutor) && executorCalls == 1,
        "cancel-before-start never invokes the executor");

    auto second = coordinator.Submit(Request(L"second", L"Contoso.Second"));
    auto third = coordinator.Submit(Request(L"third", L"Contoso.Third"));
    Expect(second.accepted && third.accepted && coordinator.Confirm(L"second") && coordinator.Confirm(L"third"),
        "two distinct requests enter the serial queue after consent");
    auto secondDone = coordinator.RunNext(successExecutor);
    auto thirdDone = coordinator.RunNext(successExecutor);
    Expect(secondDone && thirdDone && secondDone->request.id == L"second" &&
        thirdDone->request.id == L"third" && executorCalls == 3,
        "serial queue preserves explicit confirmation order");

    PackageMutationCoordinator runningCoordinator;
    auto running = runningCoordinator.Submit(Request(L"running", L"Contoso.Running"));
    Expect(running.accepted && runningCoordinator.Confirm(L"running"),
        "running-cancellation fixture is queued");
    bool cancellationRequested = false;
    auto cancelled = runningCoordinator.RunNext([&](PackageMutationRequest const&, std::stop_token token)
    {
        cancellationRequested = runningCoordinator.Cancel(L"running");
        PackageRuntimeResult result;
        result.command_started = true;
        result.cancelled = token.stop_requested();
        return result;
    });
    Expect(cancellationRequested && cancelled && cancelled->state == PackageMutationState::Cancelled &&
        cancelled->cancellation_requested,
        "running cancellation propagates a stop request to the executor");

    PackageMutationCoordinator retryCoordinator;
    auto retry = retryCoordinator.Submit(Request(L"retry", L"Contoso.Retry"));
    Expect(retry.accepted && retryCoordinator.Confirm(L"retry"), "retry fixture starts once with consent");
    auto failed = retryCoordinator.RunNext([](PackageMutationRequest const&, std::stop_token)
    {
        PackageRuntimeResult result;
        result.command_started = true;
        result.exit_code = 17;
        result.standard_error = L"password=should-not-leak token=also-secret authorization: Bearer private-value";
        result.diagnostic = L"failed with api_key=private-key";
        return result;
    });
    Expect(failed && failed->state == PackageMutationState::Failed &&
        failed->diagnostic.find(L"private-key") == std::wstring::npos &&
        failed->output_tail.find(L"should-not-leak") == std::wstring::npos &&
        failed->output_tail.find(L"also-secret") == std::wstring::npos &&
        failed->output_tail.find(L"private-value") == std::wstring::npos,
        "failure output and diagnostics are redacted before state retention");
    Expect(retryCoordinator.Retry(L"retry") && !retryCoordinator.RunNext(successExecutor),
        "retry returns to awaiting-consent and cannot rerun automatically");
    Expect(retryCoordinator.Confirm(L"retry") && retryCoordinator.RunNext(successExecutor),
        "retry runs only after fresh explicit consent");

    PackageMutationCoordinator timeoutCoordinator;
    auto timeout = timeoutCoordinator.Submit(Request(L"timeout", L"Contoso.Timeout"));
    Expect(timeout.accepted && timeoutCoordinator.Confirm(L"timeout"),
        "timeout fixture is queued after explicit consent");
    auto timedOut = timeoutCoordinator.RunNext([](PackageMutationRequest const&, std::stop_token)
    {
        PackageRuntimeResult result;
        result.command_started = true;
        result.timed_out = true;
        return result;
    });
    Expect(timedOut && timedOut->state == PackageMutationState::TimedOut,
        "runtime timeout is a distinct terminal state");

    PackageMutationCoordinator rejectedCoordinator;
    auto unsafe = Request(L"unsafe", L"safe & calc");
    auto rejected = rejectedCoordinator.Submit(std::move(unsafe));
    auto elevated = Request(L"elevated", L"Contoso.Elevated");
    elevated.install_options.run_as_administrator = true;
    auto elevatedRejected = rejectedCoordinator.Submit(std::move(elevated));
    Expect(!rejected.accepted && rejected.record.state == PackageMutationState::Rejected &&
        rejected.record.diagnostic == L"invalid-package-id",
        "unsafe package references are rejected before consent or execution");
    Expect(!elevatedRejected.accepted && elevatedRejected.record.state == PackageMutationState::Rejected &&
        elevatedRejected.record.diagnostic == L"elevated-mutations-are-not-supported",
        "elevation-requested mutations fail closed without a broker");
    Expect(!rejectedCoordinator.RunNext(successExecutor),
        "rejected requests never enter the executor queue");

    std::wstring oversized(MaximumPackageMutationOutputTail + 300, L'x');
    oversized += L" password=oversized-secret";
    auto redactedTail = RedactPackageMutationText(oversized);
    Expect(redactedTail.size() <= MaximumPackageMutationOutputTail &&
        redactedTail.find(L"oversized-secret") == std::wstring::npos,
        "redacted operation text remains bounded by the output-tail limit");

    std::cout << counts.passed << " package-mutation-coordinator tests passed, "
        << counts.failed << " failed\n";
    return counts;
}
