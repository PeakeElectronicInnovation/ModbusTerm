using ModbusTerm.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModbusTerm.Helpers
{
    /// <summary>
    /// Handles importing and exporting register configurations with checksum validation
    /// </summary>
    public static class RegisterFileHandler
    {
        // File structure version to support future format changes
        private const int FILE_VERSION = 1;
        
        /// <summary>
        /// Container for serializing register data with checksums
        /// </summary>
        private class RegisterFileContainer
        {
            [JsonPropertyName("version")]
            public int Version { get; set; } = FILE_VERSION;
            
            [JsonPropertyName("timestamp")]
            public DateTime Timestamp { get; set; } = DateTime.Now;
            
            [JsonPropertyName("registerType")]
            public string RegisterType { get; set; } = string.Empty;
            
            [JsonPropertyName("data")]
            public string RegisterData { get; set; } = string.Empty;
            
            [JsonPropertyName("checksum")]
            public string Checksum { get; set; } = string.Empty;
            
            [JsonPropertyName("metadata")]
            public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        }
        
        /// <summary>
        /// Exports all register types to a single file
        /// </summary>
        /// <param name="holdingRegisters">Collection of holding registers</param>
        /// <param name="inputRegisters">Collection of input registers</param>
        /// <param name="coils">Collection of coils</param>
        /// <param name="discreteInputs">Collection of discrete inputs</param>
        /// <param name="filePath">Path to save the file</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool ExportAllRegisters(
            IEnumerable<RegisterDefinition> holdingRegisters,
            IEnumerable<RegisterDefinition> inputRegisters,
            IEnumerable<BooleanRegisterDefinition> coils,
            IEnumerable<BooleanRegisterDefinition> discreteInputs,
            string filePath)
        {
            try
            {
                // Generate a consistent data string for checksum calculation
                string checksumData = GenerateChecksumData(
                    holdingRegisters.ToList(),
                    inputRegisters.ToList(),
                    coils.ToList(),
                    discreteInputs.ToList());
                
                // Calculate checksum on this consistent string representation
                string checksum = CalculateChecksum(checksumData);
                
                var exportData = new
                {
                    Version = "1.0",
                    Timestamp = DateTime.Now.ToString("o"),
                    RegisterCount = holdingRegisters.Count() + inputRegisters.Count() + coils.Count() + discreteInputs.Count(),
                    HoldingRegisters = holdingRegisters.ToList(),
                    InputRegisters = inputRegisters.ToList(),
                    Coils = coils.ToList(),
                    DiscreteInputs = discreteInputs.ToList()
                };

                // Prepare final export data with checksum
                var finalExportData = new
                {
                    Checksum = checksum,
                    Data = exportData
                };

                // Serialize final data
                string finalJsonData = JsonSerializer.Serialize(finalExportData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Write to file
                File.WriteAllText(filePath, finalJsonData);

                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Result class for importing all register types
        /// </summary>
        public class AllRegistersImportResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public List<RegisterDefinition> HoldingRegisters { get; set; } = new List<RegisterDefinition>();
            public List<RegisterDefinition> InputRegisters { get; set; } = new List<RegisterDefinition>();
            public List<BooleanRegisterDefinition> Coils { get; set; } = new List<BooleanRegisterDefinition>();
            public List<BooleanRegisterDefinition> DiscreteInputs { get; set; } = new List<BooleanRegisterDefinition>();
        }

        /// <summary>
        /// Imports all register types from a file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Result containing all register types if successful</returns>
        public static AllRegistersImportResult ImportAllRegisters(string filePath)
        {
            try
            {
                // Read file content
                string jsonContent = File.ReadAllText(filePath);

                // First, deserialize to get the checksum
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                // Verify file structure
                if (!root.TryGetProperty("Checksum", out var checksumProperty) ||
                    !root.TryGetProperty("Data", out var dataProperty))
                {
                    return new AllRegistersImportResult { Success = false, ErrorMessage = "Invalid file format" };
                }

                string storedChecksum = checksumProperty.GetString() ?? string.Empty;
                string dataJson = dataProperty.GetRawText();

                // Extract the register data for checksum verification
                List<RegisterDefinition>? holdingRegisters = null;
                List<RegisterDefinition>? inputRegisters = null;
                List<BooleanRegisterDefinition>? coils = null;
                List<BooleanRegisterDefinition>? discreteInputs = null;
                
                // Define deserialization options
                var deserializeOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                // Extract the register collections
                if (dataProperty.TryGetProperty("HoldingRegisters", out var holdingRegistersElement))
                {
                    holdingRegisters = JsonSerializer.Deserialize<List<RegisterDefinition>>(holdingRegistersElement.GetRawText(), deserializeOptions);
                }
                
                if (dataProperty.TryGetProperty("InputRegisters", out var inputRegistersElement))
                {
                    inputRegisters = JsonSerializer.Deserialize<List<RegisterDefinition>>(inputRegistersElement.GetRawText(), deserializeOptions);
                }
                
                if (dataProperty.TryGetProperty("Coils", out var coilsElement))
                {
                    coils = JsonSerializer.Deserialize<List<BooleanRegisterDefinition>>(coilsElement.GetRawText(), deserializeOptions);
                }
                
                if (dataProperty.TryGetProperty("DiscreteInputs", out var discreteInputsElement))
                {
                    discreteInputs = JsonSerializer.Deserialize<List<BooleanRegisterDefinition>>(discreteInputsElement.GetRawText(), deserializeOptions);
                }
                
                // Generate a consistent data string for checksum calculation
                string checksumData = GenerateChecksumData(
                    holdingRegisters ?? new List<RegisterDefinition>(),
                    inputRegisters ?? new List<RegisterDefinition>(),
                    coils ?? new List<BooleanRegisterDefinition>(),
                    discreteInputs ?? new List<BooleanRegisterDefinition>());
                
                // Calculate and verify checksum
                string calculatedChecksum = CalculateChecksum(checksumData);
                
                if (storedChecksum != calculatedChecksum)
                {
                    return new AllRegistersImportResult { Success = false, ErrorMessage = "Checksum verification failed. File may be corrupted or tampered with." };
                }

                // Check register type matches what we expect
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Full deserialization
                var allRegisters = new AllRegistersImportResult
                {
                    Success = true,
                    HoldingRegisters = new List<RegisterDefinition>(),
                    InputRegisters = new List<RegisterDefinition>(),
                    Coils = new List<BooleanRegisterDefinition>(),
                    DiscreteInputs = new List<BooleanRegisterDefinition>()
                };

                // We already have the deserialized data, use it for the result
                allRegisters.HoldingRegisters = holdingRegisters ?? new List<RegisterDefinition>();
                allRegisters.InputRegisters = inputRegisters ?? new List<RegisterDefinition>();
                allRegisters.Coils = coils ?? new List<BooleanRegisterDefinition>();
                allRegisters.DiscreteInputs = discreteInputs ?? new List<BooleanRegisterDefinition>();

                return allRegisters;
            }
            catch (Exception ex)
            {
                return new AllRegistersImportResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Generates a consistent string representation of register data for checksumming
        /// </summary>
        /// <remarks>
        /// This creates a deterministic string representation that doesn't depend on
        /// the JSON serialization format, ensuring import and export checksums match
        /// </remarks>
        private static string GenerateChecksumData(
            List<RegisterDefinition> holdingRegisters,
            List<RegisterDefinition> inputRegisters, 
            List<BooleanRegisterDefinition> coils,
            List<BooleanRegisterDefinition> discreteInputs)
        {
            StringBuilder sb = new StringBuilder();
            
            // Add holding registers
            foreach (var reg in holdingRegisters.OrderBy(r => r.Address))
            {
                sb.AppendLine($"HR|{reg.Address}|{reg.Value}|{reg.Name}|{reg.Description}");
            }
            
            // Add input registers
            foreach (var reg in inputRegisters.OrderBy(r => r.Address))
            {
                sb.AppendLine($"IR|{reg.Address}|{reg.Value}|{reg.Name}|{reg.Description}");
            }
            
            // Add coils
            foreach (var reg in coils.OrderBy(r => r.Address))
            {
                sb.AppendLine($"C|{reg.Address}|{reg.Value}|{reg.Name}|{reg.Description}");
            }
            
            // Add discrete inputs
            foreach (var reg in discreteInputs.OrderBy(r => r.Address))
            {
                sb.AppendLine($"DI|{reg.Address}|{reg.Value}|{reg.Name}|{reg.Description}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Calculates a checksum for the provided data string
        /// </summary>
        private static string CalculateChecksum(string data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                // Convert the input string to a byte array and compute the hash
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                byte[] hash = sha256.ComputeHash(bytes);

                // Convert the byte array to a hexadecimal string
                StringBuilder result = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    result.Append(hash[i].ToString("x2"));
                }

                return result.ToString();
            }
        }
    }
}
