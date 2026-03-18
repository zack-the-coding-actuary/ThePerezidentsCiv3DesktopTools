using System;
using System.Collections.Generic;
using System.Reflection;
using Blast;
using QueryCiv3;
using QueryCiv3.Biq;

namespace Civ3Tools
{
    // Utility to fetch all data on units in the imported scenario file and save them to a spreadsheet.
    public static class GetUnitInfo
    {
        // TODO: Consider returning a new object with all the relevant data we need, which could be more than just a PRTO array, resource names etc.
        private static BiqData GetBiqData(string scenarioFile)
        {
            return BiqData.LoadFile(scenarioFile);
        }

        private static string FormatValue(object? value)
        {
            return value?.ToString() ?? "null";
        }

        private static IEnumerable<int> GetIgnoredTerrainIndexes(PRTOTERR t)
        {
            for (int i = 0; i < 14; i++)
                if (t[i]) yield return i;
        }

        private static Dictionary<string, List<string>> GetUnitDict(string scenarioFile)
        {
            var biqData = GetBiqData(scenarioFile);
            var prtoData = biqData.Prto;
            FieldInfo[] fields = prtoData[0].GetType().GetFields();
            PropertyInfo[] properties = prtoData[0].GetType().GetProperties();
            var unitInfoDict = new Dictionary<string, List<string>>();

            // Add name first
            unitInfoDict.Add("Name", new List<string>());

            // Add all fields now
            foreach (var field in fields)
            {
                unitInfoDict.Add(field.Name, new List<string>());
            }

            // Break AvailableTo into each individual civ choice as dict keys
            foreach (var race in biqData.Race)
            {
                unitInfoDict.Add(race.Name, new List<string>());
            }

            // Finally, rest of properties
            foreach (var property in properties)
            {
                if (property.Name != "Name") // Name is already added as a field, so skip it here to avoid duplication
                    unitInfoDict.Add(property.Name, new List<string>());
            }

            // Delete undesired fields that we know we won't use, to avoid cluttering the spreadsheet with innocuous columns.
            unitInfoDict.Remove("Length");
            unitInfoDict.Remove("IconIndex");
            unitInfoDict.Remove("OtherStrategy");
            unitInfoDict.Remove("AvailableTo");
            unitInfoDict.Remove("TYPE_LAND");
            unitInfoDict.Remove("TYPE_SEA");
            unitInfoDict.Remove("TYPE_AIR");

            // Iterate over all prtoData items and populate the dictionary
            foreach (var prto in prtoData)
            {
                // Add fields
                foreach (var field in fields)
                {
                    if (unitInfoDict.ContainsKey(field.Name)) // Check if the field is one we want to include (after removals)
                    {
                        string valueStr;
                        switch (field.Name)
                        {
                            case "EnslaveResults":
                            case "UpgradeTo":
                                if (int.TryParse(field.GetValue(prto)?.ToString(), out int upgradeToIndex) && upgradeToIndex >= 0 && upgradeToIndex < prtoData.Length)
                                    valueStr = prtoData[upgradeToIndex].Name;
                                else valueStr = "None";
                                break;

                            case "RequiredResource1":
                            case "RequiredResource2":
                            case "RequiredResource3":
                                if (int.TryParse(field.GetValue(prto)?.ToString(), out int resourceIndex) && resourceIndex >= 0 && resourceIndex < biqData.Good.Length)
                                    valueStr = biqData.Good[resourceIndex].Name;
                                else valueStr = "None";
                                break;

                            case "AvailableTo":
                                valueStr = string.Join(", ", ((PRTORACE)field.GetValue(prto)).GetAvailableCivIndexes().Select(idx => biqData.Race[idx].Name));
                                break;

                            case "IgnoreMovementCost":
                                valueStr = string.Join(", ", GetIgnoredTerrainIndexes((PRTOTERR)field.GetValue(prto)).Select(idx => biqData.Terr[idx].Name));
                                break;

                            case "Type":
                                valueStr = field.GetValue(prto) switch
                                {
                                    0 => "Land",
                                    1 => "Sea",
                                    2 => "Air",
                                    _ => "Unknown"
                                };
                                break;

                            case "Required":
                                valueStr = field.GetValue(prto) switch
                                {
                                    -1 => "None",
                                    int techIndex when techIndex >= 0 && techIndex < biqData.Tech.Length => biqData.Tech[techIndex].Name,
                                    _ => "Unknown"
                                };
                                break;

                            default:
                                valueStr = field.GetValue(prto)?.ToString() ?? "null";
                                break;
                        }
                        unitInfoDict[field.Name].Add(valueStr);
                    }
                }

                // Then properties
                foreach (var property in properties)
                {
                    if (unitInfoDict.ContainsKey(property.Name)) // Check if the property is one we want to include (after removals)
                    {
                        string valueStr = property.GetValue(prto)?.ToString() ?? "null";
                        unitInfoDict[property.Name].Add(valueStr);
                    }
                }

                // Finally check by each available civ
                HashSet<String> availableTo = prto.AvailableTo.GetAvailableCivIndexes().Select(idx => biqData.Race[idx].Name).ToHashSet();
                foreach (var raceName in biqData.Race.Select(race => race.Name))
                {
                    if (availableTo.Contains(raceName))
                        unitInfoDict[raceName].Add("Yes");
                    else
                        unitInfoDict[raceName].Add("No");
                }
            }

            return unitInfoDict;
        }

