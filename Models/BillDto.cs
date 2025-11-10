namespace PaymentService.Models;

public class BillDto
{
    public long BillId { get; set; }
    public long PatientId { get; set; }
    public long AppointmentId { get; set; }
    public decimal AmountSubtotal { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal AmountTotal { get; set; }
    public string Status { get; set; } = "";
}