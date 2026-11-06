using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Bastion.Core
{
    /// <summary>Guarda quais itens dos planos de formatação já foram feitos.</summary>
    public static class PlanStore
    {
        static string FilePath => Path.Combine(Folders.AppData, "plano-formatacao.json");
        static Dictionary<string, bool>? done;

        static Dictionary<string, bool> Done
        {
            get
            {
                if (done != null) return done;
                try { done = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(FilePath)); } catch { }
                return done ??= new();
            }
        }

        public static bool Get(string id) => Done.TryGetValue(id, out var v) && v;

        public static void Set(string id, bool value)
        {
            Done[id] = value;
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(Done)); } catch { }
        }
    }
}
