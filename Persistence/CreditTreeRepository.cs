using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace CreditTreeCLI.Persistence
{
    public class CreditTreeRepository : IDisposable
    {
        private readonly LiteDatabase _db;
        private readonly ILiteCollection<BsonDocument> _treeCollection;

        private const string TreeDocumentId = "main_credit_tree_wrapper"; // This constant might become obsolete if using TreeId as _id

        public CreditTreeRepository(string databasePath = "CreditTreeData_SingleCollection_V2.db")
        {
            /*var mapper = BsonMapper.Global;
            mapper.Entity<CreditNode>()
                  .Ignore(x => x.Parent);*/

            _db = new LiteDatabase(databasePath);
            _treeCollection = _db.GetCollection("trees");

            _treeCollection.EnsureIndex("RootNodeData.IsTemplate");
            _treeCollection.EnsureIndex("RootNodeData.CustomerId");
            _treeCollection.EnsureIndex("RootNodeData.Name");
        }

        public void SaveTree(CreditNode rootNode)
        {
            if (rootNode == null) return;
            if (rootNode.TreeId == Guid.Empty) throw new InvalidOperationException("RootNode deve ter um TreeId definido antes de salvar.");

            var wrapperDoc = new BsonDocument
            {
                ["_id"] = rootNode.TreeId,
                ["RootNodeData"] = BsonMapper.Global.ToDocument(rootNode)
            };
            _treeCollection.Upsert(wrapperDoc);
        }

        public CreditNode? LoadTree(Guid treeId)
        {
            var wrapperDoc = _treeCollection.FindById(treeId);
            if (wrapperDoc != null && wrapperDoc.ContainsKey("RootNodeData"))
            {
                var rootNode = BsonMapper.Global.ToObject<CreditNode>(wrapperDoc["RootNodeData"].AsDocument);
                if (rootNode != null)
                {
                    rootNode.ReconstructParentReferences(null);
                    return rootNode;
                }
            }
            return null;
        }

        public IEnumerable<CreditNodeInfo> ListTemplates()
        {
             var templateDocs = _treeCollection.Find(Query.EQ("RootNodeData.IsTemplate", true));
             foreach(var doc in templateDocs)
             {
                  var rootData = doc["RootNodeData"].AsDocument;
                  yield return new CreditNodeInfo(
                       doc["_id"].AsGuid,
                       rootData["Name"].AsString,
                       isTemplate: true
                  );
             }
        }

        public IEnumerable<CreditNodeInfo> ListCustomerTrees()
        {
             var customerDocs = _treeCollection.Find(Query.Not("RootNodeData.IsTemplate", true));
             foreach(var doc in customerDocs)
             {
                  var rootData = doc["RootNodeData"].AsDocument;
                  yield return new CreditNodeInfo(
                       doc["_id"].AsGuid,
                       rootData["Name"].AsString,
                       isTemplate: false,
                       customerId: rootData.ContainsKey("CustomerId") ? rootData["CustomerId"].AsString : null,
                       createdAt: rootData.ContainsKey("CreatedAtDate") ? rootData["CreatedAtDate"].AsDateTime : default
                  );
             }
        }

         public CreditNode? CreateTreeFromTemplate(Guid templateId, string customerId)
         {
             CreditNode? templateRoot = LoadTree(templateId);
             if (templateRoot == null || !templateRoot.IsTemplate)
             {
                 throw new KeyNotFoundException($"Template com ID {templateId} não encontrado ou não é um template.");
             }

             CreditNode newTreeRoot = templateRoot.DeepClone();
             Guid newTreeId = Guid.NewGuid();
             newTreeRoot.InitializeAsCustomerTree(newTreeId, customerId);
             SaveTree(newTreeRoot);
             return newTreeRoot;
         }

        public void Dispose()
        {
            _db?.Dispose();
        }
    }

    public class CreditNodeInfo
    {
        public Guid TreeId { get; }
        public string RootName { get; }
        public bool IsTemplate { get; }
        public string? CustomerId { get; }
        public DateTime CreatedAt { get; }

        public CreditNodeInfo(Guid treeId, string rootName, bool isTemplate, string? customerId = null, DateTime? createdAt = null)
        {
            TreeId = treeId;
            RootName = rootName;
            IsTemplate = isTemplate;
            CustomerId = customerId;
            CreatedAt = createdAt ?? default;
        }

        public override string ToString()
        {
            if (IsTemplate) return $"[T] ID: {TreeId} | Nome: {RootName}";
            return $"[C] ID: {TreeId} | Cliente: {CustomerId ?? "N/A"} | Nome: {RootName} | Criado: {CreatedAt:yyyy-MM-dd}";
        }
    }
}