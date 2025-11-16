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

                // Parse JSON to extract register data
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                // Verify file structure - check for Data property
                if (!root.TryGetProperty("Data", out var dataProperty))
                {
                    return new AllRegistersImportResult { Success = false, ErrorMessage = "Invalid file format - missing Data section" };
                }

                // Define deserialization options
                var deserializeOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                // Extract the register collections
                List<RegisterDefinition>? holdingRegisters = null;
                List<RegisterDefinition>? inputRegisters = null;
                List<BooleanRegisterDefinition>? coils = null;
                List<BooleanRegisterDefinition>? discreteInputs = null;
                
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

                // Validate the imported data
                if (holdingRegisters == null || inputRegisters == null || coils == null || discreteInputs == null)
                {
                    return new AllRegistersImportResult { Success = false, ErrorMessage = "Invalid file format - missing register collections" };
                }

                // Create result with imported data
                var allRegisters = new AllRegistersImportResult
                {
                    Success = true,
                    HoldingRegisters = holdingRegisters,
                    InputRegisters = inputRegisters,
                    Coils = coils,
                    DiscreteInputs = discreteInputs
                };

                // Reconstruct multi-register data types after JSON deserialization
                ReconstructMultiRegisterData(allRegisters.HoldingRegisters);
                ReconstructMultiRegisterData(allRegisters.InputRegisters);

                return allRegisters;
            }
            catch (Exception ex)
            {
                return new AllRegistersImportResult { Success = false, ErrorMessage = ex.Message };
            }
        }
        
        /// <summary>
        /// Reconstructs multi-register data types after JSON deserialization
        /// </summary>
        /// <param name="registers">List of registers to reconstruct</param>
        private static void ReconstructMultiRegisterData(List<RegisterDefinition> registers)
        {
            foreach (var register in registers)
            {
                // Skip registers that don't have additional values (single-register types)
                if (register.AdditionalValues.Count == 0)
                    continue;
                
                // Suppress notifications during reconstruction to prevent recursive updates
                bool oldSuppressNotifications = register.SuppressNotifications;
                register.SuppressNotifications = true;
                
                try
                {
                    switch (register.DataType)
                    {
                        case ModbusDataType.UInt32:
                            // Reconstruct from Value + AdditionalValues[0]
                            if (register.AdditionalValues.Count >= 1)
                            {
                                uint reconstructedValue;
                                if (register.WordOrder == WordOrder.LowWordFirst)
                                {
                                    // Low Word First (CDAB): Value = Low Word, AdditionalValues[0] = High Word
                                    reconstructedValue = (uint)(register.AdditionalValues[0] << 16 | register.Value);
                                }
                                else
                                {
                                    // High Word First (ABCD): Value = High Word, AdditionalValues[0] = Low Word
                                    reconstructedValue = (uint)(register.Value << 16 | register.AdditionalValues[0]);
                                }
                                register.SetUInt32Value(reconstructedValue);
                            }
                            break;
                            
                        case ModbusDataType.Int32:
                            // Reconstruct from Value + AdditionalValues[0]
                            if (register.AdditionalValues.Count >= 1)
                            {
                                int reconstructedValue;
                                if (register.WordOrder == WordOrder.LowWordFirst)
                                {
                                    // Low Word First (CDAB): Value = Low Word, AdditionalValues[0] = High Word
                                    reconstructedValue = (int)(register.AdditionalValues[0] << 16 | register.Value);
                                }
                                else
                                {
                                    // High Word First (ABCD): Value = High Word, AdditionalValues[0] = Low Word
                                    reconstructedValue = (int)(register.Value << 16 | register.AdditionalValues[0]);
                                }
                                register.SetInt32Value(reconstructedValue);
                            }
                            break;
                            
                        case ModbusDataType.Float32:
                            // Reconstruct from Value + AdditionalValues[0]
                            if (register.AdditionalValues.Count >= 1)
                            {
                                byte[] bytes = new byte[4];
                                
                                if (register.WordOrder == WordOrder.LowWordFirst)
                                {
                                    // Low Word First (CDAB): Value = Low Word, AdditionalValues[0] = High Word
                                    bytes[0] = (byte)(register.Value & 0xFF);
                                    bytes[1] = (byte)(register.Value >> 8);
                                    bytes[2] = (byte)(register.AdditionalValues[0] & 0xFF);
                                    bytes[3] = (byte)(register.AdditionalValues[0] >> 8);
                                }
                                else
                                {
                                    // High Word First (ABCD): Value = High Word, AdditionalValues[0] = Low Word
                                    bytes[0] = (byte)(register.AdditionalValues[0] & 0xFF);
                                    bytes[1] = (byte)(register.AdditionalValues[0] >> 8);
                                    bytes[2] = (byte)(register.Value & 0xFF);
                                    bytes[3] = (byte)(register.Value >> 8);
                                }
                                
                                float reconstructedValue = BitConverter.ToSingle(bytes, 0);
                                register.SetFloat32Value(reconstructedValue);
                                
                                // Directly set EditableValue to match ASCII reconstruction approach
                                register.SuppressNotifications = false;
                                register.EditableValue = reconstructedValue.ToString();
                            }
                            break;
                            
                        case ModbusDataType.Float64:
                            // Reconstruct from Value + AdditionalValues[0-2]
                            if (register.AdditionalValues.Count >= 3)
                            {
                                byte[] bytes = new byte[8];
                                
                                if (register.WordOrder == WordOrder.LowWordFirst)
                                {
                                    // Low Word First (CDAB): Value = Word0, AdditionalValues[0] = Word1, AdditionalValues[1] = Word2, AdditionalValues[2] = Word3
                                    bytes[0] = (byte)(register.Value & 0xFF);
                                    bytes[1] = (byte)(register.Value >> 8);
                                    bytes[2] = (byte)(register.AdditionalValues[0] & 0xFF);
                                    bytes[3] = (byte)(register.AdditionalValues[0] >> 8);
                                    bytes[4] = (byte)(register.AdditionalValues[1] & 0xFF);
                                    bytes[5] = (byte)(register.AdditionalValues[1] >> 8);
                                    bytes[6] = (byte)(register.AdditionalValues[2] & 0xFF);
                                    bytes[7] = (byte)(register.AdditionalValues[2] >> 8);
                                }
                                else
                                {
                                    // High Word First (ABCD): Value = Word3, AdditionalValues[0] = Word2, AdditionalValues[1] = Word1, AdditionalValues[2] = Word0
                                    bytes[0] = (byte)(register.AdditionalValues[2] & 0xFF);
                                    bytes[1] = (byte)(register.AdditionalValues[2] >> 8);
                                    bytes[2] = (byte)(register.AdditionalValues[1] & 0xFF);
                                    bytes[3] = (byte)(register.AdditionalValues[1] >> 8);
                                    bytes[4] = (byte)(register.AdditionalValues[0] & 0xFF);
                                    bytes[5] = (byte)(register.AdditionalValues[0] >> 8);
                                    bytes[6] = (byte)(register.Value & 0xFF);
                                    bytes[7] = (byte)(register.Value >> 8);
                                }
                                
                                double reconstructedValue = BitConverter.ToDouble(bytes, 0);
                                register.SetFloat64Value(reconstructedValue);
                                
                                // Directly set EditableValue to match ASCII reconstruction approach
                                register.SuppressNotifications = false;
                                register.EditableValue = reconstructedValue.ToString();
                            }
                            break;
                            
                        case ModbusDataType.AsciiString:
                            // Reconstruct from Value + AdditionalValues
                            var chars = new List<char>();
                            
                            // Extract characters from Value (high byte first, then low byte - matches SetAsciiStringValue encoding)
                            chars.Add((char)(register.Value >> 8)); // First char from high byte
                            chars.Add((char)(register.Value & 0xFF)); // Second char from low byte
                                
                            // Extract characters from AdditionalValues (same encoding pattern)
                            foreach (var reg in register.AdditionalValues)
                            {
                                chars.Add((char)(reg >> 8)); // First char from high byte
                                chars.Add((char)(reg & 0xFF)); // Second char from low byte
                            }
                            
                            // Remove null terminators and create string
                            string reconstructedString = new string(chars.Where(c => c != '\0').ToArray());
                            
                            // Re-enable notifications and directly set EditableValue to bypass re-encoding
                            register.SuppressNotifications = false;
                            register.EditableValue = reconstructedString;
                            break;
                    }
                }
                finally
                {
                    register.SuppressNotifications = oldSuppressNotifications;
                }
            }
        }
        
        /// <summary>
        /// Gets the full register value including AdditionalValues for multi-register types
        /// </summary>
        /// <param name="register">The register to serialize</param>
        /// <returns>String representation of all register values</returns>
        private static string GetFullRegisterValue(RegisterDefinition register)
        {
            var values = new List<string> { register.Value.ToString() };
            
            // Add additional values for multi-register types
            foreach (var additionalValue in register.AdditionalValues)
            {
                values.Add(additionalValue.ToString());
            }
            
            return string.Join(",", values);
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
                // Include all register values for multi-register types
                string allValues = GetFullRegisterValue(reg);
                sb.AppendLine($"HR|{reg.Address}|{allValues}|{reg.DataType}|{reg.Name}|{reg.Description}");
            }
            
            // Add input registers
            foreach (var reg in inputRegisters.OrderBy(r => r.Address))
            {
                // Include all register values for multi-register types
                string allValues = GetFullRegisterValue(reg);
                sb.AppendLine($"IR|{reg.Address}|{allValues}|{reg.DataType}|{reg.Name}|{reg.Description}");
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
