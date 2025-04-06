using System;
using LiteDB;

namespace CreditTreeCLI
{
    public class CreditAdjustment
    {
        [BsonId]
        public Guid Id { get; private set; }

        public decimal Value { get; }
        public DateTime AdjustmentDate { get; }

        public CreditAdjustment(decimal value)
        {
            if (value == 0)
            {
                throw new ArgumentException("O valor do ajuste não pode ser zero.", nameof(value));
            }
            Id = Guid.NewGuid();
            Value = value;
            AdjustmentDate = DateTime.Now;
        }

        [BsonCtor]
        private CreditAdjustment() { }

        public AdjustmentType Type => Value > 0 ? AdjustmentType.Allocation : AdjustmentType.Deallocation;

        public override string ToString()
        {
            string typeString = Type == AdjustmentType.Allocation ? "Alocação" : "Desalocação";
            return $"{typeString}: {Math.Abs(Value):C}, Data: {AdjustmentDate:yyyy-MM-dd HH:mm:ss}, ID: {Id}";
        }
    }

    public enum AdjustmentType
    {
        Allocation,
        Deallocation
    }
}