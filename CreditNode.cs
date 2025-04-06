using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace CreditTreeCLI
{
    public partial class CreditNode
    {
        [BsonId]
        public Guid Id { get; private set; }
        public Guid TreeId { get; set; }
        public DateTime CreatedAtDate { get; set; }
        public string? CustomerId { get; set; }
        public bool IsTemplate { get; set; }

        public string Name { get; set;}
        public string NodeType { get; set; }
        public decimal CreditLimit { get; private set; }
        public decimal CreditTaken { get; private set; }

        public DateTime? LimitLastUpdatedDate { get; private set; }
        public DateTime? CreditTakenLastUpdatedDate { get; private set; }

        [BsonIgnore]
        public CreditNode? Parent { get; private set; }
        public List<CreditNode> Children { get; private set; }

        public List<CreditAdjustment> Adjustments { get; private set; }

        public bool AllowOverLimitUpdate { get; private set; }
        public decimal CurrentOverLimit { get; private set; }

        public decimal AvailableCredit => CreditLimit - CreditTaken;

        public CreditNode(string name, string nodeType, decimal creditLimit, Guid? specificId = null, CreditNode? parent = null, bool allowOverLimitUpdate = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome do nó não pode ser vazio.", nameof(name));
            if (creditLimit < 0) throw new ArgumentException("Limite de crédito não pode ser negativo.", nameof(creditLimit));

            Id = specificId ?? Guid.NewGuid();
            Name = name;
            NodeType = nodeType;
            CreditLimit = creditLimit;
            Parent = parent;
            CreditTaken = 0;
            Children = new List<CreditNode>();
            Adjustments = new List<CreditAdjustment>();
            AllowOverLimitUpdate = allowOverLimitUpdate;
            CurrentOverLimit = 0;
        }

        [BsonCtor]
        private CreditNode()
        {
            Children = new List<CreditNode>();
            Adjustments = new List<CreditAdjustment>();
        }

        public void AddChild(CreditNode child)
        {
             if (Adjustments.Any()) throw new InvalidOperationException($"Nó '{Name}' possui ajustes e não pode ter filhos.");
             if (Children.Sum(c => c.CreditLimit) + child.CreditLimit > this.CreditLimit) throw new InvalidOperationException($"Adicionar filho '{child.Name}' excede limite do pai '{Name}'.");
             if (Children.Any(c => c.Name.Equals(child.Name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException($"Filho com nome '{child.Name}' já existe.");

             child.Parent = this;
             child.TreeId = this.TreeId;
             child.CreatedAtDate = this.CreatedAtDate;

             Children.Add(child);
        }

         public void UpdateLimit(decimal newLimit)
         {
              if (newLimit < 0) throw new ArgumentException("Limite não pode ser negativo.");
              if (newLimit < CreditTaken && !AllowOverLimitUpdate) throw new InvalidOperationException($"Novo limite ({newLimit:C}) menor que tomado ({CreditTaken:C}) não permitido.");


                if (newLimit < CreditTaken && AllowOverLimitUpdate) {
                    this.CurrentOverLimit = CreditTaken - newLimit;
                } else {
                    this.CurrentOverLimit = 0;
                }

             this.CreditLimit = newLimit;
             this.LimitLastUpdatedDate = DateTime.Now;
         }

         private void UpdateCreditTakenRecursively(decimal amountChange)
         {
             this.CreditTaken += amountChange;
             this.CreditTakenLastUpdatedDate = DateTime.Now;

             if (CreditTaken < 0 && Math.Abs(CreditTaken) < 0.00001m) { CreditTaken = 0; }

             Parent?.UpdateCreditTakenRecursively(amountChange);
         }

        public void ReconstructParentReferences(CreditNode? parent)
        {
            this.Parent = parent;
            foreach (var child in Children)
            {
                child.ReconstructParentReferences(this);
            }
        }

        public CreditNode DeepClone()
        {
            var bsonDoc = BsonMapper.Global.ToDocument(this);
            var clonedNode = BsonMapper.Global.ToObject<CreditNode>(bsonDoc);
            clonedNode.ReconstructParentReferences(null);
            return clonedNode;
        }

        public void InitializeAsCustomerTree(Guid newTreeId, string customerId)
        {
            this.TreeId = newTreeId;
            this.CustomerId = customerId;
            this.IsTemplate = false;
            this.CreatedAtDate = DateTime.Now;
            ResetStateRecursively();
        }

        private void ResetStateRecursively()
        {
             this.CreditTaken = 0;
             this.CurrentOverLimit = 0;
             this.LimitLastUpdatedDate = null;
             this.CreditTakenLastUpdatedDate = null;
             this.Adjustments.Clear();

             foreach(var child in this.Children)
             {
                 child.ResetStateRecursively();
             }
        }

        public void DisplayNodeInfo(int indentLevel = 0)
        {
            string indent = new string(' ', indentLevel * 4);
            string rootInfo = "";
            if (Parent == null)
            {
                 rootInfo += $" | TreeId: {TreeId}";
                 rootInfo += IsTemplate ? " | [TEMPLATE]" : $" | Cliente: {CustomerId ?? "N/A"}";
                 rootInfo += $" | Criado: {CreatedAtDate:yyyy-MM-dd HH:mm}";
            }

            string overLimitInfo = "";
            if (AllowOverLimitUpdate || CurrentOverLimit > 0) {
                overLimitInfo = $" | OverLimit Permitido: {(AllowOverLimitUpdate ? "Sim" : "Não")}";
                 if (CurrentOverLimit > 0) overLimitInfo += $" | OverLimit Atual: {CurrentOverLimit:C}";
            }

            string timestamps = "";
            if (LimitLastUpdatedDate.HasValue) timestamps += $" | Limite Atualizado: {LimitLastUpdatedDate:yyyy-MM-dd HH:mm}";
            if (CreditTakenLastUpdatedDate.HasValue) timestamps += $" | Crédito Atualizado: {CreditTakenLastUpdatedDate:yyyy-MM-dd HH:mm}";

            string nodeStatus = $"- {Name} ({NodeType}) [ID: {Id}{rootInfo}] | Limite: {CreditLimit:C} | Tomado: {CreditTaken:C} | Disponível: {AvailableCredit:C}{overLimitInfo}{timestamps}";

            if (CreditTaken > CreditLimit && !IsTemplate) { Console.ForegroundColor = ConsoleColor.Yellow; }
            Console.WriteLine($"{indent}{nodeStatus}");
            Console.ResetColor();

            if (Adjustments.Any())
            {
                Console.WriteLine($"{indent}  Ajustes:");
                foreach (var adj in Adjustments.OrderBy(a => a.AdjustmentDate)) { Console.WriteLine($"{indent}    * {adj}"); }
            }

            foreach (var child in Children.OrderBy(c => c.Name)) { child.DisplayNodeInfo(indentLevel + 1); }
        }

        public void AddAdjustment(CreditAdjustment adjustment)
        {
             if (Children.Any()) throw new InvalidOperationException($"Nó '{Name}' não é folha, não pode ter ajustes.");
              if (adjustment.Type == AdjustmentType.Allocation) {
                 if (CreditTaken + adjustment.Value > CreditLimit) throw new InvalidOperationException($"Alocação excede limite do nó '{Name}'.");
                 ValidateAncestorsLimitForAllocation(adjustment.Value);
             } else {
                 if (CreditTaken + adjustment.Value < 0) throw new InvalidOperationException($"Desalocação excede valor tomado '{Name}'.");
                 ValidateAncestorsForDeallocation(adjustment.Value);
             }
             Adjustments.Add(adjustment);
             UpdateCreditTakenRecursively(adjustment.Value);
        }

        public CreditNode? FindNode(string name)
        {
             if (this.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
             {
                 return this;
             }
             foreach(var child in Children)
             {
                 var found = child.FindNode(name);
                 if (found != null) return found;
             }
             return null;
        }
        public void SetAllowOverLimit(bool allow) { this.AllowOverLimitUpdate = allow; }
        private void ValidateAncestorsLimitForAllocation(decimal amountToAdd) {
            CreditNode? current = this.Parent;
            while (current != null)
            {
                if (current.CreditTaken + amountToAdd > current.CreditLimit)
                {
                    throw new InvalidOperationException($"Alocação excede o limite de crédito do nó ancestral '{current.Name}'. Limite: {current.CreditLimit:C}, Crédito Tomado Atual: {current.CreditTaken:C}, Tentativa de Adicionar: {amountToAdd:C}");
                }
                current = current.Parent;
            }
        }
        private void ValidateAncestorsForDeallocation(decimal amountToRemove) {
             CreditNode? current = this.Parent;
             while (current != null)
             {
                 if (current.CreditTaken + amountToRemove < 0)
                 {
                     throw new InvalidOperationException($"Desalocação resultaria em crédito tomado negativo no nó ancestral '{current.Name}'. Tomado Atual: {current.CreditTaken:C}, Tentativa de Remover: {Math.Abs(amountToRemove):C}");
                 }
                 current = current.Parent;
             }
        }
    }
}