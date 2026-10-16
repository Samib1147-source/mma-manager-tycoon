using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace MmaManagerTycoon
{
    [Serializable]
    public sealed class CareerProfile
    {
        public int version = 1;
        public string fighterName = "";
        public string nickname = "";
        public string nationalityCode = "";
        public int skinTone = 2;
        public LifeProgress life;
    }

    public static class CareerProfiles
    {
        public sealed class Country
        {
            public string Code { get; }
            public string Name { get; }
            public Country(string code, string name) { Code = code; Name = name; }
        }

        public static readonly IReadOnlyList<Country> Countries = BuildCountries();
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "career-fighter.json");
        static List<Country> BuildCountries()
        {
            var result = new Dictionary<string, Country>();
            var catalog = Resources.Load<TextAsset>("CareerCountries");
            if (!catalog) throw new InvalidOperationException("Career nationality catalog is missing.");
            foreach (string line in catalog.text.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var parts = line.Trim().Split('\t');
                if (parts.Length == 2) result[parts[0]] = new Country(parts[0], parts[1]);
            }
            return result.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static Country FindCountry(string code) => Countries.FirstOrDefault(c => c.Code == code);
        public static string Clean(string value) => string.Join(" ", (value ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        public static string Validate(CareerProfile profile)
        {
            if (profile == null || profile.version != 1) return "This fighter profile cannot be read.";
            string name = Clean(profile.fighterName), nick = Clean(profile.nickname);
            if (name.Length == 0) return "Enter your fighter’s name to save your profile.";
            if (name.Length > 32 || name.Any(char.IsControl)) return "Use a fighter name of up to 32 characters.";
            if (nick.Length > 24 || nick.Any(char.IsControl)) return "Use a nickname of up to 24 characters.";
            if (FindCountry(profile.nationalityCode) == null) return "Choose your fighter’s nationality.";
            if (profile.skinTone < 0 || profile.skinTone >= FighterAppearance.ToneCount) return "Choose a skin tone.";
            try { FighterLife.Evaluate(profile.life); }
            catch (ArgumentException) { return "The saved life story is invalid."; }
            return null;
        }
        public static void Save(CareerProfile profile, string path)
        {
            var error = Validate(profile);
            if (error != null) throw new ArgumentException(error);
            profile.fighterName = Clean(profile.fighterName);
            profile.nickname = Clean(profile.nickname);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(profile, true));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        public static CareerProfile Load(string path)
        {
            if (!File.Exists(path)) return null;
            var profile = JsonUtility.FromJson<CareerProfile>(File.ReadAllText(path));
            string error = Validate(profile);
            if (error != null) throw new InvalidDataException(error);
            return profile;
        }
    }
}
