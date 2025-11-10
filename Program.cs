using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Clients;
using PaymentService.Data;
using PaymentService.Models;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using System.Globalization;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Services.AddDbContext<PaymentsDb>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpClient<BillingClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:BillingBaseUrl"]!);
});
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost("/v1/payments/charge", async (
    HttpContext ctx,
    PaymentsDb db,
    BillingClient billing,
    [FromBody] Payment request,
    [FromHeader(Name = "Idempotency-Key")] string idempotencyKey) =>
{
    if (string.IsNullOrWhiteSpace(idempotencyKey))
        return Results.BadRequest(new { message = "Idempotency-Key header required" });

    string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.BillId}|{request.Amount}|{request.Method}")));

    // Check if we already processed this key (idempotent replay)
    var existing = await db.IdempotencyKeys.FindAsync(idempotencyKey);
    if (existing != null)
        return Results.Json(existing.ResponseBody, statusCode: existing.StatusCode);

    // Get bill from Billing Service
    var bill = await billing.GetBill(request.BillId);
    if (bill is null)
        return Results.BadRequest(new { message = "Bill does not exist" });

    decimal billAmount = bill.AmountTotal;
    string billStatus = bill.Status;

    if (billStatus != "OPEN")
        return Results.BadRequest(new { message = $"Bill is {billStatus}" });

    if (billAmount != request.Amount)
        return Results.BadRequest(new { message = "Amount mismatch" });

    // Save payment
    db.Payments.Add(request);
    await db.SaveChangesAsync();

    // Notify billing to mark paid
    bool marked = await billing.MarkPaid(request.BillId, request.PaymentId, request.Amount);
    if (!marked)
    {
        request.Status = "FAILED";
        await db.SaveChangesAsync();
        return Results.StatusCode(502);
    }

    // Store idempotency record
    string responseJson = System.Text.Json.JsonSerializer.Serialize(request);
    db.IdempotencyKeys.Add(new IdempotencyKey
    {
        Key = idempotencyKey,
        RequestHash = hash,
        ResponseBody = responseJson,
        StatusCode = 201
    });
    await db.SaveChangesAsync();

    return Results.Created($"/v1/payments/{request.PaymentId}", request);
});

app.MapGet("/v1/payments/{id:long}", async (PaymentsDb db, long id) =>
{
    var p = await db.Payments.FindAsync(id);
    return p is null ? Results.NotFound(new { message = "Payment not found" }) : Results.Ok(p);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDb>();

    try
    {
        db.Database.Migrate();  // apply migrations if DB exists
    }
    catch (SqlException ex)
    {
        if (ex.Number == 4060) // Database does not exist
        {
            Console.WriteLine("PaymentDb not found — creating now...");

            var original = db.Database.GetDbConnection().ConnectionString;
            var masterConn = original.Replace("Database=PaymentDb", "Database=master");

            using (var connection = new SqlConnection(masterConn))
            {
                connection.Open();
                using (var cmd = new SqlCommand("CREATE DATABASE PaymentDb", connection))
                {
                    cmd.ExecuteNonQuery();
                    Console.WriteLine("✅ Database PaymentDb created successfully.");
                }
            }

            db.Database.Migrate(); // run migrations
        }
        else
        {
            Console.WriteLine($"SQL error: {ex.Message}");
            throw;
        }
    }
}

SeedPaymentData(app);

app.Run();

static void SeedPaymentData(IApplicationBuilder app)
{
    using var scope = app.ApplicationServices.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDb>();

    // ✅ Only seed if empty
    if (db.Payments.Any())
        return;

    using var reader = new StreamReader("Seeds/hms_payments.csv");
    using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
    var records = csv.GetRecords<PaymentCsv>();

    foreach (var r in records)
    {
        var date = DateTime.ParseExact(
            r.paid_at.Trim(),
            new[] { "dd/MM/yy H:mm", "dd/MM/yy HH:mm", "dd/MM/yyyy H:mm", "dd/MM/yyyy HH:mm" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None
        );

        db.Payments.Add(new Payment
        {
            BillId = r.bill_id,
            Amount = r.amount,
            Method = r.method,
            Reference = r.reference,
            PaidAt = date,
            Status = "SUCCEEDED"
        });
    }

    db.SaveChanges();
}

class PaymentCsv
{
    public long bill_id { get; set; }
    public decimal amount { get; set; }
    public string method { get; set; }
    public string reference { get; set; }
    public string paid_at { get; set; }
}