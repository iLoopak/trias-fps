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
    internal sealed class WireValue
    {
        static readonly WireValue Empty = new WireValue();
        internal string Text;
        internal double Number;
        internal bool Bool;
        internal List<WireValue> Items;
        Dictionary<string, WireValue> fields;

        internal int Count => Items?.Count ?? 0;
        internal int Int => (int)Number;
        internal float Float => (float)Number;
        internal WireValue this[string key] => fields != null && fields.TryGetValue(key, out var value) ? value : Empty;
        internal WireValue this[int index] => Items != null && index >= 0 && index < Items.Count ? Items[index] : Empty;
        internal static WireValue Parse(string text) => new Reader(text).Read(0);

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
                        var fields = new Dictionary<string, WireValue>();
                        if (!Take('}'))
                        {
                            do { Space(); string key = String(); Expect(':'); fields[key] = Read(depth + 1); } while (Take(','));
                            Expect('}');
                        }
                        return new WireValue { fields = fields };
                    case '[':
                        at++;
                        var list = new List<WireValue>();
                        if (!Take(']'))
                        {
                            do { list.Add(Read(depth + 1)); } while (Take(','));
                            Expect(']');
                        }
                        return new WireValue { Items = list };
                    case '"': return new WireValue { Text = String() };
                    case 't': Literal("true"); return new WireValue { Bool = true };
                    case 'f': Literal("false"); return Empty;
                    case 'n': Literal("null"); return Empty;
                    default:
                        int start = at;
                        while (at < text.Length && "-+0123456789.eE".IndexOf(text[at]) >= 0) at++;
                        if (start != at && double.TryParse(text.Substring(start, at - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                            return new WireValue { Number = result };
                        throw new FormatException("Invalid game value");
                }
            }

            string String()
            {
                Expect('"');
                int start = at;
                StringBuilder escaped = null;
                while (at < text.Length)
                {
                    char c = text[at++];
                    if (c == '"')
                    {
                        string tail = text.Substring(start, at - start - 1);
                        return escaped == null ? tail : escaped.Append(tail).ToString();
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
        int pending;
        internal bool Running => client.State == WebSocketState.Open;

        internal void Start(Uri endpoint, string handshake) => _ = Run(endpoint, handshake);

        internal bool Send(string payload)
        {
            if (!Running || stop.IsCancellationRequested) return false;
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
    }
}
