using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;

namespace CardListUpdater
{
    class Program
    {
        static string _mtgJsonSource = "https://mtgjson.com/api/v5/AllSetFiles.zip";
        static string _mtgJsonFolder = "mtgjson_allsetfiles";
        static string _mtgJsonTempFile = "mtgjson_allsetfiles.zip";

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("CardListUpdater JSON_FILE");
                return;
            }

            if (Directory.Exists(_mtgJsonFolder)) Directory.Delete(_mtgJsonFolder, true);
            Directory.CreateDirectory(_mtgJsonFolder);

            Console.WriteLine("Downloading mtgjson data files");
            new WebClient().DownloadFile(_mtgJsonSource, _mtgJsonTempFile);

            ZipFile.ExtractToDirectory(_mtgJsonTempFile, _mtgJsonFolder);
            File.Delete(_mtgJsonTempFile);

            StringBuilder landList = new StringBuilder();
            StringBuilder nonLandList = new StringBuilder();
            HashSet<string> addedCards = new HashSet<string>();

            Console.WriteLine("Loading card names from mtgjson data files");
            foreach (string setFile in Directory.GetFiles(_mtgJsonFolder))
            {
                dynamic set = JsonConvert.DeserializeObject(File.ReadAllText(setFile));
                foreach (var card in set.data.cards)
                {
                    string cardName;
                    if (card.layout == "transform" || card.layout == "flip" || card.layout == "adventure" || card.layout == "meld" || card.layout == "modal_dfc") cardName = card.faceName;
                    else cardName = card.name;

                    if (addedCards.Contains(cardName)) continue;
                    if (cardName.StartsWith("Leyline of")) continue;

                    bool isLand = false;
                    foreach (var type in card.types) if (type == "Land") isLand = true;

                    bool isFlashback = false;
                    if (card.keywords != null) foreach (var type in card.keywords) if (type == "Flashback") isFlashback = true;

                    string cardColor = "";
                    if (isLand || isFlashback) foreach (var color in card.colorIdentity) cardColor += color;
                    else foreach (var color in card.colors) cardColor += color;

                    string manaCost = card.manaCost;
                    if (manaCost != null && manaCost.Replace("{", "").Split("}").Where(c => c == "W" || c == "U" || c == "B" || c == "R" || c == "G").Count() == 0)
                        continue; // Skips colorless, hybrid and pyrexian mana cards

                    bool isModalDfc = card.layout == "modal_dfc";

                    string fixedColor = "";
                    if (cardColor.Contains('W')) fixedColor += "W";
                    if (cardColor.Contains('U')) fixedColor += "U";
                    if (cardColor.Contains('B')) fixedColor += "B";
                    if (cardColor.Contains('R')) fixedColor += "R";
                    if (cardColor.Contains('G')) fixedColor += "G";

                    if (String.IsNullOrEmpty(fixedColor))
                        continue;

                    string dataRow = $"      {{ \"Name\": \"{cardName.Replace("\"", "\\\"")}\", \"Color\": \"{fixedColor}\" }},";

                    if (isLand)
                    {
                        landList.AppendLine(dataRow);
                    }
                    else
                    {
                        nonLandList.AppendLine(dataRow);
                        if (isModalDfc)
                        {
                            // MDFCs that aren't lands on the front side should count for both lists
                            landList.AppendLine(dataRow);
                        }
                    }

                    addedCards.Add(cardName);
                }
            }

            string output = _template
                .Replace("NONLAND_LIST", nonLandList.ToString().TrimEnd('\n', '\r', ','))
                .Replace("LAND_LIST", landList.ToString().TrimEnd('\n', '\r', ','));

            string outputFile = args[0];
            if (File.Exists(outputFile)) File.Delete(outputFile);
            File.WriteAllText(outputFile, output);

            Console.WriteLine("Card list updated");
        }

        #region JSON File Template

        static string _template = @"{
   ""Lands"":
   [
LAND_LIST
   ],
   ""NonLands"":
   [
NONLAND_LIST
   ]
}";

        #endregion
    }
}
