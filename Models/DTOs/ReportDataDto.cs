namespace RekovaBE_CSharp.Models.DTOs
{
    public class ReportDataDto
    {
        // Officer Information
        public string OfficerName { get; set; } = string.Empty;
        public string OfficerEmail { get; set; } = string.Empty;
        public string OfficerRole { get; set; } = string.Empty;
        
        // Performance Metrics
        public decimal CollectionRate { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal TotalCollected { get; set; }
        public decimal MonthlyCollected { get; set; }
        public decimal WeeklyCollected { get; set; }
        
        // Customer Metrics
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public int OverdueCustomers { get; set; }
        
        // Collections
        public List<TransactionItemDto>? Transactions { get; set; }
        public List<CustomerItemDto>? Customers { get; set; }
    }

    public class TransactionItemDto
    {
        public string Date { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Receipt { get; set; }
    }

    public class CustomerItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string LoanType { get; set; } = string.Empty;
        public decimal LoanAmount { get; set; }
        public decimal Arrears { get; set; }
        public string Status { get; set; } = string.Empty;
        public string LastContact { get; set; } = string.Empty;
    }
}