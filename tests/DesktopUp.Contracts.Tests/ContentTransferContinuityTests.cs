using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentTransferContinuityTests
{
    private static ContentTransferGesture NewGesture() => new(new(Guid.NewGuid(), "A", 1), [Guid.NewGuid(), Guid.NewGuid()]);
    [Fact]
    public void InternalMoveAndExternalReturnHaveOneCommitOwner()
    {
        var g = NewGesture(); var t = g.Token;
        Assert.Equal(ContentTransferGestureEffect.PreviewLayout, g.Apply(t, 1, ContentTransferGestureSignal.InternalMotion));
        Assert.Equal(ContentTransferGestureEffect.ClearLayoutPreview | ContentTransferGestureEffect.StartNativeOnce,
            g.Apply(t, 2, ContentTransferGestureSignal.ExternalMotion));
        Assert.Equal(ContentTransferGestureEffect.None, g.Apply(t, 3, ContentTransferGestureSignal.ExternalMotion));
        Assert.Equal(ContentTransferGestureEffect.None, g.Apply(t, 4, ContentTransferGestureSignal.LocalRelease, true));
        Assert.Equal(ContentTransferGestureEffect.CommitLayoutOnce, g.Apply(t, 5, ContentTransferGestureSignal.NativeReturnedInside, true));
        Assert.Equal(ContentTransferGestureEffect.None, g.Apply(t, 6, ContentTransferGestureSignal.NativeReturnedInside, true));
        Assert.True(g.SuppressOpen);
    }
    [Fact]
    public void ExternalAcceptanceIsNotFileCompletionOrASecondLayoutCommit()
    {
        var g = NewGesture(); var t = g.Token;
        g.Apply(t, 1, ContentTransferGestureSignal.ExternalMotion);
        Assert.Equal(ContentTransferGestureEffect.AwaitFileResult, g.Apply(t, 2, ContentTransferGestureSignal.NativeDroppedOutside));
        Assert.Equal(ContentTransferGesturePhase.AwaitingResult, g.Phase);
        Assert.Equal(ContentTransferGestureEffect.None, g.Apply(t, 3, ContentTransferGestureSignal.LocalRelease, true));
    }
    [Theory]
    [InlineData(ContentTransferGestureSignal.Cancel)]
    [InlineData(ContentTransferGestureSignal.Invalidate)]
    public void EscHideExitCancelPreviewButLateNativeAcceptanceMustStillReconcile(ContentTransferGestureSignal cancel)
    {
        var g = NewGesture(); var t = g.Token;
        g.Apply(t, 1, ContentTransferGestureSignal.ExternalMotion);
        Assert.True(g.Apply(t, 2, cancel).HasFlag(ContentTransferGestureEffect.CancelNative));
        Assert.Equal(ContentTransferGesturePhase.CancelPending, g.Phase);
        Assert.Equal(ContentTransferGestureEffect.AwaitFileResult, g.Apply(t, 3, ContentTransferGestureSignal.NativeDroppedOutside));
    }
    [Fact]
    public void CancelDuringInternalDragNeverStartsAFileAction()
    {
        var g = NewGesture(); g.Apply(g.Token, 1, ContentTransferGestureSignal.InternalMotion);
        Assert.Equal(ContentTransferGestureEffect.ClearLayoutPreview, g.Apply(g.Token, 2, ContentTransferGestureSignal.Cancel));
        Assert.Equal(ContentTransferGestureEffect.None, g.Apply(g.Token, 3, ContentTransferGestureSignal.ExternalMotion));
    }
    [Fact]
    public void NoMotionRemainsACompletedClickButNotAnOpenCommand()
    {
        var g = NewGesture();
        Assert.Equal(ContentTransferGestureEffect.CompleteClick, g.Apply(g.Token, 1, ContentTransferGestureSignal.LocalRelease));
        Assert.False(g.SuppressOpen); // Existing ContentActivationSession alone decides double/Enter.
    }
    [Fact]
    public void OldGenerationCaptureAndSequenceCannotStealCurrentGesture()
    {
        var g = NewGesture(); var t = g.Token;
        Assert.Equal(ContentTransferGestureEffect.None, g.Apply(t with { Generation = 2 }, 99, ContentTransferGestureSignal.ExternalMotion));
        Assert.Equal(ContentTransferGestureEffect.None, g.Apply(t with { CaptureId = Guid.NewGuid() }, 99, ContentTransferGestureSignal.Cancel));
        g.Apply(t, 2, ContentTransferGestureSignal.InternalMotion);
        Assert.Equal(ContentTransferGestureEffect.None, g.Apply(t, 1, ContentTransferGestureSignal.ExternalMotion));
        Assert.Equal(ContentTransferGestureEffect.None, g.Apply(t, 2, ContentTransferGestureSignal.ExternalMotion));
        Assert.Equal(ContentTransferGesturePhase.Internal, g.Phase);
    }
    [Fact]
    public void SelectionIsFrozenAndPosterGesturesAreIsolated()
    {
        var cards = new List<Guid> { Guid.NewGuid() };
        var a = new ContentTransferGesture(new(Guid.NewGuid(), "A", 1), cards);
        var b = NewGesture(); cards.Clear();
        Assert.Single(a.SelectedCards);
        a.Apply(a.Token, 1, ContentTransferGestureSignal.ExternalMotion);
        Assert.Equal(ContentTransferGesturePhase.Pressed, b.Phase);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCancellationAndRejectedInternalReturnDoNotCommit(bool invalidReturn)
    {
        var g = NewGesture(); g.Apply(g.Token, 1, ContentTransferGestureSignal.ExternalMotion);
        Assert.Equal(ContentTransferGestureEffect.ClearLayoutPreview,
            g.Apply(g.Token, 2, invalidReturn ? ContentTransferGestureSignal.NativeReturnedInside : ContentTransferGestureSignal.NativeCancelled));
        Assert.Equal(ContentTransferGesturePhase.Cancelled, g.Phase);
    }
    [Fact]
    public void CancelPendingCannotCommitEvenIfReturnPreviewWasValid()
    {
        var g = NewGesture(); g.Apply(g.Token, 1, ContentTransferGestureSignal.ExternalMotion);
        g.Apply(g.Token, 2, ContentTransferGestureSignal.Cancel);
        Assert.Equal(ContentTransferGestureEffect.ClearLayoutPreview, g.Apply(g.Token, 3, ContentTransferGestureSignal.NativeReturnedInside, true));
    }

    [Theory]
    [InlineData(TransferObjectObservation.Absent, TransferObjectObservation.ExpectedObject, true, TransferRecoveryAction.ReconcileIndex)]
    [InlineData(TransferObjectObservation.ExpectedObject, TransferObjectObservation.ExpectedObject, true, TransferRecoveryAction.KeepBothForReview)]
    [InlineData(TransferObjectObservation.ExpectedObject, TransferObjectObservation.Absent, false, TransferRecoveryAction.RetryOnlyAfterRevalidation)]
    [InlineData(TransferObjectObservation.Absent, TransferObjectObservation.Absent, false, TransferRecoveryAction.LocateMissing)]
    [InlineData(TransferObjectObservation.DifferentObject, TransferObjectObservation.ExpectedObject, true, TransferRecoveryAction.PreserveConflict)]
    [InlineData(TransferObjectObservation.Absent, TransferObjectObservation.DifferentObject, true, TransferRecoveryAction.PreserveConflict)]
    [InlineData(TransferObjectObservation.Unknown, TransferObjectObservation.ExpectedObject, true, TransferRecoveryAction.Reinspect)]
    [InlineData(TransferObjectObservation.Absent, TransferObjectObservation.ExpectedObject, false, TransferRecoveryAction.Reinspect)]
    public void RecoveryUsesObservedIdentityNotTheGestureResult(TransferObjectObservation source, TransferObjectObservation destination,
        bool integrity, TransferRecoveryAction expected) =>
        Assert.Equal(expected, ContentTransferRecoveryPolicy.Decide(TransferItemPhase.Executing, new(source, destination, integrity)));

    [Fact]
    public void DurableIntentCurrentLeaseAndNotCancelledAreAllRequiredBeforeExecution()
    {
        foreach (var phase in Enum.GetValues<TransferItemPhase>())
        foreach (bool current in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
            Assert.Equal(phase == TransferItemPhase.IntentDurable && current && !cancel,
                ContentTransferRecoveryPolicy.CanStartExecution(phase, current, cancel));
    }

    [Fact]
    public void FileSuccessWithDatabaseConflictRemainsRecoverableWithoutMovingBack()
    {
        var evidence = new TransferItemEvidence(TransferObjectObservation.Absent, TransferObjectObservation.ExpectedObject, true);
        Assert.False(ContentTransferRecoveryPolicy.CanCommitIndex(TransferItemPhase.FileVerified, evidence, false));
        Assert.Equal(TransferRecoveryAction.ReconcileIndex, ContentTransferRecoveryPolicy.Decide(TransferItemPhase.FileVerified, evidence));
        Assert.True(ContentTransferRecoveryPolicy.CanCommitIndex(TransferItemPhase.FileVerified, evidence, true));
        Assert.Equal(TransferRecoveryAction.WriteIntentFirst, ContentTransferRecoveryPolicy.Decide(TransferItemPhase.Planned, evidence));
        Assert.Equal(TransferRecoveryAction.Done, ContentTransferRecoveryPolicy.Decide(TransferItemPhase.IndexCommitted, evidence));
    }

    [Fact]
    public void PartialBatchKeepsEachItemsOwnResult()
    {
        TransferItemEvidence[] results =
        [
            new(TransferObjectObservation.Absent, TransferObjectObservation.ExpectedObject, true),
            new(TransferObjectObservation.ExpectedObject, TransferObjectObservation.Absent, false),
            new(TransferObjectObservation.ExpectedObject, TransferObjectObservation.ExpectedObject, true)
        ];
        Assert.Equal(new[] { TransferRecoveryAction.ReconcileIndex, TransferRecoveryAction.RetryOnlyAfterRevalidation, TransferRecoveryAction.KeepBothForReview },
            results.Select(e => ContentTransferRecoveryPolicy.Decide(TransferItemPhase.Executing, e)));
        Assert.Equal(1, results.Count(e => ContentTransferRecoveryPolicy.CanCommitIndex(TransferItemPhase.Executing, e, true)));
    }
}