        public static void SaveUnitInfoToCsv(string scenarioFile, string outputPath)
        {
            System.IO.File.WriteAllLines(outputPath, GetUnitListString(scenarioFile));
        }

        public static List<String> GetUnitListString(string scenarioFile)
        {
            return DictToListString(GetUnitDict(scenarioFile));
        }

        private static List<String> DictToListString(Dictionary<string, List<string>> dict)
        {
            var lines = new List<string>();

            // First line is the keys of the dictionary
            lines.Add(string.Join(",", dict.Keys));

            // Then iterate over length of all lists
            for (int i = 0; i < dict.First().Value.Count; i++)
            {
                var lineValues = new List<string>();
                foreach (var key in dict.Keys)
                {
                    string val = dict[key][i];
                    lineValues.Add(val.Contains(',') ? $"\"{val}\"" : val);
                }
                lines.Add(string.Join(",", lineValues));
            }

            return lines;
        }

        public static void PrintFieldsTest(string scenarioFile)
        {
            var biqData = GetBiqData(scenarioFile);
            var prtoData = biqData.Prto;
            FieldInfo[] fields = prtoData[0].GetType().GetFields();
            PropertyInfo[] properties = prtoData[0].GetType().GetProperties();
            foreach (var prto in prtoData)
            {
                Console.WriteLine("Fields:");
                foreach (var field in fields)
                {
                    // Cases for each type of field:
                    switch (field.Name)
                    {
                        case "EnslaveResults":
                        case "UpgradeTo":
                            if (int.TryParse(field.GetValue(prto)?.ToString(), out int upgradeToIndex) && upgradeToIndex >= 0 && upgradeToIndex < prtoData.Length)
                                Console.WriteLine($"{field.Name}: {prtoData[upgradeToIndex].Name}");
                            else Console.WriteLine($"{field.Name}: None");
                            break;

                        case "RequiredResource1":
                        case "RequiredResource2":
                        case "RequiredResource3":
                            if (int.TryParse(field.GetValue(prto)?.ToString(), out int resourceIndex) && resourceIndex >= 0 && resourceIndex < biqData.Good.Length)
                                Console.WriteLine($"{field.Name}: {biqData.Good[resourceIndex].Name}");
                            else Console.WriteLine($"{field.Name}: None");
                            break;

                        case "AvailableTo":
                            string availableToCivs = string.Join(", ", ((PRTORACE)field.GetValue(prto)).GetAvailableCivIndexes().Select(idx => biqData.Race[idx].Name));
                            Console.WriteLine($"{field.Name}: {availableToCivs}");
                            break;

                        case "IgnoreMovementCost":
                            string ignoredTerrain = string.Join(", ", GetIgnoredTerrainIndexes((PRTOTERR)field.GetValue(prto)).Select(idx => biqData.Terr[idx].Name));
                            Console.WriteLine($"{field.Name}: {ignoredTerrain}");
                            break;

                        case "Type":
                            string unitType;
                            switch (field.GetValue(prto))
                            {
                                case 0: unitType = "Land"; break;
                                case 1: unitType = "Sea"; break;
                                case 2: unitType = "Air"; break;
                                default: unitType = "Unknown"; break;
                            }
                            Console.WriteLine($"{field.Name}: {unitType}");
                            break;

                        case "Required":
                            string techRequirement = field.GetValue(prto) switch
                            {
                                -1 => "None",
                                int techIndex when techIndex >= 0 && techIndex < biqData.Tech.Length => biqData.Tech[techIndex].Name,
                                _ => "Unknown"
                            };
                            Console.WriteLine($"{field.Name}: {techRequirement}");
                            break;

                        default:
                            Console.WriteLine($"{field.Name}: {FormatValue(field.GetValue(prto))}");
                            break;
                    }
                }

                Console.WriteLine("Properties:");
                foreach (var property in properties)
                {
                    Console.WriteLine($"{property.Name}: {FormatValue(property.GetValue(prto))}");
                }

                Console.WriteLine();
            }
        }
    }
}
