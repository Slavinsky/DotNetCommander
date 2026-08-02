using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace DotNetCommander
{
    internal static class DataSetCatalogService
    {
        public static Task<DataSet> ReadAsync(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return ReadStrict(path, cancellationToken);
                }
                catch (XmlException strictException)
                {
                    return ReadSanitized(path, strictException, cancellationToken);
                }
            }, cancellationToken);
        }

        private static DataSet ReadStrict(string path, CancellationToken cancellationToken)
        {
            var dataSet = new DataSet();
            try
            {
                using FileStream stream = OpenSharedRead(path);
                using XmlReader reader = XmlReader.Create(stream, CreateReaderSettings());
                dataSet.ReadXml(reader, XmlReadMode.Auto);
                cancellationToken.ThrowIfCancellationRequested();
                return dataSet;
            }
            catch
            {
                dataSet.Dispose();
                throw;
            }
        }

        private static DataSet ReadSanitized(string path, XmlException strictException, CancellationToken cancellationToken)
        {
            var dataSet = new DataSet();
            try
            {
                using FileStream stream = OpenSharedRead(path);
                using var textReader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    true,
                    4096,
                    false);
                using var sanitizedReader = new XmlSanitizingTextReader(textReader);
                using XmlReader reader = XmlReader.Create(sanitizedReader, CreateReaderSettings());
                dataSet.ReadXml(reader, XmlReadMode.Auto);
                cancellationToken.ThrowIfCancellationRequested();

                if (sanitizedReader.RemovedCharacterCount == 0)
                {
                    throw strictException;
                }

                LogService.LogInfo(
                    "DataSetCatalogService.ReadSanitized",
                    "Removed " + sanitizedReader.RemovedCharacterCount + " invalid XML character(s) from " + path);
                return dataSet;
            }
            catch
            {
                dataSet.Dispose();
                throw;
            }
        }

        private static FileStream OpenSharedRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        private static XmlReaderSettings CreateReaderSettings()
        {
            return new XmlReaderSettings
            {
                CheckCharacters = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
        }

        private sealed class XmlSanitizingTextReader : TextReader
        {
            private readonly TextReader innerReader;
            private readonly LinkedList<int> pendingRawCharacters = new LinkedList<int>();
            private readonly Queue<int> pendingOutputCharacters = new Queue<int>();
            private int cachedCharacter = -2;

            public XmlSanitizingTextReader(TextReader innerReader)
            {
                this.innerReader = innerReader;
            }

            public int RemovedCharacterCount { get; private set; }

            public override int Peek()
            {
                if (cachedCharacter == -2)
                {
                    cachedCharacter = ReadNextValidCharacter();
                }
                return cachedCharacter;
            }

            public override int Read()
            {
                if (cachedCharacter != -2)
                {
                    int cached = cachedCharacter;
                    cachedCharacter = -2;
                    return cached;
                }

                return ReadNextValidCharacter();
            }

            public override int Read(char[] buffer, int index, int count)
            {
                if (buffer == null)
                    throw new System.ArgumentNullException(nameof(buffer));
                if (index < 0 || count < 0 || buffer.Length - index < count)
                    throw new System.ArgumentOutOfRangeException();

                int written = 0;
                while (written < count)
                {
                    int value = Read();
                    if (value < 0)
                        break;
                    buffer[index + written++] = (char)value;
                }
                return written;
            }

            private int ReadNextValidCharacter()
            {
                if (pendingOutputCharacters.Count > 0)
                {
                    return pendingOutputCharacters.Dequeue();
                }

                while (true)
                {
                    int raw = ReadRawCharacter();
                    if (raw < 0)
                        return -1;

                    char character = (char)raw;
                    if (char.IsHighSurrogate(character))
                    {
                        int next = ReadRawCharacter();
                        if (next >= 0 && char.IsLowSurrogate((char)next))
                        {
                            pendingOutputCharacters.Enqueue(next);
                            return character;
                        }

                        RemovedCharacterCount++;
                        PutBackRawCharacter(next);
                        continue;
                    }

                    if (char.IsLowSurrogate(character) || !XmlConvert.IsXmlChar(character))
                    {
                        RemovedCharacterCount++;
                        continue;
                    }

                    if (character == '&' && RemoveInvalidNumericCharacterReference())
                    {
                        continue;
                    }

                    return character;
                }
            }

            private bool RemoveInvalidNumericCharacterReference()
            {
                var consumed = new List<int>();
                int current = ReadRawCharacter();
                if (current < 0)
                    return false;

                consumed.Add(current);
                if (current != '#')
                {
                    PutBackRawCharacters(consumed);
                    return false;
                }

                current = ReadRawCharacter();
                if (current < 0)
                {
                    PutBackRawCharacters(consumed);
                    return false;
                }

                consumed.Add(current);
                bool hexadecimal = current == 'x' || current == 'X';
                if (hexadecimal)
                {
                    current = ReadRawCharacter();
                    if (current < 0)
                    {
                        PutBackRawCharacters(consumed);
                        return false;
                    }
                    consumed.Add(current);
                }

                uint value = 0;
                int digitCount = 0;
                bool overflow = false;
                while (current != ';')
                {
                    int digit = GetDigitValue(current, hexadecimal);
                    if (digit < 0)
                    {
                        PutBackRawCharacters(consumed);
                        return false;
                    }

                    digitCount++;
                    uint radix = hexadecimal ? 16u : 10u;
                    if (value > (uint.MaxValue - (uint)digit) / radix)
                    {
                        overflow = true;
                    }
                    else if (!overflow)
                    {
                        value = value * radix + (uint)digit;
                    }

                    current = ReadRawCharacter();
                    if (current < 0)
                    {
                        PutBackRawCharacters(consumed);
                        return false;
                    }
                    consumed.Add(current);
                }

                if (digitCount == 0)
                {
                    PutBackRawCharacters(consumed);
                    return false;
                }

                if (overflow || !IsValidXmlCodePoint(value))
                {
                    RemovedCharacterCount++;
                    return true;
                }

                foreach (int character in consumed)
                {
                    pendingOutputCharacters.Enqueue(character);
                }
                return false;
            }

            private int ReadRawCharacter()
            {
                if (pendingRawCharacters.First != null)
                {
                    int character = pendingRawCharacters.First.Value;
                    pendingRawCharacters.RemoveFirst();
                    return character;
                }

                return innerReader.Read();
            }

            private void PutBackRawCharacter(int character)
            {
                if (character >= 0)
                {
                    pendingRawCharacters.AddFirst(character);
                }
            }

            private void PutBackRawCharacters(List<int> characters)
            {
                for (int index = characters.Count - 1; index >= 0; index--)
                {
                    PutBackRawCharacter(characters[index]);
                }
            }

            private static int GetDigitValue(int character, bool hexadecimal)
            {
                if (character >= '0' && character <= '9')
                    return character - '0';
                if (hexadecimal && character >= 'a' && character <= 'f')
                    return character - 'a' + 10;
                if (hexadecimal && character >= 'A' && character <= 'F')
                    return character - 'A' + 10;
                return -1;
            }

            private static bool IsValidXmlCodePoint(uint value)
            {
                return value == 0x9
                    || value == 0xA
                    || value == 0xD
                    || (value >= 0x20 && value <= 0xD7FF)
                    || (value >= 0xE000 && value <= 0xFFFD)
                    || (value >= 0x10000 && value <= 0x10FFFF);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    innerReader.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
