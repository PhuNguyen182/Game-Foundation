using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.Core.Migration;
using DracoRuan.Foundation.DataFlow.Runtime;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.Utilities.SceneUtils;
using VContainer.Unity;

namespace Test
{
    /// <summary>
    /// The one boot entry point. Runs save-data migration, then loads the repositories, then the
    /// remaining services, then hands off to the first scene.
    /// </summary>
    /// <remarks>
    /// <para><b>Why everything is sequenced here rather than split across entry points.</b>
    /// VContainer resolves the <c>IStartable</c> list long before the <c>IAsyncStartable</c> list,
    /// and resolving constructs those objects — so an <c>IStartable</c> taking a repository in its
    /// constructor brings it to life well before any async startable runs. Both are also dispatched
    /// to the same <c>PlayerLoopTiming.Startup</c>, and every <c>IAsyncStartable</c> is started in
    /// the same frame and forgotten. Registration order therefore guarantees nothing, and a second
    /// async entry point could not be made to wait for this one.</para>
    ///
    /// <para><see cref="DataFlowGate"/> backs this up independently of construction order: a
    /// repository cannot touch the disk before the gate opens, no matter what caused it to exist.</para>
    /// </remarks>
    public sealed class AppInitializationPipelineEntryPoint : IAsyncStartable
    {
        private const string LogTag = "AppInit";

        /// <summary>
        /// How long a single service may take before it is reported. The previous implementation
        /// polled <c>IsInitialized</c> every frame with no timeout, so a service that never
        /// finished left the game on a black screen with nothing in the log.
        /// </summary>
        private static readonly TimeSpan ServiceInitializeTimeout = TimeSpan.FromSeconds(30);

        private readonly MigrationBootstrapper _migration;
        private readonly DataFlowGate _gate;
        private readonly IReadOnlyList<IDataController> _dataControllers;
        private readonly IReadOnlyList<IAsyncInitializable> _asyncInitializables;

        public AppInitializationPipelineEntryPoint(
            MigrationBootstrapper migration,
            DataFlowGate gate,
            IReadOnlyList<IDataController> dataControllers,
            IReadOnlyList<IAsyncInitializable> asyncInitializables)
        {
            this._migration = migration;
            this._gate = gate;
            this._dataControllers = dataControllers;
            this._asyncInitializables = asyncInitializables;
        }

        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            if (!await this.RunMigrationAsync(cancellation))
                return;

            await this.InitializeDataControllersAsync(cancellation);
            await this.InitializeServicesAsync(cancellation);

            if (cancellation.IsCancellationRequested)
                return;

            await SceneUtil.LoadScene("Loading");
        }

        /// <summary>
        /// Upgrades save data. Touches only files — no repository exists yet.
        /// </summary>
        /// <returns>False when boot must stop.</returns>
        private async UniTask<bool> RunMigrationAsync(CancellationToken cancellation)
        {
            try
            {
                MigrationOutcome outcome = await this._migration.RunAsync(cancellation);

                if (!outcome.Succeeded)
                {
                    // Some domains could not be upgraded. Their files were left untouched and rolled
                    // back, so the rest of the game can still run on what did migrate. Continuing is
                    // the right default: a single failed domain should not brick the app.
                    Debug.LogError(
                        $"[{LogTag}] {outcome.Failures.Count} save domain(s) could not be migrated. " +
                        "Their data has been left unchanged. Affected: " +
                        string.Join("; ", outcome.Failures));
                }

                this._gate.Open();
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                // Migration itself broke, not just one domain. Fault the gate so nothing writes a
                // save - an autosave here would overwrite un-migrated data and make it permanent.
                this._gate.Fail(exception);

                Debug.LogError(
                    $"[{LogTag}] Save data migration failed: {exception}. " +
                    "Saving is disabled for this session to avoid overwriting player data.");

                return false;
            }
        }

        private async UniTask InitializeDataControllersAsync(CancellationToken cancellation)
        {
            if (this._dataControllers == null)
                return;

            foreach (IDataController controller in this._dataControllers)
            {
                if (cancellation.IsCancellationRequested)
                    return;

                try
                {
                    await controller.InitializeAsync(cancellation);
                }
                catch (Exception exception)
                {
                    // One repository failing to load must not stop the others; it falls back to
                    // defaults and the game continues.
                    Debug.LogError(
                        $"[{LogTag}] Failed to initialize '{controller.DomainId}': {exception.Message}");
                }
            }
        }

        private async UniTask InitializeServicesAsync(CancellationToken cancellation)
        {
            if (this._asyncInitializables == null)
                return;

            foreach (IAsyncInitializable initializable in this._asyncInitializables)
            {
                if (cancellation.IsCancellationRequested)
                    return;

                string serviceName = initializable.GetType().Name;

                try
                {
                    await UniTask
                        .WaitUntil(initializable.IsInitialized, cancellationToken: cancellation)
                        .Timeout(ServiceInitializeTimeout);
                }
                catch (TimeoutException)
                {
                    // Name the culprit. Without this the symptom is an indefinite black screen.
                    Debug.LogError(
                        $"[{LogTag}] '{serviceName}' did not finish initializing within " +
                        $"{ServiceInitializeTimeout.TotalSeconds:0}s. Continuing without it.");
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}