using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.DeleteDynamicData;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.SaveDynamicData;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Orchestrator;
using DracoRuan.Foundation.DataFlow.DataMigration.Migrator;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers;

namespace Temps.Scripts.TestRiseProgressData
{
    [DynamicGameDataController(nameof(RiseProgressionDataController))]
    public class RiseProgressionDataController : DynamicGameDataController<RiseProgressData>
    {
        public RiseProgressionDataController(IDataProviderService dataProviderService, SaveDataEvent saveDataEvent,
            DeleteDataEvent deleteDataEvent, DataMigrationOrchestrator dataMigrationOrchestrator) : base(
            dataProviderService, saveDataEvent, deleteDataEvent, dataMigrationOrchestrator)
        {
        }

        protected override RiseProgressData SourceData { get; set; }
        public override int CurrentDataVersion => 1;
        
        protected override void LoadDataFromLatestVersion(MigrationContext migrationContext)
        {
            
        }
    }
}