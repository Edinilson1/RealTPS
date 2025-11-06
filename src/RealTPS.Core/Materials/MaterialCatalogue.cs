using System;
using System.Collections.Generic;
using RealTPS.Core.Cfg;

namespace RealTPS.Core.Materials
{
    /// <summary>
    /// The set of valid material profiles plus the issues found while loading them. Registration is separate
    /// from host assignment: a profile in the catalogue is never selectable by itself.
    /// </summary>
    public sealed class MaterialCatalogue
    {
        private readonly Dictionary<string, MaterialDefinition> byId = new Dictionary<string, MaterialDefinition>(StringComparer.Ordinal);
        private readonly List<MaterialDefinition> ordered = new List<MaterialDefinition>();

        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();

        public IReadOnlyList<MaterialDefinition> Materials
        {
            get { return ordered; }
        }

        public bool TryGet(string id, out MaterialDefinition material)
        {
            if (id == null)
            {
                material = null;
                return false;
            }
            return byId.TryGetValue(id, out material);
        }

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i].IsError)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Builds a catalogue from <c>RTPS_MATERIAL</c> nodes. Invalid nodes are rejected individually;
        /// duplicate ids and consumable resources shared between materials are rejected for the later entry.
        /// </summary>
        public static MaterialCatalogue Load(IEnumerable<CfgNode> materialNodes)
        {
            if (materialNodes == null)
            {
                throw new ArgumentNullException(nameof(materialNodes));
            }

            var catalogue = new MaterialCatalogue();
            var resourceOwner = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (CfgNode node in materialNodes)
            {
                MaterialDefinition material = MaterialParser.Parse(node, catalogue.Issues);
                if (material == null)
                {
                    continue;
                }
                if (catalogue.byId.ContainsKey(material.Id))
                {
                    catalogue.Issues.Add(new ValidationIssue(true, "material '" + material.Id + "'", "duplicate id; the later definition is ignored"));
                    continue;
                }
                string resource = material.ConsumableResource;
                if (resource != null)
                {
                    string owner;
                    if (resourceOwner.TryGetValue(resource, out owner))
                    {
                        catalogue.Issues.Add(new ValidationIssue(true, "material '" + material.Id + "'", "resource '" + resource + "' already belongs to material '" + owner + "'; one resource cannot carry two material semantics"));
                        continue;
                    }
                    resourceOwner.Add(resource, material.Id);
                }
                catalogue.byId.Add(material.Id, material);
                catalogue.ordered.Add(material);
            }
            return catalogue;
        }
    }
}
