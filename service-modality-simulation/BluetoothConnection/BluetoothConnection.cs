using System.Windows;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using X25519;
using LicenseValidatorLibrary;

namespace BluetoothConnection;

public class BluetoothMessage
{
    public string type { get; set; }
    public string uuid { get; set; }
    public string? Payload { get; set; }
    public string? key { get; set; }
    public bool? ack { get; set; }
}
public class DecryptedPayload
{
    public string? time_slot { get; set; }
    public string? signature { get; set; }
    public string? data { get; set; }
}

public class BluetoothConnection
{
    private static readonly Guid _serviceUuid = new Guid("A0C10000-BEAD-BEEF-0B1D-1057AB1E1057");
    private static readonly Guid _characteristicUuid = new Guid("A0C10001-BEAD-BEEF-0B1D-1057AB1E1057");

    // 3. Class-level variables for the GATT provider
    private static GattServiceProvider? _serviceProvider;
    private static GattLocalCharacteristic? _characteristic;

    private static string _localValue = "Hello from WPF!";

    private static X25519KeyPair _keypair = new X25519KeyPair();
    private static byte[] _sharedSecret = Array.Empty<byte>();
    
    public static event Action? LicenseValidated;
    
    private static string _activeUid = "";

    public static async Task StartAdvertisingAsync()
    {
        Console.WriteLine("Starting advertising...");

        try
        {
            // 4. Create the service provider
            var creationResult = await GattServiceProvider.CreateAsync(_serviceUuid);
            if (creationResult.Error != Windows.Devices.Bluetooth.BluetoothError.Success)
            {
                Console.WriteLine($"Error creating service provider: {creationResult.Error}");
                return;
            }

            _serviceProvider = creationResult.ServiceProvider;

            // 5. Create the bidirectional characteristic (for both receiving and sending data)
            var characteristicParams = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Write | GattCharacteristicProperties.Read,
                WriteProtectionLevel = GattProtectionLevel.Plain,
                ReadProtectionLevel = GattProtectionLevel.Plain,
                UserDescription = "Bidirectional Data Channel"
            };

            var charResult =
                await _serviceProvider.Service.CreateCharacteristicAsync(_characteristicUuid, characteristicParams);
            if (charResult.Error != Windows.Devices.Bluetooth.BluetoothError.Success)
            {
                Console.WriteLine($"Error creating characteristic: {charResult.Error}");
                return;
            }

            _characteristic = charResult.Characteristic;

            // 6. Hook up event handlers for both read and write operations
            _characteristic.WriteRequested += Characteristic_WriteRequested;
            _characteristic.ReadRequested += Characteristic_ReadRequested;

            // 7. Start advertising
            var advParams = new GattServiceProviderAdvertisingParameters
            {
                IsDiscoverable = true,
                IsConnectable = true
            };
            
            _serviceProvider.StartAdvertising(advParams);

            Console.WriteLine("Service created and advertising.");
            Console.WriteLine($"Service UUID: {_serviceUuid}");
            Console.WriteLine($"Characteristic UUID: {_characteristicUuid}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting: {ex.Message}");
        }
    }

    public static void StopAdvertising()
    {
        if (_serviceProvider != null)
        {
            _serviceProvider.StopAdvertising();
            _serviceProvider = null; // This will dispose of the service

            // Unhook events
            if (_characteristic != null)
            {
                _characteristic.WriteRequested -= Characteristic_WriteRequested;
                _characteristic.ReadRequested -= Characteristic_ReadRequested;
                _characteristic = null;
            }

            Console.WriteLine("Advertising stopped.");
        }
    }

    private static async void Characteristic_WriteRequested(GattLocalCharacteristic sender,
        GattWriteRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();

        try
        {
            var request = await args.GetRequestAsync();
            // Read the data sent by the client
            var reader = DataReader.FromBuffer(request.Value);
            string receivedMessage = reader.ReadString(reader.UnconsumedBufferLength);

            string val = HandleRequests(receivedMessage);

            _localValue = val;
            
            request.Respond();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Write Error: {ex.Message}");
            try
            {
                var request = await args.GetRequestAsync();
                request.RespondWithProtocolError(GattProtocolError.WriteNotPermitted);
            }
            catch
            {
                // If we can't get the request, just complete the deferral
            }
        }

        deferral.Complete();
    }
    
    private static async void Characteristic_ReadRequested(GattLocalCharacteristic sender,
        GattReadRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();

        try
        {
            var request = await args.GetRequestAsync();
            // Create a writer to format our response
            var writer = new DataWriter();
            writer.WriteString(_localValue);

            Console.WriteLine($"CLIENT READ: Sent '{_localValue}'");

            // Send the data back to the client
            request.RespondWithValue(writer.DetachBuffer());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Read Error: {ex.Message}");
            try
            {
                var request = await args.GetRequestAsync();
                request.RespondWithProtocolError(GattProtocolError.AttributeNotFound);
            }
            catch
            {
                // If we can't get the request, just complete the deferral
            }
        }

        deferral.Complete();
    }

    private static string HandleRequests(string request)
    {
        // decode the request text into json
        try
        {
            var jsonDoc = JsonDocument.Parse(request);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("type", out JsonElement actionElement))
            {
                string type = actionElement.GetString() ?? string.Empty;

                switch (type)
                {
                    case "INIT":
                        Console.WriteLine("Handling handshake request...");
                        return HandleInit(jsonDoc);
                    case "PAYLOAD":
                        Console.WriteLine("Handling Payload request...");
                        return HandlePayload(jsonDoc);
                    default:
                        Console.WriteLine($"Unknown action: {type}");
                        return "UNKNOWN ACTION SENT OVER BLUETOOTH";
                }
            }
            else
            {
                Console.WriteLine("Invalid request: 'action' property missing.");
                return " INVALID REQUEST ";
            }
        }

        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
            return "JSON PARSE ERROR";
        }
    }

    private static string HandleInit(JsonDocument pkJson)
    {
        var root = pkJson.RootElement;

        Console.WriteLine(root);

        // get the uid and public key
        if (root.TryGetProperty("uuid", out JsonElement uidElement) &&
            root.TryGetProperty("key", out JsonElement pkElement))
        {
            string uid = uidElement.GetString() ?? string.Empty;
            string publicKey = pkElement.GetString() ?? string.Empty;

            Console.WriteLine("Handeled INIT from " + uid);

            _keypair = X25519KeyAgreement.GenerateKeyPair();

            // compute shared secret
            _sharedSecret = X25519KeyAgreement.Agreement(_keypair.PrivateKey, Convert.FromHexString(publicKey));

            Console.WriteLine("Generated the shared secret: " + Convert.ToBase64String(_sharedSecret));

            return JsonSerializer.Serialize(new BluetoothMessage
            {
                type = "RESPONSE",
                uuid = uid,
                key = Convert.ToHexStringLower(_keypair.PublicKey)
            });
        }

        return JsonSerializer.Serialize(new BluetoothMessage
        {
            type = "ACK",
            uuid = null,
            ack = false
        });
    }

    private static string HandlePayload(JsonDocument payloadJson)
    {
        var root = payloadJson.RootElement;

        // get the uid and payload
        if (root.TryGetProperty("uuid", out JsonElement uidElement) &&
            root.TryGetProperty("payload", out JsonElement payloadElement))
        {
            string uid = uidElement.GetString() ?? string.Empty;
            string payloadBase64 = payloadElement.GetString() ?? string.Empty;

            Console.WriteLine("Handeled PAYLOAD from " + uid);

            if (string.IsNullOrEmpty(payloadBase64))
            {
                Console.WriteLine($"Error from {uid}: Payload was empty.");
                return JsonSerializer.Serialize(new BluetoothMessage
                {
                    type = "ACK",
                    uuid = uid,
                    ack = false
                });
            }

            byte[] encryptedPayloadBytes;
            try
            {
                // 1. Convert the Base64 string back into a byte array
                encryptedPayloadBytes = Convert.FromBase64String(payloadBase64);
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Error from {uid}: Payload is not valid Base64. {ex.Message}");
                return JsonSerializer.Serialize(new BluetoothMessage
                {
                    type = "ACK",
                    uuid = uid,
                    ack = false
                });
            }

            // 2. Try to decrypt the payload using your shared secret
            //    (This assumes _sharedSecret is a class-level variable)
            if (TryDecryptPayload(encryptedPayloadBytes, _sharedSecret, out string decryptedMessage))
            {
                // 3. Success! Console.WriteLine the *decrypted* message
                Console.WriteLine($"Received payload from {uid}: {decryptedMessage}");

                Console.WriteLine(decryptedMessage);

                // 4. Parse the decrypted JSON to extract time_slot, signature, and data
                try
                {
                    var payloadData = JsonSerializer.Deserialize<DecryptedPayload>(decryptedMessage);

                    if (payloadData != null)
                    {
                        Console.WriteLine($"Time Slot: {payloadData.time_slot}");
                        Console.WriteLine($"Signature: {payloadData.signature}");
                        Console.WriteLine($"Data: {payloadData.data}");

                        // Validate the time_slot (check if within 2 minutes)
                        if (!string.IsNullOrEmpty(payloadData.time_slot))
                        {
                            bool isValidTime =
                                LicenseValidatorLibrary.LicenseValidator.IsValidTime(payloadData.time_slot);
                            Console.WriteLine(
                                $"Time validation: {(isValidTime ? "VALID (within 2 minutes)" : "INVALID (more than 2 minutes passed)")}");

                            if (!isValidTime)
                            {
                                Console.WriteLine($"ERROR: Time slot expired for {uid}");
                                return JsonSerializer.Serialize(new BluetoothMessage
                                {
                                    type = "ACK",
                                    uuid = uid,
                                    ack = false,
                                });
                            }

                            if (!string.IsNullOrEmpty(payloadData.data))
                            {
                                // Validate the license key

                                var (isValid, payload, error) =
                                    LicenseValidatorLibrary.LicenseValidator.ValidateJwtToken(payloadData.data);

                                if (isValid && payload != null)
                                {
                                    Console.WriteLine($"License validation SUCCESS for {uid}. License Level: {payload.Level}");
                                    
                                    var handlers = LicenseValidated;
                                    
                                    if (handlers != null)
                                    {
                                        Task.Run(() =>
                                        {
                                            try { handlers.Invoke(); }
                                            catch (Exception ex) { Console.WriteLine($"LicenseValidated handler threw: {ex}"); }
                                        });
                                    }
                                    
                                    return JsonSerializer.Serialize(new BluetoothMessage
                                    {
                                        type = "ACK",
                                        uuid = uid,
                                        ack = true,
                                    });
                                }
                                else
                                {
                                    Console.WriteLine($"ERROR: License validation failed for {uid}. {error}");
                                    return JsonSerializer.Serialize(new BluetoothMessage
                                    {
                                        type = "ACK",
                                        uuid = uid,
                                        ack = false,
                                    });
                                }
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error parsing decrypted payload: {ex.Message}");
                }

                return JsonSerializer.Serialize(new BluetoothMessage
                {
                    type = "ACK",
                    uuid = uid,
                    ack = true,
                });
            }
            else
            {
                // 4. Failure. The error was already logged by TryDecryptPayload.
                return JsonSerializer.Serialize(new BluetoothMessage
                {
                    type = "ACK",
                    uuid = uid,
                    ack = false
                });
            }
        }

        return JsonSerializer.Serialize(new BluetoothMessage
        {
            type = "ACK",
            uuid = null,
            ack = false
        });
    }

    private static bool TryDecryptPayload(byte[] payload, byte[]? sharedSecret, out string decryptedMessage)
    {
        decryptedMessage = string.Empty;

        // --- 1. Validate Inputs ---
        if (sharedSecret == null || sharedSecret.Length != 32)
        {
            Console.WriteLine("DECRYPTION ERROR: Shared secret is missing or not 32 bytes.");
            return false;
        }

        const int NonceSize = 12; // 96 bits
        const int TagSize = 16; // 128 bits

        if (payload == null || payload.Length < NonceSize + TagSize)
        {
            Console.WriteLine("DECRYPTION ERROR: Payload is too small to contain nonce and tag.");
            return false;
        }
        
        // --- 2. Parse the Payload ---
        var payloadSpan = payload.AsSpan();
        var nonce = payloadSpan.Slice(0, NonceSize);
        var tag = payloadSpan.Slice(payload.Length - TagSize, TagSize);
        var ciphertext = payloadSpan.Slice(NonceSize, payload.Length - NonceSize - TagSize);

        // Buffer for the decrypted data
        byte[] plaintextBytes = new byte[ciphertext.Length];

        // --- 3. Decrypt and Authenticate ---
        try
        {
            byte[] salt = new byte[12]; // Example salt, should match encryption
            byte[] info = Encoding.UTF8.GetBytes("encryption");
            int outputLength = 32; // Length of the derived key

            byte[] derivedKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                sharedSecret,
                outputLength,
                salt,
                info
            );

            using var aesGcm = new AesGcm(derivedKey);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);

            // --- 4. Success ---
            decryptedMessage = Encoding.UTF8.GetString(plaintextBytes);
            return true;
        }
        catch (CryptographicException ex)
        {
            // This catches authentication failures (bad tag / tampered data)
            Console.WriteLine($"DECRYPTION FAILED: Invalid tag. {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DECRYPTION FAILED: Unexpected error. {ex.Message}");
            return false;
        }
    }
    
}