using System;
using System.Collections.Generic;
using System.Text;

namespace RealTPS.Core.Cfg
{
    /// <summary>
    /// Parser and writer for the subset of the KSP config syntax RealTPS needs: named nodes with braces,
    /// <c>key = value</c> lines and <c>//</c> comments. ModuleManager decorations in node names
    /// (<c>@PART[x]:NEEDS[y]</c>) are kept verbatim as the node name.
    /// </summary>
    public static class CfgParser
    {
        public sealed class ParseException : Exception
        {
            public int Line { get; }

            public ParseException(string message, int line)
                : base(message + " (line " + line + ")")
            {
                Line = line;
            }
        }

        /// <summary>Parses file text into a synthetic root node whose children are the top-level nodes.</summary>
        public static CfgNode Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var root = new CfgNode("root");
            var stack = new Stack<CfgNode>();
            stack.Push(root);
            string pendingName = null;
            int pendingLine = 0;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;
                string line = StripComment(lines[i]).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                // A line may hold several tokens, e.g. "NAME {" or "}" or "NAME { key = value }".
                int pos = 0;
                while (pos < line.Length)
                {
                    int open = line.IndexOf('{', pos);
                    int close = line.IndexOf('}', pos);
                    int next = MinNonNegative(open, close);
                    string segment = (next < 0 ? line.Substring(pos) : line.Substring(pos, next - pos)).Trim();

                    if (segment.Length > 0)
                    {
                        int eq = FindAssignment(segment);
                        if (eq >= 0)
                        {
                            if (pendingName != null)
                            {
                                throw new ParseException("Node name '" + pendingName + "' is not followed by '{'", pendingLine);
                            }
                            stack.Peek().AddValue(segment.Substring(0, eq).Trim(), segment.Substring(eq + 1).Trim());
                        }
                        else
                        {
                            if (pendingName != null)
                            {
                                throw new ParseException("Node name '" + pendingName + "' is not followed by '{'", pendingLine);
                            }
                            pendingName = segment;
                            pendingLine = lineNo;
                        }
                    }

                    if (next < 0)
                    {
                        break;
                    }

                    if (line[next] == '{')
                    {
                        if (pendingName == null)
                        {
                            throw new ParseException("'{' without a node name", lineNo);
                        }
                        stack.Push(stack.Peek().AddNode(pendingName));
                        pendingName = null;
                    }
                    else
                    {
                        if (pendingName != null)
                        {
                            throw new ParseException("Node name '" + pendingName + "' is not followed by '{'", pendingLine);
                        }
                        if (stack.Count <= 1)
                        {
                            throw new ParseException("Unbalanced '}'", lineNo);
                        }
                        stack.Pop();
                    }
                    pos = next + 1;
                }
            }

            if (pendingName != null)
            {
                throw new ParseException("Node name '" + pendingName + "' is not followed by '{'", pendingLine);
            }
            if (stack.Count != 1)
            {
                throw new ParseException("Missing '}' for node '" + stack.Peek().Name + "'", lines.Length);
            }
            return root;
        }

        /// <summary>Writes a node (and its children) using tabs, the conventional KSP layout.</summary>
        public static string Write(CfgNode node)
        {
            var sb = new StringBuilder();
            WriteNode(sb, node, 0);
            return sb.ToString();
        }

        private static void WriteNode(StringBuilder sb, CfgNode node, int depth)
        {
            string indent = new string('\t', depth);
            sb.Append(indent).Append(node.Name).Append('\n');
            sb.Append(indent).Append("{\n");
            for (int i = 0; i < node.Values.Count; i++)
            {
                sb.Append(indent).Append('\t').Append(node.Values[i].Key).Append(" = ").Append(node.Values[i].Value).Append('\n');
            }
            for (int i = 0; i < node.Nodes.Count; i++)
            {
                WriteNode(sb, node.Nodes[i], depth + 1);
            }
            sb.Append(indent).Append("}\n");
        }

        private static string StripComment(string line)
        {
            int idx = line.IndexOf("//", StringComparison.Ordinal);
            return idx < 0 ? line : line.Substring(0, idx);
        }

        private static int MinNonNegative(int a, int b)
        {
            if (a < 0)
            {
                return b;
            }
            if (b < 0)
            {
                return a;
            }
            return Math.Min(a, b);
        }

        /// <summary>
        /// Finds the '=' of a key/value line. An '=' inside square brackets belongs to a ModuleManager
        /// selector in a node name (for example <c>@PART[*]:HAS[#x[a=b]]</c>) and is ignored.
        /// </summary>
        private static int FindAssignment(string segment)
        {
            int depth = 0;
            for (int i = 0; i < segment.Length; i++)
            {
                char c = segment[i];
                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                }
                else if (c == '=' && depth <= 0)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
