using System.Text.Json;
using LZ4;


public class ArchiveHeader {
    public CompactGroup root { get; set; }
}

public class CompactGroup {
    public Dictionary<string, CompactGroup> children { get; set; }
    public Dictionary<string, long> parts { get; set; }
}

public class CompactArchiveReader {
    public static readonly byte[] HEADER_BLOCK = { 0xAC, 0xEF, 10, 0 };

    public static async Task<(ArchiveHeader header, uint headerLength, string errorMessage)> LoadHeader(Stream source) {
        var buffer = new byte[8];
        await source.ReadAsync(buffer, 0, buffer.Length);
        if(buffer[0] != HEADER_BLOCK[0] || buffer[1] != HEADER_BLOCK[1]) return (null, 0, "This is not a valid Axure RP file");
        var headerLength = BitConverter.ToUInt32(buffer, 4);
        
        var headerStream = new MemoryStream();
        await ReadBytes(source, headerStream, headerLength);
        headerStream.Seek(0L, SeekOrigin.Begin);
        ArchiveHeader info;
        using (var header = new LZ4Stream(headerStream, LZ4StreamMode.Decompress)) {
            info = await JsonSerializer.DeserializeAsync<ArchiveHeader>(header);
            // we can remove this in a couple weeks after all files have been reasonably converted.. this is for pre-beta files only
            if(info.root == null) {
                headerStream.Seek(0, SeekOrigin.Begin);
                using(var header2 = new LZ4Stream(headerStream, LZ4StreamMode.Decompress))
                    info.root = await JsonSerializer.DeserializeAsync<CompactGroup>(headerStream);
            }
        }
        return (info, headerLength, null);
    }

    public static async Task ReadBytes(Stream source, Stream target, long byteCount) {
        var buffer = new byte[1 << 14]; // 16k
        var bytesLeft = byteCount;

        while (bytesLeft > 0) {
            var bytesRead = await source.ReadAsync(buffer, 0, Math.Min(buffer.Length, (int)bytesLeft));
            target.Write(buffer, 0, bytesRead);
            if (bytesRead == 0) throw new IndexOutOfRangeException("More data requested than is available.");
            bytesLeft -= bytesRead;
        }
    }
}