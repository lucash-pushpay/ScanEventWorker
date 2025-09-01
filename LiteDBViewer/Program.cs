using LiteDB;
using System;
using System.IO;
using System.Linq;

class LiteDBViewer
{
    static void Main(string[] args)
    {
        Console.WriteLine("🍎 LiteDB Viewer for Mac");
        Console.WriteLine($"📅 Current Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"👤 User: {Environment.UserName}");
        Console.WriteLine("=" + new string('=', 50));
        
        // Look for database files in common locations
        var searchPaths = new[]
        {
            "../../../../ScanEventWorker/bin/Debug/net8.0/scanevents.db"
        };
        
        Console.WriteLine($"Current working directory: {Directory.GetCurrentDirectory()}");
        // Print search paths for diagnostics
        Console.WriteLine("Search paths:");
        foreach (var path in searchPaths)
        {
            Console.WriteLine($"  {Path.GetFullPath(path)}");
        }
        // Add absolute path as fallback
        var absoluteDbPath = "/Users/localadmin/RiderProjects/ScanEventService/ScanEventWorker/bin/Debug/net8.0/scanevents.db";
        
        string dbPath = null;
        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                dbPath = path;
                break;
            }
        }
        if (dbPath == null && File.Exists(absoluteDbPath))
        {
            dbPath = absoluteDbPath;
            Console.WriteLine($"Found database at absolute path: {absoluteDbPath}");
        }
        
        // If not found, search recursively
        if (dbPath == null)
        {
            Console.WriteLine("🔍 Searching for .db files recursively from project root...");
            var projectRoot = AppDomain.CurrentDomain.BaseDirectory;
            var dbFiles = Directory.GetFiles(projectRoot, "*.db", SearchOption.AllDirectories);
            
            if (dbFiles.Length == 0)
            {
                Console.WriteLine("❌ No .db files found. Make sure your application has run and created the database.");
                return;
            }
            
            Console.WriteLine($"Found {dbFiles.Length} database file(s):");
            for (int i = 0; i < dbFiles.Length; i++)
            {
                var fileInfo = new FileInfo(dbFiles[i]);
                Console.WriteLine($"  {i + 1}. {dbFiles[i]} ({fileInfo.Length:N0} bytes, modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm})");
            }
            
            if (dbFiles.Length == 1)
            {
                dbPath = dbFiles[0];
                Console.WriteLine($"Using: {dbPath}");
            }
            else
            {
                Console.Write($"Enter number (1-{dbFiles.Length}): ");
                var input = Console.ReadLine();
                if (int.TryParse(input, out int choice) && choice >= 1 && choice <= dbFiles.Length)
                {
                    dbPath = dbFiles[choice - 1];
                }
                else
                {
                    dbPath = dbFiles[0]; // Default to first
                }
            }
        }
        
        Console.WriteLine($"\n📊 Opening database: {dbPath}");
        var fileSize = new FileInfo(dbPath).Length;
        Console.WriteLine($"📏 File size: {fileSize:N0} bytes ({fileSize / 1024.0:F1} KB)");
        Console.WriteLine("=" + new string('=', 50));
        
