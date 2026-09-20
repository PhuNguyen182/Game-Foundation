using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.Core.Storage;

namespace DracoRuan.Foundation.DataFlow.Runtime
{
    /// <summary>
    /// What this build knows about one save domain, without having to construct its controller.
    /// </summary>
    /// <remarks>
    /// Boot-time migration needs each domain's target schema version <i>before</i> any controller
    /// exists — constructing one would make it load the very data that has not been migrated yet.
    /// </remarks>
    public sealed class DataDomainDescriptor
    {
        public DataDomainDescriptor(
            string domainId,
            int targetSchemaVersion,
            Type dataType,
            Type controllerType,
            string legacyTypeName = null)
        {
            // Fully qualified: this class has a DomainId property, which would otherwise shadow the
            // DomainId type and resolve to a string.
            Core.Storage.DomainId.Validate(domainId, nameof(domainId));

            if (targetSchemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(targetSchemaVersion), targetSchemaVersion,
                    $"Domain '{domainId}' must declare a schema version of at least 1.");
            }

            this.DomainId = domainId;
            this.TargetSchemaVersion = targetSchemaVersion;
            this.DataType = dataType;
            this.ControllerType = controllerType;
            this.LegacyTypeName = legacyTypeName ?? dataType?.Name;
        }

        /// <summary>
        /// Stable identifier. Permanent: it names a file on every player's device, so renaming it
        /// orphans their save.
        /// </summary>
        public string DomainId { get; }

        /// <summary>Schema version this build expects.</summary>
        public int TargetSchemaVersion { get; }

        /// <summary>Current version's data class.</summary>
        public Type DataType { get; }

        /// <summary>Controller that owns this domain.</summary>
        public Type ControllerType { get; }

        /// <summary>
        /// Filename stem used by the pre-envelope layout, for the one-time import. Defaults to the
        /// data class's name, which is what the old format used.
        /// </summary>
        public string LegacyTypeName { get; }

        public override string ToString() =>
            $"{this.DomainId} (v{this.TargetSchemaVersion}, {this.DataType?.Name})";
    }

    /// <summary>
    /// Every save domain registered in this build.
    /// </summary>
    /// <remarks>
    /// <para><b>A single registered instance rather than a DI collection, deliberately.</b>
    /// VContainer's <c>CollectionInstanceProvider</c> rejects two singleton registrations that share
    /// an implementation type, so registering each descriptor as
    /// <c>IDataDomainDescriptor</c> would throw on the <i>second</i> domain — the moment the
    /// foundation is used for anything real.</para>
    ///
    /// <para>It also lets duplicate domain ids be caught at registration, where the message can name
    /// both declaring controllers, instead of at some later point where two domains silently share
    /// a file.</para>
    /// </remarks>
    public sealed class DataDomainRegistry
    {
        private readonly List<DataDomainDescriptor> _descriptors = new();
        private readonly Dictionary<string, DataDomainDescriptor> _byId = new(StringComparer.Ordinal);

        public IReadOnlyList<DataDomainDescriptor> Descriptors => this._descriptors;

        public void Add(DataDomainDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            if (this._byId.TryGetValue(descriptor.DomainId, out DataDomainDescriptor existing))
            {
                throw new ArgumentException(
                    $"Domain id '{descriptor.DomainId}' is already registered by " +
                    $"{existing.ControllerType?.Name ?? "<unknown>"}. Two controllers sharing a domain id " +
                    "would read and overwrite each other's save file.");
            }

            this._byId[descriptor.DomainId] = descriptor;
            this._descriptors.Add(descriptor);
        }

        public DataDomainDescriptor Get(string domainId) =>
            this._byId.TryGetValue(domainId, out DataDomainDescriptor descriptor) ? descriptor : null;

        /// <summary>Target schema versions, in the shape the migration planner takes.</summary>
        public Dictionary<string, int> GetTargetVersions()
        {
            Dictionary<string, int> targets = new(StringComparer.Ordinal);
            foreach (DataDomainDescriptor descriptor in this._descriptors)
                targets[descriptor.DomainId] = descriptor.TargetSchemaVersion;

            return targets;
        }
    }
}