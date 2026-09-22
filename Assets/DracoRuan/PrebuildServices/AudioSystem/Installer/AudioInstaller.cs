using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.AudioSystem.Installer
{
    /// <summary>
    /// Drops the audio service into a lifetime scope, carrying its config and collection.
    /// </summary>
    [AutoInstall(InstallerInstanceType = nameof(InstallerType.ScriptableObject))]
    [CreateAssetMenu(fileName = "AudioInstaller", menuName = "DracoRuan/AudioSystem/AudioInstaller")]
    public class AudioInstaller : ScriptableObject, IInstaller
    {
        [SerializeField] private AudioConfig config;
        [SerializeField] private AudioCollection collection;

        public void Install(IContainerBuilder builder) =>
            builder.AddAudioService(this.config, this.collection);
    }
}
