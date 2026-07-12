using System;
using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.Localization
{
    public interface ILocalizationService
    {
        public string CurrentLanguageCode { get; }
        public event Action<string> OnLanguageChanged;
        
        public List<(string, string)> GetAvailableLanguages();
        public string GetTranslation(string tableKey, string localizeKey);
        public string GetTranslation(string tableKey, string localizeKey, params object[] arguments);
        public bool SwitchLanguage(string languageCode);
    }
}
