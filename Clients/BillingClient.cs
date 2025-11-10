using System.Net.Http.Json;
using PaymentService.Models;

namespace PaymentService.Clients;

public class BillingClient
{
    private readonly HttpClient _http;
    public BillingClient(HttpClient http) => _http = http;

    public async Task<BillDto?> GetBill(long billId)
    {
        return await _http.GetFromJsonAsync<BillDto>($"/v1/bills/{billId}");
    }

    public async Task<bool> MarkPaid(long billId, long paymentId, decimal amount)
    {
        var response = await _http.PostAsJsonAsync($"/v1/bills/{billId}/mark-paid", new { paymentId, amount });
        return response.IsSuccessStatusCode;
    }
}