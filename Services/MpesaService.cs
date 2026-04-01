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
        Task<string> GetAccessTokenAsync();
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

        public async Task<string> GetAccessTokenAsync()
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
                var jsonDoc = JsonDocument.Parse(content);
                var token = jsonDoc.RootElement.GetProperty("access_token").GetString();

                return token;
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
                    // Simulate successful STK push in sandbox
                    return true;
                }

                // Production implementation
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

                // Generate timestamp in required format (YYYYMMDDHHmmss)
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

                // Generate password
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
                    CallBackURL = callbackUrl,
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

                // Extract callback data
                if (callbackData == null)
                {
                    _logger.LogError("Callback data is null");
                    return false;
                }

                // Parse the callback body
                var body = callbackData.ContainsKey("Body") ? callbackData["Body"] : null;
                if (body == null)
                {
                    _logger.LogError("No Body in callback");
                    return false;
                }

                var bodyStr = body.ToString();
                var bodyDoc = JsonDocument.Parse(bodyStr);
                var rootElement = bodyDoc.RootElement;

                // Get the stkCallback object
                if (!rootElement.TryGetProperty("stkCallback", out var stkCallback))
                {
                    _logger.LogError("No stkCallback in response");
                    return false;
                }

                // Extract data
                if (!stkCallback.TryGetProperty("MerchantRequestID", out var merchantRequestId))
                {
                    _logger.LogError("No MerchantRequestID");
                    return false;
                }

                if (!stkCallback.TryGetProperty("ResultCode", out var resultCode))
                {
                    _logger.LogError("No ResultCode");
                    return false;
                }

                var resultCodeValue = resultCode.GetInt32();

                // Result Code 0 = Success
                if (resultCodeValue == 0)
                {
                    if (!stkCallback.TryGetProperty("CallbackMetadata", out var callbackMetadata))
                    {
                        _logger.LogError("No CallbackMetadata");
                        return false;
                    }

                    var itemsArray = callbackMetadata.GetProperty("Item");

                    // Extract transaction details
                    string mpesaReceiptNumber = "";
                    decimal amount = 0;
                    string phoneNumber = "";
                    string transactionDate = "";

                    foreach (var item in itemsArray.EnumerateArray())
                    {
                        var name = item.GetProperty("Name").GetString();
                        var value = item.GetProperty("Value");

                        switch (name)
                        {
                            case "MpesaReceiptNumber":
                                mpesaReceiptNumber = value.GetString();
                                break;
                            case "Amount":
                                amount = value.GetDecimal();
                                break;
                            case "PhoneNumber":
                                phoneNumber = value.GetInt64().ToString();
                                break;
                            case "TransactionDate":
                                transactionDate = value.GetString();
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
                    // Payment failed
                    var resultDescription = stkCallback.TryGetProperty("ResultDesc", out var desc)
                        ? desc.GetString()
                        : "Unknown error";

                    _logger.LogWarning($"M-Pesa payment failed: {resultDescription} (Code: {resultCodeValue})");

                    // Find and mark transaction as failed
                    if (stkCallback.TryGetProperty("CheckoutRequestID", out var checkoutRequestId))
                    {
                        var checkoutRequestIdStr = checkoutRequestId.GetString();
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
