using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Telescope.Gemini;

public static class GeminiClient
{
    private const int GEMINIPORT = 1965;

    /// <summary>
    /// Make a Gemini request
    /// </summary>
    /// <param name="uri">Request <see cref="Uri"/></param>
    /// <returns>Server data represented as a one of the types inheriting from <see cref="GeminiResponse"/></returns>
    public static async Task<GeminiResponse> Request(Uri uri)
    {
        Encoding utf8 = new UTF8Encoding(false);
        using TcpClient tcpClient = new TcpClient(uri.Host, uri.Port == -1 ? GEMINIPORT : uri.Port);
        using SslStream stream = new SslStream(tcpClient.GetStream(), false, CertificateValidationCallback);
        await stream.AuthenticateAsClientAsync(uri.Host, null, SslProtocols.Tls12, false);
        await stream.WriteAsync(utf8.GetBytes($"{uri}\r\n"));
        await stream.FlushAsync();
        return GeminiResponse.Parse(stream, uri);
    }

    public static bool CertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate == null)
            return false;
        X509Certificate2 certificate2 = new(certificate);
        return DateTime.Now < certificate2.NotAfter && DateTime.Now > certificate2.NotBefore;
    }
}

public class GeminiResponse
{
    public int Type => StatusCode / 10;
    public int StatusCode { get; set; }

    internal string? StatusDetail { get; set; }

    private const int STATUSLENGTH = 2;

    internal GeminiResponse(string header)
    {
        StatusCode = int.Parse(header.Substring(0, STATUSLENGTH));
        if (header.Length > STATUSLENGTH)
            StatusDetail = header.Substring(STATUSLENGTH + 1);
    }

    /// <summary>
    /// Parse data from the <see cref="SslStream"/> to the server
    /// </summary>
    /// <param name="stream"><see cref="SslStream"/> to the server</param>
    /// <param name="uri">Request <see cref="Uri"/>.  It is needed to present absolute links in the result set.</param>
    /// <returns>Server data represented as a <see cref="GeminiResponse"/></returns>
    /// <exception cref="ArgumentException"></exception>
    public static GeminiResponse Parse(SslStream stream, Uri uri)
    {
        string header = Encoding.UTF8.GetString([.. ReadFirstLine(stream)]);

        return header![0] switch
        {
            '1' => new GeminiPromptResponse(header),
            '2' => new GeminiSuccessResponse(header, stream, uri),
            '3' => new GeminiRedirectResponse(header),
            '4' => new GeminiErrorResponse(header),
            '5' => new GeminiErrorResponse(header),
            '6' => new GeminiAuthResponse(header),
            _ => throw new ArgumentException(null, header, null)
        };
    }

    /// <summary>
    /// Read the first line terminated by CRLF.
    /// This has to be done manually,  as StreamReader reads blocks and advances position in the stream,  while at this point we don't know what is coming after the first line.
    /// </summary>
    /// <param name="stream"><see cref="SslStream"/> to read from</param>
    /// <returns>The first line as an <see cref="IEnumerable{byte}"/></returns>
    private static IEnumerable<byte> ReadFirstLine(SslStream stream)
    {
        int b;
        do
        {
            b = stream.ReadByte();
            if (b is not '\r' and not '\n')
                yield return (byte)b;
        }
        while (b is not (-1) and not '\n');
    }
}

public class GeminiPromptResponse : GeminiResponse
{
    public string Prompt => StatusDetail!;

    internal GeminiPromptResponse(string header) : base(header)
    { }
}

public class GeminiSuccessResponse : GeminiResponse
{
    public ContentType MimeType => new ContentType(StatusDetail!);

    public List<GeminiLine>? Lines { get; set; }

    public MemoryStream? Data { get; set; }

    internal GeminiSuccessResponse(string header, Stream stream, Uri uri) : base(header)
    {
        if (stream != null)
        {
            if (MimeType.MediaType.Equals("text/gemini", StringComparison.OrdinalIgnoreCase))
            {
                using StreamReader rdr = new(stream, MimeType.CharSet is string charset ? Encoding.GetEncoding(charset) : Encoding.UTF8);
                Lines = GeminiLine.Parse(uri, rdr.ReadToEnd().Split('\n')).ToList();
            }
            else
            {
                Data = new MemoryStream();
                stream.CopyTo(Data);
            }
        }
    }
}

public class GeminiRedirectResponse : GeminiResponse
{
    public Uri RedirectUrl => new Uri(StatusDetail!);

    internal GeminiRedirectResponse(string header) : base(header)
    { }
}

public class GeminiErrorResponse : GeminiResponse
{
    public string? Message => StatusDetail;

    internal GeminiErrorResponse(string header) : base(header)
    { }
}

public class GeminiAuthResponse : GeminiResponse
{
    public string? Message => StatusDetail;

    internal GeminiAuthResponse(string header) : base(header)
    { }
}