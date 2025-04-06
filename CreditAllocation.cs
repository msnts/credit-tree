namespace CreditTreeCLI;

public class CreditAllocation
{
    public decimal Value { get; } // Valor da alocação (imutável após criação)
    public DateTime AllocationDate { get; } // Data da alocação

    // Construtor para criar uma nova alocação
    public CreditAllocation(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentException("O valor da alocação deve ser positivo.", nameof(value));
        }
        Value = value;
        AllocationDate = DateTime.Now; // Data/Hora atual da criação
    }

    // Sobrescrevendo ToString para facilitar a visualização
    public override string ToString()
    {
        return $"Valor: {Value:C}, Data: {AllocationDate:yyyy-MM-dd HH:mm:ss}";
    }
}