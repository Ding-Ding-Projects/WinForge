#include "PackageMutationCoordinator.h"
#include "PackageMutationCoordinatorTests.h"

#include <algorithm>
#include <iostream>
#include <string_view>
#include <vector>

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

    winforge::core::packages::PackageMutationBatchRequest Batch(
        std::wstring id,
        std::vector<winforge::core::packages::PackageMutationRequest> requests)
    {
        winforge::core::packages::PackageMutationBatchRequest batch;
        batch.id = std::move(id);
        batch.requests = std::move(requests);
        return batch;
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

    PackageMutationCoordinator uniqueIdCoordinator;
    auto stableId = uniqueIdCoordinator.Submit(Request(L"stable-id", L"Contoso.StableId"));
    auto collidingId = uniqueIdCoordinator.Submit(Request(L"stable-id", L"Contoso.OtherPackage"));
    auto uniqueIdSnapshot = uniqueIdCoordinator.Snapshot();
    Expect(stableId.accepted && !collidingId.accepted && !collidingId.duplicate &&
        collidingId.record.diagnostic == L"duplicate-mutation-id" &&
        collidingId.record.request.package.id.empty() &&
        uniqueIdSnapshot.size() == 1 &&
        uniqueIdSnapshot.front().request.package.id == L"Contoso.StableId",
        "duplicate external mutation ids are rejected without aliasing or reflecting coordinator records");

    auto second = coordinator.Submit(Request(L"second", L"Contoso.Second"));
    auto third = coordinator.Submit(Request(L"third", L"Contoso.Third"));
    Expect(second.accepted && third.accepted && coordinator.Confirm(L"second") && coordinator.Confirm(L"third"),
        "two distinct requests enter the serial queue after consent");
    auto secondDone = coordinator.RunNext(successExecutor);
    auto thirdDone = coordinator.RunNext(successExecutor);
    Expect(secondDone && thirdDone && secondDone->request.id == L"second" &&
        thirdDone->request.id == L"third" && executorCalls == 3,
        "serial queue preserves explicit confirmation order");

    PackageMutationCoordinator startedCoordinator;
    auto started = startedCoordinator.Submit(Request(L"started", L"Contoso.Started"));
    bool startedCallbackObservedRunning = false;
    Expect(started.accepted && startedCoordinator.Confirm(L"started") &&
        startedCoordinator.RunNext(successExecutor, [&startedCallbackObservedRunning](
            PackageMutationRecord const& record)
        {
            startedCallbackObservedRunning = record.state == PackageMutationState::Running;
        }) && startedCallbackObservedRunning,
        "running transition notifies hosts after coordinator state is live");

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

    PackageMutationCoordinator lifecycleCoordinator;
    auto lifecycleRunning = lifecycleCoordinator.Submit(Request(L"lifecycle-running", L"Contoso.LifecycleRunning"));
    auto lifecycleQueued = lifecycleCoordinator.Submit(Request(L"lifecycle-queued", L"Contoso.LifecycleQueued"));
    auto lifecycleAwaiting = lifecycleCoordinator.Submit(Request(L"lifecycle-awaiting", L"Contoso.LifecycleAwaiting"));
    Expect(lifecycleRunning.accepted && lifecycleQueued.accepted && lifecycleAwaiting.accepted &&
        lifecycleCoordinator.Confirm(L"lifecycle-running") && lifecycleCoordinator.Confirm(L"lifecycle-queued"),
        "lifecycle cancellation fixture prepares running queued and awaiting work");
    bool lifecycleCancelled = false;
    auto lifecycleCompleted = lifecycleCoordinator.RunNext([&](PackageMutationRequest const&, std::stop_token token)
    {
        lifecycleCancelled = lifecycleCoordinator.CancelAll();
        PackageRuntimeResult result;
        result.command_started = true;
        result.cancelled = token.stop_requested();
        return result;
    });
    auto lifecycleSnapshot = lifecycleCoordinator.Snapshot();
    auto const allLifecycleCancelled = std::all_of(
        lifecycleSnapshot.begin(), lifecycleSnapshot.end(), [](PackageMutationRecord const& record)
        {
            return record.state == PackageMutationState::Cancelled;
        });
    Expect(lifecycleCancelled && lifecycleCompleted &&
        lifecycleCompleted->state == PackageMutationState::Cancelled && allLifecycleCancelled,
        "lifecycle cancellation stops active work and cancels every pending mutation");

    PackageMutationCoordinator retryCoordinator;
    auto retry = retryCoordinator.Submit(Request(L"retry", L"Contoso.Retry"));
    Expect(retry.accepted && retryCoordinator.Confirm(L"retry"), "retry fixture starts once with consent");
    auto failed = retryCoordinator.RunNext([](PackageMutationRequest const&, std::stop_token)
    {
        PackageRuntimeResult result;
        result.command_started = true;
        result.exit_code = 17;
        result.standard_error = L"password=should-not-leak token=also-secret access_token=underscore-secret "
            L"api-key=hyphen-secret {\"token\":\"json-secret\"} https://uri-secret@example.test/";
        result.diagnostic = L"failed with api_key=private-key";
        return result;
    });
    Expect(failed && failed->state == PackageMutationState::Failed &&
        failed->diagnostic.find(L"private-key") == std::wstring::npos &&
        failed->diagnostic.find(L"should-not-leak") == std::wstring::npos &&
        failed->output_tail.empty(),
        "failure output and diagnostics are withheld from retained coordinator state");
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

    PackageMutationCoordinator batchCoordinator;
    auto reviewedBatch = batchCoordinator.SubmitBatch(Batch(
        L"batch-reviewed",
        {
            Request(L"batch-reviewed-one", L"Contoso.BatchOne"),
            Request(L"batch-reviewed-two", L"Contoso.BatchTwo", PackageAction::Update),
        }));
    auto reviewedBatches = batchCoordinator.SnapshotBatches();
    Expect(reviewedBatch.accepted && reviewedBatch.batch.state == PackageMutationState::AwaitingConsent &&
        reviewedBatch.batch.records.size() == 2 && reviewedBatches.size() == 1 &&
        reviewedBatches.front().records.size() == 2 &&
        reviewedBatches.front().records.front().batch_id == L"batch-reviewed",
        "validated batch submission retains every reviewed child argv under one opaque batch id");
    auto const executorCallsBeforeBatchConsent = executorCalls;
    Expect(!batchCoordinator.RunNext(successExecutor) && executorCalls == executorCallsBeforeBatchConsent,
        "reviewed batch children cannot start a package process before batch consent");
    Expect(batchCoordinator.ConfirmBatch(L"batch-reviewed"),
        "one explicit batch consent queues every reviewed child atomically");
    std::vector<std::wstring> batchExecutionOrder;
    auto orderedBatchExecutor = [&batchExecutionOrder](PackageMutationRequest const& request, std::stop_token)
    {
        batchExecutionOrder.push_back(request.id);
        PackageRuntimeResult result;
        result.success = true;
        result.command_started = true;
        result.exit_code = 0;
        return result;
    };
    auto batchFirst = batchCoordinator.RunNext(orderedBatchExecutor);
    auto batchSecond = batchCoordinator.RunNext(orderedBatchExecutor);
    auto finishedBatch = batchCoordinator.SnapshotBatches();
    Expect(batchFirst && batchSecond && batchExecutionOrder == std::vector<std::wstring>{
            L"batch-reviewed-one", L"batch-reviewed-two" } &&
        finishedBatch.size() == 1 && finishedBatch.front().state == PackageMutationState::Succeeded,
        "confirmed batch children execute serially in their fully reviewed order");

    PackageMutationCoordinator atomicBatchCoordinator;
    auto unsafeBatchChild = Request(L"atomic-bad", L"safe & calc");
    auto atomicRejected = atomicBatchCoordinator.SubmitBatch(Batch(
        L"atomic-rejected",
        {
            Request(L"atomic-good", L"Contoso.AtomicGood"),
            std::move(unsafeBatchChild),
        }));
    Expect(!atomicRejected.accepted &&
        atomicRejected.batch.state == PackageMutationState::Rejected &&
        atomicRejected.batch.diagnostic == L"invalid-package-id" &&
        atomicBatchCoordinator.Snapshot().empty() && atomicBatchCoordinator.SnapshotBatches().empty(),
        "one invalid child rejects an entire batch without retaining or queuing a partial mutation set");

    PackageMutationCoordinator duplicateBatchCoordinator;
    auto duplicateInsideBatch = duplicateBatchCoordinator.SubmitBatch(Batch(
        L"duplicate-inside-batch",
        {
            Request(L"duplicate-inside-one", L"Contoso.Same"),
            Request(L"duplicate-inside-two", L"Contoso.Same"),
        }));
    auto activeStandalone = duplicateBatchCoordinator.Submit(Request(L"duplicate-active", L"Contoso.Active"));
    auto duplicateAgainstActive = duplicateBatchCoordinator.SubmitBatch(Batch(
        L"duplicate-against-active",
        { Request(L"duplicate-active-child", L"Contoso.Active") }));
    Expect(!duplicateInsideBatch.accepted && duplicateInsideBatch.duplicate &&
        duplicateInsideBatch.batch.diagnostic == L"duplicate-package-identity-in-batch" &&
        activeStandalone.accepted && !duplicateAgainstActive.accepted && duplicateAgainstActive.duplicate &&
        duplicateAgainstActive.batch.diagnostic == L"duplicate-package-mutation" &&
        duplicateBatchCoordinator.Snapshot().size() == 1,
        "batch review rejects duplicate identities both inside the batch and against active work");

    PackageMutationCoordinator cancellationBatchCoordinator;
    auto cancellationBatch = cancellationBatchCoordinator.SubmitBatch(Batch(
        L"batch-cancel",
        {
            Request(L"batch-cancel-running", L"Contoso.CancelRunning"),
            Request(L"batch-cancel-pending", L"Contoso.CancelPending"),
        }));
    Expect(cancellationBatch.accepted && cancellationBatchCoordinator.ConfirmBatch(L"batch-cancel"),
        "batch cancellation fixture is reviewed and confirmed");
    bool batchStopRequested = false;
    auto cancelledBatchChild = cancellationBatchCoordinator.RunNext([&](PackageMutationRequest const&, std::stop_token token)
    {
        batchStopRequested = cancellationBatchCoordinator.CancelBatch(L"batch-cancel");
        PackageRuntimeResult result;
        result.command_started = true;
        result.cancelled = token.stop_requested();
        return result;
    });
    auto cancelledBatch = cancellationBatchCoordinator.SnapshotBatches();
    auto const allBatchChildrenCancelled = !cancelledBatch.empty() && std::all_of(
        cancelledBatch.front().records.begin(), cancelledBatch.front().records.end(), [](PackageMutationRecord const& record)
        {
            return record.state == PackageMutationState::Cancelled;
        });
    Expect(batchStopRequested && cancelledBatchChild &&
        cancelledBatchChild->state == PackageMutationState::Cancelled && allBatchChildrenCancelled &&
        !cancellationBatchCoordinator.RunNext(successExecutor),
        "batch cancellation stops the active child and cancels every remaining child before execution");

    PackageMutationCoordinator retryBatchCoordinator;
    auto retryBatch = retryBatchCoordinator.SubmitBatch(Batch(
        L"batch-retry",
        {
            Request(L"batch-retry-success", L"Contoso.RetrySuccess"),
            Request(L"batch-retry-failure", L"Contoso.RetryFailure"),
        }));
    Expect(retryBatch.accepted && retryBatchCoordinator.ConfirmBatch(L"batch-retry"),
        "batch retry fixture is reviewed and confirmed");
    auto retrySuccess = retryBatchCoordinator.RunNext(successExecutor);
    auto retryFailure = retryBatchCoordinator.RunNext([](PackageMutationRequest const&, std::stop_token)
    {
        PackageRuntimeResult result;
        result.command_started = true;
        result.exit_code = 1;
        return result;
    });
    Expect(retrySuccess && retryFailure && retrySuccess->state == PackageMutationState::Succeeded &&
        retryFailure->state == PackageMutationState::Failed && retryBatchCoordinator.RetryBatch(L"batch-retry") &&
        !retryBatchCoordinator.RunNext(successExecutor),
        "batch retry returns only unsuccessful children to fresh review without replaying successful commands");
    auto retrySnapshot = retryBatchCoordinator.SnapshotBatches();
    Expect(retrySnapshot.size() == 1 && retrySnapshot.front().state == PackageMutationState::AwaitingConsent &&
        retrySnapshot.front().retry_count == 1 && retrySnapshot.front().records[0].state == PackageMutationState::Succeeded &&
        retrySnapshot.front().records[1].state == PackageMutationState::AwaitingConsent &&
        retryBatchCoordinator.ConfirmBatch(L"batch-retry") && retryBatchCoordinator.RunNext(successExecutor) &&
        !retryBatchCoordinator.RunNext(successExecutor),
        "batch retry requires fresh consent and replays only the unsuccessful child, never a completed command");

    PackageMutationCoordinator capacityBatchCoordinator;
    std::vector<PackageMutationRequest> capacityBatchRequests;
    for (std::size_t index = 0; index < MaximumPackageMutationBatchRecords + 1; ++index)
    {
        capacityBatchRequests.push_back(Request(
            L"batch-capacity-" + std::to_wstring(index),
            L"Contoso.BatchCapacity" + std::to_wstring(index)));
    }
    auto capacityBatch = capacityBatchCoordinator.SubmitBatch(Batch(L"batch-overflow", std::move(capacityBatchRequests)));
    Expect(!capacityBatch.accepted &&
        capacityBatch.batch.diagnostic == L"batch-review-capacity-exceeded" &&
        capacityBatchCoordinator.Snapshot().empty(),
        "batch review rejects more than the visible reviewed-command capacity atomically");

    PackageMutationCoordinator partialBatchCapacityCoordinator;
    auto partialBatch = partialBatchCapacityCoordinator.SubmitBatch(Batch(
        L"batch-capacity-preserve",
        {
            Request(L"batch-capacity-preserve-success", L"Contoso.BatchCapacitySuccess"),
            Request(L"batch-capacity-preserve-queued", L"Contoso.BatchCapacityQueued"),
        }));
    auto partialBatchCompleted = partialBatch.accepted &&
        partialBatchCapacityCoordinator.ConfirmBatch(L"batch-capacity-preserve") &&
        partialBatchCapacityCoordinator.RunNext(successExecutor);
    bool partialBatchFilled = partialBatchCompleted;
    for (std::size_t index = 0; index < MaximumPackageMutationRecords - 2; ++index)
    {
        partialBatchFilled = partialBatchFilled && partialBatchCapacityCoordinator.Submit(
            Request(
                L"partial-batch-capacity-" + std::to_wstring(index),
                L"Contoso.PartialBatchCapacity" + std::to_wstring(index))).accepted;
    }
    auto partialBatchOverflow = partialBatchCapacityCoordinator.Submit(
        Request(L"partial-batch-capacity-overflow", L"Contoso.PartialBatchCapacityOverflow"));
    auto partialBatchSnapshot = partialBatchCapacityCoordinator.SnapshotBatches();
    Expect(partialBatchFilled && !partialBatchOverflow.accepted &&
        partialBatchOverflow.record.diagnostic == L"mutation-queue-capacity-reached" &&
        partialBatchSnapshot.size() == 1 && partialBatchSnapshot.front().records.size() == 2 &&
        partialBatchSnapshot.front().records[0].state == PackageMutationState::Succeeded &&
        partialBatchSnapshot.front().records[1].state == PackageMutationState::Queued,
        "capacity never evicts a completed child from a still-active batch");

    PackageMutationCoordinator customArgumentsCoordinator;
    auto customArguments = Request(L"custom-arguments", L"Contoso.CustomArguments");
    customArguments.install_options.custom_args_install = L"--auth=secret-value";
    auto customArgumentsRejected = customArgumentsCoordinator.Submit(std::move(customArguments));
    Expect(!customArgumentsRejected.accepted &&
        customArgumentsRejected.record.diagnostic == L"custom-mutation-arguments-unsupported" &&
        customArgumentsRejected.record.command_preview.empty() &&
        customArgumentsRejected.record.request.install_options.custom_args_install.empty() &&
        customArgumentsCoordinator.Snapshot().empty(),
        "custom mutation arguments are rejected without retaining possible credentials");

    PackageMutationCoordinator capacityCoordinator;
    bool filledCapacity = true;
    for (std::size_t index = 0; index < MaximumPackageMutationRecords; ++index)
    {
        auto submission = capacityCoordinator.Submit(
            Request(L"capacity-" + std::to_wstring(index), L"Contoso.Capacity" + std::to_wstring(index)));
        filledCapacity = filledCapacity && submission.accepted;
    }
    auto overflow = capacityCoordinator.Submit(Request(L"capacity-overflow", L"Contoso.CapacityOverflow"));
    Expect(filledCapacity && !overflow.accepted &&
        overflow.record.diagnostic == L"mutation-queue-capacity-reached" &&
        capacityCoordinator.Snapshot().size() == MaximumPackageMutationRecords,
        "pending mutation queue rejects a request when its bounded capacity is full");
    Expect(capacityCoordinator.Cancel(L"capacity-0") &&
        capacityCoordinator.Submit(Request(L"capacity-reclaimed", L"Contoso.CapacityReclaimed")).accepted &&
        capacityCoordinator.Snapshot().size() == MaximumPackageMutationRecords,
        "terminal mutation history is reclaimed before accepting another reviewed request");

    std::wstring oversized(MaximumPackageMutationOutputTail + 300, L'x');
    oversized += L" password=oversized-secret";
    auto redactedTail = RedactPackageMutationText(oversized);
    Expect(redactedTail.size() <= MaximumPackageMutationOutputTail &&
        redactedTail.find(L"oversized-secret") == std::wstring::npos,
        "redacted operation text remains bounded by the output-tail limit");
    auto adversarialPreview = RedactPackageMutationText(
        L"--api-key=hyphen-secret --access_token=underscore-secret "
        L"{\"token\":\"json-secret\"} https://uri-secret@example.test/");
    Expect(adversarialPreview.find(L"hyphen-secret") == std::wstring::npos &&
        adversarialPreview.find(L"underscore-secret") == std::wstring::npos &&
        adversarialPreview.find(L"json-secret") == std::wstring::npos &&
        adversarialPreview.find(L"uri-secret") == std::wstring::npos,
        "reviewed command previews redact adversarial secret-like values");

    std::cout << counts.passed << " package-mutation-coordinator tests passed, "
        << counts.failed << " failed\n";
    return counts;
}
