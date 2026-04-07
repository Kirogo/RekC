// Services/MpesaService.cs
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RekovaBE_CSharp.Data;
using RekovaBE_CSharp.Models;

namespace RekovaBE_CSharp.Services
{
    public interface IMpesaService
    {
        Task<bool> InitiateStkPushAsync(string phoneNumber, decimal amount, string accountReference);
        Task<bool> VerifyPaymentAsync(string checkoutRequestId);
        Task<bool> HandleCallbackAsync(Dictionary<string, object> callbackData);
        Task<string?> GetAccessTokenAsync();  // Changed to nullable return type
    }

    public class MpesaService : IMpesaService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MpesaService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _dbContext;

        // M-Pesa Daraja API endpoints
        private const string SandboxAuthUrl = "https://sandbox.safaricom.co.ke/oauth/v1/generate?grant_type=client_credentials";
        private const string ProductionAuthUrl = "https://api.safaricom.co.ke/oauth/v1/generate?grant_type=client_credentials";
        private const string SandboxStkUrl = "https://sandbox.safaricom.co.ke/mpesa/stkpush/v1/processrequest";
        private const string ProductionStkUrl = "https://api.safaricom.co.ke/mpesa/stkpush/v1/processrequest";

        public MpesaService(
            IConfiguration configuration,
            ILogger<MpesaService> logger,
            HttpClient httpClient,
            ApplicationDbContext dbContext)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
            _dbContext = dbContext;
        }

        public async Task<string?> GetAccessTokenAsync()  // Changed return type to string?
{
    try
    {
        var environment = _configuration["Mpesa:Environment"] ?? "sandbox";
        var consumerKey = Environment.GetEnvironmentVariable("MPESA_CONSUMER_KEY")
            ?? _configuration["Mpesa:ConsumerKey"];
        var consumerSecret = Environment.GetEnvironmentVariable("MPESA_CONSUMER_SECRET")
            ?? _configuration["Mpesa:ConsumerSecret"];

        if (string.IsNullOrEmpty(consumerKey) || string.IsNullOrEmpty(consumerSecret))
        {
            _logger.LogWarning("M-Pesa credentials not configured");
            return null;
        }

        var authUrl = environment == "sandbox" ? SandboxAuthUrl : ProductionAuthUrl;
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{consumerKey}:{consumerSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Get, authUrl);
        request.Headers.Add("Authorization", $"Basic {credentials}");

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError($"Failed to get M-Pesa access token: {response.StatusCode}");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var token = jsonDoc.RootElement.GetProperty("access_token").GetString();
        
        return token ?? null;  // Handle null token
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error getting M-Pesa access token: {ex.Message}");
        return null;
    }
}

        public async Task<bool> InitiateStkPushAsync(string phoneNumber, decimal amount, string accountReference)
{
    try
    {
        var environment = _configuration["Mpesa:Environment"] ?? "sandbox";

        if (environment == "sandbox")
        {
            _logger.LogInformation($"[SANDBOX MODE] STK Push simulation - Phone: {phoneNumber}, Amount: {amount}");
            return true;
        }

        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("Failed to get M-Pesa access token");
            return false;
        }

        var passkey = Environment.GetEnvironmentVariable("MPESA_PASSKEY") 
            ?? _configuration["Mpesa:Passkey"];
        var shortCode = Environment.GetEnvironmentVariable("MPESA_SHORT_CODE") 
            ?? _configuration["Mpesa:ShortCode"];
        var callbackUrl = Environment.GetEnvironmentVariable("MPESA_CALLBACK_URL") 
            ?? _configuration["Mpesa:CallbackUrl"];

        if (string.IsNullOrEmpty(shortCode) || string.IsNullOrEmpty(passkey))
        {
            _logger.LogError("M-Pesa shortcode or passkey not configured");
            return false;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var password = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{shortCode}{passkey}{timestamp}"));

        var stkRequest = new
        {
            BusinessShortCode = shortCode,
            Password = password,
            Timestamp = timestamp,
            TransactionType = "CustomerPayBillOnline",
            Amount = (int)amount,
            PartyA = phoneNumber,
            PartyB = shortCode,
            PhoneNumber = phoneNumber,
            CallBackURL = callbackUrl ?? "https://your-domain.com/api/payments/callback",
            AccountReference = accountReference,
            TransactionDesc = "Loan Repayment"
        };

        var json = JsonSerializer.Serialize(stkRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, ProductionStkUrl)
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation($"STK Push initiated successfully for {phoneNumber}");
            return true;
        }

        _logger.LogError($"STK Push failed: {responseContent}");
        return false;
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error initiating STK Push: {ex.Message}");
        return false;
    }
}

        public async Task<bool> VerifyPaymentAsync(string checkoutRequestId)
        {
            try
            {
                _logger.LogInformation($"Verifying payment for checkout request: {checkoutRequestId}");

                // Find transaction by checkout request ID and verify status
                var transaction = await _dbContext.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionId == checkoutRequestId);

                if (transaction == null)
                {
                    _logger.LogWarning($"Transaction not found: {checkoutRequestId}");
                    return false;
                }

                // Transaction status can be checked via M-Pesa API
                // For now, return current status
                return transaction.Status == "COMPLETED" || transaction.Status == "PROCESSED";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error verifying payment: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> HandleCallbackAsync(Dictionary<string, object> callbackData)
{
    try
    {
        _logger.LogInformation($"Processing M-Pesa callback");

        if (callbackData == null || !callbackData.ContainsKey("Body"))
        {
            _logger.LogError("Invalid callback data");
            return false;
        }

        var body = callbackData["Body"];
        if (body == null)
        {
            _logger.LogError("No Body in callback");
            return false;
        }

        var bodyStr = body.ToString();
        if (string.IsNullOrEmpty(bodyStr))
        {
            _logger.LogError("Empty body string");
            return false;
        }

        using var bodyDoc = JsonDocument.Parse(bodyStr);
        var rootElement = bodyDoc.RootElement;

        if (!rootElement.TryGetProperty("stkCallback", out var stkCallback))
        {
            _logger.LogError("No stkCallback in response");
            return false;
        }

        if (!stkCallback.TryGetProperty("ResultCode", out var resultCodeElement))
        {
            _logger.LogError("No ResultCode");
            return false;
        }

        var resultCodeValue = resultCodeElement.GetInt32();

        if (resultCodeValue == 0)
        {
            if (!stkCallback.TryGetProperty("CallbackMetadata", out var callbackMetadata))
            {
                _logger.LogError("No CallbackMetadata for successful transaction");
                return false;
            }

            if (!callbackMetadata.TryGetProperty("Item", out var itemsArray))
            {
                _logger.LogError("No Item array in CallbackMetadata");
                return false;
            }

            string mpesaReceiptNumber = "";
            decimal amount = 0;
            string phoneNumber = "";
            string transactionDate = "";

            foreach (var item in itemsArray.EnumerateArray())
            {
                if (!item.TryGetProperty("Name", out var nameElement)) continue;
                var name = nameElement.GetString();
                
                if (!item.TryGetProperty("Value", out var valueElement)) continue;

                switch (name)
                {
                    case "MpesaReceiptNumber":
                        mpesaReceiptNumber = valueElement.GetString() ?? "";
                        break;
                    case "Amount":
                        amount = valueElement.GetDecimal();
                        break;
                    case "PhoneNumber":
                        phoneNumber = valueElement.GetInt64().ToString();
                        break;
                    case "TransactionDate":
                        transactionDate = valueElement.GetString() ?? "";
                        break;
                }
            }

            // Find and update transaction
            var transaction = await _dbContext.Transactions
                .FirstOrDefaultAsync(t => t.PhoneNumber == phoneNumber && t.Amount == amount);

            if (transaction != null)
            {
                transaction.Status = "COMPLETED";
                transaction.MpesaReceiptNumber = mpesaReceiptNumber;
                transaction.ProcessedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;

                _dbContext.Transactions.Update(transaction);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Transaction {mpesaReceiptNumber} marked as COMPLETED");
                return true;
            }
            else
            {
                _logger.LogWarning($"Transaction not found for phone {phoneNumber} and amount {amount}");
                return false;
            }
        }
        else
        {
            var resultDescription = stkCallback.TryGetProperty("ResultDesc", out var desc)
                ? desc.GetString() ?? "Unknown error"
                : "Unknown error";

            _logger.LogWarning($"M-Pesa payment failed: {resultDescription} (Code: {resultCodeValue})");

            if (stkCallback.TryGetProperty("CheckoutRequestID", out var checkoutRequestId))
            {
                var checkoutRequestIdStr = checkoutRequestId.GetString();
                if (!string.IsNullOrEmpty(checkoutRequestIdStr))
                {
                    var transaction = await _dbContext.Transactions
                        .FirstOrDefaultAsync(t => t.TransactionId == checkoutRequestIdStr);

                    if (transaction != null)
                    {
                        transaction.Status = "FAILED";
                        transaction.UpdatedAt = DateTime.UtcNow;
                        _dbContext.Transactions.Update(transaction);
                        await _dbContext.SaveChangesAsync();
                    }
                }
            }

            return false;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error handling callback: {ex.Message}\n{ex.StackTrace}");
        return false;
    }
}
    }
}
