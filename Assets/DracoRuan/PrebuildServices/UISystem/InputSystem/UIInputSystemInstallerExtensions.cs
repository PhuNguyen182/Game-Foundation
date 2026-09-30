using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.UISystem.Input.DracoRuan.PrebuildServices.UISystem.InputSystem
{
    public static class UIInputSystemInstallerExtensions
    {
        /// <summary>
        /// Registers the Input System side of UISystem: the InputSystemUIInputModule setup, Back
        /// routing, PC/console focus and tab navigation (inject IUITabNavigationSource into the
        /// screen and pass it to UITabGroup.AttachNavigation). Call after AddUIService.
        /// </summary>
        /// <param name="config">Which of the game's actions drive the UI; null uses defaults.</param>
        /// <param name="runtimeAsset">PlayerInput.actions (the per-player clone), when the game
        /// uses PlayerInput; actions are then resolved by id inside that clone.</param>
        public static void AddUIInputSystem(this IContainerBuilder builder, UIInputActions config,
            InputActionAsset runtimeAsset = null)
        {
            builder.RegisterInstance(new UIInputSystemOptions(config, runtimeAsset));

            builder.Register(_ => new InputSystemBackInputSource(
                UIInputActions.Resolve(config ? config.cancel : null, runtimeAsset)), Lifetime.Singleton);

            builder.Register(_ => new InputSystemTabNavigationSource(
                    UIInputActions.Resolve(config ? config.previousTab : null, runtimeAsset),
                    UIInputActions.Resolve(config ? config.nextTab : null, runtimeAsset)), Lifetime.Singleton)
                .As<IUITabNavigationSource>();

            builder.RegisterEntryPoint<UIInputSystemBinder>();
        }
    }
}
