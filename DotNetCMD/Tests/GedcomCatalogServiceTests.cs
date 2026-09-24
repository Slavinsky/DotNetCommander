using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetCommander;
using Xunit;

namespace DotNetCommander.Tests
{
    public sealed class GedcomCatalogServiceTests
    {
        private const string SampleTree =
            "0 HEAD\n" +
            "2 SOUR test\n" +
            "0 @I1@ INDI\n" +
            "1 NAME John /Doe/\n" +
            "1 SEX M\n" +
            "1 BIRT\n" +
            "2 DATE 15 MAR 1980\n" +
            "1 DEAT\n" +
            "2 DATE 4 JAN 2020\n" +
            "1 FAMC @F1@\n" +
            "1 FAMS @F1@\n" +
            "0 @I2@ INDI\n" +
            "1 NAME Jane /Smith/\n" +
            "1 SEX F\n" +
            "1 BIRT\n" +
            "2 DATE 20 JUN 1982\n" +
            "1 FAMS @F1@\n" +
            "0 @I3@ INDI\n" +
            "1 NAME Kid /Doe/\n" +
            "1 SEX M\n" +
            "1 BIRT\n" +
            "2 DATE 1 JAN 2010\n" +
            "1 FAMC @F1@\n" +
            "0 @F1@ FAM\n" +
            "1 HUSB @I1@\n" +
            "1 WIFE @I2@\n" +
            "1 CHIL @I3@\n" +
            "0 TRLR\n";

        [Fact]
        public async Task ReadAsync_ParsesPeopleFamiliesAndRecords()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "tree.ged", SampleTree);

            GedcomCatalog catalog = await GedcomCatalogService.ReadAsync(path, CancellationToken.None);

            Assert.Equal(3, catalog.People.Count);
            Assert.Single(catalog.Families);
            Assert.Equal(6, catalog.Records.Count);

            GedcomPersonEntry john = catalog.People.Single(person => person.Id == "@I1@");
            Assert.Equal("John", john.FirstName);
            Assert.Equal("Doe", john.LastName);
            Assert.Equal("M", john.Sex);
            Assert.Equal(new DateTime(1980, 3, 15), john.BirthDate);
            Assert.Equal(new DateTime(2020, 1, 4), john.DeathDate);
            Assert.Equal("15 MAR 1980", john.BirthDateText);
        }

        [Fact]
        public async Task ReadAsync_CalculatesAgeAtDeath()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "tree.ged", SampleTree);

            GedcomCatalog catalog = await GedcomCatalogService.ReadAsync(path, CancellationToken.None);

            GedcomPersonEntry john = catalog.People.Single(person => person.Id == "@I1@");
            Assert.Equal(39, john.Age);

            GedcomPersonEntry kid = catalog.People.Single(person => person.Id == "@I3@");
            Assert.True(kid.Age.HasValue);
            Assert.True(kid.Age.Value >= 15);
        }

        [Fact]
        public async Task ReadAsync_LinksFamilyMembersBothWays()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "tree.ged", SampleTree);

            GedcomCatalog catalog = await GedcomCatalogService.ReadAsync(path, CancellationToken.None);

            GedcomFamilyEntry family = Assert.Single(catalog.Families);
            Assert.Equal("@F1@", family.Id);
            Assert.Equal("@I1@", family.HusbandId);
            Assert.Equal("@I2@", family.WifeId);
            Assert.Equal("@I3@", Assert.Single(family.ChildrenIds));
            Assert.NotNull(family.Husband);
            Assert.NotNull(family.Wife);
            Assert.Equal("@I3@", Assert.Single(family.Children).Id);

            GedcomPersonEntry john = catalog.People.Single(person => person.Id == "@I1@");
            Assert.Equal("@F1@", Assert.Single(john.FamilyAsChildIds));
            Assert.Equal("@F1@", Assert.Single(john.FamiliesAsChild).Id);
            Assert.Equal("@F1@", Assert.Single(john.FamiliesAsSpouse).Id);

            GedcomPersonEntry kid = catalog.People.Single(person => person.Id == "@I3@");
            Assert.Empty(kid.FamiliesAsSpouse);
            Assert.Single(kid.FamiliesAsChild);
        }

        [Fact]
        public async Task ReadAsync_GivnSurnOverrideNameFromNameLine()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "override.ged",
                "0 @I9@ INDI\n" +
                "1 NAME A /B/\n" +
                "2 GIVN Alice\n" +
                "2 SURN Baker\n");

            GedcomCatalog catalog = await GedcomCatalogService.ReadAsync(path, CancellationToken.None);

            GedcomPersonEntry person = Assert.Single(catalog.People);
            Assert.Equal("Alice", person.FirstName);
            Assert.Equal("Baker", person.LastName);
        }

        [Fact]
        public async Task ReadAsync_SkipsMalformedLinesAndKeepsFields()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "messy.ged",
                "garbage without level\n" +
                "99\n" +
                "0 @I1@ INDI\n" +
                "1 NAME Max /Mustermann/\n" +
                "1 OCCU \"engineer\"\n" +
                "0 TRLR\n");

            GedcomCatalog catalog = await GedcomCatalogService.ReadAsync(path, CancellationToken.None);

            GedcomPersonEntry person = Assert.Single(catalog.People);
            Assert.Equal("Max", person.FirstName);

            GedcomRecordEntry record = catalog.Records.Single(entry => entry.Id == "@I1@");
            Assert.Equal("INDI", record.Tag);
            Assert.Equal(2, record.Fields.Count);
            Assert.Contains(record.Fields, field => field.Tag == "OCCU" && field.Value == "\"engineer\"");
        }

        [Fact]
        public async Task ReadAsync_EmptyFile_ReturnsEmptyCatalog()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "empty.ged", string.Empty);

            GedcomCatalog catalog = await GedcomCatalogService.ReadAsync(path, CancellationToken.None);

            Assert.Empty(catalog.People);
            Assert.Empty(catalog.Families);
            Assert.Empty(catalog.Records);
        }

        [Fact]
        public async Task ReadPeopleAsync_ReturnsOnlyPeople()
        {
            using var dir = new TemporaryDirectory();
            string path = WriteFile(dir, "tree.ged", SampleTree);

            var people = await GedcomCatalogService.ReadPeopleAsync(path, CancellationToken.None);

            Assert.Equal(3, people.Count);
            Assert.All(people, person => Assert.StartsWith("@I", person.Id));
        }

        private static string WriteFile(TemporaryDirectory dir, string name, string content)
        {
            string path = dir.GetPath(name);
            File.WriteAllText(path, content);
            return path;
        }
    }
}
