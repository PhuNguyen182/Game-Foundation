using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.Interfaces;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace DracoRuan.CoreSystems.Localization
{
    public class UnityLocalizationService : ILocalizationService, IAsyncInitializable
    {
        private bool _isInitialized;
        
        public string CurrentLanguageCode => LocalizationSettings.SelectedLocale.Identifier.Code;

        public UnityLocalizationService()
        {
            this._isInitialized = false;
            InitializeLocalization().Forget();
            return;

            async UniTask InitializeLocalization()
            {
                await LocalizationSettings.InitializationOperation;
                this._isInitialized = true;
            }
        }
        
        public string GetTranslation(string tableKey, string localizeKey)
        {
            string result = LocalizationSettings.StringDatabase.GetLocalizedString(tableKey, localizeKey);
            return result;
        }

        public string GetTranslation(string tableKey, string localizeKey, params object[] arguments)
        {
            string result = LocalizationSettings.StringDatabase.GetLocalizedString(tableKey, localizeKey, arguments: arguments);
            return result;
        }

        public bool SwitchLanguage(string languageCode)
        {
            Locale foundLocale = LocalizationSettings.AvailableLocales.GetLocale(languageCode);
            if (foundLocale)
            {
                LocalizationSettings.SelectedLocale = foundLocale;
                Debug.Log($"Successfully switched language to: {languageCode}");
                return true;
            }

            Debug.LogError($"Could not find an available Locale matching code: {languageCode}");
            return false;
        }

        public List<(string, string)> GetAvailableLanguages()
        {
            List<(string, string)> availableLanguages = new();
            int availableLanguageCount = LocalizationSettings.AvailableLocales.Locales.Count;
            for (int i = 0; i < availableLanguageCount; i++)
            {
                Locale locale = LocalizationSettings.AvailableLocales.Locales[i];
                (string languageCode, string languageName) = (locale.Identifier.Code, locale.name);
                availableLanguages.Add((languageCode, languageName));
            }
            
            return availableLanguages;
        }

        public bool IsInitialized() => this._isInitialized;
    }
}