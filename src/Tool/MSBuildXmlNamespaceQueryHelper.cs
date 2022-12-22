using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Tool
{
    internal class MSBuildXmlNamespaceQueryHelper
    {
        public const string MSBuildXmlNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";
        private XmlNamespaceManager NamespaceManager { get; set; }

        private const string MSBuildNamespacePrefix = "build";

        public bool? RequireNamespace { get; set; }

        private XmlDocument Document { get; set; }

        public bool IsUsingLegacyNamespace(string rootElement = "Project", bool? updateRequireNamespace = false)
        {
            var result = this.SelectSingleNode(this.Document, $"//{rootElement}", true) != null;
            if (updateRequireNamespace.GetValueOrDefault())
            {
                this.RequireNamespace = result;
            }

            return result;
        }

        public MSBuildXmlNamespaceQueryHelper(XmlDocument document)
        {
            this.NamespaceManager = new XmlNamespaceManager(document.NameTable);
            this.NamespaceManager.AddNamespace(MSBuildNamespacePrefix, MSBuildXmlNamespace);
            this.Document = document;
        }

        public XmlNode? SelectSingleNode(XmlNode node, string xPathQuery, bool? useNamespace = null)
        {
            // If useNamespace has a value, always use, otherwise default to RequireNamespace property and if not set, default false
            if (useNamespace.GetValueOrDefault(this.RequireNamespace.GetValueOrDefault(false)))
            {
                xPathQuery = AppendNamespaceToQuery(xPathQuery);
                return node.SelectSingleNode(xPathQuery, this.NamespaceManager);
            }

            return node.SelectSingleNode(xPathQuery);
        }

        public XmlNodeList? SelectNodes(XmlNode node, string xPathQuery, bool? useNamespace = null)
        {
            if (useNamespace.GetValueOrDefault(this.RequireNamespace.GetValueOrDefault(false)))
            {
                xPathQuery = AppendNamespaceToQuery(xPathQuery);
                return node.SelectNodes(xPathQuery, this.NamespaceManager);
            }

            return node.SelectNodes(xPathQuery);
        }

        private string AppendNamespaceToQuery(string xPathQuery)
        {
            var querySplit = xPathQuery.Split('/');
            for (int i = 0; i < querySplit.Length; i++)
            {
                if (string.IsNullOrEmpty(querySplit[i]))
                {
                    // Handles when we have multiple /
                    continue;
                }

                querySplit[i] = MSBuildNamespacePrefix + ":" + querySplit[i];
            }

            var updatedQuery = string.Join('/', querySplit);
            return updatedQuery;
        }
    }
}
