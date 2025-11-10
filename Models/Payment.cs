namespace PaymentService.Models;

public class Payment
{
    public long PaymentId { get; set; }
    public long BillId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "CARD";
    public string? Reference { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "SUCCEEDED";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}