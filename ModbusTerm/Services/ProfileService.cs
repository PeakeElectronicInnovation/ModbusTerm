using ModbusTerm.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModbusTerm.Services
{
    /// <summary>
    /// Service for managing connection profiles
    /// </summary>
    public class ProfileService
    {
        private const string PROFILES_DIRECTORY = "Profiles";
        private const string DEFAULT_PROFILE_NAME = "Default Profile";
        private const string FILE_EXTENSION = ".json";
        private readonly string _profilesPath;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Initialize a new instance of the ProfileService class
        /// </summary>
        public ProfileService()
        {
            // Set up profiles directory
            _profilesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ModbusTerm",
                PROFILES_DIRECTORY);

            // Create directory if it doesn't exist
            Directory.CreateDirectory(_profilesPath);
            
            // Create default profile if it doesn't exist - do this async but don't block constructor
            _ = Task.Run(async () => await CreateDefaultProfileIfNeeded());
        }
        
        /// <summary>
        /// Gets all available profiles
        /// </summary>
        public async Task<List<string>> GetProfileNamesAsync()
        {
            List<string> profiles = new List<string>();
            
            try
            {
                // Get all JSON files in the profiles directory
                string[] files = Directory.GetFiles(_profilesPath, $"*{FILE_EXTENSION}");
                
                // Extract profile names (without extension)
                foreach (string file in files)
                {
                    string profileName = Path.GetFileNameWithoutExtension(file);
                    profiles.Add(profileName);
                }
                
                // If we don't have a Default profile yet, create it
                if (!profiles.Contains(DEFAULT_PROFILE_NAME))
                {
                    await CreateDefaultProfileIfNeeded();
                    profiles.Add(DEFAULT_PROFILE_NAME);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting profiles: {ex.Message}");
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Saves a connection profile to a file
        /// </summary>
        /// <param name="parameters">The connection parameters to save</param>
        /// <param name="profileName">Name to save the profile as</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SaveProfileAsync(ConnectionParameters parameters, string profileName)
        {
            try
            {
                // Update profile name in parameters
                parameters.ProfileName = profileName;
                
                // Create file path
                string filePath = Path.Combine(_profilesPath, $"{profileName}{FILE_EXTENSION}");
                
                // Serialize the parameters to JSON
                string json = JsonSerializer.Serialize(parameters, parameters.GetType(), _jsonOptions);
                
                // Write to file
                await File.WriteAllTextAsync(filePath, json);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Loads a connection profile from a file
        /// </summary>
        /// <param name="profileName">Name of the profile to load</param>
        /// <returns>The loaded connection parameters, or null if loading failed</returns>
        public async Task<ConnectionParameters?> LoadProfileAsync(string profileName)
        {
            try
            {
                // Create file path
                string filePath = Path.Combine(_profilesPath, $"{profileName}{FILE_EXTENSION}");
                
                // Check if file exists
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Profile file not found: {filePath}");
                    
                    // If trying to load the default profile and it doesn't exist, create it
                    if (profileName == DEFAULT_PROFILE_NAME)
                    {
                        await CreateDefaultProfileIfNeeded();
                        return CreateDefaultProfile();
                    }
                    
                    return null;
                }
                
                // Read JSON from file
                string json = await File.ReadAllTextAsync(filePath);
                
                // First deserialize to determine the type
                JsonDocument doc = JsonDocument.Parse(json);
                ConnectionType type = ConnectionType.TCP;
                
                // Check for connection type in the JSON
                if (doc.RootElement.TryGetProperty("type", out JsonElement typeElement))
                {
                    type = (ConnectionType)typeElement.GetInt32();
                }
                
                // Deserialize to the appropriate type
                ConnectionParameters? parameters = type switch
                {
                    ConnectionType.TCP => JsonSerializer.Deserialize<TcpConnectionParameters>(json, _jsonOptions),
                    ConnectionType.RTU => JsonSerializer.Deserialize<RtuConnectionParameters>(json, _jsonOptions),
                    _ => null
                };
                
                // Set the profile name
                if (parameters != null)
                {
                    parameters.ProfileName = profileName;
                }
                
                return parameters;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading profile: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Creates the default profile if it doesn't exist
        /// </summary>
        private async Task CreateDefaultProfileIfNeeded()
        {
            string defaultFilePath = Path.Combine(_profilesPath, $"{DEFAULT_PROFILE_NAME}{FILE_EXTENSION}");
            
            if (!File.Exists(defaultFilePath))
            {
                // Create default profile
                ConnectionParameters defaultProfile = CreateDefaultProfile();
                
                // Save to file
                await SaveProfileAsync(defaultProfile, DEFAULT_PROFILE_NAME);
            }
        }
        
        /// <summary>
        /// Deletes a profile by name
        /// </summary>
        /// <param name="profileName">The name of the profile to delete</param>
        /// <returns>True if the profile was deleted, false otherwise</returns>
        public async Task<bool> DeleteProfileAsync(string profileName)
        {
            // Don't allow deleting the default profile
            if (profileName == DEFAULT_PROFILE_NAME)
                return false;
                
            try
            {
                string profilePath = Path.Combine(_profilesPath, $"{profileName}{FILE_EXTENSION}");
                
                // Check if the file exists
                if (File.Exists(profilePath))
                {
                    // Delete the file asynchronously
                    await Task.Run(() => File.Delete(profilePath));
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Creates a default profile with TCP Master mode and loopback IP
        /// </summary>
        private static ConnectionParameters CreateDefaultProfile()
        {
            return new TcpConnectionParameters
            {
                ProfileName = DEFAULT_PROFILE_NAME,
                IsMaster = true,
                IpAddress = "127.0.0.1",
                Port = 502
            };
        }
    }
}
