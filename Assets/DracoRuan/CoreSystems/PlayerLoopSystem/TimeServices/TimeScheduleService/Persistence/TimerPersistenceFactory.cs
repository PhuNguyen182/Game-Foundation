namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Persistence
{
    /// <summary>
    /// Factory để tạo ITimerPersistence instance theo type
    /// </summary>
    public static class TimerPersistenceFactory
    {
        /// <summary>
        /// Tạo ITimerPersistence instance theo type
        /// </summary>
        /// <param name="persistenceType">Loại persistence</param>
        /// <returns>ITimerPersistence instance</returns>
        public static ITimerPersistence Create(TimerPersistenceType persistenceType)
        {
            return persistenceType switch
            {
                TimerPersistenceType.File => CreateFilePersistence(),
                TimerPersistenceType.PlayerPrefs => CreatePlayerPrefsPersistence(),
                _ => CreateFilePersistence() // Default to File
            };
        }

        /// <summary>
        /// Tạo file-based persistence (Recommended)
        /// </summary>
        public static ITimerPersistence CreateFilePersistence()
        {
            Debug.Log("[TimerPersistenceFactory] Creating FileTimerPersistence (Recommended)");
            return new FileTimerPersistence();
        }

        /// <summary>
        /// Tạo PlayerPrefs-based persistence
        /// </summary>
        private static ITimerPersistence CreatePlayerPrefsPersistence()
        {
            Debug.Log("[TimerPersistenceFactory] Creating PlayerPrefsTimerPersistence");
            return new PlayerPrefsTimerPersistence();
        }
    }
}

