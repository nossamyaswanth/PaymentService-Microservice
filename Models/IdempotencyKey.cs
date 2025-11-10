namespace PaymentService.Models;

public class IdempotencyKey
{
    public string Key { get; set; } = default!;
    public string RequestHash { get; set; } = default!;
    public string ResponseBody { get; set; } = default!;
    public int StatusCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}