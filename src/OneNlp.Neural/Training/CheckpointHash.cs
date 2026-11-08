using System.Security.Cryptography;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp;

// Hash the serialized state without keeping a second copy of the tensors in memory.
internal static class CheckpointHash
{
    /// <summary>SHA-256 of the checkpoint bytes of a module, written to a stream that keeps nothing (the file may be larger than a byte array can be).</summary>
    public static string Of(NnModule model)
    {
        using var hash = new HashingStream();
        Checkpoint.Save(hash, model);
        return hash.Finish();
    }

    private sealed class HashingStream : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        public string Finish() => Convert.ToHexStringLower(_hash.GetHashAndReset());

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _hash.AppendData(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => _hash.AppendData(buffer);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hash.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
