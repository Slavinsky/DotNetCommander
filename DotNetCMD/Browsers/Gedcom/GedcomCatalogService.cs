using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCommander
{
    internal sealed class GedcomPersonEntry
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string BirthDateText { get; set; }
        public DateTime? BirthDate { get; set; }
        public DateTime? DeathDate { get; set; }
        public string DeathDateText { get; set; }
        public string Sex { get; set; }
        public int? Age { get; set; }
        public List<string> FamilyAsChildIds { get; } = new List<string>();
        public List<string> FamilyAsSpouseIds { get; } = new List<string>();
        public List<GedcomFamilyEntry> FamiliesAsChild { get; } = new List<GedcomFamilyEntry>();
        public List<GedcomFamilyEntry> FamiliesAsSpouse { get; } = new List<GedcomFamilyEntry>();
    }

    internal sealed class GedcomFamilyEntry
    {
        public string Id { get; set; }
        public string HusbandId { get; set; }
        public string WifeId { get; set; }
        public List<string> ChildrenIds { get; } = new List<string>();
        public GedcomPersonEntry Husband { get; set; }
        public GedcomPersonEntry Wife { get; set; }
        public List<GedcomPersonEntry> Children { get; } = new List<GedcomPersonEntry>();
    }

    internal static class GedcomCatalogService
    {
        public static Task<IReadOnlyList<GedcomPersonEntry>> ReadPeopleAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.Run<IReadOnlyList<GedcomPersonEntry>>(() => ReadPeople(filePath, cancellationToken), cancellationToken);
        }

        private static IReadOnlyList<GedcomPersonEntry> ReadPeople(string filePath, CancellationToken cancellationToken)
        {
            var people = new List<GedcomPersonEntry>();
            var peopleById = new Dictionary<string, GedcomPersonEntry>(StringComparer.OrdinalIgnoreCase);
            var families = new Dictionary<string, GedcomFamilyEntry>(StringComparer.OrdinalIgnoreCase);
            GedcomPersonEntry current = null;
            GedcomFamilyEntry currentFamily = null;
            string levelOneTag = string.Empty;
            bool useCurrentNameRecord = false;

            using var reader = new StreamReader(filePath, new UTF8Encoding(false, false), true);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryParseLine(line, out int level, out string xref, out string tag, out string value))
                {
                    continue;
                }

                if (level == 0)
                {
                    current = null;
                    currentFamily = null;
                    if (string.Equals(tag, "INDI", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(xref))
                    {
                        current = new GedcomPersonEntry { Id = xref };
                    }
                    else if (string.Equals(tag, "FAM", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(xref))
                    {
                        currentFamily = new GedcomFamilyEntry { Id = xref };
                        families[xref] = currentFamily;
                    }
                    if (current != null)
                    {
                        people.Add(current);
                        peopleById[current.Id] = current;
                    }

                    levelOneTag = string.Empty;
                    useCurrentNameRecord = false;
                    continue;
                }

                if (currentFamily != null)
                {
                    if (level == 1)
                    {
                        if (string.Equals(tag, "HUSB", StringComparison.OrdinalIgnoreCase))
                        {
                            currentFamily.HusbandId = value;
                        }
                        else if (string.Equals(tag, "WIFE", StringComparison.OrdinalIgnoreCase))
                        {
                            currentFamily.WifeId = value;
                        }
                        else if (string.Equals(tag, "CHIL", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                        {
                            currentFamily.ChildrenIds.Add(value);
                        }
                    }
                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                if (level == 1)
                {
                    levelOneTag = tag;
                    useCurrentNameRecord = false;
                    if (string.Equals(tag, "NAME", StringComparison.OrdinalIgnoreCase))
                    {
                        useCurrentNameRecord = string.IsNullOrWhiteSpace(current.FirstName) && string.IsNullOrWhiteSpace(current.LastName);
                        if (useCurrentNameRecord)
                        {
                            SplitName(value, out string firstName, out string lastName);
                            current.FirstName = firstName;
                            current.LastName = lastName;
                        }
                    }
                    else if (string.Equals(tag, "SEX", StringComparison.OrdinalIgnoreCase))
                    {
                        current.Sex = value;
                    }
                    else if (string.Equals(tag, "FAMC", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                    {
                        current.FamilyAsChildIds.Add(value);
                    }
                    else if (string.Equals(tag, "FAMS", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                    {
                        current.FamilyAsSpouseIds.Add(value);
                    }
                    continue;
                }

                if (level == 2 && useCurrentNameRecord && string.Equals(levelOneTag, "NAME", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(tag, "GIVN", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                    {
                        current.FirstName = value.Trim();
                    }
                    else if (string.Equals(tag, "SURN", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                    {
                        current.LastName = value.Trim();
                    }
                }
                else if (level == 2 && string.Equals(tag, "DATE", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(levelOneTag, "BIRT", StringComparison.OrdinalIgnoreCase))
                    {
                        current.BirthDateText = value?.Trim();
                        current.BirthDate = TryParseGedcomDate(value);
                    }
                    else if (string.Equals(levelOneTag, "DEAT", StringComparison.OrdinalIgnoreCase))
                    {
                        current.DeathDateText = value?.Trim();
                        current.DeathDate = TryParseGedcomDate(value);
                    }
                }
            }

            DateTime today = DateTime.Today;
            foreach (GedcomPersonEntry person in people)
            {
                if (person.BirthDate.HasValue)
                {
                    person.Age = CalculateAge(person.BirthDate.Value, person.DeathDate ?? today);
                }
            }

            foreach (GedcomFamilyEntry family in families.Values)
            {
                if (!string.IsNullOrWhiteSpace(family.HusbandId) && peopleById.TryGetValue(family.HusbandId, out GedcomPersonEntry husband))
                {
                    family.Husband = husband;
                }
                if (!string.IsNullOrWhiteSpace(family.WifeId) && peopleById.TryGetValue(family.WifeId, out GedcomPersonEntry wife))
                {
                    family.Wife = wife;
                }
                foreach (string childId in family.ChildrenIds)
                {
                    if (peopleById.TryGetValue(childId, out GedcomPersonEntry child))
                    {
                        family.Children.Add(child);
                    }
                }
            }

            foreach (GedcomPersonEntry person in people)
            {
                foreach (string familyId in person.FamilyAsChildIds)
                {
                    if (families.TryGetValue(familyId, out GedcomFamilyEntry family))
                    {
                        person.FamiliesAsChild.Add(family);
                    }
                }
                foreach (string familyId in person.FamilyAsSpouseIds)
                {
                    if (families.TryGetValue(familyId, out GedcomFamilyEntry family))
                    {
                        person.FamiliesAsSpouse.Add(family);
                    }
                }
            }

            return people;
        }

        private static bool TryParseLine(string line, out int level, out string xref, out string tag, out string value)
        {
            level = 0;
            xref = null;
            tag = null;
            value = null;
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            string[] parts = line.Trim().Split(new[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out level))
            {
                return false;
            }

            int tagIndex = 1;
            if (parts[1].Length > 2 && parts[1][0] == '@' && parts[1][parts[1].Length - 1] == '@')
            {
                xref = parts[1];
                tagIndex = 2;
            }

            if (tagIndex >= parts.Length)
            {
                return false;
            }

            tag = parts[tagIndex];
            if (tagIndex + 1 < parts.Length)
            {
                value = string.Join(" ", parts, tagIndex + 1, parts.Length - tagIndex - 1);
            }

            return true;
        }

        private static void SplitName(string value, out string firstName, out string lastName)
        {
            firstName = string.Empty;
            lastName = string.Empty;
            value = value?.Trim() ?? string.Empty;
            int firstSlash = value.IndexOf('/');
            if (firstSlash < 0)
            {
                firstName = value;
                return;
            }

            firstName = value.Substring(0, firstSlash).Trim();
            int secondSlash = value.IndexOf('/', firstSlash + 1);
            lastName = (secondSlash < 0
                ? value.Substring(firstSlash + 1)
                : value.Substring(firstSlash + 1, secondSlash - firstSlash - 1)).Trim();
        }

        private static DateTime? TryParseGedcomDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string[] tokens = value.Trim().ToUpperInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int yearIndex = -1;
            for (int i = tokens.Length - 1; i >= 0; i--)
            {
                if (int.TryParse(tokens[i], NumberStyles.None, CultureInfo.InvariantCulture, out int candidate) && candidate >= 1 && candidate <= 9999)
                {
                    yearIndex = i;
                    break;
                }
            }

            if (yearIndex < 0 || !int.TryParse(tokens[yearIndex], NumberStyles.None, CultureInfo.InvariantCulture, out int year))
            {
                return null;
            }

            int month = 1;
            int day = 1;
            if (yearIndex > 0 && TryGetMonth(tokens[yearIndex - 1], out int parsedMonth))
            {
                month = parsedMonth;
                if (yearIndex > 1 && int.TryParse(tokens[yearIndex - 2], NumberStyles.None, CultureInfo.InvariantCulture, out int parsedDay))
                {
                    day = Math.Min(Math.Max(parsedDay, 1), DateTime.DaysInMonth(year, month));
                }
            }

            return new DateTime(year, month, day);
        }

        private static bool TryGetMonth(string value, out int month)
        {
            string[] names = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            month = Array.IndexOf(names, value) + 1;
            return month > 0;
        }

        private static int CalculateAge(DateTime birthDate, DateTime endDate)
        {
            if (endDate < birthDate)
            {
                return 0;
            }

            int age = endDate.Year - birthDate.Year;
            if (endDate.Month < birthDate.Month || endDate.Month == birthDate.Month && endDate.Day < birthDate.Day)
            {
                age--;
            }

            return Math.Max(0, age);
        }
    }
}
