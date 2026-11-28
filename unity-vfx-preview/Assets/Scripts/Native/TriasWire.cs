using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Trias.Native
{
    // The server's compact v2 frames mix named world data with positional rows.
    // The parser runs for every 30 Hz state frame on the main thread, so it avoids per-value garbage:
    // numbers are read in place, keys and short strings are interned, and objects keep their fields in
    // two small arrays searched linearly (interned keys compare by reference first).
    internal sealed class WireValue
    {
        static readonly WireValue Empty = new WireValue();
        internal string Text;
        internal double Number;
        internal bool Bool;
        internal List<WireValue> Items;
        string[] keys;
        WireValue[] values;
        int fieldCount;

        internal int Count => Items?.Count ?? 0;
        internal int Int => (int)Number;
        internal float Float => (float)Number;
        internal WireValue this[string key]
        {
            get
            {
                for (int i = 0; i < fieldCount; i++) if (ReferenceEquals(keys[i], key)) return values[i];
                for (int i = 0; i < fieldCount; i++) if (string.Equals(keys[i], key)) return values[i];
                return Empty;
            }
        }
        internal WireValue this[int index] => Items != null && index >= 0 && index < Items.Count ? Items[index] : Empty;
        internal static WireValue Parse(string text) => new Reader(text).Read(0);

        // Parsed values are never mutated, so common leaves are shared instead of allocated per frame:
        // small integers, true, and every interned string.
        static readonly WireValue True = new WireValue { Bool = true };
        static readonly WireValue[] Integers = new WireValue[4097 + 256];
        static readonly Dictionary<string, WireValue> Strings = new(1024);
        static readonly List<List<WireValue>> scratch = new();

        static WireValue NumberValue(double number)
        {
            if (number >= -256 && number <= 4096 && number == Math.Floor(number))
            {
                int slot = (int)number + 256;
                return Integers[slot] ??= new WireValue { Number = number };
            }
            return new WireValue { Number = number };
        }

        static WireValue StringValue(string text, bool interned)
        {
            if (!interned) return new WireValue { Text = text };
            if (Strings.TryGetValue(text, out var shared)) return shared;
            shared = new WireValue { Text = text };
            if (Strings.Count < 8192) Strings.Add(text, shared);
            return shared;
        }

        void Set(string key, WireValue value)
        {
            for (int i = 0; i < fieldCount; i++)
                if (string.Equals(keys[i], key)) { values[i] = value; return; }
            if (keys == null) { keys = new string[8]; values = new WireValue[8]; }
            else if (fieldCount == keys.Length)
            {
                Array.Resize(ref keys, fieldCount * 2);
                Array.Resize(ref values, fieldCount * 2);
            }
            keys[fieldCount] = key;
            values[fieldCount++] = value;
        }

        // Interned strings shared across frames: field names, event types, weapon and pilot ids, names.
        static readonly Dictionary<int, string[]> pool = new();
        static int pooled;

        static string Intern(string text, int start, int length)
        {
            if (length > 48) return text.Substring(start, length);
            int hash = length;
            for (int i = 0; i < length; i++) hash = hash * 31 + text[start + i];
            if (pool.TryGetValue(hash, out var bucket))
                foreach (var candidate in bucket)
                    if (candidate.Length == length && string.CompareOrdinal(text, start, candidate, 0, length) == 0) return candidate;
            var made = text.Substring(start, length);
            if (pooled < 8192)
            {
                pooled++;
                pool[hash] = bucket == null ? new[] { made } : Append(bucket, made);
            }
            return made;
        }

        static string[] Append(string[] bucket, string item)
        {
            var grown = new string[bucket.Length + 1];
            Array.Copy(bucket, grown, bucket.Length);
            grown[bucket.Length] = item;
            return grown;
        }

        sealed class Reader
        {
            readonly string text;
            int at;
            internal Reader(string source) { text = source; }

            internal WireValue Read(int depth)
            {
                if (depth >= 24) throw new FormatException("Nested game message");
                Space();
                if (at >= text.Length) throw new FormatException("Truncated game message");
                switch (text[at])
                {
                    case '{':
                        at++;
                        var node = new WireValue();
                        if (!Take('}'))
                        {
                            do { Space(); string key = String(); Expect(':'); node.Set(key, Read(depth + 1)); } while (Take(','));
                            Expect('}');
                        }
                        return node;
                    case '[':
                        at++;
                        // Collect into a per-depth scratch list, then allocate the row once at its exact size.
                        while (scratch.Count <= depth) scratch.Add(new List<WireValue>(32));
                        var gather = scratch[depth];
                        gather.Clear();
                        if (!Take(']'))
                        {
                            do { gather.Add(Read(depth + 1)); } while (Take(','));
                            Expect(']');
                        }
                        var list = new List<WireValue>(gather.Count);
                        list.AddRange(gather);
                        gather.Clear();
                        return new WireValue { Items = list };
                    case '"':
                        string value = String(out bool interned);
                        return StringValue(value, interned);
                    case 't': Literal("true"); return True;
                    case 'f': Literal("false"); return Empty;
                    case 'n': Literal("null"); return Empty;
                    default:
                        if (Number(out var result)) return NumberValue(result);
                        throw new FormatException("Invalid game value");
                }
            }

            // JSON number without allocating: sign, integer, fraction, exponent.
            bool Number(out double result)
            {
                result = 0;
                int start = at;
                bool negative = false;
                if (at < text.Length && (text[at] == '-' || text[at] == '+')) { negative = text[at] == '-'; at++; }
                double value = 0;
                int digits = 0;
                while (at < text.Length && text[at] >= '0' && text[at] <= '9') { value = value * 10 + (text[at++] - '0'); digits++; }
                if (at < text.Length && text[at] == '.')
                {
                    at++;
                    double scale = .1;
                    while (at < text.Length && text[at] >= '0' && text[at] <= '9') { value += (text[at++] - '0') * scale; scale *= .1; digits++; }
                }
                if (digits == 0) { at = start; return false; }
                if (at < text.Length && (text[at] == 'e' || text[at] == 'E'))
                {
                    at++;
                    bool down = false;
                    if (at < text.Length && (text[at] == '-' || text[at] == '+')) { down = text[at] == '-'; at++; }
                    int exponent = 0;
                    while (at < text.Length && text[at] >= '0' && text[at] <= '9') exponent = exponent * 10 + (text[at++] - '0');
                    value *= Math.Pow(10, down ? -exponent : exponent);
                }
                result = negative ? -value : value;
                return true;
            }

            string String() => String(out _);

            string String(out bool interned)
            {
                interned = false;
                Expect('"');
                int start = at;
                StringBuilder escaped = null;
                while (at < text.Length)
                {
                    char c = text[at++];
                    if (c == '"')
                    {
                        if (escaped == null)
                        {
                            interned = at - start - 1 <= 48;
                            return Intern(text, start, at - start - 1);
                        }
                        return escaped.Append(text, start, at - start - 1).ToString();
                    }
                    if (c != '\\') continue;
                    escaped ??= new StringBuilder();
                    escaped.Append(text, start, at - start - 1);
                    if (at >= text.Length) break;
                    char code = text[at++];
                    switch (code)
                    {
                        case 'u':
                            if (at + 4 > text.Length) throw new FormatException("Truncated unicode");
                            escaped.Append((char)int.Parse(text.Substring(at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            at += 4;
                            break;
                        case 'b': escaped.Append('\b'); break;
                        case 'f': escaped.Append('\f'); break;
                        case 'n': escaped.Append('\n'); break;
                        case 'r': escaped.Append('\r'); break;
                        case 't': escaped.Append('\t'); break;
                        default: escaped.Append(code); break;
                    }
                    start = at;
                }
                throw new FormatException("Unclosed string");
            }

            void Literal(string value)
            {
                if (at + value.Length > text.Length || string.CompareOrdinal(text, at, value, 0, value.Length) != 0)
                    throw new FormatException("Invalid literal");
                at += value.Length;
            }
            void Space() { while (at < text.Length && char.IsWhiteSpace(text[at])) at++; }
            bool Take(char c) { Space(); if (at >= text.Length || text[at] != c) return false; at++; return true; }
            void Expect(char c) { if (!Take(c)) throw new FormatException("Invalid game message"); }
        }
    }

    internal sealed class TriasSocket : IDisposable
    {
        readonly ClientWebSocket client = new ClientWebSocket();
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        readonly SemaphoreSlim signal = new SemaphoreSlim(0);
        readonly ConcurrentQueue<string> send = new ConcurrentQueue<string>();
        internal readonly ConcurrentQueue<string> Received = new ConcurrentQueue<string>();
        internal volatile string Error;
        int pending, leaving;
        internal bool Running => client.State == WebSocketState.Open;

        internal void Start(Uri endpoint, string handshake) => _ = Run(endpoint, handshake);

        internal bool Send(string payload)
        {
            if (!Running || stop.IsCancellationRequested || leaving != 0) return false;
            if (Interlocked.Increment(ref pending) > 5)
            {
                Interlocked.Decrement(ref pending);
                return false;
            }
            send.Enqueue(payload);
            signal.Release();
            return true;
        }

        async Task Run(Uri endpoint, string handshake)
        {
            try
            {
                using (var opening = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                {
                    opening.CancelAfter(TimeSpan.FromSeconds(7));
                    await client.ConnectAsync(endpoint, opening.Token).ConfigureAwait(false);
                }
                await Write(handshake).ConfigureAwait(false);
                var writer = Writer();
                var buffer = new byte[16 * 1024];
                using (var message = new MemoryStream())
                {
                    while (Running && !stop.IsCancellationRequested)
                    {
                        var part = await client.ReceiveAsync(new ArraySegment<byte>(buffer), stop.Token).ConfigureAwait(false);
                        if (part.MessageType == WebSocketMessageType.Close) break;
                        message.Write(buffer, 0, part.Count);
                        if (message.Length > 256 * 1024) throw new IOException("Příliš velký stav zápasu.");
                        if (!part.EndOfMessage) continue;
                        if (Received.Count > 150) throw new IOException("Klient nestíhá přijímat stav zápasu.");
                        if (part.MessageType == WebSocketMessageType.Text)
                            Received.Enqueue(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                        message.SetLength(0);
                    }
                }
                if (!stop.IsCancellationRequested) Error = "Spojení se serverem skončilo.";
                stop.Cancel();
                await writer.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (!stop.IsCancellationRequested)
                    Error = e is OperationCanceledException ? "Server neodpověděl včas." : "Připojení se nezdařilo. Zkontroluj adresu serveru.";
            }
            finally { client.Dispose(); }
        }

        Task Write(string message) => client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, stop.Token);

        async Task Writer()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    await signal.WaitAsync(stop.Token).ConfigureAwait(false);
                    if (!send.TryDequeue(out var message)) continue;
                    if (message.Length == 0)
                    {
                        await Write("{\"type\":\"leave\"}").ConfigureAwait(false);
                        return;
                    }
                    Interlocked.Decrement(ref pending);
                    await Write(message).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (!stop.IsCancellationRequested) Error = "Spojení se serverem skončilo.";
                stop.Cancel();
            }
        }

        public void Dispose()
        {
            stop.Cancel();
        }

        internal void Leave()
        {
            if (!Running || stop.IsCancellationRequested) { Dispose(); return; }
            if (Interlocked.Exchange(ref leaving, 1) != 0) return;
            stop.CancelAfter(TimeSpan.FromMilliseconds(800));
            send.Enqueue(string.Empty);
            signal.Release();
        }
    }
}
