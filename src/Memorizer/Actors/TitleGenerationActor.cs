using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Memorizer.Models;
using Memorizer.Services;
using Memorizer.Services.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Memorizer.Actors;

/// <summary>
/// Actor responsible for generating titles for memories that don't have them.
/// Uses Become/Unbecome to switch between Idle and Running states.
/// Progress is managed via ProgressJobManager which supports multiple SSE subscribers.
/// </summary>
public sealed class TitleGenerationActor : ReceiveActor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggingAdapter _logger;
    private readonly IMaterializer _materializer;

    // Progress manager - handles subscriber management and job state
    private ProgressJobManager? _jobManager;

    // Current scope for the running job
    private IServiceScope? _currentScope;

    public TitleGenerationActor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = Context.GetLogger();
        _materializer = Context.System.Materializer();

        // Start in Idle state
        Idle();
    }

    private void Idle()
    {
        // Idle behavior - waiting for work
        ReceiveAsync<GenerateTitlesForUntitled>(HandleGenerateTitlesForUntitled);

        // Handle subscription requests - return idle status that completes immediately
        Receive<SubscribeToProgress>(msg =>
        {
            _logger.Debug("Subscription requested while idle, subscriber: {0}", msg.SubscriberId);
            // Create a temporary job manager just to create an idle subscription
            var tempManager = new ProgressJobManager(_logger, _materializer);
            var reader = tempManager.CreateIdleSubscription(msg.SubscriberId);
            Sender.Tell(new ProgressSubscription(msg.SubscriberId, reader));
        });

        Receive<UnsubscribeFromProgress>(msg =>
        {
            _logger.Debug("Unsubscribe requested while idle, subscriber: {0}", msg.SubscriberId);
            // No active job manager, nothing to clean up
        });

        Receive<GetTitleGenerationStatus>(_ => HandleGetStatusIdle());
    }

    private void Running()
    {
        // Running behavior - actively processing batch
        ReceiveAsync<GenerateTitleForMemory>(HandleGenerateTitleForMemory);
        Receive<TitleGenerationCompleted>(HandleTitleGenerationCompleted);
        Receive<TitleGenerationFailed>(HandleTitleGenerationFailed);

        // Handle subscription requests - add to active job
        Receive<SubscribeToProgress>(msg =>
        {
            if (_jobManager != null)
            {
                _logger.Debug("Adding subscriber to running job: {0}", msg.SubscriberId);
                var reader = _jobManager.AddSubscriber(msg.SubscriberId);
                Sender.Tell(new ProgressSubscription(msg.SubscriberId, reader));
            }
        });

        Receive<UnsubscribeFromProgress>(msg =>
        {
            _logger.Debug("Removing subscriber: {0}", msg.SubscriberId);
            _jobManager?.RemoveSubscriber(msg.SubscriberId);
        });

        Receive<GetTitleGenerationStatus>(_ => HandleGetStatusRunning());
    }

    private async Task HandleGenerateTitlesForUntitled(GenerateTitlesForUntitled message)
    {
        // Capture sender for reply - needed because we're in an async method
        var sender = Sender;

        _logger.Info("Starting batch title generation for up to {0} untitled memories, requested by {1}",
            message.BatchSize, message.RequestedBy);

        try
        {
            // Create a scope for the duration of this job
            _currentScope = _serviceProvider.CreateScope();
            var storage = _currentScope.ServiceProvider.GetRequiredService<IStorage>();

            // Get memories without titles first to size the job
            var untitledMemories = await storage.GetMemoriesWithoutTitles(message.BatchSize);

            // Create job manager and start job (this sizes the job)
            _jobManager = new ProgressJobManager(_logger, _materializer);
            _jobManager.StartJob(untitledMemories.Count, message.RequestedBy);

            Become(Running);

            // Reply with initial status BEFORE processing starts - this ensures
            // the HTTP request completes and SSE can connect while job is Running
            sender.Tell(new TitleGenerationStatus(
                IsRunning: true,
                Status: "Running",
                TotalProcessed: 0,
                TotalSuccessful: 0,
                TotalFailed: 0,
                Outstanding: untitledMemories.Count,
                FailedMemoryIds: [],
                StartTime: _jobManager.StartTime,
                Duration: TimeSpan.Zero,
                RequestedBy: message.RequestedBy
            ));

            if (untitledMemories.Count == 0)
            {
                _logger.Info("No untitled memories found");
                CompleteBatch();
                return;
            }

            _logger.Info("Found {0} untitled memories to process", untitledMemories.Count);

            // Process each memory
            foreach (var memory in untitledMemories)
            {
                var generateMessage = new GenerateTitleForMemory
                {
                    MemoryId = memory.Id,
                    Content = memory.Text,
                    Type = memory.Type,
                    Tags = memory.Tags,
                    RequestedBy = message.RequestedBy
                };

                // Send to self for processing
                Self.Tell(generateMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error starting batch title generation: {0}", ex.Message);
            sender.Tell(new TitleGenerationStatus(
                IsRunning: false,
                Status: "Failed",
                TotalProcessed: 0,
                TotalSuccessful: 0,
                TotalFailed: 0,
                Outstanding: 0
            ));
            _jobManager?.Fail(ex.Message);
            _jobManager = null;
            _currentScope?.Dispose();
            _currentScope = null;
            Become(Idle);
        }
    }

    private async Task HandleGenerateTitleForMemory(GenerateTitleForMemory message)
    {
        if (_currentScope == null) return;

        _logger.Debug("Generating title for memory {0}", message.MemoryId);

        try
        {
            var storage = _currentScope.ServiceProvider.GetRequiredService<IStorage>();
            var memorizerAgent = _currentScope.ServiceProvider.GetRequiredService<IMemorizerAgentProvider>();

            // Generate title using Memorizer Agent provider
            var title = await memorizerAgent.GenerateTitleAsync(
                message.Content,
                message.Type,
                message.Tags,
                maxTitleLength: 80);

            // Update the memory with the generated title
            await storage.UpdateMemoryTitle(message.MemoryId, title);

            _logger.Debug("Successfully generated title '{0}' for memory {1}", title, message.MemoryId);

            var completed = new TitleGenerationCompleted
            {
                MemoryId = message.MemoryId,
                GeneratedTitle = title,
                RequestedBy = message.RequestedBy
            };

            // Publish completion event
            Context.System.EventStream.Publish(completed);

            // Handle completion in batch context
            Self.Tell(completed);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error generating title for memory {0}: {1}", message.MemoryId, ex.Message);

            var failed = new TitleGenerationFailed
            {
                MemoryId = message.MemoryId,
                ErrorMessage = ex.Message,
                RequestedBy = message.RequestedBy,
                Exception = ex
            };

            // Publish failure event
            Context.System.EventStream.Publish(failed);

            // Handle failure in batch context
            Self.Tell(failed);
        }
    }

    private void HandleTitleGenerationCompleted(TitleGenerationCompleted message)
    {
        if (_jobManager == null) return;

        _logger.Debug("Title generation completed for memory {0} ({1}/{2})",
            message.MemoryId, _jobManager.ProcessedCount + 1, _jobManager.TotalItems);

        _jobManager.RecordSuccess();
        CheckBatchCompletion();
    }

    private void HandleTitleGenerationFailed(TitleGenerationFailed message)
    {
        if (_jobManager == null) return;

        _logger.Warning("Title generation failed for memory {0} ({1}/{2}): {3}",
            message.MemoryId, _jobManager.ProcessedCount + 1, _jobManager.TotalItems, message.ErrorMessage);

        _jobManager.RecordFailure(message.MemoryId);
        CheckBatchCompletion();
    }

    private void CheckBatchCompletion()
    {
        if (_jobManager != null && _jobManager.ProcessedCount >= _jobManager.TotalItems)
        {
            CompleteBatch();
        }
    }

    private void CompleteBatch()
    {
        PublishBatchCompleted();

        // Complete the job - this broadcasts final event and auto-completes all subscriber streams
        _jobManager?.Complete();
        _jobManager = null;

        // Dispose the scope
        _currentScope?.Dispose();
        _currentScope = null;

        Become(Idle);
    }

    private void PublishBatchCompleted()
    {
        if (_jobManager == null) return;

        var batchCompleted = new BatchTitleGenerationCompleted(
            RequestedBy: _jobManager.RequestedBy,
            StartTime: _jobManager.StartTime,
            TotalProcessed: _jobManager.ProcessedCount,
            TotalSuccessful: _jobManager.SuccessCount,
            FailedMemoryIds: _jobManager.FailedIds.ToList(),
            Duration: DateTime.UtcNow - _jobManager.StartTime
        );

        _logger.Info("Batch title generation completed: {0}/{1} successful, {2} failed, duration: {3}ms",
            _jobManager.SuccessCount, _jobManager.TotalItems, _jobManager.FailureCount,
            batchCompleted.Duration.TotalMilliseconds);

        Context.System.EventStream.Publish(batchCompleted);
    }

    private void HandleGetStatusIdle()
    {
        Sender.Tell(new TitleGenerationStatus(
            IsRunning: false,
            Status: "idle"
        ));
    }

    private void HandleGetStatusRunning()
    {
        if (_jobManager == null)
        {
            HandleGetStatusIdle();
            return;
        }

        Sender.Tell(new TitleGenerationStatus(
            IsRunning: true,
            Status: "Running",
            TotalProcessed: _jobManager.ProcessedCount,
            TotalSuccessful: _jobManager.SuccessCount,
            TotalFailed: _jobManager.FailureCount,
            Outstanding: _jobManager.TotalItems - _jobManager.ProcessedCount,
            FailedMemoryIds: _jobManager.FailedIds.ToList(),
            StartTime: _jobManager.StartTime,
            Duration: DateTime.UtcNow - _jobManager.StartTime,
            RequestedBy: _jobManager.RequestedBy
        ));
    }

}
