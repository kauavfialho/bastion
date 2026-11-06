using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Bastion.Core
{
    /// <summary>
    /// Idiomas do app: português (original), inglês e espanhol.
    /// O código continua escrevendo os textos em português; <see cref="Tr"/> traduz na hora de mostrar.
    /// Textos com números no meio entram no dicionário como modelo: "{0} de {1} feitos".
    /// </summary>
    public static partial class I18n
    {
        public static readonly string[] Languages = { "pt", "en", "es" };

        /// <summary>"pt", "en" ou "es".</summary>
        public static string Lang { get; private set; } = "pt";

        public static event Action? Changed;

        static string FilePath => Path.Combine(Folders.AppData, "idioma.txt");

        sealed record Template(Regex Pattern, string Prefix, string En, string Es);

        static readonly Dictionary<string, (string En, string Es)> exact = new(StringComparer.Ordinal);
        static readonly List<Template> templates = new();
        static readonly Dictionary<string, (string En, string Es)> byKey = new(StringComparer.Ordinal);
        static readonly HashSet<string> missing = new(StringComparer.Ordinal);

        static I18n()
        {
            foreach (var (pt, en, es) in Entries())
            {
                if (Regex.IsMatch(pt, @"\{\d+\}"))
                {
                    var literal = Regex.Split(pt, @"\{\d+\}");
                    var pattern = "^" + string.Join("(.+?)", literal.Select(Regex.Escape)) + "$";
                    templates.Add(new Template(new Regex(pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant), literal[0], en, es));
                    byKey[pt] = (en, es);
                }
                else exact[pt] = (en, es);
            }
            // Modelos mais longos primeiro: são mais específicos
            templates.Sort((a, b) => b.Pattern.ToString().Length.CompareTo(a.Pattern.ToString().Length));
        }

        /// <summary>Na primeira vez usa o idioma do Windows; se não for um dos três, inglês.</summary>
        public static void LoadSaved()
        {
            try
            {
                var saved = File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : "";
                if (Languages.Contains(saved)) { Lang = saved; return; }
            }
            catch { }
            var sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            Lang = sys is "pt" or "es" ? sys : "en";
        }

        /// <summary>Usa um idioma só nesta execução, sem salvar.</summary>
        public static void Force(string lang)
        {
            if (Languages.Contains(lang)) Lang = lang;
        }

        public static void Set(string lang)
        {
            if (!Languages.Contains(lang) || lang == Lang) return;
            Lang = lang;
            try { File.WriteAllText(FilePath, lang); } catch { }
            Changed?.Invoke();
        }

        /// <summary>Traduz um texto escrito em português para o idioma atual.</summary>
        public static string Tr(string? pt)
        {
            if (string.IsNullOrEmpty(pt) || Lang == "pt") return pt ?? "";
            if (exact.TryGetValue(pt, out var t)) return Lang == "en" ? t.En : t.Es;

            // Espaços nas pontas (comuns em textos montados) não atrapalham a busca
            var trimmed = pt.Trim();
            if (trimmed.Length != pt.Length && exact.TryGetValue(trimmed, out t))
                return pt.Replace(trimmed, Lang == "en" ? t.En : t.Es);

            foreach (var tpl in templates)
            {
                if (!pt.StartsWith(tpl.Prefix, StringComparison.Ordinal)) continue;
                var m = tpl.Pattern.Match(pt);
                if (!m.Success) continue;
                // As partes variáveis também são traduzidas se forem textos conhecidos (ex.: "Ligado")
                var args = m.Groups.Cast<Group>().Skip(1)
                    .Select(g => exact.TryGetValue(g.Value, out var a) ? (Lang == "en" ? a.En : a.Es) : g.Value)
                    .ToArray<object>();
                try { return string.Format(Lang == "en" ? tpl.En : tpl.Es, args); }
                catch (FormatException) { return pt; }
            }

            Note(pt);
            return pt;
        }

        /// <summary>Traduz um modelo do dicionário ("{0} de {1} feitos") e preenche os valores.</summary>
        public static string Tr(string pt, params object[] args)
        {
            if (Lang != "pt" && byKey.TryGetValue(pt, out var t)) return string.Format(Lang == "en" ? t.En : t.Es, args);
            if (Lang != "pt" && !exact.ContainsKey(pt)) Note(pt);
            return string.Format(Tr(pt), args);
        }

        /// <summary>Em modo de teste, guarda os textos que ainda não têm tradução.</summary>
        static void Note(string pt)
        {
            if (!pt.Any(char.IsLetter)) return;   // só números e símbolos não precisam de tradução
            if (Environment.GetEnvironmentVariable("BASTION_I18N_LOG") is not { Length: > 0 } log) return;
            lock (missing)
            {
                if (!missing.Add(pt)) return;
                try { File.AppendAllText(log, pt.Replace("\r", "").Replace("\n", "\\n") + Environment.NewLine); } catch { }
            }
        }
    }
}
