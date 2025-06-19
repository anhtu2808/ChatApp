using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ChatClientWPF
{
    public class ProgressableStreamContent : HttpContent
    {
        private readonly Stream _stream;
        private readonly int _bufferSize;
        private readonly Action<long> _progress;
        public ProgressableStreamContent(Stream stream, int bufferSize, Action<long> progress)
        {
            _stream = stream;
            _bufferSize = bufferSize;
            _progress = progress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            var buffer = new byte[_bufferSize];
            long uploaded = 0;
            int read;
            while ((read = await _stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await stream.WriteAsync(buffer, 0, read);
                uploaded += read;
                _progress?.Invoke(uploaded);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _stream.Length;
            return true;
        }
    }
}