        try
        {
            using var db = new LiteDatabase(dbPath);
            
            // List all collections with counts
            Console.WriteLine("📋 Collections:");
            var collections = db.GetCollectionNames().ToList();
            if (collections.Count == 0)
            {
                Console.WriteLine("  ❌ No collections found. Database might be empty.");
                return;
            }
            
            foreach (var collection in collections)
            {
                try
                {
                    var count = db.GetCollection(collection).Count();
                    Console.WriteLine($"  • {collection}: {count:N0} records");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  • {collection}: Error reading ({ex.Message})");
                }
            }
            
            Console.WriteLine();
            
            // Show scan events if available
            if (collections.Contains("scan_events"))
            {
                ShowScanEvents(db);
                Console.WriteLine();
            }
            
            // Show parcel states if available
            if (collections.Contains("parcel_states"))
            {
                ShowParcelStates(db);
                Console.WriteLine();
            }
            
            // Interactive query mode
            InteractiveMode(db);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error opening database: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    static void ShowScanEvents(LiteDatabase db)
    {
        Console.WriteLine("📦 Recent Scan Events (Last 10):");
        try
        {
            var collection = db.GetCollection<dynamic>("scan_events");
            var total = collection.Count();
            
            if (total == 0)
            {
                Console.WriteLine("  📭 No scan events found.");
                return;
            }
            
            var events = collection
                .Query()
                .OrderByDescending("EventId")
                .Limit(10)
                .ToList();
            
            // Diagnostic: Print all keys/values for the first 3 events
            Console.WriteLine("  [Diagnostic] First 3 event keys/values:");
            foreach (var evt in events.Take(3))
            {
                var dict = evt as System.Collections.Generic.IDictionary<string, object>;
                if (dict != null)
                {
                    foreach (var kv in dict)
                    {
                        Console.WriteLine($"    {kv.Key}: {kv.Value}");
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("    [Not a dictionary]");
                }
            }
            Console.WriteLine($"  📊 Showing 10 of {total:N0} total events:");
            
            foreach (var evt in events)
            {
                try
                {
                    var dict = evt as System.Collections.Generic.IDictionary<string, object>;
                    if (dict == null)
                    {
                        Console.WriteLine("  ❌ Event is not a dictionary");
                        continue;
                    }
                    string GetStr(string key) => dict.ContainsKey(key) && dict[key] != null ? dict[key].ToString() : "<null>";
                    string eventId = GetStr("EventId") != "<null>" ? GetStr("EventId") : GetStr("_id");
                    Console.WriteLine($"  🔸 EventId: {eventId} | ParcelId: {GetStr("ParcelId")} | Type: {GetStr("Type")}");
                    Console.WriteLine($"     Status: {GetStr("StatusCode")} | Created: {GetStr("CreatedDateTimeUtc")}");
                    Console.WriteLine($"     RunId: {GetStr("RunId")}");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Error displaying event: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error reading scan_events: {ex.Message}");
        }
    }
    
    static void ShowParcelStates(LiteDatabase db)
    {
        Console.WriteLine("📋 Parcel States (First 10):");
        try
        {
            var collection = db.GetCollection<dynamic>("parcel_states");
            var total = collection.Count();
            
            if (total == 0)
            {
                Console.WriteLine("  📭 No parcel states found.");
                return;
            }
            
            var states = collection.FindAll().Take(10);
            Console.WriteLine($"  📊 Showing 10 of {total:N0} total states:");
            
            foreach (var state in states)
            {
                try
                {
                    var dict = state as System.Collections.Generic.IDictionary<string, object>;
                    if (dict == null)
                    {
                        Console.WriteLine("  ❌ State is not a dictionary");
                        continue;
                    }
                    string GetStr(string key) => dict.ContainsKey(key) && dict[key] != null ? dict[key].ToString() : "<null>";
                    var pickup = dict.ContainsKey("PickupDateTime") && dict["PickupDateTime"] != null ? $"📤 {dict["PickupDateTime"]}" : "📤 Not picked up";
                    var delivery = dict.ContainsKey("DeliveryDateTime") && dict["DeliveryDateTime"] != null ? $"📥 {dict["DeliveryDateTime"]}" : "📥 Not delivered";
                    Console.WriteLine($"  🔸 ParcelId: {GetStr("ParcelId")} | LastEventId: {GetStr("LastEventId")}");
                    Console.WriteLine($"     Status: {GetStr("StatusCode")} | Updated: {GetStr("LastUpdated")}");
                    Console.WriteLine($"     {pickup}");
                    Console.WriteLine($"     {delivery}");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Error displaying state: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error reading parcel_states: {ex.Message}");
        }
    }
    
    static void InteractiveMode(LiteDatabase db)
    {
        Console.WriteLine("🔧 Interactive Mode");
        Console.WriteLine("Available commands:");
        Console.WriteLine("  collections                    - List all collections");
        Console.WriteLine("  count <collection>             - Count records");
        Console.WriteLine("  show <collection> [limit]      - Show records (default limit: 5)");
        Console.WriteLine("  find <collection> <field> <value> - Find records");
        Console.WriteLine("  recent <collection> [limit]    - Show recent records");
        Console.WriteLine("  help                           - Show this help");
        Console.WriteLine("  exit                           - Exit interactive mode");
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("litedb> ");
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input) || input.ToLower() == "exit")
                break;
                
            ExecuteCommand(db, input);
        }
    }
    
    static void ExecuteCommand(LiteDatabase db, string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        
        try
        {
            switch (parts[0].ToLower())
            {
                case "collections":
                    foreach (var collection in db.GetCollectionNames())
                    {
                        var collectionCount = db.GetCollection(collection).Count();
                        Console.WriteLine($"  • {collection}: {collectionCount:N0} records");
                    }
                    break;
                
                case "count":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("  Usage: count <collection>");
                        break;
                    }
                    var count = db.GetCollection(parts[1]).Count();
                    Console.WriteLine($"  {parts[1]}: {count:N0} records");
                    break;
                    
                case "show":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("  Usage: show <collection> [limit]");
                        break;
                    }
                    var limit = parts.Length > 2 && int.TryParse(parts[2], out int l) ? l : 5;
                    var items = db.GetCollection<dynamic>(parts[1]).FindAll().Take(limit);
                    var itemCount = 0;
                    foreach (var item in items)
                    {
                        itemCount++;
                        var dict = item as System.Collections.Generic.IDictionary<string, object>;
                        if (dict != null)
                        {
                            Console.WriteLine($"  {itemCount}.");
                            foreach (var kv in dict)
                            {
                                Console.WriteLine($"    {kv.Key}: {kv.Value}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  {itemCount}. {item}");
                        }
                    }
                    if (itemCount == 0)
                        Console.WriteLine($"  No records found in {parts[1]}");
                    break;
                
                case "recent":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("  Usage: recent <collection> [limit]");
                        break;
                    }
                    var recentLimit = parts.Length > 2 && int.TryParse(parts[2], out int rl) ? rl : 5;
                    var recent = db.GetCollection<dynamic>(parts[1])
                        .Query()
                        .OrderByDescending("_id")
                        .Limit(recentLimit)
                        .ToList();
                    var recentCount = 0;
                    foreach (var item in recent)
                    {
                        recentCount++;
                        var dict = item as System.Collections.Generic.IDictionary<string, object>;
                        if (dict != null)
                        {
                            Console.WriteLine($"  {recentCount}.");
                            foreach (var kv in dict)
                            {
                                Console.WriteLine($"    {kv.Key}: {kv.Value}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  {recentCount}. {item}");
                        }
                    }
                    break;
                    
                case "help":
                    Console.WriteLine("  collections                    - List all collections");
                    Console.WriteLine("  count <collection>             - Count records");
                    Console.WriteLine("  show <collection> [limit]      - Show records");
                    Console.WriteLine("  recent <collection> [limit]    - Show recent records");
                    Console.WriteLine("  exit                           - Exit");
                    break;
                    
                default:
                    Console.WriteLine($"  Unknown command: {parts[0]}. Type 'help' for available commands.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error: {ex.Message}");
        }
    }
}
