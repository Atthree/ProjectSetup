using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Atthree.ProjectSetup
{
    /// <summary>What a setup step did, line by line, for the window and the Console.</summary>
    public class SetupReport
    {
        public enum Kind { Done, Skipped, Warning, Error }

        public readonly struct Line
        {
            public readonly Kind kind;
            public readonly string text;
            public Line(Kind kind, string text) { this.kind = kind; this.text = text; }
            public override string ToString() => $"[{kind}] {text}";
        }

        public readonly List<Line> lines = new List<Line>();

        /// <summary>Set by the settings step when Unity has to restart (input handling).</summary>
        public bool restartRequired;

        public void Done(string text) => lines.Add(new Line(Kind.Done, text));
        public void Skipped(string text) => lines.Add(new Line(Kind.Skipped, text));
        public void Warning(string text) => lines.Add(new Line(Kind.Warning, text));
        public void Error(string text) => lines.Add(new Line(Kind.Error, text));

        public int Count(Kind kind) => lines.Count(l => l.kind == kind);

        public void Clear()
        {
            lines.Clear();
            restartRequired = false;
        }

        public void LogToConsole()
        {
            var sb = new StringBuilder("[Project Setup]\n");
            foreach (var l in lines) sb.AppendLine(l.ToString());
            if (Count(Kind.Error) > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }
    }

    /// <summary>Asset paths with forward slashes, no "..", no leading/trailing slashes.</summary>
    public static class SetupPaths
    {
        static readonly char[] Invalid = Path.GetInvalidFileNameChars();

        /// <summary>"/Art\\Models/ " → "Art/Models". Returns "" for anything unsafe (.., drive letters, bad characters).</summary>
        public static string Clean(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            var parts = path.Replace('\\', '/').Split('/')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();
            foreach (var part in parts)
            {
                if (part == "." || part == ".." || part.IndexOfAny(Invalid) >= 0 || part.EndsWith("~") || part.StartsWith("."))
                    return "";
            }
            return string.Join("/", parts);
        }

        public static string Combine(string a, string b)
        {
            if (string.IsNullOrEmpty(b)) return a;
            if (string.IsNullOrEmpty(a)) return b;
            return a.TrimEnd('/') + "/" + b.TrimStart('/');
        }

        /// <summary>The folder that holds Assets, Packages and ProjectSettings.</summary>
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    }

    /// <summary>Turning "My Company" and "Super Game 2" into ids and namespaces.</summary>
    public static class SetupNames
    {
        /// <summary>"Super Game 2!" → "SuperGame2"; "2 Fast" → "_2Fast"; "" → fallback.</summary>
        public static string ToIdentifier(string text, string fallback)
        {
            var sb = new StringBuilder();
            bool upperNext = true;
            foreach (var c in text ?? "")
            {
                if (char.IsLetterOrDigit(c) && c < 128)
                {
                    sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                    upperNext = false;
                }
                else upperNext = true;
            }
            if (sb.Length == 0) return fallback;
            if (char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }

        /// <summary>"My Company", "Super Game 2" → "com.mycompany.supergame2".</summary>
        public static string ToApplicationId(string company, string product)
        {
            string Part(string s, string fallback)
            {
                var id = ToIdentifier(s, fallback).ToLowerInvariant();
                return id.StartsWith("_") ? "a" + id : id; // Android: parts must start with a letter
            }
            return $"com.{Part(company, "company")}.{Part(product, "game")}";
        }

        /// <summary>"My Company", "Super Game 2" → "MyCompany.SuperGame2".</summary>
        public static string ToNamespace(string company, string product) =>
            $"{ToIdentifier(company, "Company")}.{ToIdentifier(product, "Game")}";
    }
}
