using DataIngestion.FileReaders;

namespace DataIngestion.Chunking
{
    internal class DocumentLoader
    {
        public async Task<string> LoadDocumentAsync(string fileType, Stream bytes)
        {
            var fileReader = FileExtentionMap.FileReaders[fileType];

            if (fileReader == null)
            {
                // TODO: Handle this without throwing an exception. Or maybe do throw the exception.. I dunno, I'm not your dad.
                throw new NotImplementedException();
            }

            var fileContent = await fileReader.GetFileContentAsync(bytes);

            return fileContent;
        }

    }
}
