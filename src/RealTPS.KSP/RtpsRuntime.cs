using System;
using System.Collections.Generic;
using RealTPS.Core.Cfg;
using RealTPS.Core.Materials;
using UnityEngine;

namespace RealTPS
{
    /// <summary>Log helper. Every RealTPS line starts with the same tag so that a log scan can find them.</summary>
    internal static class RtpsLog
    {
        public const string Tag = "[RealTPS] ";

        public static void Info(string message)
        {
            Debug.Log(Tag + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Tag + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(Tag + message);
        }
    }

    /// <summary>Conversion between KSP's <c>ConfigNode</c> and the KSP-independent <see cref="CfgNode"/>.</summary>
    internal static class ConfigNodeBridge
    {
        public static CfgNode ToCfg(ConfigNode node)
        {
            var result = new CfgNode(node.name);
            for (int i = 0; i < node.values.Count; i++)
            {
                ConfigNode.Value value = node.values[i];
                result.AddValue(value.name, value.value);
            }
            for (int i = 0; i < node.nodes.Count; i++)
            {
                result.Nodes.Add(ToCfg(node.nodes[i]));
            }
            return result;
        }

        public static ConfigNode ToConfigNode(CfgNode node)
        {
            var result = new ConfigNode(node.Name);
            for (int i = 0; i < node.Values.Count; i++)
            {
                result.AddValue(node.Values[i].Key, node.Values[i].Value);
            }
            for (int i = 0; i < node.Nodes.Count; i++)
            {
                result.AddNode(ToConfigNode(node.Nodes[i]));
            }
            return result;
        }
    }

    /// <summary>
    /// The material catalogue and the global settings as loaded from the game database
    /// (<c>RTPS_MATERIAL</c> and <c>RTPS_SETTINGS</c> nodes, after ModuleManager). Loaded lazily, reloaded when
    /// the game database is reloaded. A material in the catalogue is never selectable by itself: only a host
    /// part that lists it in its switcher can request it.
    /// </summary>
    public static class RtpsCatalogue
    {
        private static MaterialCatalogue current;

        /// <summary>The loaded catalogue. Never null; empty when the game database is not available.</summary>
        public static MaterialCatalogue Current
        {
            get
            {
                if (current == null)
                {
                    Reload();
                }
                return current;
            }
        }

        public static void Reload()
        {
            var nodes = new List<CfgNode>();

            if (GameDatabase.Instance != null)
            {
                ConfigNode[] materials = GameDatabase.Instance.GetConfigNodes(MaterialParser.NodeName);
                for (int i = 0; i < materials.Length; i++)
                {
                    nodes.Add(ConfigNodeBridge.ToCfg(materials[i]));
                }

            }

            try
            {
                current = MaterialCatalogue.Load(nodes);
            }
            catch (Exception e)
            {
                RtpsLog.Error("material catalogue could not be loaded: " + e);
                current = MaterialCatalogue.Load(new List<CfgNode>());
            }

            for (int i = 0; i < current.Issues.Count; i++)
            {
                ValidationIssue issue = current.Issues[i];
                if (issue.IsError)
                {
                    RtpsLog.Error("catalogue: " + issue);
                }
                else
                {
                    RtpsLog.Warn("catalogue: " + issue);
                }
            }
        }
    }
}
