
namespace WaveHeaderRemover
{
    using System;
    using System.IO;
    using System.Text;
    using System.Windows;

    public partial class MainWindow
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        protected override void OnPreviewDragOver(DragEventArgs e)
        {
            base.OnPreviewDragOver(e);

            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        protected override void OnPreviewDrop(DragEventArgs e)
        {
            base.OnPreviewDrop(e);

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (filePaths == null)
            {
                return;
            }

            foreach (var filePath in filePaths)
            {
                try
                {
                    RemoveWaveHeader(filePath);
                }
                catch (Exception exception)
                {
                    MessageBox.Show($"{exception}");
                }
            }
        }

        private static void RemoveWaveHeader(string readFilePath)
        {
            if (string.IsNullOrWhiteSpace(readFilePath) || Path.GetExtension(readFilePath) != ".wav")
            {
                return;
            }

            var readBytes = ReadWaveFile(readFilePath);

            var formatChunk = FindChunk(readBytes, "fmt ");
            var dataChunk = FindChunk(readBytes, "data");

            var writeBytes = new byte[12 + formatChunk.Length + dataChunk.Length];

            // RIFF
            Array.Copy(readBytes, 0, writeBytes, 0, 4);

            // Size
            var sizeBytes = BitConverter.GetBytes((uint)(writeBytes.Length - 8));
            Array.Copy(sizeBytes, 0, writeBytes, 4, 4);

            // WAVE
            Array.Copy(readBytes, 8, writeBytes, 8, 4);

            // Format chunk
            Array.Copy(readBytes, formatChunk.Offset, writeBytes, 12, formatChunk.Length);

            // Data chunk
            Array.Copy(readBytes, dataChunk.Offset, writeBytes, 12 + formatChunk.Length, dataChunk.Length);

            var writeFilePath = Path.Combine(
                Path.GetDirectoryName(readFilePath) ?? string.Empty,
                string.Format(
                    "{0}_HeaderRemoved{1}",
                    Path.GetFileNameWithoutExtension(readFilePath),
                    Path.GetExtension(readFilePath)));

            WriteWaveFile(writeFilePath, writeBytes);
        }

        private static byte[] ReadWaveFile(string filePath)
        {
            byte[] readBytes;
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(stream))
            {
                readBytes = new byte[stream.Length];
                reader.Read(readBytes, 0, readBytes.Length);
            }

            return readBytes;
        }

        private static void WriteWaveFile(string filePath, byte[] bytes)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var reader = new BinaryWriter(stream))
            {
                reader.Write(bytes, 0, bytes.Length);
            }
        }

        private static (int Offset, int Length) FindChunk(byte[] bytes, string identifierString)
        {
            var identifierBytes = Encoding.ASCII.GetBytes(identifierString);
            var identifier = BitConverter.ToInt32(identifierBytes, 0);

            var offset = 12;
            while (bytes.Length > offset)
            {
                var chunkIdentifier = BitConverter.ToInt32(bytes, offset);
                var chunkSize = BitConverter.ToInt32(bytes, offset + 4) + 8;
                if (chunkIdentifier == identifier)
                {
                    return (offset, chunkSize);
                }

                offset += chunkSize;
            }

            return default;
        }
    }
}
