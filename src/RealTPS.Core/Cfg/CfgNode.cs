using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealTPS.Core.Cfg
{
    /// <summary>
    /// Minimal, KSP-independent representation of a KSP config node (name, ordered values, ordered child nodes).
    /// Used by the material parser, the validators, the patch generator and the tests so that none of them
    /// needs the game assemblies.
    /// </summary>
    public sealed class CfgNode
    {
        public string Name { get; }
        public List<KeyValuePair<string, string>> Values { get; } = new List<KeyValuePair<string, string>>();
        public List<CfgNode> Nodes { get; } = new List<CfgNode>();

        public CfgNode(string name)
        {
            Name = name ?? string.Empty;
        }

        public void AddValue(string key, string value)
        {
            Values.Add(new KeyValuePair<string, string>(key, value ?? string.Empty));
        }

        public CfgNode AddNode(string name)
        {
            var node = new CfgNode(name);
            Nodes.Add(node);
            return node;
        }

        public bool HasValue(string key)
        {
            return GetValue(key) != null;
        }

        /// <summary>Returns the first value with this key, or null.</summary>
        public string GetValue(string key)
        {
            for (int i = 0; i < Values.Count; i++)
            {
                if (string.Equals(Values[i].Key, key, StringComparison.Ordinal))
                {
                    return Values[i].Value;
                }
            }
            return null;
        }

        public int CountValues(string key)
        {
            int n = 0;
            for (int i = 0; i < Values.Count; i++)
            {
                if (string.Equals(Values[i].Key, key, StringComparison.Ordinal))
                {
                    n++;
                }
            }
            return n;
        }

        public CfgNode GetNode(string name)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (string.Equals(Nodes[i].Name, name, StringComparison.Ordinal))
                {
                    return Nodes[i];
                }
            }
            return null;
        }

        public List<CfgNode> GetNodes(string name)
        {
            var result = new List<CfgNode>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (string.Equals(Nodes[i].Name, name, StringComparison.Ordinal))
                {
                    result.Add(Nodes[i]);
                }
            }
            return result;
        }

        /// <summary>Parses a finite double using the invariant culture. Returns false for missing, NaN or infinite.</summary>
        public bool TryGetDouble(string key, out double value)
        {
            value = 0.0;
            string raw = GetValue(key);
            if (raw == null)
            {
                return false;
            }
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>Splits a comma-separated list value into trimmed, non-empty items.</summary>
        public List<string> GetList(string key)
        {
            var result = new List<string>();
            string raw = GetValue(key);
            if (raw == null)
            {
                return result;
            }
            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (item.Length > 0)
                {
                    result.Add(item);
                }
            }
            return result;
        }
    }
}
