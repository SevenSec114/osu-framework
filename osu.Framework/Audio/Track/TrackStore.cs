// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Audio.Mixing;
using osu.Framework.IO.Stores;

namespace osu.Framework.Audio.Track
{
    internal class TrackStore : AudioCollectionManager<AdjustableAudioComponent>, ITrackStore
    {
        private readonly IResourceStore<byte[]> store;
        private readonly AudioMixer mixer;

        internal TrackStore([NotNull] IResourceStore<byte[]> store, [NotNull] AudioMixer mixer)
        {
            this.store = store;
            this.mixer = mixer;

            (store as ResourceStore<byte[]>)?.AddExtension(@"mp3");
        }

        public Track GetVirtual(double length = double.PositiveInfinity, string name = "virtual")
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            var track = new TrackVirtual(length, name);
            AddItem(track);
            return track;
        }

        public Track Get(string name)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (string.IsNullOrEmpty(name)) return null;

            var dataStream = store.GetStream(name);

            if (dataStream == null)
                return null;

            TrackBass trackBass = new TrackBass(dataStream, name);

            mixer.Add(trackBass);
            AddItem(trackBass);

            return trackBass;
        }

        public Track GetSilent(double length, string name = "silent")
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            var track = new TrackBass(createSilentAudioStream(length), name);
            mixer.Add(track);
            AddItem(track);
            return track;
        }

        private static Stream createSilentAudioStream(double length)
        {
            const int channels = 2;
            const int sample_rate = 44100;
            const int bytes_per_sample = 2;

            long dataSize = (long)(Math.Max(0, length) / 1000 * sample_rate * channels * bytes_per_sample);

            var ms = new MemoryStream();

            void writeString(string s) => ms.Write(Encoding.ASCII.GetBytes(s));
            void writeInt32(int v) => ms.Write(BitConverter.GetBytes(v));
            void writeInt16(short v) => ms.Write(BitConverter.GetBytes(v));

            writeString("RIFF");
            writeInt32((int)(36 + dataSize));
            writeString("WAVE");
            writeString("fmt ");
            writeInt32(16);
            writeInt16(1);
            writeInt16(channels);
            writeInt32(sample_rate);
            writeInt32(sample_rate * channels * bytes_per_sample);
            writeInt16(channels * bytes_per_sample);
            writeInt16(16);
            writeString("data");
            writeInt32((int)dataSize);

            byte[] zero = new byte[Math.Min(dataSize, 65536)];
            long remaining = dataSize;
            while (remaining > 0)
            {
                int chunk = (int)Math.Min(remaining, zero.Length);
                ms.Write(zero, 0, chunk);
                remaining -= chunk;
            }

            ms.Position = 0;
            return ms;
        }

        public Task<Track> GetAsync(string name, CancellationToken cancellationToken = default) =>
            Task.Run(() => Get(name), cancellationToken);

        public Stream GetStream(string name) => store.GetStream(name);

        public IEnumerable<string> GetAvailableResources() => store.GetAvailableResources();
    }
}
