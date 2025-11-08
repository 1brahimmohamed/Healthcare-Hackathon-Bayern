using System.Text.Json;
using System.Text.Json.Serialization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace LicenseValidatorLibrary
{
    public class LicensePayload
    {
        public string? Email { get; set; }
        public string? DeviceSerial { get; set; }
        public int Level { get; set; }
        // public string? UserId { get; set; }
        // public string? Key { get; set; }
        public long Timestamp { get; set; }
    }
    public class KeyConfiguration
    {
        [JsonPropertyName("jwtSecret")]
        public string? JwtSecret { get; set; }
        
        [JsonPropertyName("serviceKeys")]
        public List<string>? ServiceKeys { get; set; }
        
        [JsonPropertyName("legacyKeys")]
        public Dictionary<string, int>? LegacyKeys { get; set; }
    }

    public static class LicenseValidator
    {
        private static readonly KeyConfiguration Configuration;
        private static readonly JwtSecurityTokenHandler TokenHandler;

        static LicenseValidator()
        {
            TokenHandler = new JwtSecurityTokenHandler();
            
            string keyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keys.json");

            if (File.Exists(keyFilePath))
            {
                try
                {
                    string json = File.ReadAllText(keyFilePath);
                    // Remove BOM if present
                    json = json.Trim('\uFEFF', '\u200B');
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    Configuration = JsonSerializer.Deserialize<KeyConfiguration>(json, options) ?? new KeyConfiguration();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LicenseValidator] Error loading configuration: {ex.Message}");
                    Configuration = new KeyConfiguration();
                }
            }
            else
            {
                Console.WriteLine($"[LicenseValidator] Keys file not found at: {keyFilePath}");
                Configuration = new KeyConfiguration();
            }
        }
        

        // Legacy validation method for backward compatibility
        public static (bool isValid, int? level) Validate(string key)
        {
            if (Configuration.LegacyKeys != null && Configuration.LegacyKeys.TryGetValue(key, out int level))
                return (true, level);
            return (false, null);
        }

        // New JWT validation method
        public static (bool isValid, LicensePayload? payload, string? error) ValidateJwtToken(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return (false, null, "Token is empty");
                }

                if (string.IsNullOrWhiteSpace(Configuration.JwtSecret))
                {
                    return (false, null, "JWT secret not configured");
                }

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration.JwtSecret)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                var principal = TokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                var jwtToken = validatedToken as JwtSecurityToken;

                if (jwtToken == null)
                {
                    return (false, null, "Invalid token format");
                }

                // Extract payload
                var payload = new LicensePayload
                {
                    Email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
                    DeviceSerial = jwtToken.Claims.FirstOrDefault(c => c.Type == "deviceSerial")?.Value,
                    Level = int.TryParse(jwtToken.Claims.FirstOrDefault(c => c.Type == "level")?.Value, out var level) ? level : 1,
                    // UserId = jwtToken.Claims.FirstOrDefault(c => c.Type == "userId")?.Value,
                    // Key = jwtToken.Claims.FirstOrDefault(c => c.Type == "key")?.Value,
                    Timestamp = long.TryParse(jwtToken.Claims.FirstOrDefault(c => c.Type == "timestamp")?.Value, out var ts) ? ts : 0
                };

                // // Validate that the key in the token is one of the service keys (if configured)
                // if (Configuration.ServiceKeys != null && Configuration.ServiceKeys.Any())
                // {
                //     if (string.IsNullOrWhiteSpace(payload.Key) || !Configuration.ServiceKeys.Contains(payload.Key))
                //     {
                //         return (false, null, "Invalid service key in token");
                //     }
                // }

                return (true, payload, null);
            }
            catch (SecurityTokenExpiredException)
            {
                return (false, null, "Token has expired");
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                return (false, null, "Invalid token signature");
            }
            catch (Exception ex)
            {
                return (false, null, $"Token validation failed: {ex.Message}");
            }
        }
        
        public static bool IsValidTime(string timestamp)
        {
            try
            {
                // Parse the ISO 8601 datetime string
                if (!DateTime.TryParse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsedTime))
                {
                    return false;
                }
                
                // Get current UTC time
                DateTime currentTime = DateTime.UtcNow;
                
                // Calculate the difference
                TimeSpan difference = currentTime - parsedTime;
                
                // Check if less than 2 minutes (120 seconds) have passed
                return difference.TotalSeconds < 120 && difference.TotalSeconds >= 0;
            }
            catch
            {
                return false;
            }
        }
        
    }
}