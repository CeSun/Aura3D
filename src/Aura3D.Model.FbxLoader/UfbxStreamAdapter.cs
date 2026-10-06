using Ufbx.NET;

namespace Aura3D.Model;

/// <summary>
/// Adapts a <see cref="Stream"/> to the read callback surface ufbx expects.
/// </summary>
internal sealed class UfbxStreamAdapter : UfbxInputStream
{
    private readonly Stream stream;

    internal UfbxStreamAdapter(Stream stream) => this.stream = stream;

    public override bool CanSkip => stream.CanSeek;

    public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

    public override bool Skip(int size)
    {
        if (!stream.CanSeek)
            return false;

        stream.Seek(size, SeekOrigin.Current);
        return true;
    }

    public override ulong Size() => stream.CanSeek ? (ulong)stream.Length : 0;

    public override void Close() { }
}
