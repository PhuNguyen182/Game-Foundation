using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.UISystem.Input.DracoRuan.PrebuildServices.UISystem.InputSystem
{
    /// <summary>Container-friendly holder, since either value may legitimately be null.</summary>
    public sealed class UIInputSystemOptions
    {
        public readonly UIInputActions Config;
        public readonly InputActionAsset RuntimeAsset;

        public UIInputSystemOptions(UIInputActions config, InputActionAsset runtimeAsset)
        {
            this.Config = config;
            this.RuntimeAsset = runtimeAsset;
        }
    }

    /// <summary>
    /// Wires UIService to the Input System once the container is built: configures the
    /// EventSystem's input module from UIInputActions, and attaches the Back source and the
    /// focus controller. Actions borrowed from the game are never enabled/disabled/disposed
    /// here (so runtime rebinding keeps working); only defaults created by the sources are owned.
    /// </summary>
    public sealed class UIInputSystemBinder : IStartable, IDisposable
    {
        private readonly UIService _service;
        private readonly UIInputActions _config;
        private readonly InputActionAsset _runtimeAsset;
        private readonly InputSystemBackInputSource _backSource;
        private readonly List<InputActionReference> _createdReferences = new();

        private UIFocusController _focusController;

        public UIInputSystemBinder(UIService service, UIInputSystemOptions options,
            InputSystemBackInputSource backSource)
        {
            this._service = service;
            this._config = options.Config;
            this._runtimeAsset = options.RuntimeAsset;
            this._backSource = backSource;
        }

        public void Start()
        {
            EventSystem eventSystem = this._service.EventSystem;
            if (eventSystem)
            {
                RemoveLegacyModules(eventSystem);
                var module = eventSystem.GetComponent<InputSystemUIInputModule>();
                if (!module)
                    module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

                if (this._config)
                    this._config.ApplyTo(module, this._runtimeAsset, this._createdReferences);

                this._focusController = new UIFocusController(eventSystem, this.NavigationReferences(module));
                this._service.AttachFocusHandler(this._focusController);
            }

            this._service.AttachBackInputSource(this._backSource);
        }

        private InputActionReference[] NavigationReferences(InputSystemUIInputModule module) =>
            new[] { module.move, module.submit };

        private static void RemoveLegacyModules(EventSystem eventSystem)
        {
            foreach (StandaloneInputModule legacy in eventSystem.GetComponents<StandaloneInputModule>())
                UnityEngine.Object.Destroy(legacy);
        }

        public void Dispose()
        {
            this._service.DetachBackInputSource(this._backSource);
            this._service.DetachFocusHandler(this._focusController);
            this._focusController?.Dispose();

            foreach (InputActionReference created in this._createdReferences)
            {
                if (created)
                    UnityEngine.Object.Destroy(created);
            }
        }
    }
}
