using System;
using System.Text;
using NModbus;
using NModbus.Data;
using NModbus.Device;

class Program
{
    static void Main()
    {
        var dataStore = new DefaultSlaveDataStore();
        
        // Examine available methods
        Console.WriteLine("Available methods on DefaultSlaveDataStore.HoldingRegisters:");
        
        // Try to access a register (prints the type and available methods)
        var holdingRegisters = dataStore.HoldingRegisters;
        Console.WriteLine($"Type of HoldingRegisters: {holdingRegisters.GetType().FullName}");
        
        // Print available methods
        var methods = holdingRegisters.GetType().GetMethods();
        foreach (var method in methods)
        {
            Console.WriteLine($"Method: {method.Name}, Return: {method.ReturnType}, Parameters: {string.Join(", ", Array.ConvertAll(method.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"))}");
        }
    }
}
