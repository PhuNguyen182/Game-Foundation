using System;
using System.Collections.Generic;

namespace DracoRuan.Foundation.DataFlow.StaticData
{
    /// <summary>One registered static data table, known before any controller is constructed.</summary>
    /// <remarks>
    /// Deliberately does not carry the data's type. Registration sees only the controller type, and
    /// the data type sits behind a generic base class it would have to be dug out of by reflection —
    /// so the field would be null at every call site that mattered. Anything that genuinely needs the
    /// type can ask a constructed controller for <c>DataType</c>.
    /// </remarks>
    public sealed class StaticDataDescriptor
    {
        public StaticDataDescriptor(string dataId, Type controllerType)
        {
            this.DataId = dataId;
            this.ControllerType = controllerType;
        }

        public string DataId { get; }
        public Type ControllerType { get; }
    }

    /// <summary>
    /// Every static data table this build declares.
    /// </summary>
    /// <remarks>
    /// <para>Mirrors <c>DataDomainRegistry</c> on the save side, and exists for the same reason:
    /// tooling and diagnostics need to know what tables exist without constructing controllers, and
    /// duplicate ids need to be caught at registration rather than surfacing later as one table
    /// mysteriously holding another's data.</para>
    /// </remarks>
    public sealed class StaticDataRegistry
    {
        private readonly Dictionary<string, StaticDataDescriptor> _byId = new();
        private readonly List<StaticDataDescriptor> _ordered = new();

        public IReadOnlyList<StaticDataDescriptor> All => this._ordered;

        public void Add(StaticDataDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            if (string.IsNullOrWhiteSpace(descriptor.DataId))
                throw new ArgumentException(
                    $"{descriptor.ControllerType?.Name ?? "A controller"} registered without a data id.",
                    nameof(descriptor));

            if (this._byId.TryGetValue(descriptor.DataId, out StaticDataDescriptor existing))
                throw new ArgumentException(
                    $"Data id '{descriptor.DataId}' is already registered by {existing.ControllerType.Name}; " +
                    $"{descriptor.ControllerType.Name} cannot reuse it.",
                    nameof(descriptor));

            this._byId.Add(descriptor.DataId, descriptor);
            this._ordered.Add(descriptor);
        }

        public bool TryGet(string dataId, out StaticDataDescriptor descriptor) =>
            this._byId.TryGetValue(dataId, out descriptor);
    }
}